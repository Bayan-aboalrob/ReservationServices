using MediatR;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Handlers;

public class GetReservationHandler : IRequestHandler<Queries.GetReservation.GetReservationQuery, ReservationDto?>
{
    private readonly IReservationCache _cache;
    public GetReservationHandler(IReservationCache cache) => _cache = cache;
    public Task<ReservationDto?> Handle(Queries.GetReservation.GetReservationQuery q, CancellationToken ct) =>
        _cache.GetAsync(q.ReservationId, ct);
}
