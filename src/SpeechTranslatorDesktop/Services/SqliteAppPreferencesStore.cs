using Microsoft.Data.Sqlite;

namespace SpeechTranslatorDesktop.Services;

public sealed class SqliteAppPreferencesStore : IAppPreferencesStore
{
    private const string TableName = "app_preferences";
    private readonly string _databasePath;

    public SqliteAppPreferencesStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(databasePath));
        }

        _databasePath = databasePath;
    }

    public async Task<AppPreferences?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath))
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(createDirectory: false, cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ui_language, source_language, target_language, audio_input_source, recognition_mode, is_recording_save_enabled, recording_file_name_prefix
            FROM {TableName}
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AppPreferences(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetBoolean(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await using var connection = await OpenConnectionAsync(createDirectory: true, cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {TableName} (id, ui_language, source_language, target_language, audio_input_source, recognition_mode, is_recording_save_enabled, recording_file_name_prefix)
            VALUES (1, $uiLanguage, $sourceLanguage, $targetLanguage, $audioInputSource, $recognitionMode, $isRecordingSaveEnabled, $recordingFileNamePrefix)
            ON CONFLICT(id) DO UPDATE SET
                ui_language = excluded.ui_language,
                source_language = excluded.source_language,
                target_language = excluded.target_language,
                audio_input_source = excluded.audio_input_source,
                recognition_mode = excluded.recognition_mode,
                is_recording_save_enabled = excluded.is_recording_save_enabled,
                recording_file_name_prefix = excluded.recording_file_name_prefix;
            """;
        command.Parameters.AddWithValue("$uiLanguage", preferences.UiLanguage ?? string.Empty);
        command.Parameters.AddWithValue("$sourceLanguage", preferences.SourceLanguage ?? string.Empty);
        command.Parameters.AddWithValue("$targetLanguage", preferences.TargetLanguage ?? string.Empty);
        command.Parameters.AddWithValue("$audioInputSource", preferences.AudioInputSource ?? string.Empty);
        command.Parameters.AddWithValue("$recognitionMode", preferences.RecognitionMode ?? string.Empty);
        command.Parameters.AddWithValue("$isRecordingSaveEnabled", preferences.IsRecordingSaveEnabled ?? true);
        command.Parameters.AddWithValue("$recordingFileNamePrefix", preferences.RecordingFileNamePrefix ?? string.Empty);

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
                ui_language TEXT NOT NULL,
                source_language TEXT NOT NULL,
                target_language TEXT NOT NULL,
                audio_input_source TEXT NOT NULL,
                recognition_mode TEXT NOT NULL,
                is_recording_save_enabled INTEGER NOT NULL DEFAULT 1,
                recording_file_name_prefix TEXT NOT NULL DEFAULT ''
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);

        await AddColumnIfMissingAsync(connection, "is_recording_save_enabled", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync(connection, "recording_file_name_prefix", "TEXT NOT NULL DEFAULT ''", cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection connection, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        await using var tableInfoCommand = connection.CreateCommand();
        tableInfoCommand.CommandText = $"PRAGMA table_info({TableName});";
        await using var reader = await tableInfoCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {TableName} ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
