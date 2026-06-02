namespace SpeechTranslatorDesktop.Services;

public abstract class SpeechRecognizerWorkerBase
{
    public abstract void OnRecognizing(SpeechRecognitionEventArgs e);

    public abstract void OnRecognized(SpeechRecognitionEventArgs e);

    public abstract void OnCanceled(SpeechRecognitionCanceledEventArgs e);

    public abstract void OnSpeechStartDetected(RecognitionEventArgs e);

    public abstract void OnSpeechEndDetected(RecognitionEventArgs e);

    public abstract void OnSessionStarted(SessionEventArgs e);

    public abstract void OnSessionStopped(SessionEventArgs e);
}
