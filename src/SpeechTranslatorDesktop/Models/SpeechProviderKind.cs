namespace SpeechTranslatorDesktop.Models;

public enum SpeechProviderKind
{
    AzureAiSpeech,
    GoogleCloud,
    AzureOpenAIRealtime,
    AzureOpenAIWhisper,
    OpenAIDirect,
    AwsTranscribe,
    Deepgram,
    AssemblyAI
}

public sealed record SpeechProviderOption(SpeechProviderKind Provider, string DisplayName, string Description)
{
    public override string ToString() => DisplayName;
}
