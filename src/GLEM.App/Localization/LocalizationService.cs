using System.Globalization;
using GLEM.App.Properties;
using GLEM.Core.Models;

namespace GLEM.App.Localization;

/// <summary>
/// 言語設定の解決・適用と、リソース文字列の取得を行う静的サービス。
/// </summary>
public static class LocalizationService
{
    private static readonly CultureInfo English = new("en-US");
    private static readonly CultureInfo Japanese = new("ja-JP");

    /// <summary>
    /// 言語設定から使用するカルチャを解決する。
    /// English は en-US、Japanese は ja-JP を返す。System（および未定義の列挙値）は、
    /// 指定されたシステム UI カルチャ（未指定の場合は現在の UI カルチャ）の二文字 ISO 言語名が
    /// "ja"（大文字小文字を区別しない）の場合に ja-JP を返し、それ以外は en-US を返す。
    /// </summary>
    public static CultureInfo ResolveCulture(LanguagePreference preference, CultureInfo? systemUiCulture = null)
    {
        return preference switch
        {
            LanguagePreference.English => English,
            LanguagePreference.Japanese => Japanese,
            _ => IsJapanese(systemUiCulture ?? CultureInfo.CurrentUICulture) ? Japanese : English
        };
    }

    /// <summary>
    /// 言語設定を解決し、スレッドおよび新規スレッドのカルチャに適用する。
    /// <see cref="AppResources.Culture"/> はクリアされ、リソース参照が CurrentUICulture に従うようになる。
    /// </summary>
    public static CultureInfo Apply(LanguagePreference preference, CultureInfo? systemUiCulture = null)
    {
        var culture = ResolveCulture(preference, systemUiCulture);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // AppResources のカルチャをクリアし、リソース参照が CurrentUICulture に従うようにする。
        AppResources.Culture = null!;

        return culture;
    }

    /// <summary>
    /// 現在の UI カルチャでリソース文字列を取得する。キーが存在しないか空の場合はキー自体を返す。
    /// </summary>
    public static string GetString(string key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key), "Resource key must not be blank.");

        var value = AppResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    /// <summary>
    /// 現在のカルチャでローカライズ済みリソース文字列を整形して返す。
    /// </summary>
    public static string Format(string key, params object?[] args)
    {
        var value = GetString(key);
        return string.Format(CultureInfo.CurrentCulture, value, args ?? Array.Empty<object?>());
    }

    /// <summary>
    /// 斜面解析手法の表示名を、現在の UI カルチャのリソースから取得する。
    /// 未定義の列挙値は <c>method.ToString()</c> にフォールバックする。
    /// </summary>
    public static string GetSlopeMethodDisplay(SlopeMethod method) => method switch
    {
        SlopeMethod.Fellenius => AppResources.SlopeMethod_Fellenius,
        SlopeMethod.BishopSimplified => AppResources.SlopeMethod_BishopSimplified,
        SlopeMethod.JanbuGeneralized => AppResources.SlopeMethod_JanbuGeneralized,
        _ => method.ToString()
    };

    /// <summary>
    /// 排水条件の表示名を、現在の UI カルチャのリソースから取得する。
    /// リソースキーが存在しない（未定義の列挙値）場合は <c>drainage.ToString()</c> にフォールバックする。
    /// </summary>
    public static string GetDrainageDisplay(Drainage drainage)
    {
        var key = $"Drainage_{drainage}";
        var value = AppResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrWhiteSpace(value) ? drainage.ToString() : value;
    }

    private static bool IsJapanese(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase);
}
