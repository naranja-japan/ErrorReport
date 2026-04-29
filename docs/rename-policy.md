# NdcErrorReport → ErrorReport 命名変更方針

## 目的
- 既存の `NdcErrorReport` を、新しい `ErrorReport` ソリューションへ全面改名する。
- 名前空間は `Naranja.Error.Report` とし、共通基盤参照は `Naranja.Platform.*` に切り替える。

## 命名ルール
- ソリューションフォルダ: `ErrorReport`
- ソリューション名: `ErrorReport`
- メインプロジェクトフォルダ: `ErrorReport\ErrorReport`
- メインプロジェクト名: `ErrorReport`
- 名前空間: `Naranja.Error.Report`
- 出力 EXE: `ErrorReport.exe`

## 今回の自動変換内容
- `NdcErrorReport` を `ErrorReport` に置換
- `Ndc.Common` / `Ndc.Data` を `Naranja.Platform.Common` / `Naranja.Platform.Data` に置換
- ローカル NuGet パッケージ参照先を `PlatformPackages` 前提へ更新
- deploy スクリプトや manifest 内の旧名称を新名称へ置換

## 次の確認ポイント
1. `ErrorReport.slnx` が開けること
2. `Naranja.Error.Report` 名前空間でビルドできること
3. `NuGet.config` が `PlatformPackages` を参照していること
4. 新しい GitHub リポジトリへ初回 push できること