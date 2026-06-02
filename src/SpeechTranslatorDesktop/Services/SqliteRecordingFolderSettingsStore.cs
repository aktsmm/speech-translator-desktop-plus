using Microsoft.Data.Sqlite;

namespace SpeechTranslatorDesktop.Services;

public sealed class SqliteRecordingFolderSettingsStore : IRecordingFolderSettingsStore
{
    private const string TableName = "recording_folder_settings";
    private readonly string _databasePath;

    public SqliteRecordingFolderSettingsStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(databasePath));
        }

        _databasePath = databasePath;
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath))
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(createDirectory: false, cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT directory_path
            FROM {TableName}
            WHERE id = 1;
            """;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value as string;
    }

    public async Task SaveAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("保存フォルダーを指定してください。", nameof(directoryPath));
        }

        await using var connection = await OpenConnectionAsync(createDirectory: true, cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {TableName} (id, directory_path)
            VALUES (1, $directoryPath)
            ON CONFLICT(id) DO UPDATE SET
                directory_path = excluded.directory_path;
            """;
        command.Parameters.AddWithValue("$directoryPath", Path.GetFullPath(directoryPath.Trim()));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(bool createDirectory, CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(_databasePath);
        if (createDirectory && !string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = createDirectory ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var connection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                directory_path TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
