namespace ReservationService.Application.Contracts;

public interface IInventoryFinalizer
{
    Task<bool> FinalizeAsync(Guid productId, int quantity, CancellationToken ct = default);
}
