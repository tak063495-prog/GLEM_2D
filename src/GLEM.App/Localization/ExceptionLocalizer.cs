using GLEM.Core;

namespace GLEM.App.Localization;

/// <summary>
/// GlemException（GLEM-2xxx/3xxx）の UI 表示用ローカライズを行う静的ヘルパー。
/// Core のコード・メッセージ・シリアライズは変更せず、表示文字列のみを置き換える。
/// </summary>
public static class ExceptionLocalizer
{
    /// <summary>
    /// [Code] + ローカライズ済みメッセージの形式で例外を整形する。
    /// 未定義のコードは "[Code] 元のメッセージ" にフォールバックする。
    /// </summary>
    public static string Format(GlemException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return $"[{ex.Code}] {GetMessage(ex)}";
    }

    /// <summary>
    /// コードからローカライズ済みメッセージを返す。未定義のコードは元のメッセージにフォールバックする。
    /// </summary>
    public static string GetMessage(GlemException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var key = ex.Code switch
        {
            "GLEM-2001" => "Exception_GLEM2001",
            "GLEM-2002" => "Exception_GLEM2002",
            "GLEM-2003" => "Exception_GLEM2003",
            "GLEM-3001" => "Exception_GLEM3001",
            "GLEM-3002" => "Exception_GLEM3002",
            _ => null
        };

        return key is null ? ex.Message : LocalizationService.GetString(key);
    }
}
