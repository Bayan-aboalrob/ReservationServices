using MediatR;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;
using ReservationService.Application.Services;

namespace ReservationService.Application.Handlers;

public class CreateReservationHandler : IRequestHandler<Commands.CreateReservation.CreateReservationCommand, ReservationDto>
{
    private readonly IReservationCache _cache;
    private readonly IBusPublisher _bus; 
    public CreateReservationHandler(IReservationCache cache, IBusPublisher bus) { _cache = cache; _bus = bus; }

    public async Task<ReservationDto> Handle(Commands.CreateReservation.CreateReservationCommand r, CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(r.TtlSeconds <= 0 ? 180 : r.TtlSeconds);

        var dto = await _cache.CreateAsync(r.UserId, r.ProductId, r.Quantity, ttl, r.IdempotencyKey, ct);

        if (r.UseDistributedMode)
        {
            await _bus.PublishAsync("Reservation.Created", new
            {
                dto.Id,
                dto.UserId,
                dto.ProductId,
                dto.Quantity,
                ExpiryTimeUtc = dto.ExpiryTimeUtc,
                CorrelationId = r.CorrelationId
            }, ct);
        }

        return dto;
    }
}
