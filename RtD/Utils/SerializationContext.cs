namespace RtD.Utils
{
    using System.Text.Json.Serialization;
    using RtD.Models;

    [JsonSerializable(typeof(GraphQLResponse))]
    [JsonSerializable(typeof(GraphQLRequest))]
    [JsonSerializable(typeof(GraphQLVariables))]
    [JsonSerializable(typeof(ResponseData))]
    [JsonSerializable(typeof(UserRate))]
    [JsonSerializable(typeof(Anime))]
    [JsonSerializable(typeof(Genre))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}