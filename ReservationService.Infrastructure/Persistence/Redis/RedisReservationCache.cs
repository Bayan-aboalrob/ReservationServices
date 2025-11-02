using System.Text.Json;
using StackExchange.Redis;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;

namespace ReservationService.Infrastructure.Redis;

public class RedisReservationCache : IReservationCache
{
    private readonly IConnectionMultiplexer _mux;

    // KEYS
    // inv:{productId} -> Hash { available:int, reserved:int }
    // res:{reservationId} -> Hash { userId, productId, qty, createdAt, expiresAt, orderId? }
    // user:{userId}:res -> Set(reservationIds)

    private static string InvKey(Guid productId) => $"inv:{productId:N}";
    private static string ResKey(Guid resId) => $"res:{resId:N}";
    private static string UserSetKey(Guid userId) => $"user:{userId:N}:res";

    private const string LUA_RESERVE = @"
         local inv = KEYS[1]
         local res = KEYS[2]
         local userset = KEYS[3]
         local now = ARGV[1]
         local ttlSec = tonumber(ARGV[2])
         local uid = ARGV[3]
         local pid = ARGV[4]
         local qty = tonumber(ARGV[5])
         local rid = ARGV[6]

         local available = tonumber(redis.call('HGET', inv, 'available') or '0')
         if available < qty then
           return cjson.encode({ ok=false, reason='INSUFFICIENT' })
           end

        redis.call('HINCRBY', inv, 'available', -qty)
        redis.call('HINCRBY', inv, 'reserved', qty)

        redis.call('HSET', res,
        'userId', uid,
        'productId', pid,
        'qty', qty,
        'createdAt', now,
        'expiresAt', now + ttlSec
         )
        redis.call('SADD', userset, rid)
        redis.call('EXPIRE', res, ttlSec)

       return cjson.encode({ ok=true })
        ";

    private const string LUA_CANCEL = @"
       local inv = KEYS[1]
       local res = KEYS[2]
       local userset = KEYS[3]

       if redis.call('EXISTS', res) == 0 then
          return 1
          end

       local qty = tonumber(redis.call('HGET', res, 'qty') or '0')
       if qty > 0 then
         redis.call('HINCRBY', inv, 'available', qty)
         redis.call('HINCRBY', inv, 'reserved', -qty)
         end
        redis.call('DEL', res)
        redis.call('SREM', userset, ARGV[1])
        return 1
        ";

    private const string LUA_CONSUME = @"
         local inv = KEYS[1]
         local res = KEYS[2]
         local userset = KEYS[3]
         local now = ARGV[1]
         local orderId = ARGV[2]

         if redis.call('EXISTS', res) == 0 then
             return cjson.encode({ ok=false, reason='NOT_FOUND' })
             end

        local exp = tonumber(redis.call('HGET', res, 'expiresAt') or '0')
        if now > exp then
            return cjson.encode({ ok=false, reason='EXPIRED' })
            end

       local qty = tonumber(redis.call('HGET', res, 'qty') or '0')
       redis.call('HINCRBY', inv, 'reserved', -qty)
      -- keep res as record with orderId (no TTL) for tracing
      redis.call('PERSIST', res)
      redis.call('HSET', res, 'orderId', orderId)
      return cjson.encode({ ok=true, qty=qty })
     ";

    public RedisReservationCache(IConnectionMultiplexer mux) => _mux = mux;

    public async Task<ReservationDto> CreateAsync(Guid userId, Guid productId, int quantity, TimeSpan ttl, string idempotencyKey, CancellationToken ct)
    {
        // idempotency best-effort: caller should generate stable key & call Get if needed
        var rid = Guid.NewGuid();
        var db = _mux.GetDatabase();

        var invKey = InvKey(productId);
        if (!(await db.KeyExistsAsync(invKey)))
        {
            // if you have a bootstrapper, populate from SQL at service startup
            await db.HashSetAsync(invKey, new HashEntry[] { new("available", 0), new("reserved", 0) });
        }

        var res = await db.ScriptEvaluateAsync(LUA_RESERVE,
            keys: new RedisKey[] { invKey, ResKey(rid), UserSetKey(userId) },
            values: new RedisValue[] {
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                (long)ttl.TotalSeconds,
                userId.ToString("N"),
                productId.ToString("N"),
                quantity,
                rid.ToString("N")
            });

        var parsed = JsonSerializer.Deserialize<ReserveReply>(res.ToString()!);
        if (parsed is null || !parsed.ok) throw new InvalidOperationException(parsed?.reason ?? "RESERVE_FAILED");

        var dto = new ReservationDto(
            rid, userId, productId, null,
            DateTime.UtcNow.Add(ttl), DateTime.UtcNow,
            ReservationComputedStatus.Active, quantity);

        return dto;
    }

    public async Task<ReservationDto?> GetAsync(Guid reservationId, CancellationToken ct)
    {
        var db = _mux.GetDatabase();
        var key = ResKey(reservationId);
        if (!await db.KeyExistsAsync(key)) return null;

        var entries = await db.HashGetAllAsync(key);
        var map = entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

        var userId = Guid.Parse(map["userId"]);
        var productId = Guid.Parse(map["productId"]);
        var qty = int.Parse(map["qty"]);
        var expUnix = long.Parse(map["expiresAt"]);
        var createdUnix = long.Parse(map["createdAt"]);
        var orderId = map.ContainsKey("orderId") && !string.IsNullOrWhiteSpace(map["orderId"])
            ? Guid.Parse(map["orderId"])
            : Guid.Empty;

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var status = orderId != Guid.Empty ? ReservationComputedStatus.Consumed
                 : (nowUnix > expUnix ? ReservationComputedStatus.Expired : ReservationComputedStatus.Active);

        return new ReservationDto(
            reservationId, userId, productId,
            orderId == Guid.Empty ? null : orderId,
            DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime,
            DateTimeOffset.FromUnixTimeSeconds(createdUnix).UtcDateTime,
            status, qty
        );
    }

    public async Task<bool> CancelAsync(Guid reservationId, CancellationToken ct)
    {
        var db = _mux.GetDatabase();
        // read userId to remove from user set
        var key = ResKey(reservationId);
        var userId = await db.HashGetAsync(key, "userId");
        var productId = await db.HashGetAsync(key, "productId");
        if (userId.IsNullOrEmpty || productId.IsNullOrEmpty) return true; // already gone

        var res = await db.ScriptEvaluateAsync(LUA_CANCEL,
            keys: new RedisKey[] { InvKey(Guid.Parse(productId!)), key, UserSetKey(Guid.Parse(userId!)) },
            values: new RedisValue[] { reservationId.ToString("N") });

        return (int)res == 1;
    }

    public async Task<(bool ok, Guid productId, int qty)> ConsumeAsync(Guid reservationId, Guid orderId, CancellationToken ct)
    {
        var db = _mux.GetDatabase();

        // need product & user to build keys
        var productIdVal = await db.HashGetAsync(ResKey(reservationId), "productId");
        var userIdVal = await db.HashGetAsync(ResKey(reservationId), "userId");
        if (productIdVal.IsNullOrEmpty || userIdVal.IsNullOrEmpty) return (false, Guid.Empty, 0);

        var productId = Guid.Parse(productIdVal!);
        var userId = Guid.Parse(userIdVal!);

        var res = await db.ScriptEvaluateAsync(LUA_CONSUME,
            keys: new RedisKey[] { InvKey(productId), ResKey(reservationId), UserSetKey(userId) },
            values: new RedisValue[] { DateTimeOffset.UtcNow.ToUnixTimeSeconds(), orderId.ToString("N") });

        var parsed = JsonSerializer.Deserialize<ConsumeReply>(res.ToString()!);
        if (parsed is null || !parsed.ok) return (false, Guid.Empty, 0);

        return (true, productId, parsed.qty);
    }

    private record ReserveReply(bool ok, string? reason);
    private record ConsumeReply(bool ok, string? reason, int qty);
}
