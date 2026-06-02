using SpeechTranslatorDesktop.Models;
using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public sealed class DesktopTranslationWorker : TranslationRecognizerWorkerBase, IDesktopTranslationWorker
{
    private readonly string _targetLanguage;
    private readonly string? _recordingFileName;
    private readonly IRecordingFileService _recordingFileService;
    private readonly UiLanguage _uiLanguage;

    public DesktopTranslationWorker(string targetLanguage, string? recordingFileName, IRecordingFileService recordingFileService, UiLanguage uiLanguage = UiLanguage.Japanese)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(targetLanguage));
        }

        _targetLanguage = targetLanguage;
        _recordingFileName = recordingFileName;
        _recordingFileService = recordingFileService ?? throw new ArgumentNullException(nameof(recordingFileService));
        _uiLanguage = uiLanguage;
    }

    public TranslationRecognizerWorkerBase RecognizerWorker => this;

    public event EventHandler<string>? MessageLogged;

    public event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<TranslationLogItem>? TranslationLogged;

    public override void OnRecognizing(TranslationRecognitionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.Recognizing, Text("認識中", "Recognizing"));
    }

    public override void OnRecognized(TranslationRecognitionEventArgs e)
    {
        var result = e.Result;

        if (result.Reason == ResultReason.TranslatedSpeech)
        {
            var translatedText = result.Translations.TryGetValue(_targetLanguage, out var value)
                ? value
                : result.Translations.Values.FirstOrDefault() ?? string.Empty;
            HandleTranslatedSpeech(result.Text, translatedText);
            return;
        }

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            RaiseStatusChanged(DesktopTranslationStatus.RecognizedSpeech, Text("認識のみ", "Recognized only"));
            MessageLogged?.Invoke(this, Text($"認識のみ: {result.Text}", $"Recognized only: {result.Text}"));
            return;
        }

        if (result.Reason == ResultReason.NoMatch)
        {
            RaiseStatusChanged(DesktopTranslationStatus.NoMatch, "NoMatch");
            MessageLogged?.Invoke(this, "NOMATCH: Speech could not be recognized.");
        }
    }

    public override void OnCanceled(TranslationRecognitionCanceledEventArgs e)
    {
        var message = e.Reason == CancellationReason.Error
            ? $"Cancel/Error: {e.ErrorDetails}"
            : $"Canceled: {e.Reason}";

        RaiseStatusChanged(DesktopTranslationStatus.Canceled, "Cancel/Error");
        MessageLogged?.Invoke(this, message);
    }

    public override void OnSpeechStartDetected(RecognitionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SpeechStartDetected, Text("音声開始を検出", "Speech start detected"));
    }

    public override void OnSpeechEndDetected(RecognitionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SpeechEndDetected, Text("音声終了を検出", "Speech end detected"));
    }

    public override void OnSessionStarted(SessionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SessionStarted, Text("セッション開始", "Session started"));
    }

    public override void OnSessionStopped(SessionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SessionStopped, Text("セッション停止", "Session stopped"));
    }

    internal void HandleTranslatedSpeech(string sourceText, string translatedText)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        var translation = new TranslationLogItem(sourceText.Trim(), translatedText.Trim());
        TranslationLogged?.Invoke(this, translation);
        MessageLogged?.Invoke(this, Text($"原文: {translation.SourceText}", $"Source: {translation.SourceText}"));
        MessageLogged?.Invoke(this, Text($"翻訳: {translation.TranslatedText}", $"Translation: {translation.TranslatedText}"));

        try
        {
            _recordingFileService.AppendTranslation(_recordingFileName, translation.SourceText, translation.TranslatedText);
            RaiseStatusChanged(DesktopTranslationStatus.TranslatedSpeech, Text("翻訳成功", "Translation succeeded"));
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(DesktopTranslationStatus.Error, Text($"記録ファイルの保存に失敗しました: {ex.Message}", $"Failed to save recording file: {ex.Message}"));
        }
    }

    private void RaiseStatusChanged(DesktopTranslationStatus status, string message)
    {
        StatusChanged?.Invoke(this, new WorkerStatusChangedEventArgs(status, message));
    }

    private string Text(string japanese, string english)
    {
        return _uiLanguage == UiLanguage.English ? english : japanese;
    }
}
