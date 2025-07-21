namespace RtD.Models
{
    using System.Text.Json.Serialization;

    class UserRate
    {
        [JsonPropertyName("anime")] public Anime Anime { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
        [JsonPropertyName("createdAt")] public string CreatedAt { get; set; }
        [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; }
    }
}