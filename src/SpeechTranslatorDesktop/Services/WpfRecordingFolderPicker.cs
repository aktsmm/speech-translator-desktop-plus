using Microsoft.Win32;

namespace SpeechTranslatorDesktop.Services;

public sealed class WpfRecordingFolderPicker : IRecordingFolderPicker
{
    public string? PickFolder(string initialDirectory, string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
