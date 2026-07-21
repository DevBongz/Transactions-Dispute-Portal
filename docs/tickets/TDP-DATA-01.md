# TDP-DATA-01 — EF Core Domain Models & DbContext

**Jira summary:** Implement the five core domain entities — `User`, `Transaction`, `Dispute`, `DisputeEvent`, `Resolution` — as C# classes in `src/DisputePortal.Api/Domain`, together with `DisputePortalDbContext` (Npgsql-backed) and its Fluent API configuration in `src/DisputePortal.Api/Data`. This ticket defines the persistent shape of the whole system: table/column mappings, PostgreSQL types (UUID, TIMESTAMPTZ, JSONB, DECIMAL), unique constraints, enum-as-string storage, and all foreign-key relationships from SPEC §3.2. It produces the model the migrations (TDP-DATA-02) and every repository/service will build on.

## 1. Context & Motivation

- **Background:** The scaffold (TDP-INFRA-01) reserved `Domain/` and `Data/` folders and referenced `Npgsql.EntityFrameworkCore.PostgreSQL`, but there are no entities and no `DbContext` yet. SPEC §3.2 specifies five entities with exact columns, types, nullability, and a relationships summary. Nothing can be persisted or queried until these exist.
- **Business Impact:** Every functional requirement — transaction listing (TXN-01..03), dispute submission (DISP-01..04), tracking (TRACK-01..03), ops resolution (OPS-01..06), AI classification (AI-02) — reads or writes these tables. Getting the model right on Day 1 avoids expensive schema churn later in the 7-day timeline (SPEC §4.1).
- **User Story:** As a backend developer, I want strongly-typed EF Core entities and a configured `DbContext` matching SPEC §3.2 exactly, so that I can write repositories and services against a stable, correctly-typed PostgreSQL schema.
- **Dependencies:** **TDP-INFRA-01** (project, folders, Npgsql package). Directly enables **TDP-DATA-02** (migrations + seed) and **TDP-AUTH-01** (reads `User`). Maps to **Milestone Day 1 — Foundation** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Enums

Model the constrained string columns as C# enums stored as strings (via `HasConversion<string>()`), so the DB holds human-readable values matching SPEC §3.2 exactly. Place in `src/DisputePortal.Api/Domain/Enums.cs`.

```csharp
namespace DisputePortal.Api.Domain;

public enum UserRole { CUSTOMER, OPS_ANALYST, OPS_MANAGER }

public enum TransactionStatus { SETTLED, PENDING, REVERSED }

public enum DisputeStatus { OPEN, UNDER_REVIEW, RESOLVED, CLASSIFICATION_FAILED }

public enum DisputeCategory { UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER }

public enum DisputePriority { LOW, MEDIUM, HIGH, CRITICAL }

public enum DisputeEventType { SUBMITTED, CLASSIFIED, ASSIGNED, UNDER_REVIEW, RESOLVED }

public enum ResolutionOutcome { UPHELD, DECLINED, PARTIAL }
```

### 2.2 Entities — `src/DisputePortal.Api/Domain/`

Each entity is a POCO. Nullability follows SPEC §3.2 (`category`/`priority` are null until classified; `assigned_to_id`, `actor_id` nullable). Navigation properties model the relationships.

`User.cs`:

```csharp
public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;   // bcrypt, work factor >= 12
    public string FullName { get; set; } = default!;
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}
```

`Transaction.cs`:

```csharp
public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Reference { get; set; } = default!;        // TXN-YYYYMMDD-NNNNN
    public string MerchantName { get; set; } = default!;
    public string? MerchantCategory { get; set; }            // MCC description
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";            // ISO 4217, CHAR(3)
    public DateTimeOffset TransactionDate { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User Customer { get; set; } = default!;
    public Dispute? Dispute { get; set; }                    // zero or one
}
```

`Dispute.cs`:

```csharp
public sealed class Dispute
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = default!;        // DSP-YYYYMMDD-NNNNN
    public Guid TransactionId { get; set; }
    public Guid CustomerId { get; set; }
    public DisputeStatus Status { get; set; }
    public DisputeCategory? Category { get; set; }           // null until classified
    public DisputePriority? Priority { get; set; }           // null until classified
    public string CustomerDescription { get; set; } = default!;
    public string? ExtractedFieldsJson { get; set; }         // JSONB
    public Guid? AssignedToId { get; set; }                  // ops analyst, nullable
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Transaction Transaction { get; set; } = default!;
    public User Customer { get; set; } = default!;
    public User? AssignedTo { get; set; }
    public ICollection<DisputeEvent> Events { get; set; } = new List<DisputeEvent>();
    public Resolution? Resolution { get; set; }              // zero or one
}
```

`DisputeEvent.cs`:

```csharp
public sealed class DisputeEvent
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }
    public DisputeEventType EventType { get; set; }
    public Guid? ActorId { get; set; }                       // null for system events
    public string Description { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public Dispute Dispute { get; set; } = default!;
    public User? Actor { get; set; }
}
```

`Resolution.cs`:

```csharp
public sealed class Resolution
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }                      // UNIQUE — one per dispute
    public ResolutionOutcome Outcome { get; set; }
    public string InternalNotes { get; set; } = default!;
    public string? CustomerSummary { get; set; }             // AI-generated
    public Guid ResolvedById { get; set; }
    public DateTimeOffset ResolvedAt { get; set; }

    public Dispute Dispute { get; set; } = default!;
    public User ResolvedBy { get; set; } = default!;
}
```

### 2.3 `DisputePortalDbContext` — `src/DisputePortal.Api/Data/DisputePortalDbContext.cs`

```csharp
using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Data;

public sealed class DisputePortalDbContext(DbContextOptions<DisputePortalDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvent> DisputeEvents => Set<DisputeEvent>();
    public DbSet<Resolution> Resolutions => Set<Resolution>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(DisputePortalDbContext).Assembly);
    }
}
```

### 2.4 Fluent configuration — `src/DisputePortal.Api/Data/Configurations/`

Use `IEntityTypeConfiguration<T>` per entity so the mapping is explicit and testable. Snake_case table/column names match SPEC §3.2 exactly. Enums stored as strings.

`UserConfiguration.cs`:

```csharp
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        e.Property(x => x.Role).HasColumnName("role").HasMaxLength(50)
            .HasConversion<string>().IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
```

`TransactionConfiguration.cs` (key points):

```csharp
e.ToTable("transactions");
e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100).IsRequired();
e.HasIndex(x => x.Reference).IsUnique();
e.Property(x => x.MerchantName).HasColumnName("merchant_name").HasMaxLength(255).IsRequired();
e.Property(x => x.MerchantCategory).HasColumnName("merchant_category").HasMaxLength(100);
e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
e.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").HasDefaultValue("ZAR");
e.Property(x => x.TransactionDate).HasColumnName("transaction_date").IsRequired();
e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().IsRequired();
e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
e.HasOne(x => x.Customer).WithMany(u => u.Transactions)
    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
```

`DisputeConfiguration.cs` (key points — note JSONB and the one-to-one to Transaction/Resolution):

```csharp
e.ToTable("disputes");
e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(30).IsRequired();
e.HasIndex(x => x.Reference).IsUnique();
e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasConversion<string>().IsRequired();
e.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).HasConversion<string?>();   // nullable
e.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20).HasConversion<string?>();   // nullable
e.Property(x => x.CustomerDescription).HasColumnName("customer_description").IsRequired();
e.Property(x => x.ExtractedFieldsJson).HasColumnName("extracted_fields_json").HasColumnType("jsonb");
e.Property(x => x.AssignedToId).HasColumnName("assigned_to_id");
e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

// One Transaction -> zero or one Dispute (unique FK)
e.HasOne(x => x.Transaction).WithOne(t => t.Dispute)
    .HasForeignKey<Dispute>(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
e.HasIndex(x => x.TransactionId).IsUnique();

e.HasOne(x => x.Customer).WithMany(u => u.Disputes)
    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
e.HasOne(x => x.AssignedTo).WithMany()
    .HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);

// helpful indexes for ops filtering (OPS-01/02) and dashboard (OPS-06)
e.HasIndex(x => x.Status);
e.HasIndex(x => new { x.Priority, x.Status });
```

`DisputeEventConfiguration.cs` (key points):

```csharp
e.ToTable("dispute_events");
e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).HasConversion<string>().IsRequired();
e.Property(x => x.ActorId).HasColumnName("actor_id");
e.Property(x => x.Description).HasColumnName("description").IsRequired();
e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
e.HasOne(x => x.Dispute).WithMany(d => d.Events)
    .HasForeignKey(x => x.DisputeId).OnDelete(DeleteBehavior.Cascade);
e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
e.HasIndex(x => x.DisputeId);
```

`ResolutionConfiguration.cs` (key points — unique one-to-one with Dispute):

```csharp
e.ToTable("resolutions");
e.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(50).HasConversion<string>().IsRequired();
e.Property(x => x.InternalNotes).HasColumnName("internal_notes").IsRequired();
e.Property(x => x.CustomerSummary).HasColumnName("customer_summary");
e.Property(x => x.ResolvedById).HasColumnName("resolved_by_id").IsRequired();
e.Property(x => x.ResolvedAt).HasColumnName("resolved_at").IsRequired();
e.HasOne(x => x.Dispute).WithOne(d => d.Resolution)
    .HasForeignKey<Resolution>(x => x.DisputeId).OnDelete(DeleteBehavior.Cascade);
e.HasIndex(x => x.DisputeId).IsUnique();
e.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.Restrict);
```

### 2.5 DI registration in `Program.cs`

Register the context with Npgsql, reading the connection string the compose stack injects (`ConnectionStrings__Default`, SPEC §3.1):

```csharp
builder.Services.AddDbContext<DisputePortalDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

### 2.6 Relationships summary (SPEC §3.2)

- One `User` → many `Transaction` (as customer) and many `Dispute`.
- One `Transaction` → zero or one `Dispute` (unique FK + unique index on `transaction_id`).
- One `Dispute` → many `DisputeEvent` (cascade delete), zero or one `Resolution` (unique FK, cascade).
- `assigned_to_id` and `actor_id` are nullable FKs to `User` with `SetNull` on delete.

## 3. Acceptance Criteria

- All five entities exist under `src/DisputePortal.Api/Domain` with properties matching SPEC §3.2 column names, types, and nullability.
- `DisputePortalDbContext` exposes `DbSet`s for all five entities and applies configurations from the assembly.
- Table names are snake_case (`users`, `transactions`, `disputes`, `dispute_events`, `resolutions`) and column names match SPEC §3.2 exactly.
- `email` (User) and `reference` (Transaction, Dispute) have unique indexes; `disputes.transaction_id` and `resolutions.dispute_id` are unique (enforcing the zero-or-one relationships).
- `amount` maps to `decimal(18,2)`; `currency` to `char(3)` default `ZAR`; `extracted_fields_json` to `jsonb`; all timestamp columns to `timestamptz`.
- Enum columns (`role`, `status`, `category`, `priority`, `event_type`, `outcome`) are stored as their string names, not integers.
- `dotnet build` is green and `dotnet ef migrations add` (in TDP-DATA-02) can read a valid model with no warnings about unmapped members.
- All queries are parameterised through EF Core (SPEC §3.6 SQL-injection prevention) — no raw string SQL introduced.

## 4. Technical Notes

- **`DateTimeOffset` + Npgsql:** Npgsql maps `DateTimeOffset` to `timestamptz` and normalises to UTC. Always persist UTC (`DateTimeOffset.UtcNow`) to avoid the "Cannot write DateTimeOffset with non-zero offset" exception. This satisfies the TIMESTAMPTZ requirement in SPEC §3.2.
- **Nullable enum conversion:** `HasConversion<string?>()` is required for the nullable `Category`/`Priority`; using the non-nullable converter drops nullability and breaks the "null until classified" contract (SPEC §3.2, AC-AI-02).
- **JSONB:** `extracted_fields_json` is stored as raw JSON text in a `jsonb` column. Keep it as `string?` here; serialization of the AI extraction payload is TDP-AI-01/DISP-01's concern. This keeps the domain layer free of AI DTO coupling.
- **`DeleteBehavior.Restrict`** on customer FKs prevents accidental cascade deletion of a user wiping their transactions/disputes; `Cascade` is intentional only for `DisputeEvent` and `Resolution` (children of a dispute).
- **Composite index `(Priority, Status)`** anticipates the ops queue "open disputes ranked by priority" (OPS-01) and dashboard aggregations (OPS-06); cheap to add now.
- **Do not seed here:** entity/config only. Seed data and migrations belong to TDP-DATA-02; keeping them separate keeps this ticket reviewable.
- **`char(3)` currency:** stored fixed-length; trim on read if needed. ISO 4217, default `ZAR` (single-currency scope per SPEC §1.2 Out-of-Scope).

## 5. Definition of Done

- [ ] Five entity classes created under `Domain/` matching SPEC §3.2.
- [ ] `Enums.cs` with all seven enums using SPEC string values.
- [ ] `DisputePortalDbContext` with five `DbSet`s and assembly-scanned configurations.
- [ ] One `IEntityTypeConfiguration<T>` per entity under `Data/Configurations/`, with correct column names, types, unique indexes, and relationships.
- [ ] `AddDbContext<DisputePortalDbContext>` registered in `Program.cs` using `ConnectionStrings:Default`.
- [ ] `dotnet build DisputePortal.sln` green; model validates (no EF model warnings).
- [ ] Relationships confirmed against SPEC §3.2 summary (1-to-many, zero-or-one, nullable FKs).
- [ ] Reviewed and merged; unblocks TDP-DATA-02 and TDP-AUTH-01.
