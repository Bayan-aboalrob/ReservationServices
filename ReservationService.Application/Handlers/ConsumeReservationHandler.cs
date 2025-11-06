using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ReservationService.Application.Commands.ConsumeReservation;
using ReservationService.Application.Contracts;

namespace ReservationService.Application.Handlers
{
    public sealed class ConsumeReservationHandler : IRequestHandler<ConsumeReservationCommand, bool>
    {
        private readonly IReservationCache _cache;
        private readonly IInventoryFinalizer _finalizer;

        public ConsumeReservationHandler(IReservationCache cache, IInventoryFinalizer finalizer)
        {
            _cache = cache;
            _finalizer = finalizer;
        }

        public async Task<bool> Handle(ConsumeReservationCommand request, CancellationToken ct)
        {
            var begin = await _cache.BeginConsumeAsync(request.ReservationId, request.OrderId, ct);
            if (!begin.ok) return false;

            var sqlOk = await _finalizer.FinalizeAsync(begin.productId, begin.qty, ct);
            if (!sqlOk)
            {
                await _cache.RollbackConsumeAsync(request.ReservationId, ct);
                return false;
            }

            return await _cache.CommitConsumeAsync(request.ReservationId, ct);
        }
    }
}
