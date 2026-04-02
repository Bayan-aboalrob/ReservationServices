namespace ReservationService.Application.Contracts
{
    public interface IBusPublisher
    {
        Task PublishAsync(string routingKey, object payload, CancellationToken ct = default);
    }
}