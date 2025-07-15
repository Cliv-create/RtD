using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

class Program
{
    private const string GraphQLEndpoint = "https://shikimori.one/api/graphql";

    /*
     * Private marker for your notes.
     * After reading API output and attempting to write text into a file private marker will stop writer from overwriting your additional notes.
     * This was added because of character limitation in anime list (4 064 symbols), not because of comment, review or critique character limitation.
     * 
     * Choose your private marker carefully, there's no "Change one private marker for another" feature right now.
     * So, if you would need to change the marker, you would have to manually transfer all of the text below private markers into newly generated files.
     * 
     * Below private marker you can write your text that you dont want to post on a website, but want to add anyway.
     */
    private const string PrivateMarker = "<!-- PRIVATE -->";

    private const string GraphQLQuery = @"
    query($page: PositiveInt!, $limit: PositiveInt!, $userId: ID!) {
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
    }
    ";


    static async Task Main()
    {
        Console.Write("Enter path to Anime folder: ");
        var rootPath = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            Console.WriteLine("Invalid path.");
            return;
        }
        Directory.CreateDirectory(rootPath);

        Console.Write("Enter your Shikimori userId: ");
        if (!long.TryParse(Console.ReadLine()?.Trim(), out var userId))
        {
            Console.WriteLine("Invalid userId (number expected).");
            return;
        }


        // Preload all existing updatedAt values into a dictionary, excluding .obsidian and similar folders
        var updatedAtCache = new Dictionary<string, string>();

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories))
        {
            // Skip if the file is in any hidden/system-like directory such as .obsidian; .git, etc
            var relativePath = Path.GetRelativePath(rootPath, file);
            if (relativePath.Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith(".")))
                continue;

            var name = Path.GetFileNameWithoutExtension(file);
            var updated = ReadFrontmatterValue(file, "updatedAt");
            if (updated != null)
                updatedAtCache[name] = updated;
        }


        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RtD/1.0");

        const int limit = 50;
        int page = 1;
        bool hasMore;

        do
        {
            var variables = new { page, limit, userId };
            var payloadObj = new { operationName = (string)null, query = GraphQLQuery, variables };
            var payload = JsonSerializer.Serialize(payloadObj);

            var response = await http.PostAsync(GraphQLEndpoint, new StringContent(payload, Encoding.UTF8, "application/json"));
            var rawResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"GraphQL Error: {(int)response.StatusCode} {response.ReasonPhrase}");
                Console.WriteLine(rawResponse);
                return;
            }

            var data = JsonSerializer.Deserialize<GraphQLResponse>(rawResponse);

            var rates = data?.Data?.UserRates ?? new List<UserRate>();
            hasMore = rates.Count == limit;

            foreach (var rate in rates)
            {
                var anime = rate.Anime;

                // Prepare paths
                var folderName = SanitizeFileName(anime.Russian ?? anime.Name);
                var dir = Path.Combine(rootPath, folderName);
                Directory.CreateDirectory(dir);

                var filePath = Path.Combine(dir, folderName + ".md");

                // Check if update is needed
                if (updatedAtCache.TryGetValue(folderName, out var existingUpdated) && existingUpdated == rate.UpdatedAt)
                {
                    Console.WriteLine($"No changes: {filePath}");
                    continue;
                }

                // Generate new YAML frontmatter
                var newAutoPart = BuildYamlAnimeFrontmatter(
                    anime,
                    rate.Text,
                    rate.CreatedAt,
                    rate.UpdatedAt
                );

                // Write file
                if (File.Exists(filePath))
                {
                    var existingPrivate = await ExtractPrivateSectionAsync(filePath);

                    var merged = newAutoPart + existingPrivate;
                    await File.WriteAllTextAsync(filePath, merged, Encoding.UTF8);

                    Console.WriteLine($"Updated: {filePath}");
                }
                else
                {
                    var fullContent = newAutoPart + $"\n{PrivateMarker}\n\n";

                    await File.WriteAllTextAsync(filePath, fullContent, Encoding.UTF8);

                    Console.WriteLine($"Created: {filePath}");
                }
            }

            page++;
        } while (hasMore);

        Console.WriteLine("Finished generation.");
    }


    static string BuildYamlAnimeFrontmatter(Anime anime, string reviewText, string createdAt, string updatedAt)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");

        sb.AppendLine($"title: \"{EscapeYaml(anime.Russian)}\"");

        sb.AppendLine($"original_title: \"{EscapeYaml(anime.Name)}\"");

        sb.AppendLine($"createdAt: \"{createdAt}\"");

        sb.AppendLine($"updatedAt: \"{updatedAt}\"");


        sb.AppendLine("aliases:");
        if (!string.IsNullOrWhiteSpace(anime.AlternativeName))
            sb.AppendLine($"  - \"{EscapeYaml(anime.AlternativeName)}\"");

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
        sb.AppendLine("# Review");

        sb.AppendLine();
        sb.AppendLine(!string.IsNullOrWhiteSpace(reviewText) ? reviewText.Trim() : "*Your review...*");
        sb.AppendLine();

        return sb.ToString();
    }

    static string ReadFrontmatterValue(string path, string key)
    {
        using var reader = new StreamReader(path);
        bool inFrontmatter = false;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim() == "---")
            {
                // First  "---" — beginning to read frontmatter,
                // Second "---" — Stop reading and exit
                if (!inFrontmatter) { inFrontmatter = true; continue; }
                break;
            }

            if (inFrontmatter && line.StartsWith(key + ":", StringComparison.Ordinal))
            {
                return line[(line.IndexOf(':') + 1)..].Trim().Trim('"');
            }
        }
        return null;
    }


    static async Task<string> ExtractPrivateSectionAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        var index = content.IndexOf(PrivateMarker);
        return index >= 0 ? content.Substring(index) : "\n" + PrivateMarker + "\n\n";
    }

    /// <summary>
    /// Escapes YAML " characters.
    /// </summary>
    /// <param name="value">String that will be checked for un-escaped " characters.</param>
    /// <returns>String with " characters escaped.</returns>
    static string EscapeYaml(string? value)
    {
        return value?.Replace("\"", "\\\"") ?? string.Empty;
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
