using System.ComponentModel.DataAnnotations;

namespace Sun2Sat.Models;

/// <summary>A "general event" preset such as Work or Gym. Picking one pre-fills
/// the event form; the user only supplies the time.</summary>
public class EventTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DefaultDescription { get; set; }

    /// <summary>Used to pre-fill the end time once the user picks a start time.</summary>
    public int DefaultDurationMinutes { get; set; } = 60;
}
