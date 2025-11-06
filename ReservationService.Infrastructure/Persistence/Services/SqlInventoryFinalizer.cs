using System;
using System.Threading;
using System.Threading.Tasks;
using FlashSaleDB;
using Microsoft.EntityFrameworkCore;
using ReservationService.Application.Contracts;

namespace ReservationService.Infrastructure.Services
{
    internal sealed class SqlInventoryFinalizer : IInventoryFinalizer
    {
        private readonly FlashSaleDbContext _db;
        public SqlInventoryFinalizer(FlashSaleDbContext db) => _db = db;

        public async Task<bool> FinalizeAsync(Guid productId, int quantity, CancellationToken ct = default)
        {
            if (quantity <= 0) return false;

            var rows = await _db.Database.ExecuteSqlRawAsync(@"
                UPDATE Inventory WITH (ROWLOCK, UPDLOCK)
                SET AvailableQuantity = AvailableQuantity - {0}
                WHERE ProductId = {1} AND AvailableQuantity >= {0};
                ", new object[] { quantity, productId }, ct);

            return rows > 0;
        }
    }
}
