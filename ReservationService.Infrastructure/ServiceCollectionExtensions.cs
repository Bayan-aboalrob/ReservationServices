
using FlashSaleDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReservationService.Application.Contracts;
using ReservationService.Application.Services;
using ReservationService.Infrastructure.Messaging;
using ReservationService.Infrastructure.Redis;
using ReservationService.Infrastructure.Services;
using StackExchange.Redis;
namespace ReservationService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var sql = cfg.GetConnectionString("FlashSaleDb")
                  ?? "Server=.;Database=FlashSaleDatabase;Trusted_Connection=True;TrustServerCertificate=True";
        services.AddDbContext<FlashSaleDbContext>(opt =>
            opt.UseSqlServer(sql, b => b.MigrationsAssembly(typeof(FlashSaleDbContext).Assembly.FullName)));

        var redisConn = cfg.GetSection("Redis")["Connection"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddScoped<IReservationCache, RedisReservationCache>();

        services.AddScoped<IInventoryFinalizer, SqlInventoryFinalizer>();

        services.AddSingleton<IBusPublisher, RabbitMqPublisher>();

        return services;
    }
}
