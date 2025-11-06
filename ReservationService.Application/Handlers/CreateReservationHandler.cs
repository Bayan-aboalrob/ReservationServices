// ReservationService.Application/Handlers/CreateReservationHandler.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ReservationService.Application.Commands.CreateReservation;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Handlers
{
    public sealed class CreateReservationHandler : IRequestHandler<CreateReservationCommand, ReservationDto>
    {
        private readonly IReservationCache _cache;
        private readonly IBusPublisher _bus;

        public CreateReservationHandler(IReservationCache cache, IBusPublisher bus)
        {
            _cache = cache;
            _bus = bus;
        }

        public async Task<ReservationDto> Handle(CreateReservationCommand request, CancellationToken ct)
        {
            var ttl = TimeSpan.FromSeconds(request.TtlSeconds <= 0 ? 180 : request.TtlSeconds);

            var dto = await _cache.CreateAsync(
                request.UserId,
                request.ProductId,
                request.Quantity,
                ttl,
                request.IdempotencyKey,
                ct
            ) ?? throw new InvalidOperationException("Reservation creation failed (insufficient stock or invalid request).");

            if (!request.UseDistributedMode)
                return dto;

            var payload = new
            {
                dto.Id,
                dto.UserId,
                dto.ProductId,
                dto.Quantity,
                dto.ExpiryTimeUtc,
                request.CorrelationId
            };

            if (request.ExecutionMode == ReservationExecutionMode.Synchronous)
            {
                await _bus.PublishAsync("Reservation.Created", payload, ct);
            }
            else
            {
                _ = Task.Run(() => _bus.PublishAsync("Reservation.Created", payload, CancellationToken.None));
            }

            return dto;
        }
    }
}
