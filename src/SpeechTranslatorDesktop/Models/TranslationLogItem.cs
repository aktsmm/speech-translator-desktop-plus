namespace SpeechTranslatorDesktop.Models;

public sealed record TranslationLogItem(
    string SourceText,
    string TranslatedText,
    AudioSourceKind AudioSource = AudioSourceKind.Unspecified,
    string? SpeakerLabel = null)
{
    public string DisplaySourceText => string.IsNullOrWhiteSpace(SpeakerLabel)
        ? SourceText
        : $"{SpeakerLabel}: {SourceText}";

    public string AutomationText => string.IsNullOrWhiteSpace(TranslatedText)
        ? DisplaySourceText
        : $"{DisplaySourceText} {TranslatedText}";
}
