using MediatR;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;
using ReservationService.Application.Queries.GetReservation;

namespace ReservationService.Application.Handlers
{
    public sealed class GetReservationHandler : IRequestHandler<GetReservationQuery, ReservationDto?>
    {
        private readonly IReservationCache _cache;
        public GetReservationHandler(IReservationCache cache) => _cache = cache;

        public Task<ReservationDto?> Handle(GetReservationQuery request, CancellationToken ct)
            => _cache.GetAsync(request.ReservationId, ct);
    }
}
