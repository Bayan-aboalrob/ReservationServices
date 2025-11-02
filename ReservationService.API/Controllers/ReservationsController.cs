using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Application.Commands.CreateReservation;
using ReservationService.Application.Commands.CancelReservation;
using ReservationService.Application.Commands.ConsumeReservation;
using ReservationService.Application.Queries.GetReservation;
using ReservationService.Application.Dtos;

namespace ReservationService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReservationsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create(
        [FromBody] CreateReservationRequest req,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Correlation-Id")] string? correlationId)
    {
        var dto = await _mediator.Send(new CreateReservationCommand(
            req.UserId, req.ProductId, req.Quantity,
            req.TtlSeconds,
            idempotencyKey ?? Guid.NewGuid().ToString(),
            req.UseDistributedMode,
            string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString() : correlationId!),
            HttpContext.RequestAborted);

        return Ok(dto);
    }

    [HttpGet("{id:guid}")]
    public Task<ReservationDto?> Get(Guid id) =>
        _mediator.Send(new GetReservationQuery(id), HttpContext.RequestAborted);

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var ok = await _mediator.Send(new CancelReservationCommand(id), HttpContext.RequestAborted);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/consume")]
    public async Task<IActionResult> Consume(Guid id, [FromBody] ConsumeReservationRequest body)
    {
        var ok = await _mediator.Send(new ConsumeReservationCommand(id, body.OrderId), HttpContext.RequestAborted);
        return ok ? NoContent() : NotFound();
    }
}

public record CreateReservationRequest(
    Guid UserId,
    Guid ProductId,
    int Quantity,
    int TtlSeconds,
    bool UseDistributedMode
);

public record ConsumeReservationRequest(Guid OrderId);
