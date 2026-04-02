using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Application.Commands.CreateReservation;
using ReservationService.Application.Dtos;

namespace ReservationService.API.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public sealed class ReservationsV1Controller : ControllerBase
    {
        private readonly IMediator _mediator;
        public ReservationsV1Controller(IMediator mediator) => _mediator = mediator;

        [HttpPost("from-cart")]
        public async Task<ActionResult<IReadOnlyCollection<CartReservationResponse>>> CreateFromCart(
            [FromBody] V1CreateReservationFromCartRequest req)
        {
            var cmd = new CreateCartReservationCommand(
                req.UserId,
                req.CartId,
                req.TtlSeconds,
                req.IdempotencyKey,
                UseDistributedMode: true,
                req.CorrelationId,
                ReservationExecutionMode.Synchronous
            );

            var list = await _mediator.Send(cmd);

            var response = list
                .Select(r => new CartReservationResponse(
                    CartId: req.CartId,
                    Id: r.Id,
                    UserId: r.UserId,
                    ExpiryTimeUtc: r.ExpiryTimeUtc,
                    CreatedAtUtc: r.CreatedAtUtc,
                    Status: r.Status
                ))
                .ToList();

            return Ok(response);
        }
    }

    public sealed record V1CreateReservationFromCartRequest(
        Guid UserId,
        Guid CartId,
        int TtlSeconds,
        string? IdempotencyKey,
        string? CorrelationId
    );

    public sealed record CartReservationResponse(
        Guid CartId,
        Guid Id,
        Guid UserId,
        DateTime ExpiryTimeUtc,
        DateTime CreatedAtUtc,
        ReservationComputedStatus Status
    );
}
