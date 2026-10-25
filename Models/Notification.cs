using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class Notification
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(100)] public string Title { get; set; } = string.Empty;
    [StringLength(500)] public string? Body { get; set; }
    [StringLength(300)] public string? Link { get; set; }

    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
