# S2-001 解析実行ボタンのコマンド名バインディング不一致で無反応になる

- 重大度: **S2（重大）** — テスト計画書 §6.1「主要機能が使えないもの（例: 解析実行ボタンが無反応）」に該当
- 発見: 2026-08-23、C-03/C-04 の初回実行時（L2 自動テストのコンパイルエラーとして検出）
- 状態: **修正済み・検証済**

## 症状

S-3（斜面解析設定）・S-5（沈下解析設定）の「Run Analysis」ボタンが押しても何も起きない。

## 原因

CommunityToolkit.Mvvm のソースジェネレータは `async Task RunAsync()` に `[RelayCommand]` を付与した場合、生成されるコマンドプロパティ名から **"Async" サフィックスを除去**する（`RunAsync` → `RunCommand`）。XAML 側が `{Binding RunAsyncCommand}` とバインディングしていたため、実行時にサイレントにバインド失敗していた。

```
SlopeAnalysisSettingsView.xaml: Command="{Binding RunAsyncCommand}"   ← 存在しないプロパティ
SettlementSettingsView.xaml:   Command="{Binding RunAsyncCommand}"   ← 同上
```

## 修正内容

- `src/GLEM.App/Views/SlopeAnalysisSettingsView.xaml`: `{Binding RunCommand}` に変更
- `src/GLEM.App/Views/SettlementSettingsView.xaml`: `{Binding RunCommand}` に変更
- テスト側も `RunCommand`（IAsyncRelayCommand）で実行するよう修正

## 検証

- `C03_MethodSwitching_ProducesValidFsForAllMethods` — 3手法すべてで FS が計算される
- `C04_JanbuControlPoints_ProducesNonCircularResult` — 非円滑動面が計算され結果画面へ遷移する
- `C05_Cancel_StopsSearchAndClearsState` — 実行中キャンセルで停止する
- `C08_SettlementRun_ProducesCurveBreakdownAndT50T90` — 沈下解析が実行され T50/T90 が得られる

（2026-08-23、Release ビルド・全テスト 57/57 パスで確認）
