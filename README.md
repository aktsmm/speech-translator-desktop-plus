# Speech Translator Desktop Plus

English | [日本語](#日本語)

Speech Translator Desktop Plus is a Windows desktop speech translator and recorder using [Azure AI Speech](https://azure.microsoft.com/en-us/products/ai-services/ai-speech).

This project is based on [tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator). The original project is licensed under the MIT License. The original copyright and license text are preserved in [LICENSE](./LICENSE).

## Features

- Real-time speech translation with Azure AI Speech.
- Microphone input.
- PC audio input using WASAPI loopback, so YouTube, livestreams, and other system playback can be translated without VB-CABLE.
- Translation logs are shown newest-first, so the latest text stays visible.
- Translation log recording as UTF-8 text.
- Recording folder selection, persistence, and "open folder" UI.
- Japanese and English UI language switching.
- Major speech translation languages are available from the source/target language selectors.
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

1. Select UI language: `日本語` or `English`.
2. Enter the Azure AI Speech `Region` and `API Key`, then click `Save`.
3. Select source language and target language.
4. Select audio input:
   - `Microphone`
   - `PC audio (default playback device)`
5. Optionally enter a recording file name stem, using letters/numbers/`-`/`_` only.
6. Click `Start`.

If the recording file name is empty, translation is shown in the UI but not saved to a text file.

## Recording folder

Use the recordings folder controls in the app to choose and open the folder where translation logs are saved.

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

## 日本語

Speech Translator Desktop Plus は、[Azure AI Speech](https://azure.microsoft.com/ja-jp/products/ai-services/ai-speech) を使った Windows デスクトップ向けのリアルタイム音声翻訳・記録アプリです。

このプロジェクトは [tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator) をベースにしています。元プロジェクトは MIT License です。元の著作権表示とライセンス文は [LICENSE](./LICENSE) に残しています。

## 機能

- Azure AI Speech によるリアルタイム音声翻訳
- マイク入力
- WASAPI loopback による PC 音声入力
  - YouTube、ライブ配信、PC で再生中の音声を VB-CABLE なしで翻訳できます
- 翻訳ログは新しいものが上に表示されるため、最新テキストをスクロールせず確認できます
- UTF-8 テキストとして翻訳ログを保存
- 保存先フォルダーの選択、永続化、フォルダーを開く UI
- 日本語 / 英語 UI の切り替え
- 主要な音声翻訳言語を話者言語・翻訳先言語として選択可能
- Azure AI Speech のリージョン/APIキーを SQLite に保存
  - APIキーは Windows DPAPI で保護されます

## 前提条件

- Windows 10/11
- 開発時は [.NET 10.0 SDK](https://dot.net/download)
- Azure AI Speech リソース
  - Azure Speech の F0 レベルには月あたりの無料利用枠があります

## ソースからデスクトップアプリを起動

```powershell
.\Start-SpeechTranslatorDesktopPlus.ps1
```

または:

```cmd
Start-SpeechTranslatorDesktopPlus.cmd
```

アプリ内での手順:

1. UI言語として `日本語` または `English` を選択します。
2. Azure AI Speech の `Region` と `API Key` を入力し、`保存` を押します。
3. 話者言語と翻訳先言語を選択します。
4. 音声入力を選択します。
   - `マイク`
   - `PC音声（既定の再生デバイス）`
5. 必要に応じて記録ファイル名を入力します。
   - 英数字、`-`、`_` のみ使用できます
6. `開始` を押します。

記録ファイル名が空の場合、翻訳は画面に表示されますがテキストファイルには保存されません。

## 保存先フォルダー

アプリ内の保存先コントロールから、翻訳ログを保存するフォルダーを選択・開くことができます。

選択した保存先はローカル設定DBに保存されます。保存先を選択していない場合は、アプリ実行ファイルから見た `recordings/` 配下に保存されます。

## 任意フォルダーへの配置

次の publish helper で、好きなフォルダーにアプリを配置できます。

```powershell
.\Publish-SpeechTranslatorDesktopPlus.ps1 -InstallPath "$env:LOCALAPPDATA\Programs\SpeechTranslatorDesktopPlus" -CreateDesktopShortcut
```

`-InstallPath` は任意の書き込み可能なフォルダーに置き換えられます。これは MSI/MSIX ではなく、フォルダー配置型の軽量インストール方式です。

## コンソールアプリ

元のコンソールアプリも残しています。

1. Azure AI Speech リソースを作成します。([Bicep](./infra/main.bicep))
2. Azure Portal から `Subscription Key` と `Region` をコピーします。
3. `src/SpeechTranslatorConsole/appsettings.Development.json` を作成します。
4. `appsettings.Development.json` を設定します。

    ```json
    {
        "Settings": {
            "Region": "<Region>",
            "SubscriptionKey": "<Subscription Key>"
        }
    }
    ```

5. 翻訳対象のマイクデバイスを既定の入力デバイスに設定します。
6. 実行します。

    ```powershell
    dotnet run --project src/SpeechTranslatorConsole
    ```

## 参考

- https://learn.microsoft.com/azure/ai-services/speech-service/language-identification
- https://learn.microsoft.com/azure/ai-services/speech-service/how-to-translate-speech
- https://learn.microsoft.com/azure/ai-services/speech-service/speech-translation

## ライセンスと帰属

このリポジトリは MIT License で配布します。詳細は [LICENSE](./LICENSE) を参照してください。

ベース:

- 元リポジトリ: https://github.com/tsubakimoto/speech-translator
- 元の著作権表示: `Copyright (c) 2023 Yuta Matsumura`
- 元ライセンス: MIT License
