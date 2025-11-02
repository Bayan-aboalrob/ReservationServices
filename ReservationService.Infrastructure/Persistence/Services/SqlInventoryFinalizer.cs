using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReservationService.Application.Services;
using FlashSaleDB;

namespace ReservationService.Infrastructure.Services;

public class SqlInventoryFinalizer : IInventoryFinalizer
{
    private readonly FlashSaleDbContext _db;
    public SqlInventoryFinalizer(FlashSaleDbContext db) => _db = db;

    public async Task<bool> FinalizeAsync(Guid productId, int qty, CancellationToken ct)
    {
        var rows = await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE Inventory WITH (ROWLOCK, UPDLOCK)
            SET AvailableQuantity = CASE WHEN AvailableQuantity >= @qty THEN AvailableQuantity - @qty ELSE AvailableQuantity END
            WHERE ProductId = @pid AND AvailableQuantity >= @qty",
            new[] { new SqlParameter("@qty", qty), new SqlParameter("@pid", productId) }, ct);

        return rows > 0;
    }
}
