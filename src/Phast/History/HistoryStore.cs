using System.IO;
using Microsoft.Data.Sqlite;
using Phast.Core;

namespace Phast.History;

/// <summary>One URL in the browsing history (aggregated over all its visits).</summary>
public sealed record HistoryEntry(long Id, string Url, string Title, int VisitCount, int TypedCount, DateTime LastVisit);

/// <summary>
/// Browsing history stored in a small SQLite database (history.db).
/// The database is opened lazily on first use and every query runs off the UI thread,
/// serialized through a single connection, so the browser never stutters because of history.
/// </summary>
public static class HistoryStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static SqliteConnection? _db;

    private static string DbPath => Path.Combine(AppPaths.Root, "history.db");

    private static SqliteConnection Db()
    {
        if (_db != null) return _db;
        var db = new SqliteConnection($"Data Source={DbPath}");
        db.Open();
        Exec(db, """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            CREATE TABLE IF NOT EXISTS urls (
                id          INTEGER PRIMARY KEY,
                url         TEXT    NOT NULL UNIQUE,
                title       TEXT    NOT NULL DEFAULT '',
                visit_count INTEGER NOT NULL DEFAULT 0,
                typed_count INTEGER NOT NULL DEFAULT 0,
                last_visit  INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS visits (
                id     INTEGER PRIMARY KEY,
                url_id INTEGER NOT NULL REFERENCES urls(id) ON DELETE CASCADE,
                time   INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_urls_last ON urls(last_visit);
            CREATE INDEX IF NOT EXISTS idx_visits_time ON visits(time);
            CREATE INDEX IF NOT EXISTS idx_visits_url ON visits(url_id);
            PRAGMA foreign_keys = ON;
            """);
        return _db = db;
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Runs a database operation on a background thread, one at a time.</summary>
    private static async Task<T> Run<T>(Func<SqliteConnection, T> work)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try { return await Task.Run(() => work(Db())).ConfigureAwait(false); }
        finally { Gate.Release(); }
    }

    private static Task Run(Action<SqliteConnection> work) => Run(db => { work(db); return 0; });

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Only real web pages are recorded (not internal pages, about:blank, data: URLs...).</summary>
    public static bool IsRecordable(string? url) =>
        url != null && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        && !InternalPages.IsInternal(url);

    public static Task AddVisitAsync(string url, string title, bool typed)
    {
        if (!IsRecordable(url)) return Task.CompletedTask;
        return Run(db =>
        {
            var now = Now();
            using var tx = db.BeginTransaction();
            using (var up = db.CreateCommand())
            {
                up.CommandText = """
                    INSERT INTO urls(url, title, visit_count, typed_count, last_visit) VALUES($u, $t, 1, $typed, $now)
                    ON CONFLICT(url) DO UPDATE SET
                        visit_count = visit_count + 1,
                        typed_count = typed_count + $typed,
                        last_visit  = $now,
                        title       = CASE WHEN $t <> '' THEN $t ELSE title END;
                    """;
                up.Parameters.AddWithValue("$u", url);
                up.Parameters.AddWithValue("$t", title ?? "");
                up.Parameters.AddWithValue("$typed", typed ? 1 : 0);
                up.Parameters.AddWithValue("$now", now);
                up.ExecuteNonQuery();
            }
            using (var visit = db.CreateCommand())
            {
                visit.CommandText = "INSERT INTO visits(url_id, time) SELECT id, $now FROM urls WHERE url = $u;";
                visit.Parameters.AddWithValue("$u", url);
                visit.Parameters.AddWithValue("$now", now);
                visit.ExecuteNonQuery();
            }
            tx.Commit();
        });
    }

    public static Task UpdateTitleAsync(string url, string title)
    {
        if (!IsRecordable(url) || string.IsNullOrWhiteSpace(title)) return Task.CompletedTask;
        return Run(db =>
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE urls SET title = $t WHERE url = $u;";
            cmd.Parameters.AddWithValue("$t", title);
            cmd.Parameters.AddWithValue("$u", url);
            cmd.ExecuteNonQuery();
        });
    }

    /// <summary>
    /// Candidates for address bar suggestions: every word typed must appear in the URL or the title.
    /// Results are roughly ordered by popularity; final ranking is done by the caller.
    /// </summary>
    public static Task<List<HistoryEntry>> SearchAsync(string text, int limit = 60)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Task.FromResult(new List<HistoryEntry>());
        return Run(db =>
        {
            using var cmd = db.CreateCommand();
            var where = new List<string>();
            for (int i = 0; i < words.Length; i++)
            {
                where.Add($"(url LIKE $w{i} ESCAPE '\\' OR title LIKE $w{i} ESCAPE '\\')");
                cmd.Parameters.AddWithValue($"$w{i}", "%" + EscapeLike(words[i]) + "%");
            }
            cmd.CommandText = $"""
                SELECT id, url, title, visit_count, typed_count, last_visit FROM urls
                WHERE {string.Join(" AND ", where)}
                ORDER BY typed_count * 5 + visit_count DESC, last_visit DESC
                LIMIT {limit};
                """;
            return ReadEntries(cmd);
        });
    }

    /// <summary>Most visited sites (for the new tab page).</summary>
    public static Task<List<HistoryEntry>> TopSitesAsync(int limit = 8) => Run(db =>
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, url, title, visit_count, typed_count, last_visit FROM urls
            ORDER BY typed_count * 5 + visit_count DESC, last_visit DESC LIMIT {limit};
            """;
        return ReadEntries(cmd);
    });

    public static Task DeleteUrlAsync(string url) => Run(db =>
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM visits WHERE url_id IN (SELECT id FROM urls WHERE url = $u); DELETE FROM urls WHERE url = $u;";
        cmd.Parameters.AddWithValue("$u", url);
        cmd.ExecuteNonQuery();
    });

    private static List<HistoryEntry> ReadEntries(SqliteCommand cmd)
    {
        var list = new List<HistoryEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new HistoryEntry(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4),
                DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(5)).LocalDateTime));
        }
        return list;
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>Flushes and closes the database (on exit).</summary>
    public static void Close()
    {
        var acquired = Gate.Wait(TimeSpan.FromSeconds(2)); // let a pending write finish
        try
        {
            _db?.Close();
            _db?.Dispose();
            _db = null;
        }
        finally
        {
            if (acquired) Gate.Release();
        }
    }
}
