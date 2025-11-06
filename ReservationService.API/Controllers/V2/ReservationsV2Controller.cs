using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Application.Commands.CreateReservation;
using ReservationService.Application.Dtos;

namespace ReservationService.API.Controllers.V2
{
    [ApiController]
    [Route("api/v2/[controller]")]
    public sealed class ReservationsV2Controller : ControllerBase
    {
        private readonly IMediator _mediator;
        public ReservationsV2Controller(IMediator mediator) => _mediator = mediator;

        [HttpPost]
        public async Task<ActionResult<ReservationDto>> Create([FromBody] V2CreateReservationRequest req)
        {
            var cmd = new CreateReservationCommand(
                req.UserId,
                req.ProductId,
                req.Quantity,
                req.TtlSeconds,
                req.IdempotencyKey,
                UseDistributedMode: true,
                req.CorrelationId,
                ReservationExecutionMode.FireAndForgetBus
            );

            var dto = await _mediator.Send(cmd);
            return CreatedAtRoute("GetReservationById", new { id = dto.Id }, dto);
        }
    }

    public sealed record V2CreateReservationRequest(
        Guid UserId,
        Guid ProductId,
        int Quantity,
        int TtlSeconds,
        string? IdempotencyKey,
        string? CorrelationId
    );
}
