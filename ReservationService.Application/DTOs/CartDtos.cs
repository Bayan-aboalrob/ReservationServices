namespace ReservationService.Application.Dtos
{
    public sealed record CartItemDto(Guid ProductId, int Quantity);

    public sealed record CartDto(
        Guid Id,
        Guid UserId,
        IReadOnlyCollection<CartItemDto> Items
    );
}
