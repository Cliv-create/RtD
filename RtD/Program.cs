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

using Microsoft.Extensions.Configuration;

using RtD.Services;
using RtD.Models;
using RtD.Utils;

class Program
{
    /*
     * Private marker for your notes.
     * After reading API output and attempting to write text into a file, private marker will stop writer from overwriting your additional notes.
     * This was added because of character limitation in anime list (4 064 symbols), not because of comment, review or critique character limitation.
     * 
     * Warning! Choose your private marker carefully, there's no "Change one private marker for another" feature right now.
     * So, if you would need to change the marker, you would have to manually transfer all of the text below private markers into newly generated files.
     * 
     * Below private marker you can write your text that you dont want to post on a website, but want to add anyway.
     */
    private const string PrivateMarker = "<!-- PRIVATE -->";


    static async Task<int> Main()
    {
        try
        {
            // TODO: Add a constructor for Program that will handle values.
            // Add a function HandleConfig(rootPath, userId).
            //
            // rtd_config values:
            // AutomaticAppExecution - true/false
            // RootPath - string
            // UserId - number

            // TODO: Add rtd_config.json value for OAuth2ApplicationName so each user can input his own application name into the program
            // TODO: Add an option for files indentation. Whether or not to nest .md files into their own folder?
            // TODO: Add clarification messages for network requests?

            // Stopwatches (Timers)
            Stopwatch api_request_time_timer = new Stopwatch();
            Stopwatch executuion_time_timer = new Stopwatch();
            Stopwatch database_initialization_timer = new Stopwatch();
            Stopwatch database_executuion_time_timer = new Stopwatch();

            //
            // Main program
            //

            executuion_time_timer.Start();

            // TODO: Move into a default constructor, add config[] to the class values set and pull rootPath, userId and all values that could be needed from there.
            string? rootPath = string.Empty;
            long userId = 0;
            bool AutoLoadingActive = false;

            if (File.Exists("rtd_config.json"))
            {
                Console.WriteLine("Found rtd_config.json");

                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("rtd_config.json", optional: false, reloadOnChange: false)
                    .Build();

                bool.TryParse(config["AutomaticAppExecution"]?.Trim(), out AutoLoadingActive);

                if (AutoLoadingActive)
                {
                    rootPath = config["RootPath"]?.Trim().Trim('"');
                    if (!long.TryParse(config["UserId"], out userId))
                    {
                        Console.WriteLine("Invalid userId in rtd_config.json.");
                        return 1;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                Console.Write("Enter path to root folder: ");
                rootPath = Console.ReadLine()?.Trim().Trim('"');
            }

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                Console.WriteLine("Invalid path.");
                return 1;
            }

            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            if (userId == 0)
            {
                Console.Write("Enter your Shikimori userId: ");
                if (!long.TryParse(Console.ReadLine()?.Trim(), out userId))
                {
                    Console.WriteLine("Invalid userId (number expected).");
                    return 1;
                }
            }

            /* Old fetching approach, left for testing purposes
            // TODO: Add rtd_config.json value for customizing anime_cache.db location path.
            var dbPath = Path.Combine(AppContext.BaseDirectory, "anime_cache.db");
            Console.WriteLine($"Found DB: {dbPath}");

            database_initialization_timer.Start();
            ICacheRepository cache = new AnimeCacheRepository(dbPath);
            database_initialization_timer.Stop();

            // TODO: Add timers to DB access.
            // database_executuion_time_timer.Start();
            // var updatedAtCache = cache.LoadAllUpdatedAt();
            // database_executuion_time_timer.Stop();

            const int limit = 50;
            int page = 1;
            bool hasMore = true;
            // TODO: Add manga fetching feature using if statements for this bool value
            // bool wasAnimeProcessed = false;
            // After anime is processed, switch to different Service

            // Statistics values
            int anime_entries_amount = 0;
            int anime_entries_updated_amount = 0;
            int anime_entries_created_amount = 0;

            do
            {
                var api_service = new HttpApiService(api_request_time_timer);
                var shikimori_api_service = new ShikimoriAnimeApiService(api_service);

                // TODO: Ensure the timings works correctly
                var rawResponse = await shikimori_api_service.FetchRates(userId, page, limit);

                var data = JsonSerializer.Deserialize<GraphQLResponse<AnimeResponseData>>(rawResponse, AppJsonContext.Default.GraphQLResponseAnimeResponseData);

                var rates = data?.Data?.UserRates ?? new List<AnimeUserRate>();
                hasMore = rates.Count == limit;

                foreach (var rate in rates)
                {
                    Interlocked.Increment(ref anime_entries_amount);

                    var anime = rate.Anime;
                    if (!long.TryParse(anime.Id, out long animeId)) continue;

                    // Prepare paths
                    var folderName = Helpers.SanitizeFileName(anime.Russian ?? anime.Name);
                    var dir = Path.Combine(rootPath, folderName);

                    var filePath = Path.Combine(dir, folderName + ".md");

                    // Check if update is needed
                    var cached = cache.GetUpdatedAt(animeId);
                    if (cached == rate.UpdatedAt)
                    {
                        Console.WriteLine($"No changes: {filePath}");
                        hasMore = false;
                        break;
                    }

                    Directory.CreateDirectory(dir);

                    // Generate new YAML frontmatter
                    var newYaml = BuildYamlAnimeFrontmatter(
                        anime,
                        rate.Text,
                        rate.CreatedAt,
                        rate.UpdatedAt
                    );

                    // Write file
                    if (File.Exists(filePath))
                    {
                        var existingPrivate = await ExtractPrivateSectionAsync(filePath);

                        var merged = newYaml + existingPrivate;
                        await File.WriteAllTextAsync(filePath, merged, Encoding.UTF8);

                        Interlocked.Increment(ref anime_entries_updated_amount);
                        Console.WriteLine($"Updated: {filePath}");
                    }
                    else
                    {
                        var fullContent = newYaml + $"\n{PrivateMarker}\n\n";

                        await File.WriteAllTextAsync(filePath, fullContent, Encoding.UTF8);

                        Interlocked.Increment(ref anime_entries_created_amount);
                        Console.WriteLine($"Created: {filePath}");
                    }

                    cache.QueueUpsert(animeId, rate.UpdatedAt, folderName);
                }

                page++;
            } while (hasMore);
            */

            var dbPath = Path.Combine(AppContext.BaseDirectory, "anime_cache.db");
            Console.WriteLine($"Found DB: {dbPath}");

            database_initialization_timer.Start();
            ICacheRepository animeCache = new AnimeCacheRepository(dbPath);
            ICacheRepository mangaCache = new MangaCacheRepository(dbPath);
            database_initialization_timer.Stop();

            var processor = new MediaProcessor(rootPath, PrivateMarker);
            var api_service = new HttpApiService(api_request_time_timer);

            int titles_entries_amount = 0;
            int titles_entries_updated_amount = 0;
            int titles_entries_created_amount = 0;

            // Process Anime
            var animeService = new ShikimoriAnimeApiService(api_service);
            var animeContext = new MediaContext<AnimeUserRate, Anime>
            {
                MediaType = "Anime",
                FetchRatesAsync = async (page, limit) =>
                {
                    var rawResponse = await animeService.FetchRates(userId, page, limit);
                    var data = JsonSerializer.Deserialize<GraphQLResponse<AnimeResponseData>>(rawResponse, AppJsonContext.Default.GraphQLResponseAnimeResponseData);
                    return data?.Data?.UserRates ?? new List<AnimeUserRate>();
                },
                GetMedia = rate => rate.Anime,
                GetId = anime => anime.Id,
                GetTitle = anime => anime.Russian ?? anime.Name,
                GetUpdatedAt = rate => rate.UpdatedAt,
                BuildFrontmatter = (rate, anime) => BuildYamlAnimeFrontmatter(anime, rate.Text, rate.CreatedAt, rate.UpdatedAt),
                Cache = animeCache
            };

            var animeStats = await processor.ProcessMediaAsync(animeContext);

            // Process Manga
            var mangaService = new ShikimoriMangaApiService(api_service);
            var mangaContext = new MediaContext<MangaUserRate, Manga>
            {
                MediaType = "Manga",
                FetchRatesAsync = async (page, limit) =>
                {
                    var rawResponse = await mangaService.FetchRates(userId, page, limit);
                    var data = JsonSerializer.Deserialize<GraphQLResponse<MangaResponseData>>(rawResponse, AppJsonContext.Default.GraphQLResponseMangaResponseData);
                    return data?.Data?.UserRates ?? new List<MangaUserRate>();
                },
                GetMedia = rate => rate.Manga,
                GetId = manga => manga.Id,
                GetTitle = manga => manga.Russian ?? manga.Name,
                GetUpdatedAt = rate => rate.UpdatedAt,
                BuildFrontmatter = (rate, manga) => BuildYamlMangaFrontmatter(manga, rate.Text, rate.CreatedAt, rate.UpdatedAt),
                Cache = mangaCache,

                GetSubType = manga => string.IsNullOrEmpty(manga.Kind) ? "Unknown" : manga.Kind
            };

            var mangaStats = await processor.ProcessMediaAsync(mangaContext);

            executuion_time_timer.Stop();

            // Merge stats
            titles_entries_amount = animeStats.EntriesProcessed + mangaStats.EntriesProcessed;
            titles_entries_updated_amount = animeStats.EntriesUpdated + mangaStats.EntriesUpdated;
            titles_entries_created_amount = animeStats.EntriesCreated + mangaStats.EntriesCreated;

            Console.WriteLine($"Application execution time:\n{executuion_time_timer.Elapsed}");
            Console.WriteLine($"Pure application execution time (no network):\n{executuion_time_timer.Elapsed - api_request_time_timer.Elapsed}");

            Console.WriteLine($"Database initialization timer: {database_initialization_timer.ElapsedMilliseconds} ms");
            // Console.WriteLine($"Database execution timer: {database_executuion_time_timer.ElapsedMilliseconds} ms");

            Console.WriteLine($"API call execution time: {api_request_time_timer.ElapsedMilliseconds} ms");

            Console.WriteLine($"Entries processed: {titles_entries_amount}");
            Console.WriteLine($"Entries updated:   {titles_entries_updated_amount}");
            Console.WriteLine($"Entries created:   {titles_entries_created_amount}");

            Console.WriteLine("Finished processing.");
            return 0;
        }
        catch (HttpRequestException httpEx)
        {
            Console.Error.WriteLine("GraphQL Error: Request failed.");
            Console.Error.WriteLine(httpEx.Message);

            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Unexpected error: " + ex.Message);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            return 1;
        }
    }


    static string BuildYamlAnimeFrontmatter(Anime anime, string reviewText, string createdAt, string updatedAt)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");

        sb.AppendLine($"id: {anime.Id}");

        sb.AppendLine($"malId: {anime.MalId ?? "null"}");

        sb.AppendLine($"updatedAt: \"{updatedAt}\"");

        sb.AppendLine($"createdAt: \"{createdAt}\"");

        sb.AppendLine($"url: \"{Helpers.EscapeYaml(anime.Url)}\"");

        sb.AppendLine("tags:");
        sb.AppendLine("  - \"anime\"");

        sb.AppendLine("genres:");
        foreach (var g in anime.Genres)
            sb.AppendLine($"  - \"{Helpers.EscapeYaml(g.Name)}\"");

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

    static string BuildYamlMangaFrontmatter(Manga manga, string reviewText, string createdAt, string updatedAt)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");

        sb.AppendLine($"id: {manga.Id}");

        sb.AppendLine($"malId: {manga.MalId ?? "null"}");

        sb.AppendLine($"updatedAt: \"{updatedAt}\"");

        sb.AppendLine($"createdAt: \"{createdAt}\"");

        sb.AppendLine($"url: \"{Helpers.EscapeYaml(manga.Url)}\"");

        sb.AppendLine("tags:");
        sb.AppendLine($"  - \"{manga.Kind}\"");

        sb.AppendLine("genres:");
        foreach (var g in manga.Genres)
            sb.AppendLine($"  - \"{Helpers.EscapeYaml(g.Name)}\"");

        if (manga.Volumes.HasValue)
            sb.AppendLine($"volumes: {manga.Volumes}");

        if (manga.Chapters.HasValue)
            sb.AppendLine($"chapters: {manga.Chapters}");

        if (!string.IsNullOrWhiteSpace(manga.Description))
        {
            sb.AppendLine("description: |");
            foreach (var line in manga.Description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
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

    // NOTE: ReadFrontmatterValue and ReadFrontmatterArrayValues are currently not used (DB cache is used instead)
    // However, these methods will remain here temporarily.
    // TODO: Should this have check for the existance of the file or not? Currently the logic uses method that returns only existing files.
    /// <summary>
    /// Reads frontmatter of the file (YAML data between two "---"). Starts a StreamReader at given path parameter and searches for a value corresponding to the key.
    /// </summary>
    /// <remarks>
    /// Warning! Expects that the key-value is not array. Method does not check if the file exists or not. This behaviour might change in the future.
    /// </remarks>
    /// <param name="path">Path to the file.</param>
    /// <param name="key">YAML key to be searched for.</param>
    /// <returns>Value corresponding to the given key (or string.Empty).</returns>
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
        return string.Empty;
    }


    /// <summary>
    /// Searches and returns YAML array values for a given key. Supports "-" arrays, [ "" ] arrays and non-array values (will return array with 1 element). Does a file existance check.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="key"></param>
    /// <returns>Array with string values for the given key. If value is not array - will return array with a single value. If the value does not exist will return null. If the file does not exist, will return null. If frontmatter wasn't found returns null pointer to the array.</returns>
    static List<string>? ReadFrontmatterArrayValues(string path, string key)
    {
        if (!File.Exists(path))
            return null;

        using var reader = new StreamReader(path);
        bool inFrontmatter = false;
        bool foundKey = false;
        List<string>? values = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim() == "---")
            {
                if (!inFrontmatter)
                {
                    inFrontmatter = true;
                    continue;
                }
                break; // End of frontmatter
            }

            if (!inFrontmatter)
                continue;

            if (foundKey)
            {
                if (line.StartsWith("  - ", StringComparison.Ordinal))
                {
                    values!.Add(line[4..].Trim().Trim('"'));
                }
                else
                {
                    // End of array block
                    break;
                }
            }
            else if (line.StartsWith(key + ":", StringComparison.Ordinal))
            {
                foundKey = true;

                var valuePart = line[(line.IndexOf(':') + 1)..].Trim();

                if (valuePart.StartsWith("[") && valuePart.EndsWith("]"))
                {
                    // Inline array
                    var inner = valuePart[1..^1];
                    var items = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    return items.Select(s => s.Trim().Trim('"')).ToList();
                }
                else if (string.IsNullOrEmpty(valuePart))
                {
                    // Start of block array
                    values = new List<string>();
                }
                else
                {
                    // Single value treated as 1-element list
                    return new List<string> { valuePart.Trim('"') };
                }
            }
        }

        // Final return logic
        return foundKey ? values ?? new List<string>() : null;
    }



    /// <summary>
    /// Starts new StreamReader using async FileStream with sequential scanning at given path and searches for PrivateMarker and the text after it.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <returns>PrivateMarker and all the text after it, or a placeholder for private section (see code for placeholder structure).</returns>
    static async Task<string> ExtractPrivateSectionAsync(string path)
    {
        // Early return if the file does not exist
        if (!File.Exists(path))
            return "\n" + PrivateMarker + "\n\n";

        // Opening the file in async, sequential‑scan mode for best throughput
        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(fs, Encoding.UTF8);

        var sb = new StringBuilder();
        bool foundMarker = false;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!foundMarker)
            {
                // Detecting the private marker line
                if (line.Contains(PrivateMarker, StringComparison.Ordinal))
                {
                    foundMarker = true;
                    sb.AppendLine(line);   // keeping the marker itself
                }
                continue;                  // keep scanning until we find it
            }

            // After the marker: copy every remaining line verbatim
            sb.AppendLine(line);
        }

        // If the marker was never found, creating a default section
        if (!foundMarker)
            return "\n" + PrivateMarker + "\n\n";

        return sb.ToString();
    }
}
