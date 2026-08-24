namespace GLEM.App.Localization;

/// <summary>
/// ユーザーの言語設定。既定値は <see cref="LanguagePreference.System"/>（OS の言語に従う）。
/// </summary>
public sealed record LanguageSettings(LanguagePreference Language = LanguagePreference.System);
