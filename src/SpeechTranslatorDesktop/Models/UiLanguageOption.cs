namespace SpeechTranslatorDesktop.Models;

public enum UiLanguage
{
    Japanese,
    English
}

public sealed record UiLanguageOption(UiLanguage Language, string DisplayName)
{
    public override string ToString() => DisplayName;
}
