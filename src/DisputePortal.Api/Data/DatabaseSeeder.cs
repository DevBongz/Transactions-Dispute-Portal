using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Data;

public static class DatabaseSeeder
{
    // Deterministic IDs for stable cross-references and test assertions.
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnalystId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(DisputePortalDbContext db, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            logger.LogInformation("Seed skipped: users already present.");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // bcrypt work factor 12 (SPEC §3.6 security: >= 12)
        string Hash(string pw) => BCrypt.Net.BCrypt.HashPassword(pw, workFactor: 12);

        var customer = new User
        {
            Id = CustomerId,
            Email = "maya@example.com",
            PasswordHash = Hash("Password123!"),
            FullName = "Maya Naidoo",
            Role = UserRole.CUSTOMER,
            CreatedAt = now
        };
        var analyst = new User
        {
            Id = AnalystId,
            Email = "sipho@capitec.ops",
            PasswordHash = Hash("Password123!"),
            FullName = "Sipho Dlamini",
            Role = UserRole.OPS_ANALYST,
            CreatedAt = now
        };
        var manager = new User
        {
            Id = ManagerId,
            Email = "zanele@capitec.ops",
            PasswordHash = Hash("Password123!"),
            FullName = "Zanele Khumalo",
            Role = UserRole.OPS_MANAGER,
            CreatedAt = now
        };

        await db.Users.AddRangeAsync(customer, analyst, manager);

        var txns = new[]
        {
            NewTxn("TXN-20260710-00001", "Shoprite", "Grocery Stores", 450.00m, TransactionStatus.SETTLED, now.AddDays(-11)),
            NewTxn("TXN-20260714-00002", "Shoprite", "Grocery Stores", 450.00m, TransactionStatus.SETTLED, now.AddDays(-7)),   // duplicate demo
            NewTxn("TXN-20260715-00003", "Uber", "Transportation", 129.50m, TransactionStatus.SETTLED, now.AddDays(-6)),
            NewTxn("TXN-20260716-00004", "Takealot", "E-commerce", 1299.00m, TransactionStatus.PENDING, now.AddDays(-5)),
            NewTxn("TXN-20260717-00005", "Unknown Merch", "Uncategorised", 7999.99m, TransactionStatus.SETTLED, now.AddDays(-4)), // unauthorised demo
            NewTxn("TXN-20260718-00006", "Netflix", "Streaming", 199.00m, TransactionStatus.SETTLED, now.AddDays(-3)),
        };
        await db.Transactions.AddRangeAsync(txns);

        await db.SaveChangesAsync();
        logger.LogInformation("Seed complete: {Users} users, {Txns} transactions.", 3, txns.Length);

        Transaction NewTxn(string reference, string merchant, string mcc, decimal amount,
                           TransactionStatus status, DateTimeOffset date) => new()
                           {
                               Id = Guid.NewGuid(),
                               CustomerId = CustomerId,
                               Reference = reference,
                               MerchantName = merchant,
                               MerchantCategory = mcc,
                               Amount = amount,
                               Currency = "ZAR",
                               TransactionDate = date,
                               Status = status,
                               CreatedAt = now
                           };
    }
}
