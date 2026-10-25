using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class Message
{
    public int Id { get; set; }

    [Required] public string SenderId { get; set; } = string.Empty;
    public ApplicationUser? Sender { get; set; }

    [Required] public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser? Receiver { get; set; }

    [Required, StringLength(2000)] public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
