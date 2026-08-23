# GLEM（Generalized Limit Equilibrium Method）

地盤工学における斜面安定解析・沈下予測を行う Windows デスクトップアプリケーション（C# / .NET 8 + WPF）。

## ドキュメント

| 文書 | 内容 |
|---|---|
| [docs/GLEM_機能仕様書.md](docs/GLEM_機能仕様書.md) | 要求事項（F-01〜F-06, V-*, A-*, T-*） |
| [docs/GLEM_基本設計書.md](docs/GLEM_基本設計書.md) | システム全体方針・機能分解・品質方針 |
| [docs/GLEM_詳細設計書.md](docs/GLEM_詳細設計書.md) | クラス・アルゴリズム・UI 詳細 |
| [docs/GLEM_テスト計画書.md](docs/GLEM_テスト計画書.md) | テストレベル・不具合管理・CI |
| [docs/GLEM_ユーザーマニュアル.md](docs/GLEM_ユーザーマニュアル.md) | ユーザー向け操作手順・FAQ |
| [docs/GLEM_実装計画書.md](docs/GLEM_実装計画書.md) | マイルストーン M0〜M4・WBS・リスク |

## ソリューション構成

```
GLEM.sln
├── src/GLEM.App/     WPF アプリケーション（net8.0-windows）
├── src/GLEM.Core/    ドメインモデル＋解析エンジン（net8.0、UI 非依存）
└── tests/GLEM.Tests/ xUnit テスト
```

## 開発環境の前提

- .NET SDK 8.0.x が PATH に存在すること
- カスタムディレクトリにインストールした場合（例: `%LOCALAPPDATA%\Microsoft\dotnet`）、**`DOTNET_ROOT` 環境変数**をそのディレクトリに設定する（apphost が hostfxr.dll を解決するために必要）

## ビルド・テスト

```powershell
dotnet build GLEM.sln -c Release
dotnet test tests/GLEM.Tests/GLEM.Tests.csproj -c Release --collect:"XPlat Code Coverage"
# カバレッジゲート（GLEM.Core >= 80%、テスト計画書 §5.2）
$xml = Get-ChildItem -Recurse -Include *.cobertura.xml | Sort-Object LastWriteTime -Descending | Select-Object -First 1
powershell -NoProfile -File scripts/coverage-gate.ps1 -CoverageXmlPath $xml.FullName
```

## 配布物の生成

自己完結型の Windows x64 配布物は次のコマンドで生成できます。`dist/` は実行ファイルが大きいため GitHub のソース管理対象外です。

```powershell
dotnet publish src/GLEM.App/GLEM.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o dist/GLEM-1.0.0-win-x64
```

## CI

`.github/workflows/ci.yml`：build → test（カバレッジ収集）→ カバレッジゲート（GLEM.Core ≥80%）。
