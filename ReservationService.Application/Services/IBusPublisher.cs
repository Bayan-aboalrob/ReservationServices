namespace ReservationService.Application.Services;

public interface IBusPublisher
{
    Task PublishAsync(string type, object payload, CancellationToken ct);
}
