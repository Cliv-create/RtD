namespace RtD.Models
{
    using System.Text.Json.Serialization;

    public class UserRate
    {
        [JsonPropertyName("anime")] public Anime Anime { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
        [JsonPropertyName("createdAt")] public string CreatedAt { get; set; }
        [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; }
    }

    public class AnimeUserRate
    {
        [JsonPropertyName("anime")] public Anime Anime { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
        [JsonPropertyName("createdAt")] public string CreatedAt { get; set; }
        [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; }
    }

    public class MangaUserRate
    {
        [JsonPropertyName("manga")] public Manga Manga { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
        [JsonPropertyName("createdAt")] public string CreatedAt { get; set; }
        [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; }
    }
}