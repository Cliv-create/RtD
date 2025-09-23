using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

using RtD.Models;

namespace RtD.Services
{
    public interface ICacheRepository
    {
        /// <summary>
        /// Returns UpdatedAt string value for given titleId
        /// </summary>
        /// <param name="titleId">Title ID from ID on Shikimori (see DB implementation).</param>
        /// <returns>updated_at string in ISO8601 DateTime format.</returns>
        string? GetUpdatedAt(long titleId);

        /// <summary>
        /// Adds passed variables to the pending inserts List that will be batch inserted into DB.
        /// </summary>
        /// <param name="titleId">titleId on Shikimori.</param>
        /// <param name="updatedAt">ISO8601 DateTime format string.</param>
        /// <param name="folderName">Folder, where the .md file created from the titles list will be put.</param>
        void QueueUpsert(long titleId, string updatedAt, string folderName, string subType);

        /// <summary>
        /// Starts SQL transaction that will insert all of the values from pending inserts List to the DB.
        /// </summary>
        void FlushUpserts();

        /// <summary>
        /// Inserts values into DB immediatly, without placing it in pending inserts List. Not recommended.
        /// </summary>
        /// <param name="titleId">titleId on Shikimori.</param>
        /// <param name="updatedAt">ISO8601 DateTime format string.</param>
        /// <param name="folderName">Folder, where the .md file created from the titles list will be put.</param>
        void UpsertTitle(long titleId, string updatedAt, string folderName, string subType);

        /// <summary>
        /// Retrieves a batch of cache entries with pagination.
        /// </summary>
        /// <param name="offset">Starting index for the batch.</param>
        /// <param name="limit">Maximum number of entries to return.</param>
        /// <returns>Enumerable of CacheEntry records.</returns>
        IEnumerable<CacheEntry> GetEntries(int offset, int limit);

        /// <summary>
        /// Returns total number of cache entries.
        /// </summary>
        int GetTotalEntries();
    }

    public class AnimeCacheRepository : ICacheRepository
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly SQLiteConnection _connection;

        private readonly List<(long animeId, string updatedAt, string folderName, string subType)> _pendingUpserts = new(batch_size);
        private const int batch_size = 50;

        public AnimeCacheRepository(string dbFilePath)
        {
            _dbPath = dbFilePath;
            _connectionString = $"Data Source={_dbPath};Version=3;";
            _connection = new SQLiteConnection(_connectionString);
            _connection.Open();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
            }

            string createTablesQuery = @"
            CREATE TABLE IF NOT EXISTS anime_subtypes (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                name    TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS anime_cache (
                anime_id     INTEGER PRIMARY KEY,
                updated_at   TEXT NOT NULL,
                folder_name  TEXT NOT NULL,
                subtype_id   INTEGER NOT NULL,
                FOREIGN KEY(subtype_id) REFERENCES anime_subtypes(id)
            );";

            using var command = new SQLiteCommand(createTablesQuery, _connection);
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// A helper method to get the ID for a given subtype name.
        /// If the subtype doesn't exist, it's created and its new ID is returned.
        /// </summary>
        private long GetOrCreateSubtypeId(string subTypeName)
        {
            // Try to find the existing subtype
            using (var selectCmd = new SQLiteCommand("SELECT id FROM anime_subtypes WHERE name = @name", _connection))
            {
                selectCmd.Parameters.AddWithValue("@name", subTypeName);
                var result = selectCmd.ExecuteScalar();
                
                if (result != null)
                {
                    return Convert.ToInt64(result);
                }
            }

            // If not found, insert it and get the new ID.
            using (var insertCmd = new SQLiteCommand("INSERT INTO anime_subtypes (name) VALUES (@name); SELECT last_insert_rowid();", _connection))
            {
                insertCmd.Parameters.AddWithValue("@name", subTypeName);
                return Convert.ToInt64(insertCmd.ExecuteScalar());
            }
        }
        
        public string? GetUpdatedAt(long animeId)
        {

            string query = "SELECT updated_at FROM anime_cache WHERE anime_id = @id";

            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@id", animeId);

            var result = command.ExecuteScalar();
            return result?.ToString();
        }

        public void QueueUpsert(long animeId, string updatedAt, string folderName, string subType)
        {
            _pendingUpserts.Add((animeId, updatedAt, folderName, subType));

            if (_pendingUpserts.Count >= batch_size)
            {
                FlushUpserts();
            }
        }

        public void FlushUpserts()
        {
            if (_pendingUpserts.Count == 0) return;

            using var transaction = _connection.BeginTransaction();

            string upsertQuery = @"
                INSERT INTO anime_cache (anime_id, updated_at, folder_name, subtype_id)
                VALUES (@id, @updatedAt, @folderName, @subTypeId)
                ON CONFLICT(anime_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                            folder_name = excluded.folder_name,
                            subtype_id = excluded.subtype_id;
            ";

            using var command = new SQLiteCommand(upsertQuery, _connection, transaction);

            foreach (var (animeId, updatedAt, folderName, subType) in _pendingUpserts)
            {
                var subTypeId = GetOrCreateSubtypeId(subType); // Get the ID for the subtype
                
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@id", animeId);
                command.Parameters.AddWithValue("@updatedAt", updatedAt);
                command.Parameters.AddWithValue("@folderName", folderName);
                command.Parameters.AddWithValue("@subTypeId", subTypeId);
                
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            _pendingUpserts.Clear();
        }

        public void UpsertTitle(long animeId, string updatedAt, string folderName, string subType)
        {
            using var transaction = _connection.BeginTransaction();
            
            var subTypeId = GetOrCreateSubtypeId(subType);
            
            string upsertQuery = @"
                INSERT INTO anime_cache (anime_id, updated_at, folder_name, subtype_id)
                VALUES (@id, @updatedAt, @folderName, @subTypeId)
                ON CONFLICT(anime_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                            folder_name = excluded.folder_name,
                            subtype_id = excluded.subtype_id;
            ";

            using var command = new SQLiteCommand(upsertQuery, _connection, transaction);
            
            command.Parameters.AddWithValue("@id", animeId);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
            command.Parameters.AddWithValue("@folderName", folderName);
            command.Parameters.AddWithValue("@subTypeId", subTypeId);
            
            command.ExecuteNonQuery();

            transaction.Commit();
        }

        public IEnumerable<CacheEntry> GetEntries(int offset, int limit)
        {
            string query = @"
                SELECT ac.anime_id, ac.updated_at, ac.folder_name, ast.name
                FROM anime_cache AS ac
                JOIN anime_subtypes AS ast ON ac.subtype_id = ast.id
                ORDER BY ac.anime_id
                LIMIT @limit OFFSET @offset;
            ";

            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", offset);

            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                yield return new CacheEntry(
                    Id: reader.GetInt64(0),
                    UpdatedAt: reader.GetString(1),
                    FolderName: reader.GetString(2),
                    SubType: reader.GetString(3)
                );
            }
        }

        public int GetTotalEntries()
        {
            using var command = new SQLiteCommand("SELECT COUNT(*) FROM anime_cache", _connection);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public class MangaCacheRepository : ICacheRepository
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly SQLiteConnection _connection;

        private readonly List<(long mangaId, string updatedAt, string folderName, string subType)> _pendingUpserts = new(batch_size);
        private const int batch_size = 50;

        public MangaCacheRepository(string dbFilePath)
        {
            _dbPath = dbFilePath;
            _connectionString = $"Data Source={_dbPath};Version=3;";
            _connection = new SQLiteConnection(_connectionString);
            _connection.Open();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
            }

            string createTablesQuery = @"
                CREATE TABLE IF NOT EXISTS manga_subtypes (
                    id      INTEGER PRIMARY KEY AUTOINCREMENT,
                    name    TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS manga_cache (
                    manga_id     INTEGER PRIMARY KEY,
                    updated_at   TEXT NOT NULL,
                    folder_name  TEXT NOT NULL,
                    subtype_id   INTEGER NOT NULL,
                    FOREIGN KEY(subtype_id) REFERENCES manga_subtypes(id)
                );
            ";

            using var command = new SQLiteCommand(createTablesQuery, _connection);
            command.ExecuteNonQuery();
        }

        private long GetOrCreateSubtypeId(string subTypeName)
        {
            using (var selectCmd = new SQLiteCommand("SELECT id FROM manga_subtypes WHERE name = @name", _connection))
            {
                selectCmd.Parameters.AddWithValue("@name", subTypeName);
                var result = selectCmd.ExecuteScalar();
                if (result != null) return Convert.ToInt64(result);
            }
            
            using (var insertCmd = new SQLiteCommand("INSERT INTO manga_subtypes (name) VALUES (@name); SELECT last_insert_rowid();", _connection))
            {
                insertCmd.Parameters.AddWithValue("@name", subTypeName);
                return Convert.ToInt64(insertCmd.ExecuteScalar());
            }
        }

        public string? GetUpdatedAt(long mangaId)
        {
            string query = "SELECT updated_at FROM manga_cache WHERE manga_id = @id";

            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@id", mangaId);

            var result = command.ExecuteScalar();
            return result?.ToString();
        }
        
        public void QueueUpsert(long mangaId, string updatedAt, string folderName, string subType)
        {
            _pendingUpserts.Add((mangaId, updatedAt, folderName, subType));

            if (_pendingUpserts.Count >= batch_size)
            {
                FlushUpserts();
            }
        }

        public void FlushUpserts()
        {
            if (_pendingUpserts.Count == 0) return;

            using var transaction = _connection.BeginTransaction();

            string upsertQuery = @"
                INSERT INTO manga_cache (manga_id, updated_at, folder_name, subtype_id)
                VALUES (@id, @updatedAt, @folderName, @subTypeId)
                ON CONFLICT(manga_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                            folder_name = excluded.folder_name,
                            subtype_id = excluded.subtype_id;
            ";

            using var command = new SQLiteCommand(upsertQuery, _connection, transaction);

            foreach (var (mangaId, updatedAt, folderName, subType) in _pendingUpserts)
            {
                var subTypeId = GetOrCreateSubtypeId(subType); // Get the ID for the subtype
                
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@id", mangaId);
                command.Parameters.AddWithValue("@updatedAt", updatedAt);
                command.Parameters.AddWithValue("@folderName", folderName);
                command.Parameters.AddWithValue("@subTypeId", subTypeId);
                
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            _pendingUpserts.Clear();
        }

        public void UpsertTitle(long mangaId, string updatedAt, string folderName, string subType)
        {
            using var transaction = _connection.BeginTransaction();
            
            long subTypeId = GetOrCreateSubtypeId(subType);
            
            string upsertQuery = @"
                INSERT INTO manga_cache (manga_id, updated_at, folder_name, subtype_id)
                VALUES (@id, @updatedAt, @folderName, @subTypeId)
                ON CONFLICT(manga_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                            folder_name = excluded.folder_name,
                            subtype_id = excluded.subtype_id;
            ";
            
            using var command = new SQLiteCommand(upsertQuery, _connection, transaction);
            
            command.Parameters.AddWithValue("@id", mangaId);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
            command.Parameters.AddWithValue("@folderName", folderName);
            command.Parameters.AddWithValue("@subTypeId", subTypeId);
            
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public IEnumerable<CacheEntry> GetEntries(int offset, int limit)
        {
            string query = @"
                SELECT mc.manga_id, mc.updated_at, mc.folder_name, mst.name
                FROM manga_cache AS mc
                JOIN manga_subtypes AS mst ON mc.subtype_id = mst.id
                ORDER BY mc.manga_id
                LIMIT @limit OFFSET @offset;
            ";

            using var command = new SQLiteCommand(query, _connection);
            
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", offset);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                yield return new CacheEntry(
                    Id: reader.GetInt64(0),
                    UpdatedAt: reader.GetString(1),
                    FolderName: reader.GetString(2),
                    SubType: reader.GetString(3)
                );
            }
        }

        public int GetTotalEntries()
        {
            using var command = new SQLiteCommand("SELECT COUNT(*) FROM manga_cache", _connection);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }
}