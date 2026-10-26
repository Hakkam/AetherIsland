using Microsoft.Data.Sqlite;

namespace AetherIsland.Data;

/// <summary>Owns the SQLite database schema and provides connections.</summary>
public sealed class Db : IDisposable
{
    private readonly string _connectionString;
    private readonly object _lock = new();

    public string DatabasePath { get; }

    public Db(string? appDataDir = null)
    {
        var dir = appDataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AetherIsland");
        Directory.CreateDirectory(dir);
        DatabasePath = Path.Combine(dir, "aether.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
        EnsureSchema();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS tasks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    title TEXT NOT NULL,
                    completed INTEGER NOT NULL DEFAULT 0,
                    priority INTEGER NOT NULL DEFAULT 0,
                    due_date TEXT,
                    created_at TEXT NOT NULL,
                    completed_at TEXT,
                    deleted INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS notes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    content TEXT NOT NULL,
                    pinned INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS shelf_files (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    path TEXT NOT NULL UNIQUE,
                    name TEXT NOT NULL,
                    is_directory INTEGER NOT NULL DEFAULT 0,
                    size_bytes INTEGER NOT NULL DEFAULT 0,
                    added_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS app_usage (
                    day TEXT NOT NULL,
                    exe TEXT NOT NULL,
                    display_name TEXT NOT NULL,
                    category TEXT NOT NULL,
                    seconds INTEGER NOT NULL DEFAULT 0,
                    switches INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (day, exe)
                );
                CREATE TABLE IF NOT EXISTS focus_sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    kind TEXT NOT NULL,
                    started_at TEXT NOT NULL,
                    ended_at TEXT NOT NULL,
                    planned_seconds INTEGER NOT NULL,
                    actual_seconds INTEGER NOT NULL,
                    completed INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS ai_usage_cache (
                    provider_id TEXT NOT NULL,
                    period_kind TEXT NOT NULL,
                    payload TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    PRIMARY KEY (provider_id, period_kind)
                );
                CREATE TABLE IF NOT EXISTS health_log (
                    day TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    value INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (day, kind)
                );
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose() { }
}
