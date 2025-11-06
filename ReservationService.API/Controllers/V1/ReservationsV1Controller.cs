using System;
using System.Threading.Tasks;
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

        [HttpPost]
        public async Task<ActionResult<ReservationDto>> Create([FromBody] V1CreateReservationRequest req)
        {
            var cmd = new CreateReservationCommand(
                req.UserId,
                req.ProductId,
                req.Quantity,
                req.TtlSeconds,
                req.IdempotencyKey,
                UseDistributedMode: true,
                req.CorrelationId,
                ReservationExecutionMode.Synchronous
            );

            var dto = await _mediator.Send(cmd);
            return CreatedAtRoute("GetReservationById", new { id = dto.Id }, dto);
        }
    }

    public sealed record V1CreateReservationRequest(
        Guid UserId,
        Guid ProductId,
        int Quantity,
        int TtlSeconds,
        string? IdempotencyKey,
        string? CorrelationId
    );
}
