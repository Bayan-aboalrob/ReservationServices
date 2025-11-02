using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using ReservationService.Application.Services;

namespace ReservationService.Infrastructure.Messaging;

public sealed class RabbitMqPublisher : IBusPublisher, IDisposable
{
    private readonly RabbitMQ.Client.IConnection _conn;
    private readonly RabbitMQ.Client.IModel _ch;
    private readonly ILogger<RabbitMqPublisher> _log;
    private const string EX = "flashsale.topic";

    public RabbitMqPublisher(ILogger<RabbitMqPublisher> log)
    {
        _log = log;
        var f = new ConnectionFactory { HostName = "localhost", DispatchConsumersAsync = true };
        _conn = f.CreateConnection();
        _ch = _conn.CreateModel();
        _ch.ExchangeDeclare(EX, ExchangeType.Topic, durable: true);
    }

    public Task PublishAsync(string type, object payload, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var props = _ch.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;
        _ch.BasicPublish(EX, type, props, body);
        _log.LogInformation("Published {type}", type);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _ch?.Dispose();
        _conn?.Dispose();
    }
}
