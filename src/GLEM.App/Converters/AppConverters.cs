using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GLEM.App.Localization;
using GLEM.App.ViewModels;
using GLEM.Core.Models;

namespace GLEM.App.Converters;

// ActiveScreen と画面名を比較して Visibility を返す（MainWindow のナビゲーション用）
public sealed class ScreenMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Screen screen
        && parameter is string name
        && Enum.TryParse<Screen>(name, out var target)
        && screen == target
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// 検証エラー有無 → 枠線ブラシ（S-2 の地下水位入力用）
public sealed class BoolToBorderConverter : IValueConverter
{
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x30, 0x30));

    private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? ErrorBrush : NormalBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// 排水条件（Drainage）の列挙値をローカライズ済み表示名に変換する（SettlementSettingsView の ComboBox 用）。
// リソースキーのない未定義の列挙値は ToString() にフォールバックする。
public sealed class DrainageDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Drainage drainage ? LocalizationService.GetDrainageDisplay(drainage) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// 斜面解析手法（SlopeMethod）の列挙値をローカライズ済み表示名に変換する（SlopeResultView の結果ヘッダ用）。
// リソースキーのない未定義の列挙値は ToString() にフォールバックする。
public sealed class SlopeMethodDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SlopeMethod method ? LocalizationService.GetSlopeMethodDisplay(method) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// 真偽値をローカライズ済みの「Yes/No」表示に変換する（SlopeResultView の収束フラグ用）。
public sealed class BoolYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? LocalizationService.GetString(b ? "Bool_Yes" : "Bool_No") : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// LayerRow.ErrorFields にフィールド名が含まれるか（S-2 のセルハイライト用、§5.5）
public sealed class FieldInSetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlySet<string> fields && parameter is string field && fields.Contains(field);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
