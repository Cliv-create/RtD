using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace RtD.Services
{
    public class AnimeCacheRepository
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public AnimeCacheRepository(string dbFilePath)
        {
            _dbPath = dbFilePath;
            _connectionString = $"Data Source={_dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
            }

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS anime_cache (
                    anime_id     INTEGER PRIMARY KEY,
                    updated_at   TEXT NOT NULL,
                    folder_name  TEXT NOT NULL
                );";

            using var command = new SQLiteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        public Dictionary<long, string> LoadAllUpdatedAt()
        {
            var result = new Dictionary<long, string>();

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            string query = "SELECT anime_id, updated_at FROM anime_cache";

            using var command = new SQLiteCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                string updatedAt = reader.GetString(1);
                result[id] = updatedAt;
            }

            return result;
        }

        public void UpsertAnime(long animeId, string updatedAt, string folderName)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            string upsertQuery = @"
                INSERT INTO anime_cache (anime_id, updated_at, folder_name)
                VALUES (@id, @updatedAt, @folderName)
                ON CONFLICT(anime_id)
                DO UPDATE SET updated_at = excluded.updated_at,
                            folder_name = excluded.folder_name;
            ";

            using var command = new SQLiteCommand(upsertQuery, connection);
            command.Parameters.AddWithValue("@id", animeId);
            command.Parameters.AddWithValue("@updatedAt", updatedAt);
            command.Parameters.AddWithValue("@folderName", folderName);
            command.ExecuteNonQuery();

            transaction.Commit();
        }
    }
}