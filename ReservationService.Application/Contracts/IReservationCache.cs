using System;
using System.Threading;
using System.Threading.Tasks;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Contracts
{
    public interface IReservationCache
    {
        Task<ReservationDto?> CreateAsync(
            Guid userId,
            Guid productId,
            int quantity,
            TimeSpan ttl,
            string? idempotencyKey,
            CancellationToken ct = default);

        Task<ReservationDto?> GetAsync(string reservationId, CancellationToken ct = default);

        Task<bool> CancelAsync(string reservationId, CancellationToken ct = default);

        Task<(bool ok, Guid productId, int qty, DateTime? expiresAtUtc)> BeginConsumeAsync(
            string reservationId,
            string orderId,
            CancellationToken ct = default);

        Task<bool> CommitConsumeAsync(string reservationId, CancellationToken ct = default);

        Task<bool> RollbackConsumeAsync(string reservationId, CancellationToken ct = default);
    }
}
