# GLEM L4 受入判定書

| 項目 | 内容 |
|---|---|
| 文書ID | GLEM-L4-ACC-001 |
| バージョン | 1.0.0 |
| 日付 | 2026-08-23 |
| 対象 | GLEM（Generalized Limit Equilibrium Method）斜面安定解析・沈下予測ソフトウェア v1.0.0 |
| 判定根拠 | テスト計画書 §1.3 の Q-01〜Q-04、実装計画書 M4 DoD |

## 1. 目的と範囲

本ソフトウェアが機能仕様書・基本設計書で定義した受入基準（Q-01〜Q-04）を満たすかを検証し、v1.0.0 のリリース可否を判定する。

## 2. 受入基準の検証結果

### Q-01: A-1〜A-6 がすべて達成されていること — **合格**

| 基準 | 内容（機能仕様書 §8/§9） | 証拠（テスト計画書 §8.2、全パス） |
|---|---|---|
| A-1 | 標準検証ケースで FS の誤差 ±0.01 以内 | `T01_Fellenius_MatchesHandCalculation`、`T02_Bishop_ConvergesAndSatisfiesFixedPoint`、`T03a〜T03e` と `ReferenceCaseTests`。T03の λc は公開Janbu補正の考え方を参考にしたGLEM固有式の固定値回帰であり、完全なJanbu一般化法との照合ではない |
| A-2 | 全テストモデルで最大反復回数以内に収束 | `T02_Bishop_ConvergesAndSatisfiesFixedPoint`（固定点条件の検証を含む） |
| A-3 | ru=0 と静水圧条件の両方で FS が正しく変化（水位上昇で低下） | `T04_PoreWaterPressure_DecreasesSafetyFactor` |
| A-4 | 単一層圧密問題で理論解との沈下量誤差 ±2% 以内 | `T07a_PrimaryConsolidation_MatchesHandCalculation`、`T07b_ImmediateSettlement_MatchesAnalyticalInfluenceFactor`、`T07c_ImmediateSettlement_IsLinearInLoad` |
| A-5 | U-Tv 関係が近似式と一致し Tv=0.197 で U≈50%（±1%） | `T06a_ConsolidationRatio_AtTv0197_IsApproximately50Percent`、`T06b_ConsolidationRatio_MatchesExactSeries`、`T06c_ConsolidationRatio_ContinuousAtBranchBoundary` |
| A-6 | 二重排水で到達時刻が約 1/4 に短縮 | `T08_DoubleDrainage_QuartersTimeToConsolidation` |

補助: `T05_SeismicCoefficient_DecreasesSafetyFactor`（地震係数の影響）、`DiscretizeCircle_ProducesValidGeometry` / `DiscretizeFunction_ProducesValidSlices`（条体分割の妥当性）。

**総合**: 全テストスイート **59/59 パス**（2026-08-23、Release、.NET 8.0.30）。

### Q-02: GLEM.Core のカバレッジが 80% 以上 — **合格**

- 計測: `dotnet test --collect:"XPlat Code Coverage"` + `scripts\coverage-gate.ps1`
- 結果: **GLEM.Core line coverage = 94.09%**（891/947）→ 閾値 80% を超過、ゲート **PASS**

### Q-03: S1/S2 の不具合が「検証済」未満で残っていないこと — **合格**

| Issue | 内容 | 状態 |
|---|---|---|
| S2-001 | Run ボタンバインディング名不一致（CT.MVV が `RunAsync`→`RunCommand` を生成） | 修正済・検証済（XAML 2箇所+テスト2箇所修正、C-03/C-04/C-05 で再確認） |
| S2-002 | MainWindow の画面切替バインディングが Window.ActiveScreen を参照し全画面が重なる | 修正済・検証済（`DataContext.ActiveScreen` に修正、スクリーンショットで各画面の独立表示を確認） |
| S2-003 | 設定系 UserControl にコードビハインド欠如で白紙画面 | 修正済・検証済（.xaml.cs 追加、スモークテスト+スクリーンショットで内容表示を確認） |

`docs/issues/` に「検証済」未満の S1/S2 は **0 件**。S1 は未発生。

### Q-04: T-12 が基準内であり `docs/perf/` に記録されていること — **合格**

| 項目 | 実測（2026-08-23、開発機） | 基準（機能仕様書 §7） | 判定 |
|---|---|---|---|
| 臨界滑動面探索（標準モデル: 15層・全厚50m、Bishop簡化法・自動探索） | **7.79 s** | 60秒以内 | 合格 |
| 沈下解析（期間100年・出力50点） | **0.012 s** | 30秒以内 | 合格 |

記録: `docs/perf/perf-log.jsonl`（環境情報付き: Windows 10.0.26200 / 12コア / .NET 8.0.30）、基準と履歴は `docs/perf/README.md`。テスト実装: `tests/GLEM.Tests/PerformanceTests.cs::StandardModel_WithinTimeBudget`（閾値超過で失敗するハードゲート）。

## 3. M4 DoD の確認

| DoD | 証拠 | 判定 |
|---|---|---|
| ユーザーマニュアルに「[要記入]」が残っていないこと | `docs/GLEM_ユーザーマニュアル.md` を grep して該当 **0 件**（全節を実 UI に基づき補完、画面キャプチャ `docs/manual/screenshots/` 5枚を参照） | 合格 |
| 配布 exe がクリーン環境で起動し C-01〜C-03 を再確認できること | `%LOCALAPPDATA%\GLEM` を削除した状態で `dist/GLEM-1.0.0-win-x64/GLEM.exe` を起動 → メイン画面表示まで **2.4 s**（機能仕様書 §7 の 10秒以内を充足）。続けて `--selftest` 実行: `[PASS] C-01 new project -> 2 layers -> validation passed` / `[PASS] C-02 invalid input -> cell highlighted + spec message` / `[PASS] C-03 method switching -> FS recomputed (Fellenius/Bishop/Janbu)`、終了コード **0** | 合格 |
| L4 受入の書面承認が得られていること（Q-01〜Q-04） | 本文書 §2・§5 の承認欄 | 下記のとおり |

## 4. 配布物

| 項目 | 内容 |
|---|---|
| パッケージ | `dist/GLEM-1.0.0-win-x64/`（単一 exe + ネイティブ DLL、自己完結型 .NET 8 ランタイム同梱） |
| メインバイナリ | `GLEM.exe`（`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` で生成、詳細設計書 §9 に従う） |
| 動作要件 | Windows x64（.NET ランタイムの事前インストール不要） |

## 5. 承認

| 役割 | 氏名/署名 | 日付 | 判定 |
|---|---|---|---|
| プロジェクトオーナー（受入責任者） | ____________________ | 2026-08-23 | **合格 — v1.0.0 リリース承認** |

> 判定: Q-01〜Q-04 の全基準が証拠付きで充足され、M4 DoD を満たすため、GLEM v1.0.0 をリリースする。
