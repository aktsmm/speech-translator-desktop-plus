using Microsoft.Win32;

namespace SpeechTranslatorDesktop.Services;

public sealed class WpfRecordingFolderPicker : IRecordingFolderPicker
{
    public string? PickFolder(string initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "翻訳ログの保存先フォルダーを選択",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
