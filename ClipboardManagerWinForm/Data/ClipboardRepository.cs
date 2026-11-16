using System.Text;
using Microsoft.Data.Sqlite;

namespace ClipboardManagerWinForm.Data;

public sealed class ClipboardRepository(Database database)
{
    public Database Database { get; } = database;

    public IReadOnlyList<ClipEntry> GetHistory(int limit)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Text, CreatedTicks
            FROM History
            ORDER BY CreatedTicks DESC, Id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 10_000));

        return ReadEntries(command, ClipEntry.History);
    }

    public long AddHistory(string text, DateTime timestampUtc, int maxItems)
    {
        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var ticks = timestampUtc.ToUniversalTime().Ticks;
        long id;

        using (var latestCommand = connection.CreateCommand())
        {
            latestCommand.Transaction = transaction;
            latestCommand.CommandText = "SELECT Id, Text FROM History ORDER BY CreatedTicks DESC, Id DESC LIMIT 1;";
            using var reader = latestCommand.ExecuteReader();

            if (reader.Read() && string.Equals(reader.GetString(1), text, StringComparison.Ordinal))
            {
                id = reader.GetInt64(0);
                reader.Close();

                using var touchCommand = connection.CreateCommand();
                touchCommand.Transaction = transaction;
                touchCommand.CommandText = "UPDATE History SET CreatedTicks = @ticks WHERE Id = @id;";
                touchCommand.Parameters.AddWithValue("@ticks", ticks);
                touchCommand.Parameters.AddWithValue("@id", id);
                touchCommand.ExecuteNonQuery();
                transaction.Commit();

                return id;
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO History (Text, SearchText, CreatedTicks)
                VALUES (@text, @searchText, @ticks);
                SELECT last_insert_rowid();
                """;
            insertCommand.Parameters.AddWithValue("@text", text);
            insertCommand.Parameters.AddWithValue("@searchText", ToSearchText(text));
            insertCommand.Parameters.AddWithValue("@ticks", ticks);
            id = (long)(insertCommand.ExecuteScalar() ?? 0L);
        }

        using (var trimCommand = connection.CreateCommand())
        {
            trimCommand.Transaction = transaction;
            trimCommand.CommandText = """
                DELETE FROM History
                WHERE Id NOT IN (
                    SELECT Id FROM History
                    ORDER BY CreatedTicks DESC, Id DESC
                    LIMIT @maxItems
                );
                """;
            trimCommand.Parameters.AddWithValue("@maxItems", Math.Max(1, maxItems));
            trimCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return id;
    }

    /// <summary>
    /// Moves a clip to the first position of the history, so it is the most recent clip again.
    /// The clip is also added when the history does not contain it yet.
    /// </summary>
    public void PromoteHistory(string text, DateTime timestampUtc, int maxItems)
    {
        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM History WHERE Text = @text;";
            deleteCommand.Parameters.AddWithValue("@text", text);
            deleteCommand.ExecuteNonQuery();
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO History (Text, SearchText, CreatedTicks)
                VALUES (@text, @searchText, @ticks);
                """;
            insertCommand.Parameters.AddWithValue("@text", text);
            insertCommand.Parameters.AddWithValue("@searchText", ToSearchText(text));
            insertCommand.Parameters.AddWithValue("@ticks", timestampUtc.ToUniversalTime().Ticks);
            insertCommand.ExecuteNonQuery();
        }

        using (var trimCommand = connection.CreateCommand())
        {
            trimCommand.Transaction = transaction;
            trimCommand.CommandText = """
                DELETE FROM History
                WHERE Id NOT IN (
                    SELECT Id FROM History
                    ORDER BY CreatedTicks DESC, Id DESC
                    LIMIT @maxItems
                );
                """;
            trimCommand.Parameters.AddWithValue("@maxItems", Math.Max(1, maxItems));
            trimCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void TrimHistory(int maxItems)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM History
            WHERE Id NOT IN (
                SELECT Id FROM History
                ORDER BY CreatedTicks DESC, Id DESC
                LIMIT @maxItems
            );
            """;
        command.Parameters.AddWithValue("@maxItems", Math.Max(1, maxItems));
        command.ExecuteNonQuery();
    }

    public void ClearHistory()
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM History;";
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ClipEntry> GetArchive(int limit)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Text, CreatedTicks
            FROM Archive
            ORDER BY CreatedTicks DESC, Id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 100_000));

        return ReadEntries(command, ClipEntry.History);
    }

    public IReadOnlyList<ClipEntry> SearchArchive(ArchiveSearchQuery query, DateTime? sinceUtc, int limit)
    {
        var sql = new StringBuilder("SELECT Id, Text, CreatedTicks FROM Archive WHERE 1 = 1");
        var parameters = new List<(string Name, object Value)>();

        if (sinceUtc.HasValue)
        {
            sql.Append(" AND CreatedTicks >= @since");
            parameters.Add(("@since", sinceUtc.Value.ToUniversalTime().Ticks));
        }

        for (var index = 0; index < query.Terms.Count; index++)
        {
            var name = $"@term{index}";
            sql.Append($" AND SearchText LIKE {name} ESCAPE '\\'");
            parameters.Add((name, ToLikePattern(query.Terms[index])));
        }

        for (var index = 0; index < query.ExcludedTerms.Count; index++)
        {
            var name = $"@excluded{index}";
            sql.Append($" AND SearchText NOT LIKE {name} ESCAPE '\\'");
            parameters.Add((name, ToLikePattern(query.ExcludedTerms[index])));
        }

        sql.Append(" ORDER BY CreatedTicks DESC, Id DESC LIMIT @limit;");
        parameters.Add(("@limit", Math.Clamp(limit, 1, 100_000)));

        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql.ToString();

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return ReadEntries(command, ClipEntry.History);
    }

    public long AddArchive(string text, DateTime timestampUtc)
    {
        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var latestCommand = connection.CreateCommand())
        {
            latestCommand.Transaction = transaction;
            latestCommand.CommandText = "SELECT Text FROM Archive ORDER BY CreatedTicks DESC, Id DESC LIMIT 1;";
            var latest = latestCommand.ExecuteScalar() as string;
            if (string.Equals(latest, text, StringComparison.Ordinal))
            {
                transaction.Commit();
                return 0;
            }
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO Archive (Text, SearchText, CreatedTicks)
            VALUES (@text, @searchText, @ticks);
            SELECT last_insert_rowid();
            """;
        insertCommand.Parameters.AddWithValue("@text", text);
        insertCommand.Parameters.AddWithValue("@searchText", ToSearchText(text));
        insertCommand.Parameters.AddWithValue("@ticks", timestampUtc.ToUniversalTime().Ticks);
        var id = (long)(insertCommand.ExecuteScalar() ?? 0L);

        transaction.Commit();
        return id;
    }

    public void TrimArchive(int maxItems)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM Archive
            WHERE Id NOT IN (
                SELECT Id FROM Archive
                ORDER BY CreatedTicks DESC, Id DESC
                LIMIT @maxItems
            );
            """;
        command.Parameters.AddWithValue("@maxItems", Math.Max(1, maxItems));
        command.ExecuteNonQuery();
    }

    public void DeleteArchive(long id)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Archive WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void ClearArchive()
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Archive;";
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ClipEntry> GetStickies()
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Text, CreatedTicks
            FROM Stickies
            ORDER BY SortOrder, Id;
            """;

        List<ClipEntry> entries = [];
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ClipEntry.Sticky(
                reader.GetInt64(0),
                reader.GetString(1),
                new DateTime(reader.GetInt64(2), DateTimeKind.Utc)));
        }

        return entries;
    }

    public long AddSticky(string text)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Stickies (Text, SortOrder, CreatedTicks)
            VALUES (@text, (SELECT COALESCE(MAX(SortOrder), 0) + 1 FROM Stickies), @ticks);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@text", text);
        command.Parameters.AddWithValue("@ticks", DateTime.UtcNow.Ticks);

        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void UpdateSticky(long id, string text)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Stickies SET Text = @text WHERE Id = @id;";
        command.Parameters.AddWithValue("@text", text);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void DeleteSticky(long id)
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Stickies WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public int CountArchive()
    {
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Archive;";
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    private static List<ClipEntry> ReadEntries(SqliteCommand command, Func<long, string, DateTime, ClipEntry> factory)
    {
        List<ClipEntry> entries = [];
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(factory(
                reader.GetInt64(0),
                reader.GetString(1),
                new DateTime(reader.GetInt64(2), DateTimeKind.Utc)));
        }

        return entries;
    }

    private static string ToSearchText(string text) => text.ToLowerInvariant();

    private static string ToLikePattern(string term)
    {
        var escaped = term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return "%" + escaped.ToLowerInvariant() + "%";
    }
}
