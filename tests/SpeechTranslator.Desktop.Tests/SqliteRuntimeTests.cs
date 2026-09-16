using Microsoft.Data.Sqlite;

namespace SpeechTranslator.Desktop.Tests;

public class SqliteRuntimeTests
{
    [Fact]
    public void NativeRuntimeVersion_IsAtLeastTheSecurityFixedVersion()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version();";

        var runtimeVersion = Version.Parse(Assert.IsType<string>(command.ExecuteScalar()));

        // https://github.com/advisories/GHSA-2m69-gcr7-jv3q affects SQLite versions before 3.50.2.
        Assert.True(
            runtimeVersion >= new Version(3, 50, 2),
            $"The loaded native SQLite runtime ({runtimeVersion}) must be at least 3.50.2.");
    }
}
