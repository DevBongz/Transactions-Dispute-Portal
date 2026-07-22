namespace DisputePortal.Api.Domain;

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
