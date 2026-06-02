namespace SpeechTranslatorDesktop.Models;

public enum RecognitionMode
{
    Translation,
    TranscriptionOnly
}

public sealed record RecognitionModeOption(RecognitionMode Mode, string DisplayName)
{
    public override string ToString() => DisplayName;
}
