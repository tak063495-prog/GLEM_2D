using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GLEM.App.ViewModels;

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

// LayerRow.ErrorFields にフィールド名が含まれるか（S-2 のセルハイライト用、§5.5）
public sealed class FieldInSetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IReadOnlySet<string> fields && parameter is string field && fields.Contains(field);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
