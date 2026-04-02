using MediatR;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Queries.GetReservation
{
    public sealed record GetReservationQuery(string ReservationId) : IRequest<ReservationDto?>;
}
