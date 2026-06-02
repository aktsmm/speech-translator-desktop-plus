# Speech Translator Desktop Plus

[日本語 README](README.ja.md)

Speech Translator Desktop Plus is a Windows desktop speech translator and recorder using [Azure AI Speech](https://azure.microsoft.com/en-us/products/ai-services/ai-speech).

![Speech Translator Desktop Plus translating a Microsoft Build livestream](docs/images/build-live-translation-demo.png)

This project is based on [tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator). The original project is licensed under the MIT License. The original copyright and license text are preserved in [LICENSE](./LICENSE).

## Features

- Real-time speech translation with Azure AI Speech.
- Microphone input.
- PC audio input using WASAPI loopback, so YouTube, livestreams, and other system playback can be translated without VB-CABLE.
- Translation logs are shown newest-first, so the latest text stays visible.
- Transcript-only mode for cases where translation is not needed.
- Translation log recording as UTF-8 text.
- Last-used UI language, source language, target language, input source, and mode are restored on the next launch.
- Recording folder selection, persistence, and "open folder" UI.
- Dedicated settings window for Azure Speech credentials and recording folder controls.
- Japanese and English UI language switching.
- Major speech translation languages are available from the source/target language selectors.
- Azure AI Speech region/API key persistence in SQLite with the API key protected by Windows DPAPI.
- Self-contained Windows publish and zip packaging scripts for easy setup.

## Changes from the upstream fork

Compared with [tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator), this Plus fork adds:

- Desktop app branding as **Speech Translator Desktop Plus**.
- A custom application icon.
- Modern card-based desktop UI focused on live captioning and note-taking.
- A mode selector for `Translate + transcript` and `Transcript only`.
- PC audio capture via WASAPI loopback.
- Combined `Microphone + PC audio` input mode.
- `Microphone + PC audio` is the default input mode for livestream translation and note-taking.
- Transcript-only mode using Azure Speech recognition when translation is unnecessary.
- Last-used language/input/mode preferences are persisted and restored.
- Newest-first translation and status logs.
- Wrapped translation table rows, capped to roughly three visible lines.
- Empty translation row suppression.
- Clear logs action that resets the on-screen logs and starts a fresh recording file on the next start.
- Optional translation pop-out window with card-style monitoring for the three newest source/translation pairs.
- Configurable and persisted recording folder.
- Dedicated settings window to keep the main translation screen focused.
- `Open folder` and `Choose folder` controls.
- Japanese/English UI switching.
- Additional source/target language choices.
- English-first README plus separate Japanese README.
- One-command setup, self-contained publish, and release zip packaging scripts.
- GitHub Release asset support for easy exe-based installation.

## Recommended setup

### Option 1: Download the release zip

1. Open the latest GitHub release.
2. Download `SpeechTranslatorDesktopPlus-win-x64.zip`.
3. Extract it to any writable folder.
4. Run `SpeechTranslatorDesktopPlus.exe`.

### Option 2: One-command setup from source

From the repository root:

```powershell
.\scripts\setup.ps1
```

The setup script installs a local .NET 10 SDK if needed, publishes a self-contained Windows build to `%LOCALAPPDATA%\Programs\SpeechTranslatorDesktopPlus`, and creates a desktop shortcut.

## Run from source

```powershell
.\scripts\run-dev.ps1
```

or:

```cmd
scripts\run-dev.cmd
```

## Basic usage

1. Select UI language: `日本語` or `English`.
2. Open `Settings` and save the Azure AI Speech `Region` and `API Key`.
3. Select mode:
   - `Translate + transcript`
   - `Transcript only`
4. Select source language and, when translation is enabled, target language.
5. Select audio input:
   - `Microphone`
   - `PC audio (default playback device)`
   - `Microphone + PC audio` (default)
6. Optionally enter a recording file name stem, using letters/numbers/`-`/`_` only.
7. Click `Start`.
8. Click `Clear logs` to clear the visible translation/status logs. If a recording file name is set, the next `Start` creates a new timestamped recording file instead of appending to the previous file.
9. Click `Open translations window` to open a separate window that shows only the three newest source/translation pairs.

If the recording file name is empty, translation/transcription is shown in the UI but not saved to a text file.

Your last-used UI language, mode, source language, target language, and audio input are saved locally and restored when you launch the app again.

Rows with no source text are ignored. In translation mode, rows with no translated text are also ignored to avoid blank lines in the UI and recording files.

## Recording folder

Use `Settings` to choose and open the folder where translation logs are saved.

The selected folder is persisted in the local settings database. If no custom folder is selected, logs are saved under `recordings/` relative to the app executable directory.

## Publish and package

Publish to a folder:

```powershell
.\scripts\publish.ps1 -InstallPath "$env:LOCALAPPDATA\Programs\SpeechTranslatorDesktopPlus" -CreateDesktopShortcut
```

Create a release zip:

```powershell
.\scripts\package.ps1
```

Helper scripts live in [`scripts/`](scripts/) to keep the repository root focused.

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
