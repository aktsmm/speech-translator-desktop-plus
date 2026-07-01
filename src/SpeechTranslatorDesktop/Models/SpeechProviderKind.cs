namespace SpeechTranslatorDesktop.Models;

public enum SpeechProviderKind
{
    AzureAiSpeech,
    GoogleCloud
}

public sealed record SpeechProviderOption(SpeechProviderKind Provider, string DisplayName, string Description)
{
    public override string ToString() => DisplayName;
}
