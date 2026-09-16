namespace SpeechTranslatorDesktop.Services;

public sealed class SessionEndedEventArgs(IDesktopTranslationWorker worker, Exception? error = null) : EventArgs
{
    public IDesktopTranslationWorker Worker { get; } = worker;

    public Exception? Error { get; } = error;
}
