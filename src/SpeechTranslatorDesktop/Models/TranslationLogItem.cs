namespace SpeechTranslatorDesktop.Models;

public sealed record TranslationLogItem(string SourceText, string TranslatedText)
{
    public string AutomationText => string.IsNullOrWhiteSpace(TranslatedText)
        ? SourceText
        : $"{SourceText} {TranslatedText}";
}
