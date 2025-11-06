using MediatR;

namespace ReservationService.Application.Commands.ConsumeReservation;
public sealed record ConsumeReservationCommand(string ReservationId, string OrderId) : IRequest<bool>;
