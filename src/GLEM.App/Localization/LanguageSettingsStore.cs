using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GLEM.App.Localization;

/// <summary>
/// 言語設定の永続化ストア。既定の保存先は %LOCALAPPDATA%\GLEM\settings.json。
/// </summary>
public sealed class LanguageSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>既定の保存先パス（%LOCALAPPDATA%\GLEM\settings.json）。</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GLEM", "settings.json");

    private readonly string _path;

    /// <summary>実際に使用される設定ファイルのパス。</summary>
    public string SettingsPath => _path;

    /// <param name="path">設定ファイルのパス。null/空の場合は既定値を使用する（テスト用）。</param>
    public LanguageSettingsStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
    }

    /// <summary>
    /// 設定を読み込む。ファイル欠落・不正な JSON・null ドキュメント・未知の列挙値・通常の IO/アクセスエラー時は
    /// 既定値（<see cref="LanguagePreference.System"/>）を返し、例外は投げない。
    /// </summary>
    public LanguageSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new LanguageSettings();

            var settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(_path), JsonOptions);
            if (settings is null || !Enum.IsDefined(settings.Language))
                return new LanguageSettings();

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // 読み込み失敗は既定値にフォールバックする（FileNotFoundException/DirectoryNotFoundException/PathTooLongException は IOException の派生型）。
            return new LanguageSettings();
        }
    }

    /// <summary>設定を保存する。親ディレクトリが存在しない場合は作成し、UTF-8（BOM なし）で書き出す。失敗時は例外を投げる。</summary>
    public void Save(LanguageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions), new UTF8Encoding(false));
    }
}
