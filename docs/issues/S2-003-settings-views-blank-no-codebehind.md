# S2-003 設定画面（S-3/S-5）が白紙で表示される（InitializeComponent 未呼び出し）

- 重大度: **S2（重大）** — テスト計画書 §6.1「主要機能が使えないもの」に該当（解析実行ボタンが表示されない）
- 発見: 2026-08-23、M4 の画面キャプチャ作業中（S-3/S-5 が白紙 PNG として生成されることを確認）
- 状態: **修正済み・検証済**

## 症状

「Slope - Settings (S-3)」「Settlement - Settings (S-5)」の画面が完全に白紙で、入力項目も実行ボタンも表示されない。

## 原因

`SlopeAnalysisSettingsView.xaml` と `SettlementSettingsView.xaml` に **コードビハインド（.xaml.cs）が存在しなかった**。WPF では XAML のみから生成されるクラスのデフォルトコンストラクタは BAML をロードせず、`InitializeComponent()` は明示的に呼び出さないと実行されない。そのため `UserControl.Content` が null のままとなり、テンプレートの ContentPresenter が何も描画しなかった。

（他の 3 つの View — GroundModelEditorView / SlopeResultView / SettlementResultView — はコードビハインドが存在したため正常に表示されていた。）

## 修正内容

両 View にコンストラクタで `InitializeComponent()` を呼び出すコードビハインドを追加:

- `src/GLEM.App/Views/SlopeAnalysisSettingsView.xaml.cs`（新規）
- `src/GLEM.App/Views/SettlementSettingsView.xaml.cs`（新規）

## 検証

- spcheck（参照プロジェクト）で各 View の `Content` を構築直後に検査し、修正前は null / 修正後はルート要素が設定されることを確認
- `--capture` モードで S-3/S-5 にフォーム内容（密度 >0%）が描画されることを確認
