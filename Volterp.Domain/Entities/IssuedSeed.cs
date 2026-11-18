namespace Volterp.Domain.Entities;

// The seed this platform handed out to a third party that integrates against
// it. A seed is worth nothing until it comes back signed, so what is kept is
// the value, the moment it was issued, the moment it dies and whether it has
// already been used.
//
// It is not tenant data: the seed belongs to whoever asked for it, so the
// table carries no company and no tenant filter applies to it.
public class IssuedSeed
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsExpired(DateTime now) => ExpiresAt <= now;
}
