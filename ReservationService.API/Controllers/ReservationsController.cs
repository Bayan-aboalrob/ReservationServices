// ReservationService.API/Controllers/ReservationsController.cs
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Application.Commands.CancelReservation;
using ReservationService.Application.Commands.ConsumeReservation;
using ReservationService.Application.Dtos;
using ReservationService.Application.Queries.GetReservation;

namespace ReservationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ReservationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ReservationsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("{id}", Name = "GetReservationById")]
        public Task<ReservationDto?> GetById(string id)
            => _mediator.Send(new GetReservationQuery(id));

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(string id)
            => await _mediator.Send(new CancelReservationCommand(id)) ? NoContent() : NotFound();

        [HttpPost("{id}/consume")]
        public async Task<IActionResult> Consume(string id, [FromBody] ConsumeReservationRequest req)
        {
            var normalizedId = id.Replace("-", string.Empty);

            var ok = await _mediator.Send(
                new ConsumeReservationCommand(normalizedId, req.OrderId)
            );

            return ok ? NoContent() : NotFound();
        }
    }

    public sealed record ConsumeReservationRequest(string OrderId);
}
