using AetherIsland.Core.Models;
using Microsoft.Data.Sqlite;

namespace AetherIsland.Data;

public sealed class TaskRepository
{
    private readonly Db _db;
    public TaskRepository(Db db) => _db = db;

    public List<TaskItem> GetAll()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id,title,completed,priority,due_date,created_at,completed_at FROM tasks WHERE deleted=0 ORDER BY completed ASC, priority DESC, created_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<TaskItem>();
        while (r.Read())
        {
            list.Add(new TaskItem
            {
                Id = r.GetInt64(0),
                Title = r.GetString(1),
                Completed = r.GetInt32(2) == 1,
                Priority = r.GetInt32(3),
                DueDate = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                CreatedAt = DateTime.Parse(r.GetString(5)),
                CompletedAt = r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6))
            });
        }
        return list;
    }

    public long Add(TaskItem item)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO tasks(title,completed,priority,due_date,created_at) VALUES(@t,@c,@p,@d,@a); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@t", item.Title);
        cmd.Parameters.AddWithValue("@c", item.Completed ? 1 : 0);
        cmd.Parameters.AddWithValue("@p", item.Priority);
        cmd.Parameters.AddWithValue("@d", item.DueDate?.ToString("O") as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@a", item.CreatedAt.ToString("O"));
        return (long)(cmd.ExecuteScalar() ?? 0);
    }

    public void Update(TaskItem item)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tasks SET title=@t,completed=@c,priority=@p,due_date=@d,completed_at=@ca WHERE id=@id";
        cmd.Parameters.AddWithValue("@t", item.Title);
        cmd.Parameters.AddWithValue("@c", item.Completed ? 1 : 0);
        cmd.Parameters.AddWithValue("@p", item.Priority);
        cmd.Parameters.AddWithValue("@d", item.DueDate?.ToString("O") as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ca", item.CompletedAt?.ToString("O") as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.ExecuteNonQuery();
    }

    public void SoftDelete(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tasks SET deleted=1 WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}

public sealed class NoteRepository
{
    private readonly Db _db;
    public NoteRepository(Db db) => _db = db;

    public List<NoteItem> GetAll()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id,content,pinned,created_at,updated_at FROM notes ORDER BY pinned DESC, updated_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<NoteItem>();
        while (r.Read())
        {
            list.Add(new NoteItem
            {
                Id = r.GetInt64(0),
                Content = r.GetString(1),
                Pinned = r.GetInt32(2) == 1,
                CreatedAt = DateTime.Parse(r.GetString(3)),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            });
        }
        return list;
    }

    public long Add(string content)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO notes(content,created_at,updated_at) VALUES(@c,@t,@t); SELECT last_insert_rowid();";
        var now = DateTime.Now.ToString("O");
        cmd.Parameters.AddWithValue("@c", content);
        cmd.Parameters.AddWithValue("@t", now);
        return (long)(cmd.ExecuteScalar() ?? 0);
    }

    public void Update(long id, string content, bool pinned)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE notes SET content=@c,pinned=@p,updated_at=@u WHERE id=@id";
        cmd.Parameters.AddWithValue("@c", content);
        cmd.Parameters.AddWithValue("@p", pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@u", DateTime.Now.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}

public sealed class FileShelfRepository
{
    private readonly Db _db;
    public FileShelfRepository(Db db) => _db = db;

    public List<ShelfFile> GetAll()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id,path,name,is_directory,size_bytes,added_at FROM shelf_files ORDER BY added_at DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<ShelfFile>();
        while (r.Read())
        {
            list.Add(new ShelfFile
            {
                Id = r.GetInt64(0),
                Path = r.GetString(1),
                Name = r.GetString(2),
                IsDirectory = r.GetInt32(3) == 1,
                SizeBytes = r.GetInt64(4),
                AddedAt = DateTime.Parse(r.GetString(5))
            });
        }
        return list;
    }

    public bool Add(ShelfFile file)
    {
        try
        {
            using var conn = _db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO shelf_files(path,name,is_directory,size_bytes,added_at) VALUES(@p,@n,@d,@s,@a)";
            cmd.Parameters.AddWithValue("@p", file.Path);
            cmd.Parameters.AddWithValue("@n", file.Name);
            cmd.Parameters.AddWithValue("@d", file.IsDirectory ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", file.SizeBytes);
            cmd.Parameters.AddWithValue("@a", file.AddedAt.ToString("O"));
            return cmd.ExecuteNonQuery() > 0;
        }
        catch { return false; }
    }

    public void Remove(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM shelf_files WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void Clear()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM shelf_files";
        cmd.ExecuteNonQuery();
    }
}

public sealed class ScreenTimeRepository
{
    private readonly Db _db;
    public ScreenTimeRepository(Db db) => _db = db;

    public sealed class AppUsageRow
    {
        public string Exe { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category { get; set; } = "";
        public long Seconds { get; set; }
        public long Switches { get; set; }
    }

    public Dictionary<string, AppUsageRow> GetDay(string day)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT exe,display_name,category,seconds,switches FROM app_usage WHERE day=@d";
        cmd.Parameters.AddWithValue("@d", day);
        var result = new Dictionary<string, AppUsageRow>(StringComparer.OrdinalIgnoreCase);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var exe = r.GetString(0);
            result[exe] = new AppUsageRow
            {
                Exe = exe,
                DisplayName = r.GetString(1),
                Category = r.GetString(2),
                Seconds = r.GetInt64(3),
                Switches = r.GetInt64(4)
            };
        }
        return result;
    }

    public void AddSeconds(string day, string exe, string displayName, string category, int seconds, int switchesDelta)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_usage(day,exe,display_name,category,seconds,switches) VALUES(@d,@e,@n,@c,@s,@w)
            ON CONFLICT(day,exe) DO UPDATE SET seconds=seconds+@s, switches=switches+@w,
                display_name=excluded.display_name, category=excluded.category
            """;
        cmd.Parameters.AddWithValue("@d", day);
        cmd.Parameters.AddWithValue("@e", exe);
        cmd.Parameters.AddWithValue("@n", displayName);
        cmd.Parameters.AddWithValue("@c", category);
        cmd.Parameters.AddWithValue("@s", seconds);
        cmd.Parameters.AddWithValue("@w", switchesDelta);
        cmd.ExecuteNonQuery();
    }

    public void DeleteAll()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM app_usage";
        cmd.ExecuteNonQuery();
    }

    public void PruneOlderThan(int retentionDays)
    {
        var cutoff = DateTime.Now.Date.AddDays(-retentionDays).ToString("yyyy-MM-dd");
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM app_usage WHERE day < @c; DELETE FROM focus_sessions WHERE started_at < @c;";
        cmd.Parameters.AddWithValue("@c", cutoff);
        cmd.ExecuteNonQuery();
    }
}

public sealed class FocusRepository
{
    private readonly Db _db;
    public FocusRepository(Db db) => _db = db;

    public void Add(string kind, DateTime startedAt, DateTime endedAt, int plannedSeconds, int actualSeconds, bool completed)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO focus_sessions(kind,started_at,ended_at,planned_seconds,actual_seconds,completed) VALUES(@k,@s,@e,@p,@a,@c)";
        cmd.Parameters.AddWithValue("@k", kind);
        cmd.Parameters.AddWithValue("@s", startedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@e", endedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@p", plannedSeconds);
        cmd.Parameters.AddWithValue("@a", actualSeconds);
        cmd.Parameters.AddWithValue("@c", completed ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public int CountCompletedToday()
    {
        var day = DateTime.Now.Date.ToString("yyyy-MM-dd");
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM focus_sessions WHERE completed=1 AND substr(started_at,1,10)=@d";
        cmd.Parameters.AddWithValue("@d", day);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }
}

public sealed class HealthLogRepository
{
    private readonly Db _db;
    public HealthLogRepository(Db db) => _db = db;

    public int Get(string kind, string? day = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM health_log WHERE kind=@k AND day=@d";
        cmd.Parameters.AddWithValue("@k", kind);
        cmd.Parameters.AddWithValue("@d", day ?? DateTime.Now.Date.ToString("yyyy-MM-dd"));
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void Set(string kind, int value, string? day = null)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO health_log(day,kind,value) VALUES(@d,@k,@v)
            ON CONFLICT(day,kind) DO UPDATE SET value=@v
            """;
        cmd.Parameters.AddWithValue("@d", day ?? DateTime.Now.Date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@k", kind);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }
}

public sealed class AiCacheRepository
{
    private readonly Db _db;
    public AiCacheRepository(Db db) => _db = db;

    public string? Get(string providerId, string periodKind)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT payload FROM ai_usage_cache WHERE provider_id=@p AND period_kind=@k";
        cmd.Parameters.AddWithValue("@p", providerId);
        cmd.Parameters.AddWithValue("@k", periodKind);
        return cmd.ExecuteScalar() as string;
    }

    public void Put(string providerId, string periodKind, string payloadJson)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ai_usage_cache(provider_id,period_kind,payload,updated_at) VALUES(@p,@k,@j,@u)
            ON CONFLICT(provider_id,period_kind) DO UPDATE SET payload=@j, updated_at=@u
            """;
        cmd.Parameters.AddWithValue("@p", providerId);
        cmd.Parameters.AddWithValue("@k", periodKind);
        cmd.Parameters.AddWithValue("@j", payloadJson);
        cmd.Parameters.AddWithValue("@u", DateTime.Now.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
