using MediatR;
using ReservationService.Application.Commands.CreateReservation;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;

namespace ReservationService.Application.Handlers
{
    public sealed class CreateCartReservationHandler
        : IRequestHandler<CreateCartReservationCommand, IReadOnlyCollection<ReservationDto>>
    {
        private readonly ICartReader _cartReader;
        private readonly IReservationCache _cache;
        private readonly IBusPublisher _bus;

        public CreateCartReservationHandler(
            ICartReader cartReader,
            IReservationCache cache,
            IBusPublisher bus)
        {
            _cartReader = cartReader;
            _cache = cache;
            _bus = bus;
        }

        public async Task<IReadOnlyCollection<ReservationDto>> Handle(
            CreateCartReservationCommand request,
            CancellationToken ct)
        {
            var cart = await _cartReader.GetCartWithItemsAsync(request.CartId, ct);
            if (cart is null || cart.Items.Count == 0)
                throw new InvalidOperationException("Cart not found or empty.");

            if (cart.UserId != request.UserId)
                throw new InvalidOperationException("Cart does not belong to the user.");

            var ttl = TimeSpan.FromSeconds(request.TtlSeconds <= 0 ? 180 : request.TtlSeconds);
            var created = new List<ReservationDto>();

            foreach (var item in cart.Items)
            {
                if (item.Quantity <= 0)
                    continue;

                var dto = await _cache.CreateAsync(
                    cart.UserId,
                    item.ProductId,
                    item.Quantity,
                    ttl,
                    request.IdempotencyKey,
                    ct
                );

                if (dto is not null)
                    created.Add(dto);
            }

            if (request.UseDistributedMode && created.Count > 0)
            {
                var payload = new
                {
                    cartId = request.CartId,
                    userId = request.UserId,
                    correlationId = request.CorrelationId,
                    expiresAtUtc = DateTime.UtcNow.Add(ttl),
                    reservations = created.Select(r => new
                    {
                        reservationId = r.Id,
                        productId = r.ProductId,
                        quantity = r.Quantity,
                        expiryTimeUtc = r.ExpiryTimeUtc
                    })
                };

                if (request.ExecutionMode == ReservationExecutionMode.Synchronous)
                {
                    await _bus.PublishAsync("Reservation.CartCreated", payload, ct);
                }
                else
                {
                    _ = Task.Run(() =>
                        _bus.PublishAsync("Reservation.CartCreated", payload, CancellationToken.None));
                }
            }

            return created;
        }
    }
}
