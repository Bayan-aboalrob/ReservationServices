using MediatR;
using ReservationService.Application.Commands.ConsumeReservation;
using ReservationService.Application.Contracts;
using ReservationService.Application.Services;

namespace ReservationService.Application.Handlers;

public class ConsumeReservationHandler : IRequestHandler<ConsumeReservationCommand, bool>
{
    private readonly IReservationCache _cache;
    private readonly IInventoryFinalizer _finalizer;

    public ConsumeReservationHandler(IReservationCache cache, IInventoryFinalizer finalizer)
    { _cache = cache; _finalizer = finalizer; }

    public async Task<bool> Handle(ConsumeReservationCommand cmd, CancellationToken ct)
    {
        var (ok, productId, qty) = await _cache.ConsumeAsync(cmd.ReservationId, cmd.OrderId, ct);
        if (!ok) return false;

        var finalized = await _finalizer.FinalizeAsync(productId, qty, ct);
        return finalized;
    }
}
