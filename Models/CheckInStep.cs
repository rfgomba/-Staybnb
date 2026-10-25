using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Staybnb.Models;

public enum CheckInStepType { Document, Agreement, Information }

/// <summary>
/// One step of a property's check-in process.
/// Steps are stored as JSON in CheckInProcess.StepsJson.
/// </summary>
public class CheckInStep
{
    [StringLength(100)] public string? Name { get; set; }
    public CheckInStepType StepType { get; set; } = CheckInStepType.Information;
    [StringLength(100)] public string? DocumentType { get; set; }   // only for Document steps
    [StringLength(1000)] public string? Description { get; set; }   // instructions or house rules

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<CheckInStep> FromJson(string? json)
    {
        try { return JsonSerializer.Deserialize<List<CheckInStep>>(json ?? "[]", JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public static string ToJson(IEnumerable<CheckInStep> steps) => JsonSerializer.Serialize(steps, JsonOptions);

    public static List<CheckInStep> Defaults() => new()
    {
        new() { Name = "Upload ID or Passport", StepType = CheckInStepType.Document, DocumentType = "ID or Passport",
                Description = "Upload a clear photo of your South African ID or passport for the host to verify." },
        new() { Name = "Accept house rules", StepType = CheckInStepType.Agreement,
                Description = "No smoking indoors. No parties or events. Quiet hours 22:00 to 07:00. Check-in from 14:00, check-out by 10:00." },
        new() { Name = "Arrival information", StepType = CheckInStepType.Information,
                Description = "The host will share access codes and directions once your documents are verified." }
    };
}
