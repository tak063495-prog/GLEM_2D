# 性能計測記録（T-12）

## 基準（機能仕様書 §7、詳細設計書 §8.2 T-12）

| 項目 | 基準値 | CI/定期実行の失敗条件（テスト計画書 §5.3「閾値2倍超過で失敗」） |
|---|---|---|
| 臨界滑動面探索（標準モデル: 層数≤15・全厚≤50m、Bishop簡化法・自動探索範囲） | **60秒以内** | >120秒 |
| 沈下解析（期間100年・出力50点） | **30秒以内** | >60秒 |

## 実施方法

- テスト: `tests/GLEM.Tests/PerformanceTests.cs` → `StandardModel_WithinTimeBudget`
- 実行: `dotnet test GLEM.sln -c Release --filter "FullyQualifiedName~PerformanceTests"`
- 判定は閾値2倍超過で失敗（CI ランナーの性能ばらつき対策、TRISK-02/PRISK-04）。通常のローカルテストは追跡対象ファイルを書き換えない。記録が必要な場合だけ、下記の環境変数を設定する。

```powershell
$env:GLEM_RECORD_PERF = '1'
$env:GLEM_PERF_OUTPUT = Join-Path $env:TEMP 'GLEM\perf\perf-log.jsonl'
dotnet test GLEM.sln -c Release --filter "FullyQualifiedName~PerformanceTests"
```

CIではランナーの一時ディレクトリへ記録し、`glem-performance-log` artifactとして保存する。

## 記録ファイル

記録モードで指定した `perf-log.jsonl` へ、テスト実行ごとに1行（JSON Lines）を追記する。各エントリのフィールド:

| フィールド | 内容 |
|---|---|
| `TimestampUtc` | 実行時刻 (ISO 8601) |
| `MachineName` / `OsDescription` / `ProcessorCount` / `TotalRamGb` / `Framework` | 環境情報（テスト計画書 §5.3「実測値（環境情報付き）」） |
| `SearchSeconds` | 臨界滑動面探索の実測時間 [s] |
| `SettlementSeconds` | 沈下解析の実測時間 [s] |
| `SlopeFs` / `SettlementTotalMm` | 実行結果の妥当性確認用（FS、総沈下量 [mm]） |

## 履歴サマリ

| 日付 (UTC) | 環境 | 探索 [s] | 沈下 [s] | 判定 |
|---|---|---|---|---|
| 2026-08-23T06:03+09:00 | Windows 10.0.26200 / 12コア / .NET 8.0.30（開発機 RICO） | 7.79 | 0.012 | パス（基準内: <60s / <30s） |

> 過去の基準記録はリポジトリ内の `perf-log.jsonl` を参照。新しい測定は明示的に記録モードを有効にした場合、またはCI artifactにだけ保存される。
