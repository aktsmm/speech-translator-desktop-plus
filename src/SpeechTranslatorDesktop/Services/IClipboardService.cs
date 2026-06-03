namespace SpeechTranslatorDesktop.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
