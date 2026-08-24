# GLEM（Generalized Limit Equilibrium Method）

English version: [README.md](README.md)

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

## 主な機能

- Fellenius法・Bishop簡化法・Janbu一般化条体法による斜面安定解析
- 円弧滑動面の自動探索、中心・半径・条体幅の設定
- 制御点で定義するJanbu非円滑動面
- ruまたは地下水位線による間隙水圧、載荷、擬静力学的地震係数
- 即時沈下・一次圧密・二次圧縮を含む一次元沈下予測
- `.glem`プロジェクトファイル、自動保存・復元、CSV/HTMLレポート出力
- 日本語・英語のUI、検証メッセージ、グラフ、HTMLレポート

## 表示言語

既定ではWindowsの表示言語に従い、日本語環境では日本語、それ以外の環境では英語を使用します。明示的に切り替える場合は、メニューの **言語 > システム既定 / 英語 / 日本語** を選択してGLEMを再起動してください。設定はユーザーごとの `%LOCALAPPDATA%\GLEM\settings.json` に保存されます。

プロジェクト（`.glem`）とCSVのデータ形式は表示言語に依存せず、数値も常に一定の形式で保存されるため、英語環境と日本語環境の間で交換できます。

## 開発環境の前提

- Windows 10 / 11（64bit）
- .NET SDK 8.0.x

## ビルド・テスト

```powershell
dotnet restore GLEM.sln
dotnet build GLEM.sln -c Release --no-restore
dotnet test tests/GLEM.Tests/GLEM.Tests.csproj -c Release --no-build
```

カバレッジを計測する場合:

```powershell
dotnet test tests/GLEM.Tests/GLEM.Tests.csproj -c Release --collect:"XPlat Code Coverage"
$xml = Get-ChildItem -Recurse -Include *.cobertura.xml | Sort-Object LastWriteTime -Descending | Select-Object -First 1
powershell -NoProfile -File scripts/coverage-gate.ps1 -CoverageXmlPath $xml.FullName
```

## Release用パッケージ生成

自己完結型Windows x64配布物は次のコマンドで生成できます。出力先はGit管理対象外の `artifacts/release/` です。

```powershell
pwsh -NoProfile -File scripts/package-release.ps1
```

`-Version` を省略すると、`Directory.Build.props` の製品バージョンを使用します。タグ `v1.1.0` をpushすると、GitHub Actionsがzipを生成し、展開後の `GLEM.exe --selftest` に成功した場合だけReleaseへアップロードします。

## 免責事項

計算結果は入力値と解析モデルの仮定に依存します。設計または安全性に関わる判断へ使用する場合は、必ず有資格技術者が内容を確認してください。

## ライセンス

GLEMは [MIT License](LICENSE) の下で公開されています。
