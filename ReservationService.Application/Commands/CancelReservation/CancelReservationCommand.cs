using MediatR;

namespace ReservationService.Application.Commands.CancelReservation;
public sealed record CancelReservationCommand(string ReservationId) : IRequest<bool>;

