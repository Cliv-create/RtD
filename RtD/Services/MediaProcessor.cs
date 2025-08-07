using System.Text;
using System.Text.Json;

using RtD.Utils;

namespace RtD.Services
{
    public class MediaProcessor
    {
        private readonly string _rootPath;
        private readonly string _privateMarker;

        public MediaProcessor(string rootPath, string privateMarker = "<!-- PRIVATE -->")
        {
            _rootPath = rootPath;
            _privateMarker = privateMarker;
        }

        public async Task<ProcessingStats> ProcessMediaAsync<TRate, TMedia>(MediaContext<TRate, TMedia> context)
        {
            const int limit = 50;
            int page = 1;
            bool hasMore = true;
            var stats = new ProcessingStats();

            do
            {
                var rates = await context.FetchRatesAsync(page, limit);
                hasMore = rates.Count == limit;

                foreach (var rate in rates)
                {
                    stats.EntriesProcessed++;

                    var media = context.GetMedia(rate);
                    if (media == null) continue;

                    var mediaIdStr = context.GetId(media);
                    if (string.IsNullOrEmpty(mediaIdStr) || !long.TryParse(mediaIdStr, out long mediaId)) continue;

                    // Prepare paths
                    var folderName = Helpers.SanitizeFileName(context.GetTitle(media));
                    // var dir = Path.Combine(_rootPath, folderName);
                    /*
                    var dir = context.GetSubType != null 
                        ? Path.Combine(_rootPath, context.MediaType, context.GetSubType(media), folderName)
                        : Path.Combine(_rootPath, context.MediaType, folderName);
                    */
                    var dir = context.MediaType == "Anime" 
                    ? Path.Combine(_rootPath, folderName)
                    : Path.Combine(_rootPath, context.MediaType, context.GetSubType!(media), folderName);
                    var filePath = Path.Combine(dir, folderName + ".md");

                    // Check if update is needed
                    var cached = context.Cache.GetUpdatedAt(mediaId);
                    var updatedAt = context.GetUpdatedAt(rate);

                    if (cached == updatedAt)
                    {
                        Console.WriteLine($"No changes: {filePath}");
                        hasMore = false;
                        break;
                    }

                    Directory.CreateDirectory(dir);

                    // Generate new content
                    var newContent = context.BuildFrontmatter(rate, media);

                    // Write file
                    if (File.Exists(filePath))
                    {
                        var existingPrivate = await ExtractPrivateSectionAsync(filePath);
                        var merged = newContent + existingPrivate;
                        await File.WriteAllTextAsync(filePath, merged, Encoding.UTF8);

                        stats.EntriesUpdated++;
                        Console.WriteLine($"Updated: {filePath}");
                    }
                    else
                    {
                        var fullContent = newContent + $"\n{_privateMarker}\n\n";
                        await File.WriteAllTextAsync(filePath, fullContent, Encoding.UTF8);

                        stats.EntriesCreated++;
                        Console.WriteLine($"Created: {filePath}");
                    }

                    context.Cache.QueueUpsert(mediaId, updatedAt, folderName);
                }

                page++;
            } while (hasMore);

            context.Cache.FlushUpserts();
            return stats;
        }

        private async Task<string> ExtractPrivateSectionAsync(string path)
        {
            if (!File.Exists(path))
                return "\n" + _privateMarker + "\n\n";

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
                    if (line.Contains(_privateMarker, StringComparison.Ordinal))
                    {
                        foundMarker = true;
                        sb.AppendLine(line);
                    }
                    continue;
                }
                sb.AppendLine(line);
            }

            if (!foundMarker)
                return "\n" + _privateMarker + "\n\n";

            return sb.ToString();
        }
    }

    public class MediaContext<TRate, TMedia>
    {
        /// <summary>
        /// "Anime", "Manga" values are expected, but not limited to.
        /// </summary>
        public required string MediaType { get; init; }
        public required Func<int, int, Task<List<TRate>>> FetchRatesAsync { get; init; }
        public required Func<TRate, TMedia?> GetMedia { get; init; }
        public required Func<TMedia, string> GetId { get; init; }
        public required Func<TMedia, string> GetTitle { get; init; }
        public required Func<TRate, string> GetUpdatedAt { get; init; }
        public required Func<TRate, TMedia, string> BuildFrontmatter { get; init; }
        public required ICacheRepository Cache { get; init; }

        /// <summary>
        /// Intended for "Manga" MediaType.
        /// </summary>
        public Func<TMedia, string>? GetSubType { get; init; }
    }

    public class ProcessingStats
    {
        public int EntriesProcessed { get; set; }
        public int EntriesUpdated { get; set; }
        public int EntriesCreated { get; set; }
    }
}
