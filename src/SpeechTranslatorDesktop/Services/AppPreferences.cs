namespace SpeechTranslatorDesktop.Services;

public sealed record AppPreferences(
    string? UiLanguage,
    string? SourceLanguage,
    string? TargetLanguage,
    string? AudioInputSource,
    string? RecognitionMode,
    bool? IsRecordingSaveEnabled = null,
    string? RecordingFileNamePrefix = null);
