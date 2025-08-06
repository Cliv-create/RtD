namespace RtD.Utils
{
    using System.Text.Json.Serialization;
    using RtD.Models;

    // [JsonSerializable(typeof(GraphQLResponse))]
    [JsonSerializable(typeof(GraphQLResponse<AnimeResponseData>))]
    [JsonSerializable(typeof(GraphQLResponse<MangaResponseData>))]
    [JsonSerializable(typeof(GraphQLRequest))]
    [JsonSerializable(typeof(GraphQLVariables))]
    // [JsonSerializable(typeof(ResponseData))]
    [JsonSerializable(typeof(AnimeResponseData))]
    [JsonSerializable(typeof(MangaResponseData))]
    [JsonSerializable(typeof(UserRate))]
    [JsonSerializable(typeof(AnimeUserRate))]
    [JsonSerializable(typeof(MangaUserRate))]
    [JsonSerializable(typeof(Anime))]
    [JsonSerializable(typeof(Manga))]
    [JsonSerializable(typeof(Genre))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}