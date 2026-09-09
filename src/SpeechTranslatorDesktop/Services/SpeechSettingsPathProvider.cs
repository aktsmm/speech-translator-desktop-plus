namespace SpeechTranslatorDesktop.Services;

public static class SpeechSettingsPathProvider
{
    public static string GetAppDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SpeechTranslatorDesktop");
    }

    public static string GetDatabasePath()
    {
        return Path.Combine(GetAppDataDirectory(), "speech-translator-desktop.db");
    }
}
