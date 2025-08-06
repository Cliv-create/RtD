namespace RtD.Models
{
    using System.Text.Json.Serialization;

    public class Manga
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("malId")] public string? MalId { get; set; }
        [JsonPropertyName("russian")] public string? Russian { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("alternative_name")] public string? AlternativeName { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; }
        [JsonPropertyName("kind")] public string Kind { get; set; }
        [JsonPropertyName("genres")] public List<Genre>? Genres { get; set; }
        [JsonPropertyName("volumes")] public int? Volumes { get; set; }
        [JsonPropertyName("chapters")] public int? Chapters { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }
}