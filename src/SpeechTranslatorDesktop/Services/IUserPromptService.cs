namespace SpeechTranslatorDesktop.Services;

public interface IUserPromptService
{
    bool Confirm(string title, string message);
}
