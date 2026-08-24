using System.Windows.Markup;

namespace GLEM.App.Localization;

/// <summary>
/// XAML からローカライズ済み文字列を参照するためのマークアップ拡張。
/// 例: <c>&lt;TextBlock Text="{localization:Loc Key='App.Title'}" /&gt;</c>
/// 起動時の静的ローカライズであり、言語変更は再起動後に反映されるため動的な更新は行わない。
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    /// <summary>
    /// XAML ツール（デザイナ等）がパラメータなしコンストラクタでインスタンス化する際に使用される。
    /// </summary>
    public LocExtension()
    {
        Key = string.Empty;
    }

    /// <summary>
    /// リソースキーを指定して初期化する。
    /// </summary>
    /// <param name="key">取得するリソースキー。</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> が <see langword="null" /> の場合。</exception>
    public LocExtension(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// 取得するリソースキー。
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 現在の UI カルチャで <see cref="Key"/> に対応するリソース文字列を返す。
    /// </summary>
    /// <param name="serviceProvider">マークアップ拡張のサービスプロバイダ。</param>
    public override object ProvideValue(IServiceProvider serviceProvider) => LocalizationService.GetString(Key);
}
