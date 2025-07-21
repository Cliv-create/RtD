namespace RtD.Models
{
    using System.Text.Json.Serialization;
    
    class Genre
    {
        [JsonPropertyName("name")] public string Name { get; set; }
    }
}