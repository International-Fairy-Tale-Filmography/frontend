using System.Text.Json;

namespace Data.Core.Models;

public class ContributionEntry
{
    public string Who { get; set; } = string.Empty;
    public string What { get; set; } = string.Empty;
    public DateTime? When { get; set; }
}

public static class ContributionHistoryJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<ContributionEntry> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ContributionEntry>();
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Contribution history must be a list.");
        }

        var entries = new List<ContributionEntry>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var entry = JsonSerializer.Deserialize<ContributionEntry>(element.GetRawText(), Options)
                ?? throw new JsonException("A contribution entry could not be read.");

            entry.Who = ReadString(element, "Who") ?? ReadString(element, "PersonName") ?? string.Empty;
            entry.What = ReadString(element, "What") ?? ReadString(element, "Contribution") ?? string.Empty;
            entry.When = ReadDate(element, "When") ?? ReadDate(element, "ContributedOn");
            entries.Add(entry);
        }

        return entries;
    }

    public static string? Serialize(IReadOnlyCollection<ContributionEntry> entries) =>
        entries.Count == 0 ? null : JsonSerializer.Serialize(entries, Options);

    private static string? ReadString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static DateTime? ReadDate(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String &&
                property.Value.TryGetDateTime(out var date))
            {
                return date;
            }
        }

        return null;
    }
}
