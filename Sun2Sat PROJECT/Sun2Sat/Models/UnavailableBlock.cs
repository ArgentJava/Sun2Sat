using System.ComponentModel.DataAnnotations;
using Sun2Sat.Data;

namespace Sun2Sat.Models;

/// <summary>Time a user has blocked as "Unavailable". Connections see only that the
/// time is blocked; the Note is visible to the owner alone.</summary>
public class UnavailableBlock : IValidatableObject
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndUtc <= StartUtc)
            yield return new ValidationResult("End time must be after the start time.",
                new[] { nameof(EndUtc) });
    }
}
