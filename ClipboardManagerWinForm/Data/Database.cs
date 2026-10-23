using Microsoft.Data.Sqlite;

namespace ClipboardManagerWinForm.Data;

public sealed class Database
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS History (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Text         TEXT    NOT NULL,
            SearchText   TEXT    NOT NULL,
            CreatedTicks INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_History_Created ON History (CreatedTicks DESC);

        CREATE TABLE IF NOT EXISTS Archive (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Text         TEXT    NOT NULL,
            SearchText   TEXT    NOT NULL,
            CreatedTicks INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Archive_Created ON Archive (CreatedTicks DESC);

        CREATE TABLE IF NOT EXISTS Stickies (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Text         TEXT    NOT NULL,
            SortOrder    INTEGER NOT NULL DEFAULT 0,
            CreatedTicks INTEGER NOT NULL
        );
        """;

    public Database(string filePath)
    {
        FilePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EnsureSchema();
    }

    public string FilePath { get; }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString());

        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=3000;";
        command.ExecuteNonQuery();

        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        command.ExecuteNonQuery();
    }
}
