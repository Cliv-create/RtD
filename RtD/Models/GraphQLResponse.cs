namespace RtD.Models
{
    using System.Text.Json.Serialization;

    public class GraphQLResponse<T>
    {
        [JsonPropertyName("data")] public T Data { get; set; }
    }
 
    /*
    public class GraphQLResponse
    {
        [JsonPropertyName("data")] public ResponseData Data { get; set; }
    }
    */

    /*
    public class ResponseData
    {
        [JsonPropertyName("userRates")] public List<UserRate> UserRates { get; set; }
    }
    */
    
    public class AnimeResponseData
    {
        [JsonPropertyName("userRates")] public List<AnimeUserRate> UserRates { get; set; }
    }

    public class MangaResponseData
    {
        [JsonPropertyName("userRates")] public List<MangaUserRate> UserRates { get; set; }
    }

    public class GraphQLRequest
    {
        public string? OperationName { get; set; }
        public string Query { get; set; }
        public GraphQLVariables Variables { get; set; }
    }

    public class GraphQLVariables
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public long UserId { get; set; }
    }
}