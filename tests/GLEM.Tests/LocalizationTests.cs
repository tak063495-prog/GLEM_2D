using System.Collections;
using System.Globalization;
using System.Resources;
using System.Xml.Linq;
using FluentAssertions;
using GLEM.App.Localization;
using GLEM.App.Properties;
using GLEM.App.ViewModels;
using GLEM.Core;
using Xunit;

namespace GLEM.Tests;

// P0 localization regression suite: language preference resolution, settings persistence,
// resource parity, localized UI strings, and exception message localization.
public sealed class LocalizationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"glem-loc-{Guid.NewGuid():N}");

    public LocalizationTests() => Directory.CreateDirectory(_dir);

    // --- LanguagePreference resolution (LocalizationService) ---

    [Fact]
    public void ResolveCulture_ExplicitEnglish_AlwaysResolvesToEnUs()
    {
        foreach (var systemUi in new[] { "ja-JP", "en-US", "de-DE" })
        {
            LocalizationService.ResolveCulture(LanguagePreference.English, new CultureInfo(systemUi))
                .Name.Should().Be("en-US", $"regardless of the system UI culture ({systemUi})");
        }
    }

    [Fact]
    public void ResolveCulture_ExplicitJapanese_AlwaysResolvesToJaJp()
    {
        foreach (var systemUi in new[] { "ja-JP", "en-US", "de-DE" })
        {
            LocalizationService.ResolveCulture(LanguagePreference.Japanese, new CultureInfo(systemUi))
                .Name.Should().Be("ja-JP", $"regardless of the system UI culture ({systemUi})");
        }
    }

    [Theory]
    [InlineData("ja-JP")]
    [InlineData("ja")]
    public void ResolveCulture_System_JapaneseSystemUi_ResolvesToJaJp(string systemUi) =>
        LocalizationService.ResolveCulture(LanguagePreference.System, new CultureInfo(systemUi))
            .Name.Should().Be("ja-JP");

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void ResolveCulture_System_NonJapaneseSystemUi_ResolvesToEnUs(string systemUi) =>
        LocalizationService.ResolveCulture(LanguagePreference.System, new CultureInfo(systemUi))
            .Name.Should().Be("en-US");

    [Fact]
    public void ResolveCulture_System_NullSystemUi_FollowsCurrentUICulture()
    {
        using (new CultureScope(new CultureInfo("ja-JP")))
        {
            LocalizationService.ResolveCulture(LanguagePreference.System).Name.Should().Be("ja-JP");
        }

        using (new CultureScope(new CultureInfo("en-US")))
        {
            LocalizationService.ResolveCulture(LanguagePreference.System).Name.Should().Be("en-US");
        }
    }

    [Fact]
    public void Apply_SetsThreadAndDefaultThreadCultures()
    {
        // Start from a Japanese host culture; applying English must switch all four culture slots.
        using var _ = new CultureScope(new CultureInfo("ja-JP"));

        var culture = LocalizationService.Apply(LanguagePreference.English);

        culture.Name.Should().Be("en-US");
        CultureInfo.CurrentCulture.Name.Should().Be("en-US");
        CultureInfo.CurrentUICulture.Name.Should().Be("en-US");
        CultureInfo.DefaultThreadCurrentCulture!.Name.Should().Be("en-US");
        CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be("en-US");
    }

    // --- LanguageSettingsStore persistence (unique temp paths, no %LOCALAPPDATA% access) ---

    [Fact]
    public void Store_MissingFile_ReturnsSystemDefault()
    {
        var store = new LanguageSettingsStore(Path.Combine(_dir, "missing", "settings.json"));

        var settings = store.Load();

        settings.Language.Should().Be(LanguagePreference.System);
        File.Exists(store.SettingsPath).Should().BeFalse();
    }

    [Theory]
    [InlineData(LanguagePreference.English)]
    [InlineData(LanguagePreference.Japanese)]
    public void Store_SaveLoad_RoundTripsExplicitLanguage(LanguagePreference preference)
    {
        var store = new LanguageSettingsStore(Path.Combine(_dir, $"roundtrip-{preference}.json"));

        store.Save(new LanguageSettings(preference));

        store.Load().Language.Should().Be(preference);
    }

    [Fact]
    public void Store_Save_WritesUtf8WithoutBomAndStableEnumWireValue()
    {
        // Nested path also verifies that Save creates missing parent directories.
        var path = Path.Combine(_dir, "nested", "settings.json");
        new LanguageSettingsStore(path).Save(new LanguageSettings(LanguagePreference.English));

        var bytes = File.ReadAllBytes(path);
        (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            .Should().BeFalse("settings.json must be UTF-8 without BOM");

        // Stable ASCII enum wire value (not a numeric or localized form).
        File.ReadAllText(path).Should().Contain("\"Language\": \"English\"");
    }

    [Fact]
    public void Store_CorruptJson_FallsBackToSystem()
    {
        var path = Path.Combine(_dir, "corrupt.json");
        File.WriteAllText(path, "{ invalid json ");

        new LanguageSettingsStore(path).Load().Language.Should().Be(LanguagePreference.System);
    }

    [Fact]
    public void Store_UnknownEnumString_FallsBackToSystem()
    {
        var path = Path.Combine(_dir, "unknown-enum.json");
        File.WriteAllText(path, """{"Language":"Klingon"}""");

        new LanguageSettingsStore(path).Load().Language.Should().Be(LanguagePreference.System);
    }

    [Fact]
    public void Store_UndefinedNumericEnum_FallsBackToSystem()
    {
        var path = Path.Combine(_dir, "numeric-enum.json");
        File.WriteAllText(path, """{"Language":99}""");

        new LanguageSettingsStore(path).Load().Language.Should().Be(LanguagePreference.System);
    }

    // --- Resource strings and EN/JA key parity ---

    [Theory]
    [InlineData("Status_Ready", "en-US", "Ready")]
    [InlineData("Status_Ready", "ja-JP", "準備完了")]
    [InlineData("Validation_NoIssues", "en-US", "✓ Passed: no validation issues")]
    [InlineData("Validation_NoIssues", "ja-JP", "✓ 合格：検証上の問題はありません")]
    [InlineData("Default_ProjectName", "en-US", "Untitled")]
    [InlineData("Default_ProjectName", "ja-JP", "無題")]
    [InlineData("SlopeAnalysis_Cancelled", "en-US", "Cancelled")]
    [InlineData("SlopeAnalysis_Cancelled", "ja-JP", "キャンセルしました")]
    public void Resources_RepresentativeStrings_MatchExpectedValues(string key, string cultureName, string expected) =>
        AppResources.ResourceManager.GetString(key, new CultureInfo(cultureName)).Should().Be(expected);

    [Fact]
    public void Resources_EnAndJa_HaveCompleteKeyParity()
    {
        var enKeys = ResourceKeyNames(CultureInfo.InvariantCulture); // neutral (EN) resources
        var jaKeys = ResourceKeyNames(new CultureInfo("ja"));        // JA satellite

        enKeys.Should().NotBeEmpty();
        jaKeys.Except(enKeys).Should().BeEmpty("every JA resource key must exist in EN");
        enKeys.Except(jaKeys).Should().BeEmpty("every EN resource key must exist in JA");
    }

    [Fact]
    public void Resources_ResxSources_HaveNoDuplicateKeys()
    {
        // Locate the resx sources relative to the test output directory (repo layout: tests/GLEM.Tests/bin/<config>/<tfm>).
        var propertiesDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "GLEM.App", "Properties"));
        var enResx = Path.Combine(propertiesDir, "AppResources.resx");
        var jaResx = Path.Combine(propertiesDir, "AppResources.ja.resx");

        if (!File.Exists(enResx) || !File.Exists(jaResx))
        {
            return; // layout not available (e.g. copied output dir); embedded-resource parity is still covered above
        }

        foreach (var resx in new[] { enResx, jaResx })
        {
            var names = XDocument.Load(resx).Descendants("data").Select(d => d.Attribute("name")!.Value).ToList();
            names.Should().NotBeEmpty($"{Path.GetFileName(resx)} must define resource keys");
            names.Distinct().Count().Should().Be(names.Count, $"{Path.GetFileName(resx)} has no duplicate keys");
        }
    }

    private static HashSet<string> ResourceKeyNames(CultureInfo culture)
    {
        var rm = new ResourceManager("GLEM.App.Properties.AppResources", typeof(AppResources).Assembly);
        var set = rm.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"Embedded resource set not found for culture '{culture.Name}'.");

        using (set)
        {
            var names = new HashSet<string>();
            foreach (DictionaryEntry entry in set)
            {
                if (entry.Key is string key)
                {
                    names.Add(key);
                }
            }

            return names;
        }
    }

    // --- Localized view-model behavior under both languages ---

    [Fact]
    public void ViewModel_UnderEnglish_DefaultProjectValidationAndLayerNames_AreLocalized()
    {
        using var _ = new CultureScope(new CultureInfo("en-US"));

        var vm = new MainViewModel();

        vm.Project.ProjectName.Should().Be("Untitled");
        vm.GroundModelEditor.Layers.Select(l => l.Name).Should().Equal("TopSoil", "Clay");

        vm.GroundModelEditor.AddLayerCommand.Execute(null);
        vm.GroundModelEditor.Layers[^1].Name.Should().Be("Layer 3");

        vm.GroundModelEditor.RunValidationCommand.Execute(null);
        vm.GroundModelEditor.HasValidationErrors.Should().BeFalse();
        vm.GroundModelEditor.ValidationSummary.Should().Be("✓ Passed: no validation issues");
    }

    [Fact]
    public void ViewModel_UnderJapanese_DefaultProjectValidationAndLayerNames_AreLocalized()
    {
        using var _ = new CultureScope(new CultureInfo("ja-JP"));

        var vm = new MainViewModel();

        vm.Project.ProjectName.Should().Be("無題");
        vm.GroundModelEditor.Layers.Select(l => l.Name).Should().Equal("表層土", "粘土");

        vm.GroundModelEditor.AddLayerCommand.Execute(null);
        vm.GroundModelEditor.Layers[^1].Name.Should().Be("層 3");

        vm.GroundModelEditor.RunValidationCommand.Execute(null);
        vm.GroundModelEditor.HasValidationErrors.Should().BeFalse();
        vm.GroundModelEditor.ValidationSummary.Should().Be("✓ 合格：検証上の問題はありません");
    }

    [Fact]
    public void ViewModel_UnderEnglish_InvalidThickness_SummaryIsLocalized()
    {
        using var _ = new CultureScope(new CultureInfo("en-US"));

        var vm = new MainViewModel();
        vm.GroundModelEditor.Layers[0].ThicknessM = -5.0;
        vm.GroundModelEditor.RunValidationCommand.Execute(null);

        vm.GroundModelEditor.HasValidationErrors.Should().BeTrue();
        vm.GroundModelEditor.LastIssues.Should().Contain(i => i.Code == "GLEM-1002");
        vm.GroundModelEditor.ValidationSummary.Should()
            .Contain("[GLEM-1002]")
            .And.Contain("The thickness of a ground layer must be a value greater than 0");
    }

    [Fact]
    public void ViewModel_UnderJapanese_InvalidThickness_SummaryIsLocalized()
    {
        using var _ = new CultureScope(new CultureInfo("ja-JP"));

        var vm = new MainViewModel();
        vm.GroundModelEditor.Layers[0].ThicknessM = -5.0;
        vm.GroundModelEditor.RunValidationCommand.Execute(null);

        vm.GroundModelEditor.HasValidationErrors.Should().BeTrue();
        vm.GroundModelEditor.LastIssues.Should().Contain(i => i.Code == "GLEM-1002");
        vm.GroundModelEditor.ValidationSummary.Should()
            .Contain("[GLEM-1002]")
            .And.Contain("地盤層の厚さは、0 より大きい値で指定してください。");
    }

    // --- ExceptionLocalizer (GlemException display messages) ---

    [Theory]
    [InlineData("en-US", "[GLEM-3001] This project was saved by a newer version of GLEM. Do you want to load it anyway?")]
    [InlineData("ja-JP", "[GLEM-3001] このプロジェクトは新しいバージョンの GLEM で保存されています。それでも読み込みますか？")]
    public void ExceptionLocalizer_KnownFileCode_PreservesCodeAndSwitchesLanguage(string cultureName, string expected)
    {
        using var _ = new CultureScope(new CultureInfo(cultureName));

        var ex = new ProjectFileException("GLEM-3001", "original core message");

        ExceptionLocalizer.Format(ex).Should().Be(expected);
        ExceptionLocalizer.GetMessage(ex).Should().NotBe("original core message"); // localized, not the raw Core message
    }

    [Theory]
    [InlineData("en-US", "[GLEM-2001] An invalid analysis method, depth, or layer was specified for this ground model.")]
    [InlineData("ja-JP", "[GLEM-2001] この地盤モデルに対して無効な解析手法・深さ・層が指定されています。")]
    public void ExceptionLocalizer_KnownEngineCode_PreservesCodeAndSwitchesLanguage(string cultureName, string expected)
    {
        using var _ = new CultureScope(new CultureInfo(cultureName));

        var ex = new EngineException("GLEM-2001", "original core message");

        ExceptionLocalizer.Format(ex).Should().Be(expected);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void ExceptionLocalizer_UnknownCode_FallsBackToOriginalMessage(string cultureName)
    {
        using var _ = new CultureScope(new CultureInfo(cultureName));

        const string original = "custom core message";
        var ex = new EngineException("GLEM-9999", original);

        ExceptionLocalizer.Format(ex).Should().Be($"[GLEM-9999] {original}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
