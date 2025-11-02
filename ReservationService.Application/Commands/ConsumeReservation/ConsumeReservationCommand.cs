using MediatR;

namespace ReservationService.Application.Commands.ConsumeReservation;
public record ConsumeReservationCommand(Guid ReservationId, Guid OrderId) : IRequest<bool>;
