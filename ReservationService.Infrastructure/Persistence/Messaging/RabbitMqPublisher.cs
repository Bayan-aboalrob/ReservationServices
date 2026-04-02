using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using ReservationService.Application.Contracts;

namespace ReservationService.Infrastructure.Messaging
{
    internal sealed class RabbitMqPublisher : IBusPublisher, IDisposable
    {
        private readonly ILogger<RabbitMqPublisher> _log;
        private readonly IConnection _conn;
        private readonly IModel _ch;
        private readonly string _exchange;
        private readonly TimeSpan _confirmTimeout;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public RabbitMqPublisher(ILogger<RabbitMqPublisher> log, IConfiguration cfg)
        {
            _log = log;

            var section = cfg.GetSection("RabbitMQ");
            var factory = new ConnectionFactory
            {
                HostName = section["HostName"] ?? "localhost",
                Port = int.TryParse(section["Port"], out var p) ? p : 5672,
                VirtualHost = section["VirtualHost"] ?? "/",
                UserName = section["UserName"] ?? "guest",
                Password = section["Password"] ?? "guest",
                DispatchConsumersAsync = true
            };

            _exchange = section["Exchange"] ?? "flashsale.topic";
            var exchangeType = section["ExchangeType"] ?? "topic";
            _confirmTimeout = TimeSpan.FromSeconds(
                int.TryParse(section["PublisherConfirmTimeoutSeconds"], out var t) ? t : 5);

            _conn = factory.CreateConnection("reservation-publisher");
            _ch = _conn.CreateModel();

            _ch.ExchangeDeclare(_exchange, exchangeType, durable: true, autoDelete: false);

            _ch.ConfirmSelect();
        }

        public Task PublishAsync(string routingKey, object payload, CancellationToken ct = default)
        {
            if (routingKey is null) throw new ArgumentNullException(nameof(routingKey));
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));

            var props = _ch.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // persistent
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _ch.BasicPublish(
                exchange: _exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body
            );

            if (!_ch.WaitForConfirms(_confirmTimeout))
            {
                _log.LogError("RabbitMQ publish not confirmed for routingKey='{RoutingKey}'", routingKey);
                throw new Exception($"RabbitMQ publish not confirmed for '{routingKey}'.");
            }

            _log.LogInformation("Published event routingKey='{RoutingKey}'", routingKey);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try { _ch?.Close(); } catch { }
            try { _conn?.Close(); } catch { }
            _ch?.Dispose();
            _conn?.Dispose();
        }
    }
}
