using System.Windows;

namespace SpeechTranslatorDesktop.Services;

public sealed class WpfUserPromptService : IUserPromptService
{
    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
