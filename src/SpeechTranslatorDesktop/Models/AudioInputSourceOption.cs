namespace SpeechTranslatorDesktop.Models;

public enum AudioInputSource
{
    Microphone,
    SystemAudio,
    MicrophoneAndSystemAudio
}

public sealed record AudioInputSourceOption(AudioInputSource Source, string DisplayName)
{
    public override string ToString() => DisplayName;
}
