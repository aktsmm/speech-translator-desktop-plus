# Speech Translator Desktop Plus

Speech Translator Desktop Plus is a Windows desktop speech translator and recorder using [Azure AI Speech](https://azure.microsoft.com/en-us/products/ai-services/ai-speech).

This project is based on [tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator).
The original project is licensed under the MIT License. The original copyright and license text are preserved in [LICENSE](./LICENSE).

## Plus features

- Real-time speech translation with Azure AI Speech.
- Microphone input.
- PC audio input using WASAPI loopback, so YouTube, livestreams, and other system playback can be translated without VB-CABLE.
- Translation log recording as UTF-8 text.
- Recording folder selection, persistence, and "open folder" UI.
- Azure AI Speech region/API key persistence in SQLite with the API key protected by Windows DPAPI.

## Prerequisites

- Windows 10/11
- [.NET 10.0 SDK](https://dot.net/download) for development
- Azure AI Speech resource
  - The Azure Speech F0 tier includes limited free monthly usage.

## Run the desktop app from source

```powershell
.\Start-SpeechTranslatorDesktopPlus.ps1
```

or:

```cmd
Start-SpeechTranslatorDesktopPlus.cmd
```

In the app:

1. Enter the Azure AI Speech `Region` and `API Key`, then click `保存`.
2. Select source language and target language.
3. Select audio input:
   - `マイク`
   - `PC音声（既定の再生デバイス）`
4. Optionally enter a recording file name stem, using letters/numbers/`-`/`_` only.
5. Click `開始`.

If the recording file name is empty, translation is shown in the UI but not saved to a text file.

## Recording folder

Use the `保存先` controls in the app to choose and open the folder where translation logs are saved.

The selected folder is persisted in the local settings database. If no custom folder is selected, logs are saved under `recordings/` relative to the app executable directory.

## Publish to an install folder

Use the publish helper to place the app in a folder of your choice:

```powershell
.\Publish-SpeechTranslatorDesktopPlus.ps1 -InstallPath "$env:LOCALAPPDATA\Programs\SpeechTranslatorDesktopPlus" -CreateDesktopShortcut
```

You can replace `-InstallPath` with any writable folder. This is a lightweight folder-based install, not an MSI/MSIX installer.

## Console app

The original console app remains available:

1. Create Azure AI Speech resource. ([Bicep](./infra/main.bicep))
2. Copy `Subscription Key` and `Region` from Azure Portal.
3. Create `src/SpeechTranslatorConsole/appsettings.Development.json`.
4. Setup `appsettings.Development.json`:

    ```json
    {
        "Settings": {
            "Region": "<Region>",
            "SubscriptionKey": "<Subscription Key>"
        }
    }
    ```

5. Set the microphone device for translation as the default input device.
6. Run:

    ```powershell
    dotnet run --project src/SpeechTranslatorConsole
    ```

## References

- https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-identification
- https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-translate-speech
- https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-translation

## License and attribution

This repository is distributed under the MIT License. See [LICENSE](./LICENSE).

Based on:

- Original repository: https://github.com/tsubakimoto/speech-translator
- Original author/license notice: `Copyright (c) 2023 Yuta Matsumura`
- Original license: MIT License
