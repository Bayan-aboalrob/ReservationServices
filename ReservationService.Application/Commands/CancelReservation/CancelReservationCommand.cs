using MediatR;

namespace ReservationService.Application.Commands.CancelReservation;
public record CancelReservationCommand(Guid ReservationId) : IRequest<bool>;
