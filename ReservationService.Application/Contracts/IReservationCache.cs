using ReservationService.Application.Dtos;

namespace ReservationService.Application.Contracts;

public interface IReservationCache
{
    Task<ReservationDto> CreateAsync(
        Guid userId, Guid productId, int quantity, TimeSpan ttl, string idempotencyKey, CancellationToken ct);

    Task<ReservationDto?> GetAsync(Guid reservationId, CancellationToken ct);

    Task<bool> CancelAsync(Guid reservationId, CancellationToken ct);

    Task<(bool ok, Guid productId, int qty)> ConsumeAsync(Guid reservationId, Guid orderId, CancellationToken ct);
}
