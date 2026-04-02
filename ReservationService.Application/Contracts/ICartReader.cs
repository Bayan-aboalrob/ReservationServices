using ReservationService.Application.Dtos;

namespace ReservationService.Application.Contracts
{
    public interface ICartReader
    {
        Task<CartDto?> GetCartWithItemsAsync(Guid cartId, CancellationToken ct = default);
    }
}
