using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class CheckInProcess
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }

    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    // JSON list of steps, e.g. [{"Name":"Upload ID","RequiresDocument":true,"DocumentType":"ID or Passport"}]
    public string StepsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GuestCheckIn> GuestCheckIns { get; set; } = new List<GuestCheckIn>();
}
