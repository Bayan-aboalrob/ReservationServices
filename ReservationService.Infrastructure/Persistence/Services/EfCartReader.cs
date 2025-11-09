using FlashSaleDB;
using Microsoft.EntityFrameworkCore;
using ReservationService.Application.Contracts;
using ReservationService.Application.Dtos;

namespace ReservationService.Infrastructure.Services
{
    internal sealed class EfCartReader : ICartReader
    {
        private readonly FlashSaleDbContext _db;

        public EfCartReader(FlashSaleDbContext db)
        {
            _db = db;
        }

        public async Task<CartDto?> GetCartWithItemsAsync(Guid cartId, CancellationToken ct = default)
        {
            var cart = await _db.Cart
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cartId, ct);

            if (cart is null)
                return null;

            var items = await _db.CartItem
                .AsNoTracking()
                .Where(ci => ci.CartId == cartId)
                .Select(ci => new CartItemDto(ci.ProductId, ci.Quantity))
                .ToListAsync(ct);

            return new CartDto(
                cart.Id,
                cart.UserId,
                items
            );
        }
    }
}
