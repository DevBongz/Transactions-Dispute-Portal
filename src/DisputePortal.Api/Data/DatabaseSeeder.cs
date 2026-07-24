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
        var now = DateTimeOffset.UtcNow;
        var usersExisted = await db.Users.AnyAsync();

        if (!usersExisted)
        {
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
            logger.LogInformation("Seeded demo users.");
        }
        else
        {
            logger.LogInformation("Seed users skipped: already present.");
        }

        // Idempotent by reference: fresh DBs get the full set; existing DBs only gain new demo rows.
        var existingRefs = await db.Transactions.Select(t => t.Reference).ToListAsync();
        var existing = existingRefs.ToHashSet(StringComparer.Ordinal);
        var desired = BuildDemoTransactions(now);
        var missing = desired.Where(t => !existing.Contains(t.Reference)).ToArray();

        if (missing.Length > 0)
        {
            await db.Transactions.AddRangeAsync(missing);
            logger.LogInformation("Seeded {Count} transaction(s).", missing.Length);
        }
        else
        {
            logger.LogInformation("Seed transactions skipped: all demo references already present.");
        }

        if (!usersExisted || missing.Length > 0)
            await db.SaveChangesAsync();
    }

    /// <summary>
    /// Demo ledger for Maya. Each row is chosen to back a clear dispute narrative
    /// (duplicate, wrong amount, merchant error, unauthorised / high-value priority).
    /// </summary>
    private static Transaction[] BuildDemoTransactions(DateTimeOffset now)
    {
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

        return
        [
            // --- SPEC / original journeys ---
            // Journey 1: identical Shoprite pair → DUPLICATE_CHARGE
            NewTxn("TXN-20260710-00001", "Shoprite", "Grocery Stores", 450.00m, TransactionStatus.SETTLED, now.AddDays(-11)),
            NewTxn("TXN-20260714-00002", "Shoprite", "Grocery Stores", 450.00m, TransactionStatus.SETTLED, now.AddDays(-7)),
            NewTxn("TXN-20260715-00003", "Uber", "Transportation", 129.50m, TransactionStatus.SETTLED, now.AddDays(-6)),
            NewTxn("TXN-20260716-00004", "Takealot", "E-commerce", 1299.00m, TransactionStatus.PENDING, now.AddDays(-5)),
            // >R5000 + unauthorised wording → HIGH / CRITICAL classification
            NewTxn("TXN-20260717-00005", "Unknown Merch", "Uncategorised", 7999.99m, TransactionStatus.SETTLED, now.AddDays(-4)),
            NewTxn("TXN-20260718-00006", "Netflix", "Streaming", 199.00m, TransactionStatus.SETTLED, now.AddDays(-3)),

            // --- Extra demo narratives ---
            // WRONG_AMOUNT: pump showed ~R720, statement shows R856.40
            NewTxn("TXN-20260708-00007", "Shell", "Fuel", 856.40m, TransactionStatus.SETTLED, now.AddDays(-13)),
            // MERCHANT_ERROR / OTHER: subscription cancelled but still billed
            NewTxn("TXN-20260711-00008", "DSTV", "Subscriptions", 899.00m, TransactionStatus.SETTLED, now.AddDays(-10)),
            // UNAUTHORISED: surprise digital goods / card-not-present
            NewTxn("TXN-20260712-00009", "Steam", "Digital Goods", 749.00m, TransactionStatus.SETTLED, now.AddDays(-9)),
            // DUPLICATE_CHARGE: same-day Engen pair
            NewTxn("TXN-20260713-00010", "Engen", "Fuel", 720.00m, TransactionStatus.SETTLED, now.AddDays(-8)),
            NewTxn("TXN-20260713-00011", "Engen", "Fuel", 720.00m, TransactionStatus.SETTLED, now.AddDays(-8).AddHours(3)),
            // MERCHANT_ERROR: returned goods, still charged
            NewTxn("TXN-20260719-00012", "Woolworths", "Retail", 2450.00m, TransactionStatus.SETTLED, now.AddDays(-2)),
            // CRITICAL path: high-value travel / unauthorised booking
            NewTxn("TXN-20260720-00013", "Booking.com", "Travel", 12450.00m, TransactionStatus.SETTLED, now.AddDays(-1)),
            // UNAUTHORISED: SIM-swap / airtime fraud narrative
            NewTxn("TXN-20260721-00014", "MTN Airtime", "Telecoms", 500.00m, TransactionStatus.SETTLED, now),
            // Everyday spend (filter/list demos; optional dispute)
            NewTxn("TXN-20260709-00015", "Dis-Chem", "Pharmacy", 312.50m, TransactionStatus.SETTLED, now.AddDays(-12)),
            // Already reversed — shows status variety; typically not disputed
            NewTxn("TXN-20260707-00016", "Pick n Pay", "Grocery Stores", 678.20m, TransactionStatus.REVERSED, now.AddDays(-14)),
        ];
    }
}
