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