using MediatR;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Queries.GetReservation;
public record GetReservationQuery(Guid ReservationId) : IRequest<ReservationDto?>;
