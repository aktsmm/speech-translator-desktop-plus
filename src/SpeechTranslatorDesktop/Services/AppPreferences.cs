namespace SpeechTranslatorDesktop.Services;

public sealed record AppPreferences(
    string? UiLanguage,
    string? SourceLanguage,
    string? TargetLanguage,
    string? AudioInputSource,
    string? RecognitionMode,
    bool? IsRecordingSaveEnabled = null,
    string? RecordingFileNamePrefix = null,
    string? SpeechProvider = null,
    string? GoogleProjectId = null,
    string? GoogleLocation = null,
    string? GoogleSpeechModel = null,
    string? GoogleCredentialsPath = null);
