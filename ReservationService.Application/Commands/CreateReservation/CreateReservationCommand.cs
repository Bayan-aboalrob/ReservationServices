using MediatR;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Commands.CreateReservation
{
    // this enum will be used by v1 vs v2 controllers
    public enum ReservationExecutionMode
    {
        Synchronous = 0,
        FireAndForgetBus = 1
    }

    public sealed record CreateReservationCommand(
        Guid UserId,
        Guid ProductId,
        int Quantity,
        int TtlSeconds,
        string? IdempotencyKey,
        bool UseDistributedMode,
        string? CorrelationId,
        ReservationExecutionMode ExecutionMode
    ) : IRequest<ReservationDto>;
}
