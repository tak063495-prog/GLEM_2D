# S2-002 画面ナビゲーションが機能せず全画面が重なる（Visibility バインディング誤り）

- 重大度: **S2（重大）** — テスト計画書 §6.1「主要機能が使えないもの」に該当（ナビゲーションが無反応に見える）
- 発見: 2026-08-23、M4 の画面キャプチャ作業中（`--capture` モードで全画面が同一画像になることを確認）
- 状態: **修正済み・検証済**

## 症状

S-1 のナビゲーションボタンを押しても画面が切り替わらず、常に複数の画面が重なって表示される。

## 原因

MainWindow.xaml でサブビューの Visibility を以下のようにバインディングしていた:

```xml
Visibility="{Binding ActiveScreen, RelativeSource={RelativeSource AncestorType=Window}, ...}"
```

`RelativeSource AncestorType=Window` は **Window オブジェクト自身**のプロパティを参照するが、`MainWindow` に `ActiveScreen` プロパティは存在しない（VM 側にある）。バインディングが解決できずデフォルト値 `Visibility.Visible` が採用され、5 つのサブビューが常に Visible のまま重なっていた。

## 修正内容

Window の DataContext（MainViewModel）経由で参照するよう変更:

```xml
Visibility="{Binding DataContext.ActiveScreen, RelativeSource={RelativeSource AncestorType=Window}, ...}"
```

`src/GLEM.App/Views/MainWindow.xaml` の 5 箇所のバインディングを修正。

## 検証

- `--capture` モードで S-2〜S-6 を順にキャプチャし、各画面の Visibility が排他的（1 つのみ Visible）であることを視覚ツリー検査で確認
- 5 枚のスクリーンショットが互いに異なる内容（MD5 相違・内容密度差）として生成されることを確認
