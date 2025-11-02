using MediatR;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Commands.CreateReservation;

public record CreateReservationCommand(
    Guid UserId,
    Guid ProductId,
    int Quantity,
    int TtlSeconds,
    string IdempotencyKey,
    bool UseDistributedMode,
    string CorrelationId
) : IRequest<ReservationDto>;
