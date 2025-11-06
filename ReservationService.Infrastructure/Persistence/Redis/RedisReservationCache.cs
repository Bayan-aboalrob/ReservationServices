using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;
using StackExchange.Redis;

namespace ReservationService.Infrastructure.Redis
{
    internal sealed class RedisReservationCache : IReservationCache
    {
        private readonly IDatabase _db;
        public RedisReservationCache(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        private static string InvKey(Guid productId) => $"inv:{productId:N}";
        private static string ResKey(string rid) => $"res:{rid}";
        private static string UserSetKey(Guid userId) => $"user:{userId:N}:res";
        private static string IdemKey(string key) => $"idem:{key}";


        private const string LUA_RESERVE = @"
        -- KEYS[1] inv:{productId} ; KEYS[2] res:{rid} ; KEYS[3] user:{uid}:res ; KEYS[4] idem:{key} (optional)
        -- ARGV[1] nowMs ; [2] ttlMs ; [3] uid ; [4] pid ; [5] qty ; [6] rid ; [7] idemKeyOrEmpty ; [8] expAtMs
        local inv     = KEYS[1]
        local res     = KEYS[2]
        local userset = KEYS[3]
        local idem    = KEYS[4]

        local nowMs   = tonumber(ARGV[1])
        local ttlMs   = tonumber(ARGV[2])
        local uid     = ARGV[3]
        local pid     = ARGV[4]
        local qty     = tonumber(ARGV[5])
        local rid     = ARGV[6]
        local idemKey = ARGV[7]
        local expAtMs = tonumber(ARGV[8])

        -- Idempotency hit?
        if idemKey ~= '' then
        local existing = redis.call('GET', idem)
        if existing and existing ~= '' then
           return cjson.encode({ ok=false, reason='IDEMPOTENT_HIT', rid=existing })
           end
           end

        -- Ensure inventory hash exists
        if redis.call('EXISTS', inv) == 0 then
            redis.call('HSET', inv, 'available', 0, 'reserved', 0)
        end

        local avail = tonumber(redis.call('HGET', inv, 'available')) or 0
        if avail < qty then
            return cjson.encode({ ok=false, reason='INSUFFICIENT' })
        end

        -- decrement available, increment reserved
        redis.call('HINCRBY', inv, 'available', -qty)
        redis.call('HINCRBY', inv, 'reserved',   qty)

        -- write reservation hash with TTL
        redis.call('HSET', res,
        'userId', uid,
        'productId', pid,
        'qty', qty,
        'createdAt', nowMs,
        'expiresAt', expAtMs,
        'state', 'active'
        )
        redis.call('PEXPIRE', res, ttlMs)

        -- add to user's reservations set
        redis.call('SADD', userset, rid)

        -- persist idempotency mapping
        if idemKey ~= '' then
           redis.call('SET', idem, rid, 'PX', ttlMs)
        end

        return cjson.encode({ ok=true, rid=rid })
        ";

        private const string LUA_CANCEL = @"
        -- KEYS: [1] inv:{pid}, [2] res:{rid}, [3] user:{uid}:res
        -- ARGV: [1] nowMs (unused for now)
        local inv = KEYS[1]
        local res = KEYS[2]
        local userset = KEYS[3]

        if redis.call('EXISTS', res) == 0 then return 0 end
        local state = redis.call('HGET', res, 'state')
        if state ~= 'active' then return -1 end

        local qty = tonumber(redis.call('HGET', res, 'qty')) or 0
        redis.call('HINCRBY', inv, 'available', qty)
        redis.call('HINCRBY', inv, 'reserved', -qty)

        redis.call('DEL', res)
        redis.call('SREM', userset, string.sub(res,5)) -- remove rid from set (strip 'res:')

        return 1
         ";

        private const string LUA_CONSUME_BEGIN = @"
         -- KEYS: [1] res:{rid}
         -- ARGV: [1] nowMs, [2] orderId
         local res = KEYS[1]
         local nowMs = tonumber(ARGV[1])
         local orderId = ARGV[2]

         if redis.call('EXISTS', res) == 0 then
           return cjson.encode({ ok=false, reason='NOT_FOUND' })
           end

        local state = redis.call('HGET', res, 'state')
         if state ~= 'active' then
            return cjson.encode({ ok=false, reason='INVALID_STATE', state=state })
            end

        local expAt = tonumber(redis.call('HGET', res, 'expiresAt')) or 0
        if nowMs >= expAt then
            return cjson.encode({ ok=false, reason='EXPIRED' })
        end

        -- enter consuming and remove TTL while consuming
        redis.call('HSET', res, 'state', 'consuming', 'orderId', orderId)
        redis.call('PERSIST', res)

        local pid = redis.call('HGET', res, 'productId')
        local qty = tonumber(redis.call('HGET', res, 'qty')) or 0

        return cjson.encode({ ok=true, productId=pid, qty=qty, expiresAt=expAt })
        ";

        private const string LUA_CONSUME_COMMIT = @"
        -- KEYS: [1] inv:{pid}, [2] res:{rid}
        local inv = KEYS[1]
        local res = KEYS[2]

        if redis.call('EXISTS', res) == 0 then return 0 end
        if redis.call('HGET', res, 'state') ~= 'consuming' then return -1 end

        local qty = tonumber(redis.call('HGET', res, 'qty')) or 0
        if qty > 0 then redis.call('HINCRBY', inv, 'reserved', -qty) end
        redis.call('HSET', res, 'state', 'consumed')

        return 1
        ";

        private const string LUA_CONSUME_ROLLBACK = @"
        -- KEYS: [1] res:{rid}
        -- ARGV: [1] nowMs
        local res = KEYS[1]
        local nowMs = tonumber(ARGV[1])

        if redis.call('EXISTS', res) == 0 then return 0 end
        if redis.call('HGET', res, 'state') ~= 'consuming' then return -1 end

        redis.call('HSET', res, 'state', 'active')
        redis.call('HDEL', res, 'orderId')

        local expAt = tonumber(redis.call('HGET', res, 'expiresAt')) or 0
        if nowMs < expAt then
            redis.call('PEXPIREAT', res, expAt)
        else
           redis.call('HSET', res, 'state', 'expired')
        end

        return 1
        ";

  
        public async Task<ReservationDto?> CreateAsync(Guid userId, Guid productId, int quantity, TimeSpan ttl, string? idempotencyKey, CancellationToken ct = default)
        {
            if (quantity <= 0) return null;

            var now = DateTime.UtcNow;
            var rid = Guid.NewGuid().ToString("N");

            var keys = string.IsNullOrWhiteSpace(idempotencyKey)
                ? new RedisKey[] { InvKey(productId), ResKey(rid), UserSetKey(userId), (RedisKey)"idem:_skip" }
                : new RedisKey[] { InvKey(productId), ResKey(rid), UserSetKey(userId), (RedisKey)IdemKey(idempotencyKey!) };

            RedisResult eval = await _db.ScriptEvaluateAsync(
                LUA_RESERVE,
                keys,
                new RedisValue[] {
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    (long)ttl.TotalMilliseconds,
                    userId.ToString("N"),
                    productId.ToString("N"),
                    quantity,
                    rid,
                    idempotencyKey ?? string.Empty,
                    DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeMilliseconds()
                });

            if (eval.IsNull) return null; 

            var result = eval.ToString();
            if (string.IsNullOrWhiteSpace(result)) return null;

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;

            if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            {
                return new ReservationDto(
                    Id: Guid.Parse(rid),
                    UserId: userId,
                    ProductId: productId,
                    OrderId: null,
                    ExpiryTimeUtc: now.Add(ttl),
                    CreatedAtUtc: now,
                    Status: ReservationComputedStatus.Active,
                    Quantity: quantity
                );
            }

            if (root.TryGetProperty("reason", out var reason) && reason.GetString() == "IDEMPOTENT_HIT")
            {
                var existed = root.GetProperty("rid").GetString()!;
                return await GetAsync(existed, ct);
            }

            return null;
        }

        public async Task<ReservationDto?> GetAsync(string reservationId, CancellationToken ct = default)
        {
            var resKey = ResKey(reservationId);
            var h = await _db.HashGetAllAsync(resKey);
            if (h.Length == 0) return null;

            string V(string n)
            {
                foreach (var e in h) if (e.Name == n) return e.Value.ToString();
                return "";
            }

            var userId = Guid.TryParse(V("userId"), out var u) ? u : Guid.Empty;
            var productId = Guid.TryParse(V("productId"), out var p) ? p : Guid.Empty;
            var qty = int.TryParse(V("qty"), NumberStyles.Any, CultureInfo.InvariantCulture, out var q) ? q : 0;
            var created = long.TryParse(V("createdAt"), out var c) ? DateTimeOffset.FromUnixTimeMilliseconds(c).UtcDateTime : DateTime.UtcNow;
            var exp = long.TryParse(V("expiresAt"), out var ex) ? DateTimeOffset.FromUnixTimeMilliseconds(ex).UtcDateTime : DateTime.UtcNow.AddMinutes(5);
            Guid? orderId = Guid.TryParse(V("orderId"), out var o) ? o : (Guid?)null;

            var state = V("state") switch
            {
                "active" => ReservationComputedStatus.Active,
                "consuming" => ReservationComputedStatus.Active,
                "consumed" => ReservationComputedStatus.Consumed,
                "expired" => ReservationComputedStatus.Expired,
                _ => ReservationComputedStatus.Expired
            };

            return new ReservationDto(
                Id: Guid.Parse(reservationId),
                UserId: userId,
                ProductId: productId,
                OrderId: orderId,
                ExpiryTimeUtc: exp,
                CreatedAtUtc: created,
                Status: state,
                Quantity: qty
            );
        }

        public async Task<bool> CancelAsync(string reservationId, CancellationToken ct = default)
        {
            var resKey = ResKey(reservationId);
            var pid = await _db.HashGetAsync(resKey, "productId");
            var uid = await _db.HashGetAsync(resKey, "userId");
            if (!pid.HasValue || !uid.HasValue) return false;

            RedisResult eval = await _db.ScriptEvaluateAsync(
                LUA_CANCEL,
                new RedisKey[] { InvKey(Guid.Parse(pid!)), resKey, UserSetKey(Guid.Parse(uid!)) },
                new RedisValue[] { DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            if (eval.IsNull) return false;
            var r = (long)eval;
            return r == 1;
        }

        public async Task<(bool ok, Guid productId, int qty, DateTime? expiresAtUtc)> BeginConsumeAsync(string reservationId, string orderId, CancellationToken ct = default)
        {
            var resKey = ResKey(reservationId);
            RedisResult eval = await _db.ScriptEvaluateAsync(
                LUA_CONSUME_BEGIN,
                new RedisKey[] { resKey },
                new RedisValue[] { DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), orderId });

            if (eval.IsNull) return (false, Guid.Empty, 0, null);

            var result = eval.ToString();
            if (string.IsNullOrWhiteSpace(result)) return (false, Guid.Empty, 0, null);

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                return (false, Guid.Empty, 0, null);

            var pid = Guid.Parse(root.GetProperty("productId").GetString()!);
            var qty = root.GetProperty("qty").GetInt32();
            var expAt = DateTimeOffset.FromUnixTimeMilliseconds(root.GetProperty("expiresAt").GetInt64()).UtcDateTime;

            return (true, pid, qty, expAt);
        }

        public async Task<bool> CommitConsumeAsync(string reservationId, CancellationToken ct = default)
        {
            var pid = await _db.HashGetAsync(ResKey(reservationId), "productId");
            if (!pid.HasValue) return false;

            RedisResult eval = await _db.ScriptEvaluateAsync(
                LUA_CONSUME_COMMIT,
                new RedisKey[] { InvKey(Guid.Parse(pid!)), ResKey(reservationId) },
                Array.Empty<RedisValue>());

            if (eval.IsNull) return false;
            var r = (long)eval;
            return r == 1;
        }

        public async Task<bool> RollbackConsumeAsync(string reservationId, CancellationToken ct = default)
        {
            RedisResult eval = await _db.ScriptEvaluateAsync(
                LUA_CONSUME_ROLLBACK,
                new RedisKey[] { ResKey(reservationId) },
                new RedisValue[] { DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            if (eval.IsNull) return false;
            var r = (long)eval;
            return r == 1;
        }
    }
}
