using Microsoft.Data.Sqlite;
using SpeechTranslatorDesktop.Models;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslator.Desktop.Tests;

public class SqliteAppPreferencesStoreTests : IDisposable
{
    private readonly string _testDirectory;

    public SqliteAppPreferencesStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), nameof(SqliteAppPreferencesStoreTests), Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsPreferences()
    {
        var databasePath = Path.Combine(_testDirectory, "speech-translator-desktop.db");
        var store = new SqliteAppPreferencesStore(databasePath);

        await store.SaveAsync(new AppPreferences(
            "English",
            "ja-JP",
            "en-US",
            "SystemAudio",
            "TranscriptionOnly",
            false,
            "build2026",
            nameof(SpeechProviderKind.GoogleCloud),
            "my-project",
            "global",
            "chirp_3",
            @"C:\keys\google.json",
            "endpoint",
            "region",
            "model",
            "deployment",
            "profile"));

        var preferences = await store.LoadAsync();

        preferences.Should().BeEquivalentTo(new AppPreferences(
            "English",
            "ja-JP",
            "en-US",
            "SystemAudio",
            "TranscriptionOnly",
            false,
            "build2026",
            nameof(SpeechProviderKind.GoogleCloud),
            "my-project",
            "global",
            "chirp_3",
            @"C:\keys\google.json",
            "endpoint",
            "region",
            "model",
            "deployment",
            "profile"));
    }

    [Fact]
    public async Task LoadAsync_WithLegacyTable_AddsNewColumnsAndReturnsDefaults()
    {
        var databasePath = Path.Combine(_testDirectory, "speech-translator-desktop.db");
        Directory.CreateDirectory(_testDirectory);

        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE app_preferences (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    ui_language TEXT NOT NULL,
                    source_language TEXT NOT NULL,
                    target_language TEXT NOT NULL,
                    audio_input_source TEXT NOT NULL,
                    recognition_mode TEXT NOT NULL
                );
                INSERT INTO app_preferences (id, ui_language, source_language, target_language, audio_input_source, recognition_mode)
                VALUES (1, 'Japanese', 'en-US', 'ja-JP', 'MicrophoneAndSystemAudio', 'Translation');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteAppPreferencesStore(databasePath);
        var preferences = await store.LoadAsync();

        preferences.Should().BeEquivalentTo(new AppPreferences(
            "Japanese",
            "en-US",
            "ja-JP",
            "MicrophoneAndSystemAudio",
            "Translation",
            true,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
