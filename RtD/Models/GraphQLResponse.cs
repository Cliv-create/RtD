namespace RtD.Models
{
    using System.Text.Json.Serialization;
    
    class GraphQLResponse
    {
        [JsonPropertyName("data")] public ResponseData Data { get; set; }
    }


    class ResponseData
    {
        [JsonPropertyName("userRates")] public List<UserRate> UserRates { get; set; }
    }
}