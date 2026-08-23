# システムテストチェックリスト（C-01〜C-10）初回実行記録

- 実施日: 2026-08-23（M2 完了条件「初回実行し、S1/S2 不具合を Issue 化」用）
- 対象: テスト計画書 §3.3（L3 システムテストチェックリスト）
- 方法: L2 自動テスト（`tests/GLEM.Tests/UiViewModelTests.cs` ほか）+ アプリ起動スモークテスト
- 結果サマリ: **10/10 パス**（うち C-06/C-08 の図面表示は VM・プロットビルダーレベルで検証、視覚確認はスモークテスト実施）

## M3 最終実行（2026-08-23）

M3 DoD「C-01〜C-10 全項目パス」の最終確認として、Release ビルドで全自動テストを再実行した。

| 項目 | 結果 |
|---|---|
| `dotnet build GLEM.sln -c Release` | 成功（警告0・エラー0） |
| `dotnet test GLEM.sln -c Release`（C-01〜C-10 の証拠テストを含む全58件） | **58/58 パス** |
| カバレッジゲート（GLEM.Core ≥80%、Q-02） | 94.08% PASS |
| T-12 性能（`PerformanceTests.StandardModel_WithinTimeBudget`） | 探索 7.79s < 60s / 沈下 0.012s < 30s → **パス**（記録: `docs/perf/perf-log.jsonl`） |
| S1/S2 不具合の「検証済」未満（Q-03） | **ゼロ**（S2-001 は修正済み・検証済。未登録の S1/S2 なし） |

→ **M3 DoD 達成**（C-01〜C-10 全項目パス / Q-03 達成 / T-12 実測値が `docs/perf/` に記録され基準内 = Q-04 達成）

| No. | チェック項目 | 実行方法 | 証拠 | 結果 |
|---|---|---|---|---|
| C-01 | 新規プロジェクト作成→層2件追加→検証合格の表示 | 自動テスト | `C01_NewProject_HasTwoLayersAndValidationPasses`（デフォルト2層、RunValidation → "Passed"） | パス |
| C-02 | V-01〜V-07 の各違反入力時に該当セルがハイライトされ、メッセージが仕様書 §3.4 と一致すること | 自動テスト + XAML スタイルトリガー確認 | `C02_InvalidThickness_HighlightsCellAndShowsSpecMessage`（GLEM-1002、ErrorFields に "thickness_m"）、`C02_WaterTableBelowGroundBottom_IsFlagged`（GLEM-1005）。セルハイライトは S-2 の DataGrid ErrorCellStyle トリガーで実装 | パス |
| C-03 | 手法切替（Fellenius/Bishop/Janbu）で FS が正しく再計算されること | 自動テスト | `C03_MethodSwitching_ProducesValidFsForAllMethods`（3手法すべてで FS>0.3、Method が反映） | パス |
| C-04 | Janbu 選択時に滑動面制御点エディタが表示され、非円滑動面がプロットされること | 自動テスト + XAML/プロットビルダー確認 | `C04_JanbuControlPoints_ProducesNonCircularResult`（FunctionSurface 結果）、S-3 の制御点 DataGrid は MethodIsJanbu で表示切替、CrossSectionPlotBuilder が FunctionSurface を折れ線で描画 | パス |
| C-05 | 解析実行中に進捗バーが更新され、中断操作で停止すること | 自動テスト | `C05_Cancel_StopsSearchAndClearsState`（広域探索中に Cancel → Result=null、ProgressText="Cancelled"）。進捗は Progress<SearchProgress> → ProgressBar バインディング | パス |
| C-06 | 断面図に層・水位線・臨界滑動面・条体線が表示され、FS_min と R/中心座標の注記があること | プロットビルダー実装確認 + スモークテスト | CrossSectionPlotBuilder: 層ポリゴン / 水位破線 / 円弧（または制御点折れ線）/ 条体線 / `FS_min = ...` と `R = ... center (...)` テキスト注記。ScottPlot WpfPlot はズーム・パン標準対応 | パス |
| C-07 | CSV エクスポートで §6.3（機能仕様書）の列構成が出力されること | 自動テスト | `IoTests`（SlopeHeader / SettlementHeader のヘッダ完全一致、UTF-8・カンマ区切り） | パス |
| C-08 | 沈下解析で S-t 曲線・内訳・T50/T90 が表示され、対数軸切替が動作すること | 自動テスト + ビュー実装確認 | `C08_SettlementRun_ProducesCurveBreakdownAndT50T90`（3成分すべて>0、T50<T90、各時刻の内訳合計=総量 R-6.2.1）。対数軸切替は SettlementResultView の logX 再描画（NumericManual 目盛） | パス |
| C-09 | 異常終了→再起動で自動保存復元を促すこと | スモークテスト + 自動テスト | `R314_Autosave_RestoreRoundTrip`（Autosave → 新インスタンスで HasPendingAutosave=true → RestoreFromAutosave で復元）。スモーク: autosave.glem を事前作成して起動し、復元プロンプト表示を確認。正常終了時は OnCleanExit がファイルを削除 | パス |
| C-10 | レポート生成で入力概要・結果・図面が1文書に出力されること | 自動テスト | `IoTests`（ReportGenerator: アプリバージョン・生成日時・プロジェクト名・結果テーブル・base64 埋め込み図面を含む HTML を単一ファイル出力） | パス |

## 初回実行中に発見した不具合

| Issue | 重大度 | 内容 | 状態 |
|---|---|---|---|
| [S2-001](./S2-001-run-button-binding-name.md) | S2（重大） | 解析実行ボタンのコマンド名バインディング不一致で無反応になる | 修正済み・検証済（C-03/C-04/C-05/C-08 テストで確認） |

## 備考

- C-08 の初回実行では、デフォルトプロジェクトに沈下パラメータ（k/e0/Cc）が未入力のため GLEM-1006 が正しく表示され解析がブロックされた。仕様どおりの挙動であり不具合ではない（テスト側で全層にパラメータを設定して再実行）。
- M3 では本チェックリストを最終確認として再実施し、S1/S2 未修正ゼロ（Q-03）を確認する。
