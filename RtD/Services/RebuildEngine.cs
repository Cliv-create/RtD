// In RtD.Services/RebuildEngine.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RtD.Models;
using RtD.Utils;

namespace RtD.Services
{
    public class RebuildOptions
    {
        public bool UseCacheOnly { get; set; } = true;
        public bool CreatePlaceholdersForMissing { get; set; } = false;
        public bool BackupDestinationIfExists { get; set; } = true;
        public bool DryRun { get; set; } = false;
        public string MangaFolderPrefix { get; set; } = "!";
    }

    public class RebuildResult
    {
        public int TotalProcessed { get; set; }
        public int FilesCopied { get; set; }
        public int PlaceholdersCreated { get; set; }
        public int MissingFiles { get; set; }
    }

    public class RebuildEngine
    {
        private const int BatchSize = 50;
        private readonly string _sourceRoot;
        private readonly ICacheRepository _animeCache;
        private readonly ICacheRepository _mangaCache;
        private readonly string _privateMarker;

        public RebuildEngine(string sourceRoot, ICacheRepository animeCache, ICacheRepository mangaCache, string privateMarker = "<!-- PRIVATE -->")
        {
            _sourceRoot = sourceRoot ?? throw new ArgumentNullException(nameof(sourceRoot));
            _animeCache = animeCache ?? throw new ArgumentNullException(nameof(animeCache));
            _mangaCache = mangaCache ?? throw new ArgumentNullException(nameof(mangaCache));
            _privateMarker = privateMarker;
        }

        public async Task<RebuildResult> RunAsync(string destinationRoot, RebuildOptions options)
        {
            if (string.IsNullOrWhiteSpace(destinationRoot))
                throw new ArgumentException("Destination path required", nameof(destinationRoot));

            var result = new RebuildResult();

            if (Directory.Exists(destinationRoot) && !options.DryRun)
            {
                if (options.BackupDestinationIfExists)
                {
                    var backup = destinationRoot.TrimEnd(Path.DirectorySeparatorChar) + "_backup_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    Console.WriteLine($"Backing up '{destinationRoot}' -> '{backup}'");
                    Directory.Move(destinationRoot, backup);
                }
                else
                {
                    Directory.Delete(destinationRoot, true);
                }
            }
            if (!options.DryRun)
            {
                Directory.CreateDirectory(destinationRoot);
            }

            await ProcessCacheAsync(_animeCache, destinationRoot, "Anime", options, result);
            await ProcessCacheAsync(_mangaCache, destinationRoot, "Manga", options, result);

            return result;
        }

        private async Task ProcessCacheAsync(ICacheRepository cache, string destinationRoot, string mediaType, RebuildOptions options, RebuildResult result)
        {
            int totalEntries = cache.GetTotalEntries();
            for (int offset = 0; offset < totalEntries; offset += BatchSize)
            {
                IEnumerable<CacheEntry> entries = cache.GetEntries(offset, BatchSize);

                foreach (var entry in entries)
                {
                    result.TotalProcessed++;
                    string folderName = entry.FolderName;
                    string subType = entry.SubType;

                    // Pass context (mediaType, subType) to FindSourceFile
                    string? sourceFile = FindSourceFile(_sourceRoot, folderName, mediaType, subType);

                    if (sourceFile == null)
                    {
                        result.MissingFiles++;
                        Console.WriteLine($"[WARN] Missing source file for ({mediaType}/{subType}): {folderName}");
                        // Placeholder logic is fine.
                        continue;
                    }

                    // Build the destination path based on the clean data from our cache.
                    string destinationDirectory;
                    if (mediaType == "Anime")
                    {
                        destinationDirectory = Path.Combine(destinationRoot, folderName);
                    }
                    else // It's Manga
                    {
                        string mangaTopLevelFolder = options.MangaFolderPrefix + "Manga";
                        destinationDirectory = Path.Combine(destinationRoot, mangaTopLevelFolder, subType, folderName);
                    }
                    
                    string destinationFile = Path.Combine(destinationDirectory, folderName + ".md");

                    if (!options.DryRun)
                    {
                        Directory.CreateDirectory(destinationDirectory);
                        File.Copy(sourceFile, destinationFile, overwrite: true);
                    }

                    result.FilesCopied++;
                    Console.WriteLine($"Copied ({mediaType}): {sourceFile} -> {destinationFile}");
                }
            }
        }
        
        /// <summary>
        /// Finds the correct source file even when multiple files share the same name.
        /// </summary>
        private string? FindSourceFile(string root, string folderName, string mediaType, string subType)
        {
            var allMatches = Directory.EnumerateFiles(root, folderName + ".md", SearchOption.AllDirectories).ToList();

            if (!allMatches.Any()) return null; // No file found at all.
            if (allMatches.Count == 1) return allMatches[0]; // Only one match, no confusion.

            // CONFLICT RESOLUTION: More than one file has the same name.
            Console.WriteLine($"[INFO] Multiple sources found for '{folderName}'. Resolving using path...");
            foreach(var match in allMatches) Console.WriteLine($"  - Found: {match}");

            if (mediaType == "Anime")
            {
                // For Anime, we want the file that is NOT inside a "Manga" top-level folder.
                var animeMatch = allMatches.FirstOrDefault(path => 
                    !Path.GetRelativePath(root, path).StartsWith("Manga", StringComparison.OrdinalIgnoreCase));
                
                if (animeMatch != null)
                {
                    Console.WriteLine($"  - Chose (Anime): {animeMatch}");
                    return animeMatch;
                }
            }
            else // It's Manga
            {
                // For Manga, we want the file that IS inside a "Manga" folder, and ideally, the correct subtype folder.
                string expectedPathFragment = Path.Combine("Manga", subType);
                var mangaMatch = allMatches.FirstOrDefault(path => 
                    Path.GetRelativePath(root, path).Contains(expectedPathFragment, StringComparison.OrdinalIgnoreCase));

                if (mangaMatch != null)
                {
                    Console.WriteLine($"  - Chose (Manga/{subType}): {mangaMatch}");
                    return mangaMatch;
                }
            }

            // Fallback
            Console.WriteLine($"  - [WARN] Could not resolve confidently. Defaulting to first match: {allMatches[0]}");
            return allMatches[0];
        }

        // BuildPlaceholder method remains the same.
        private string BuildPlaceholder(long id, string updatedAt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"id: {id}");
            sb.AppendLine($"updatedAt: \"{updatedAt}\"");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(_privateMarker);
            sb.AppendLine();
            return sb.ToString();
        }
    }
}