namespace ReservationService.Application.Services;

public interface IInventoryFinalizer
{
    Task<bool> FinalizeAsync(Guid productId, int qty, CancellationToken ct);
}
