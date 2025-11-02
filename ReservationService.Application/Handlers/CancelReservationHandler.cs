using MediatR;
using ReservationService.Application.Commands.CancelReservation;
using ReservationService.Application.Contracts;

namespace ReservationService.Application.Handlers;

public class CancelReservationHandler : IRequestHandler<CancelReservationCommand, bool>
{
    private readonly IReservationCache _cache;
    public CancelReservationHandler(IReservationCache cache) => _cache = cache;

    public Task<bool> Handle(CancelReservationCommand r, CancellationToken ct) =>
        _cache.CancelAsync(r.ReservationId, ct);
}
