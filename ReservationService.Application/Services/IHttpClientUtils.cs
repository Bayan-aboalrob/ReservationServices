namespace ReservationService.Application.Services;

public interface IHttpClientUtils
{
    Task SendPostRequest(string url, object payload);
}