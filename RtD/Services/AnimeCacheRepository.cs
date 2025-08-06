using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace RtD.Services
{
    public interface IAnimeCacheRepository
    {
        /// <summary>
        /// Returns UpdatedAt string value for given animeId
        /// </summary>
        /// <param name="animeId">animeId from anime id on Shikimori (see DB implementation).</param>
        /// <returns>updated_at string in ISO8601 DateTime format.</returns>
        string? GetUpdatedAt(long animeId);
        /// <summary>
        /// Adds passed variables to the pending inserts List that will be batch inserted into DB.
        /// </summary>
        /// <param name="animeId">Anime ID on Shikimori.</param>
        /// <param name="updatedAt">ISO8601 DateTime format string.</param>
        /// <param name="folderName">Folder, where the .md file created from the titles list will be put.</param>
        void QueueUpsert(long animeId, string updatedAt, string folderName);
        /// <summary>
        /// Starts SQL transaction that will insert all of the values from pending inserts List to the DB.
        /// </summary>
        void FlushUpserts();
        /// <summary>
        /// Inserts values into DB immediatly, without placing it in pending inserts List. Not recommended.
        /// </summary>
        /// <param name="animeId">Anime ID on Shikimori.</param>
        /// <param name="updatedAt">ISO8601 DateTime format string.</param>
        /// <param name="folderName">Folder, where the .md file created from the titles list will be put.</param>
        void UpsertAnime(long animeId, string updatedAt, string folderName);
    }

    public class AnimeCacheRepository : IAnimeCacheRepository
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly SQLiteConnection _connection;

        private readonly List<(long animeId, string updatedAt, string folderName)> _pendingUpserts = new(batch_size);
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

            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS anime_cache (
                    anime_id     INTEGER PRIMARY KEY,
                    updated_at   TEXT NOT NULL,
                    folder_name  TEXT NOT NULL
                );";

            using var command = new SQLiteCommand(createTableQuery, _connection);
            command.ExecuteNonQuery();
        }

        public string? GetUpdatedAt(long animeId)
        {

            string query = "SELECT updated_at FROM anime_cache WHERE anime_id = @id";

            using var command = new SQLiteCommand(query, _connection);
            command.Parameters.AddWithValue("@id", animeId);

            var result = command.ExecuteScalar();
            return result?.ToString();
        }

        public void QueueUpsert(long animeId, string updatedAt, string folderName)
        {
            _pendingUpserts.Add((animeId, updatedAt, folderName));

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
                INSERT INTO anime_cache (anime_id, updated_at, folder_name)
                VALUES (@id, @updatedAt, @folderName)
                ON CONFLICT(anime_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                              folder_name = excluded.folder_name;
            ";

            using var command = new SQLiteCommand(upsertQuery, _connection);

            foreach (var (animeId, updatedAt, folderName) in _pendingUpserts)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@id", animeId);
                command.Parameters.AddWithValue("@updatedAt", updatedAt);
                command.Parameters.AddWithValue("@folderName", folderName);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            _pendingUpserts.Clear();
        }

        public void UpsertAnime(long animeId, string updatedAt, string folderName)
        {
            using var transaction = _connection.BeginTransaction();

            string upsertQuery = @"
                INSERT INTO anime_cache (anime_id, updated_at, folder_name)
                VALUES (@id, @updatedAt, @folderName)
                ON CONFLICT(anime_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                            folder_name = excluded.folder_name;
            ";

            using var command = new SQLiteCommand(upsertQuery, _connection);
            command.Parameters.AddWithValue("@id", animeId);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
            command.Parameters.AddWithValue("@folderName", folderName);
            command.ExecuteNonQuery();

            transaction.Commit();
        }
    }
}