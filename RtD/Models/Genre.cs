namespace RtD.Models
{
    using System.Text.Json.Serialization;
    
    public class Genre
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}