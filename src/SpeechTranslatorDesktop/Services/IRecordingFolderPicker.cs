namespace SpeechTranslatorDesktop.Services;

public interface IRecordingFolderPicker
{
    string? PickFolder(string initialDirectory, string title);
}
