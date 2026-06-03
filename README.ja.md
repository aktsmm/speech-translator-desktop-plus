# Speech Translator Desktop Plus

[English README](README.md)

Speech Translator Desktop Plus は、[Azure AI Speech](https://azure.microsoft.com/ja-jp/products/ai-services/ai-speech) を使った Windows デスクトップ向けのリアルタイム音声翻訳・記録アプリです。

このプロジェクトは [tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator) をベースにしています。元プロジェクトは MIT License です。元の著作権表示とライセンス文は [LICENSE](./LICENSE) に残しています。

> この README は version 1.8.2 を対象にしています。

## スクリーンショット

### 日本語UI

![Speech Translator Desktop Plus の日本語UI](docs/images/main-window-ja.png)

### English UI

![Speech Translator Desktop Plus English UI](docs/images/main-window-en.png)

### ライブ配信翻訳の例

![Microsoft Build のライブ配信を翻訳している Speech Translator Desktop Plus](docs/images/livestream-translation-demo.png)

## 機能

- Azure AI Speech によるリアルタイム音声翻訳
- マイク入力
- WASAPI loopback による PC 音声入力
  - YouTube、ライブ配信、PC で再生中の音声を VB-CABLE なしで翻訳できます
- 翻訳ログは新しいものが上に表示されるため、最新テキストをスクロールせず確認できます
- 翻訳ログと状態ログは、必要に応じて折りたためます
- 翻訳が不要な場面向けの `書き起こしのみ` モード
- タイムスタンプ付きファイル名で、翻訳/書き起こしログを UTF-8 テキストとして保存
- ライブログ全体、個別カード、原文のみ、訳文のみをワンクリックでコピー
  - カード単位のコピー操作は本文横に配置し、コピー用の行で縦スペースを消費しないようにしています
- 前回利用した UI言語、話者言語、翻訳先言語、入力、利用モード、保存設定、ファイル名prefixを次回起動時に復元
- 保存先フォルダーの選択、永続化、フォルダーを開く UI
- Azure Speech 認証情報と保存先をメイン画面から分離した設定ウィンドウ
- 日本語 / 英語 UI の切り替え
- 主要な音声翻訳言語を話者言語・翻訳先言語として選択可能
- Azure AI Speech のリージョン/APIキーを SQLite に保存
  - APIキーは Windows DPAPI で保護されます
- 自己完結型 Windows publish と zip パッケージ作成スクリプトに対応

## fork元からの主な変更点

[tsubakimoto/speech-translator](https://github.com/tsubakimoto/speech-translator) と比べて、この Plus fork では次を追加・改善しています。

- デスクトップアプリ名を **Speech Translator Desktop Plus** に変更
- カスタムアプリアイコンを追加
- ライブ字幕・メモ取りに集中しやすいモダンなカード型デスクトップUI
- 表形式ではなくカード型で読めるライブログ表示
- `翻訳 + 書き起こし` / `書き起こしのみ` を選べる利用モード切り替え
- WASAPI loopback による PC 音声キャプチャ
- `マイク + PC音声` 入力モード
- ライブ配信やメモ取りに使いやすいよう、既定の入力は `マイク + PC音声`
- 翻訳不要時に Azure Speech 認識だけを使う `書き起こしのみ` モード
- 前回利用した言語・入力・モード・保存設定・ファイル名prefixの永続化と復元
- `記録を保存する` のON/OFF切り替えと `{prefix}_yyyyMMdd_HHmmss.txt` 形式の自動命名
- 翻訳ログ・状態ログを新しい順に表示
- 翻訳ログ・状態ログの折りたたみ表示
- 全ログコピー、原文のみコピー、訳文のみコピー、カード単位コピーに対応
- 状態イベントをコンパクトなActivity feedとして表示
- 原文・翻訳文を読みやすく折り返すライブノートカード
- メイン画面と別ウィンドウの両方で、省スペースなライブノートカードを使用
- 空の翻訳行を抑止
- ログクリア操作
  - 画面上の翻訳ログ・状態ログのみをクリアします。記録ファイルは開始ごとに新規作成されます
- 最新3件だけをカード形式の別ウィンドウで表示する `別ウィンドウでライブノートを開く` 機能
- 保存先フォルダーの選択・永続化
- 翻訳に集中できるよう、Azure設定と保存先設定を専用の設定ウィンドウへ移動
- `開く` / `選択` UI
- 日本語 / 英語 UI 切り替え
- 主要言語の選択肢追加
- 英語READMEをメインにし、日本語READMEを別ファイル化
- ワンコマンドセットアップ、自己完結型publish、Release zip作成スクリプト
- exeベースで導入しやすいGitHub Release配布

## おすすめセットアップ

### 方法1: Release zip をダウンロード

1. 最新の GitHub Release を開きます。
2. `SpeechTranslatorDesktopPlus-win-x64.zip` をダウンロードします。保管用に `SpeechTranslatorDesktopPlus-win-x64-1.8.2.zip` のような version 付きコピーも公開されます。
3. 任意の書き込み可能なフォルダーへ展開します。
4. `SpeechTranslatorDesktopPlus.exe` を実行します。

### 方法2: ソースからワンコマンドセットアップ

リポジトリルートで実行します。

```powershell
.\scripts\setup.ps1
```

セットアップスクリプトは、必要に応じてユーザー領域に .NET 10 SDK を導入し、自己完結型の Windows ビルドを `%LOCALAPPDATA%\Programs\SpeechTranslatorDesktopPlus` に配置し、デスクトップショートカットを作成します。システム全体の .NET インストールは変更しません。

必要条件:

- Windows 10 以降
- ローカル .NET 10 SDK が未導入の場合はインターネット接続
- PowerShell スクリプト実行が許可されていること。Windows にローカルスクリプト実行をブロックされた場合は、現在のユーザーで PowerShell を開き、次を実行してください。

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

実行ポリシーを変更できない場合は、cmd ラッパーを使えます。

```cmd
scripts\setup.cmd
```

保存済み設定を残したままローカルアプリ本体を削除する場合:

```powershell
.\scripts\uninstall.ps1
```

ローカル設定DBも削除したい場合だけ、`.\scripts\uninstall.ps1 -RemoveSettings` を使ってください。

## ソースから起動

```powershell
.\scripts\run-dev.ps1
```

または:

```cmd
scripts\run-dev.cmd
```

## 基本的な使い方

1. UI言語として `日本語` または `English` を選択します。
2. `設定` を開き、Azure AI Speech の `Region` と `API Key` を保存します。
3. 利用モードを選択します。
   - `翻訳 + 書き起こし`
   - `書き起こしのみ`
4. 話者言語を選択し、翻訳する場合は翻訳先言語も選択します。
5. 音声入力を選択します。
   - `マイク`
   - `PC音声（既定の再生デバイス）`
   - `マイク + PC音声`（既定）
6. 記録を保存するか選択します。既定では保存ONです。
7. 必要に応じてファイル名 prefix を入力します。
   - 英数字、`-`、`_` のみ使用できます
   - 空欄の場合は `session` を使います
   - 開始するたびに `{prefix}_yyyyMMdd_HHmmss.txt` 形式の新しいファイルを作成します。例: `build2026_20260603_080250.txt`
8. `開始` を押します。
9. `ログクリア` を押すと、画面上の翻訳ログ・状態ログをクリアします。記録ファイルは開始ごとに新規作成されます。
10. 表示領域を減らしたいときは、`翻訳ログ` または `状態ログ` を折りたためます。
11. `すべてコピー`、`原文をすべてコピー`、`訳文をすべてコピー` でライブログ全体を形式別にコピーできます。カード内の `コピー`、`原文コピー`、`訳文コピー` でそのブロックだけコピーできます。キーボードショートカットを使う場合は、ライブログ一覧をクリックするか `Tab` で一覧へフォーカスし、カードを選択してから `Ctrl+C` で選択中ブロック、`Ctrl+Shift+C` で原文、`Ctrl+T` で訳文をコピーできます。
12. `別ウィンドウでライブノートを開く` を押すと、原文・書き起こし/翻訳文の最新3件だけを別ウィンドウで確認できます。

`記録を保存する` がOFFの場合、翻訳/書き起こしは画面に表示されますがテキストファイルには保存されません。

前回利用した UI言語、利用モード、話者言語、翻訳先言語、音声入力、保存設定、ファイル名prefixはローカルに保存され、次回起動時に復元されます。

原文が空の行は、UIにも記録ファイルにも出さないようにしています。翻訳モードでは、翻訳文が空の行も空行防止のため抑止します。

## トラブルシュート

| 症状 | 確認すること |
| --- | --- |
| セットアップが publish 前に失敗する | Windows 10 以降か確認してください。.NET のダウンロードに失敗する場合は、インターネット/プロキシ設定を確認するか、.NET 10 SDK を手動インストールしてから `.\scripts\setup.ps1 -SkipDotNetInstall` を実行してください。 |
| PowerShell がスクリプト実行をブロックする | `scripts\setup.cmd` を使うか、`Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser` でローカルスクリプトを許可してください。 |
| Azure 認証情報が未設定と表示される | `設定` を開き、Azure AI Speech の `Region` と `API Key` を保存してください。または、起動前にユーザー環境変数 `SPEECH_REGION` / `SPEECH_KEY` を設定してください。 |
| マイクまたは PC 音声が認識されない | Windows のマイクプライバシー設定と、翻訳したい再生デバイスが Windows の既定デバイスになっているか確認してください。 |
| 設定や保存先フォルダーが保持されない | 書き込み可能なフォルダーにインストールし、`%LOCALAPPDATA%\SpeechTranslatorDesktop` に書き込めることを確認してください。 |
| Release zip の整合性を確認したい | Release の `SHA256SUMS.txt` をダウンロードし、`Get-FileHash -Algorithm SHA256 .\SpeechTranslatorDesktopPlus-win-x64.zip` の結果と比較してください。 |

## Azure AI Speech の無料枠

参考情報として、Azure AI Speech には、このアプリを従量課金へ進む前に試しやすい **Free (F0)** SKU があります。

2026-06-03 時点で、Azure Speech の価格ページには次の無料枠が記載されています。

- Speech to Text: Real-time Transcription が月5音声時間まで無料
- Speech Translation: Standard が月5音声時間まで無料
- Text to Speech: Neural が月50万文字まで無料

無料枠の有無、クォータ、含まれる時間や文字数は変更される可能性があります。利用前に公式の価格ページとクォータページを確認してください。

- Azure Speech pricing: https://azure.microsoft.com/pricing/details/cognitive-services/speech-services/
- Azure Speech quotas and limits: https://learn.microsoft.com/azure/ai-services/speech-service/speech-services-quotas-and-limits

## 保存先フォルダー

`設定` から、翻訳ログを保存するフォルダーを選択・開くことができます。

選択した保存先はローカル設定DBに保存されます。保存先を選択していない場合は、アプリ実行ファイルから見た `recordings/` 配下に保存されます。

## publish と zip 作成

任意フォルダーへ配置:

```powershell
.\scripts\publish.ps1 -InstallPath "$env:LOCALAPPDATA\Programs\SpeechTranslatorDesktopPlus" -CreateDesktopShortcut
```

Release zip 作成:

```powershell
.\scripts\package.ps1
```

デスクトップ版の Release asset 名は `SpeechTranslatorDesktopPlus-win-x64.zip` です。保管用に `SpeechTranslatorDesktopPlus-win-x64-{version}.zip` という version 付きコピーも作成します。Release にはチェックサム用の `SHA256SUMS.txt` もアップロードします。GitHub Actions workflow では、元のコンソールアプリをソース利用者向けに確認できる console zip もアップロードします。

補助スクリプトは [`scripts/`](scripts/) にまとめ、リポジトリルートを見やすくしています。

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
