using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

class Program
{
    private const string GraphQLEndpoint = "https://shikimori.one/api/graphql";
    private const string PrivateMarker = "<!-- PRIVATE -->";

    static async Task Main()
    {
        Console.Write("Введите путь к папке Anime: ");
        var rootPath = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            Console.WriteLine("Неверный путь.");
            return;
        }
        Directory.CreateDirectory(rootPath);

        Console.Write("Введите ваш userId Shikimori: ");
        if (!long.TryParse(Console.ReadLine()?.Trim(), out var userId))
        {
            Console.WriteLine("Неверный userId.");
            return;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ObsidianAnimeGen/1.0");

        const int limit = 50;
        int page = 1;
        bool hasMore;

        do
        {
            var graphQuery = @"query($page: PositiveInt!, $limit: PositiveInt!, $userId: ID!) {
  userRates(page: $page, limit: $limit, userId: $userId, targetType: Anime, order: {field: updated_at, order: desc}) {
    id
    anime { id russian name url genres { name } episodes description }
    text
    chapters
    createdAt
    updatedAt
    score
    status
    rewatches
    volumes
  }
}";
            var variables = new { page, limit, userId };
            var payloadObj = new { operationName = (string)null, query = graphQuery, variables };
            var payload = JsonSerializer.Serialize(payloadObj);

            var response = await http.PostAsync(GraphQLEndpoint, new StringContent(payload, Encoding.UTF8, "application/json"));
            var rawResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Ошибка GraphQL: {(int)response.StatusCode} {response.ReasonPhrase}");
                Console.WriteLine(rawResponse);
                return;
            }

            var data = JsonSerializer.Deserialize<GraphQLResponse>(rawResponse);
            var rates = data?.Data?.UserRates ?? new List<UserRate>();
            hasMore = rates.Count == limit;

            foreach (var rate in rates)
            {
                var anime = rate.Anime;
                var folderName = SanitizeFileName(anime.Russian ?? anime.Name);
                var dir = Path.Combine(rootPath, folderName);
                Directory.CreateDirectory(dir);

                var filePath = Path.Combine(dir, folderName + ".md");
                var newAutoPart = BuildMarkdown(anime, rate.Text, rate.CreatedAt, rate.UpdatedAt);

                if (File.Exists(filePath))
                {
                    var existingUpdated = ReadFrontmatterValue(filePath, "updatedAt");
                    if (existingUpdated == rate.UpdatedAt)
                    {
                        Console.WriteLine($"Без изменений: {filePath}");
                        continue;
                    }

                    var existingPrivate = ExtractPrivateSection(filePath);
                    var merged = newAutoPart + existingPrivate;
                    await File.WriteAllTextAsync(filePath, merged, Encoding.UTF8);
                    Console.WriteLine($"Обновлено: {filePath}");
                }
                else
                {
                    var fullContent = newAutoPart + $"\n{PrivateMarker}\n\n";
                    await File.WriteAllTextAsync(filePath, fullContent, Encoding.UTF8);
                    Console.WriteLine($"Создано: {filePath}");
                }
            }

            page++;
        } while (hasMore);

        Console.WriteLine("Генерация завершена.");
    }

    static string BuildMarkdown(Anime anime, string reviewText, string createdAt, string updatedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{anime.Russian}\"");
        sb.AppendLine($"original_title: \"{anime.Name}\"");
        sb.AppendLine($"createdAt: \"{createdAt}\"");
        sb.AppendLine($"updatedAt: \"{updatedAt}\"");
        sb.AppendLine("aliases:");
        if (!string.IsNullOrWhiteSpace(anime.AlternativeName))
            sb.AppendLine($"  - \"{anime.AlternativeName}\"");
        sb.AppendLine($"url: \"{anime.Url}\"");
        sb.AppendLine("tags:");
        sb.AppendLine("  - \"anime\"");
        sb.AppendLine("  - \"watched\"");
        sb.AppendLine("genres:");
        foreach (var g in anime.Genres)
            sb.AppendLine($"  - \"{g.Name}\"");
        if (anime.Episodes.HasValue)
            sb.AppendLine($"episodes: {anime.Episodes}");
        if (!string.IsNullOrWhiteSpace(anime.Description))
        {
            sb.AppendLine("description: |");
            foreach (var line in anime.Description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"  {line}");
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Отзыв");
        sb.AppendLine();
        sb.AppendLine(!string.IsNullOrWhiteSpace(reviewText) ? reviewText.Trim() : "*Ваш отзыв...*");
        sb.AppendLine();
        return sb.ToString();
    }

    static string ReadFrontmatterValue(string path, string key)
    {
        using var reader = new StreamReader(path);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith(key + ":"))
            {
                var idx = line.IndexOf(':');
                return line.Substring(idx + 1).Trim().Trim('"');
            }
            if (line.Trim() == "---") break;
        }
        return null;
    }

    static string ExtractPrivateSection(string path)
    {
        var content = File.ReadAllText(path);
        var index = content.IndexOf(PrivateMarker);
        return index >= 0 ? content.Substring(index) : "\n" + PrivateMarker + "\n\n";
    }

    static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    class GraphQLResponse
    {
        [JsonPropertyName("data")] public ResponseData Data { get; set; }
    }
    class ResponseData
    {
        [JsonPropertyName("userRates")] public List<UserRate> UserRates { get; set; }
    }
    class UserRate
    {
        [JsonPropertyName("anime")] public Anime Anime { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
        [JsonPropertyName("createdAt")] public string CreatedAt { get; set; }
        [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; }
    }
    class Anime
    {
        [JsonPropertyName("russian")] public string Russian { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("alternative_name")] public string AlternativeName { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; }
        [JsonPropertyName("genres")] public List<Genre> Genres { get; set; }
        [JsonPropertyName("episodes")] public int? Episodes { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }
    }
    class Genre
    {
        [JsonPropertyName("name")] public string Name { get; set; }
    }
}
