using FlashSaleDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ReservationService.Infrastructure.Startup
{
    internal sealed class InventoryWarmupHostedService : IHostedService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<InventoryWarmupHostedService> _log;

        public InventoryWarmupHostedService(IServiceProvider sp, ILogger<InventoryWarmupHostedService> log)
        {
            _sp = sp; _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>();
                var mux = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                var redis = mux.GetDatabase();

                var items = await db.Inventory.AsNoTracking().ToListAsync(cancellationToken);

                var tasks = new List<Task>(items.Count);
                foreach (var inv in items)
                {
                    tasks.Add(redis.HashSetAsync($"inv:{inv.ProductId:N}", new HashEntry[]
                    {
                        new("available", inv.AvailableQuantity),
                        new("reserved", inv.ReservedQuantity)
                    }));
                }

                await Task.WhenAll(tasks);

                _log.LogInformation("Warm-up loaded {Count} inventory rows into Redis.", items.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Inventory warm-up failed.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
