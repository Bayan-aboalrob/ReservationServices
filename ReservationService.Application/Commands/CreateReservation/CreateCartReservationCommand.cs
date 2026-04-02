using MediatR;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Commands.CreateReservation
{
    public sealed record CreateCartReservationCommand(
        Guid UserId,
        Guid CartId,
        int TtlSeconds,
        string? IdempotencyKey,
        bool UseDistributedMode,
        string? CorrelationId,
        ReservationExecutionMode ExecutionMode
    ) : IRequest<IReadOnlyCollection<ReservationDto>>;
}
