namespace ReservationService.Application.Dtos;

public enum ReservationComputedStatus { Active, Consumed, Expired }

public record ReservationDto(
    Guid Id,
    Guid UserId,
    Guid ProductId,
    Guid? OrderId,
    DateTime ExpiryTimeUtc,
    DateTime CreatedAtUtc,
    ReservationComputedStatus Status,
    int Quantity
);
