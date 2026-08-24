using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace GLEM.Tests;

public sealed class P1QualityTests
{
    [Fact]
    public void MainWindow_DefinesRequiredFileKeyboardShortcuts()
    {
        var document = XDocument.Load(RepoPath("src", "GLEM.App", "Views", "MainWindow.xaml"));
        var bindings = document.Descendants()
            .Where(e => e.Name.LocalName == "KeyBinding")
            .Select(e => ($"{e.Attribute("Modifiers")?.Value}+{e.Attribute("Key")?.Value}").ToUpperInvariant())
            .ToHashSet();

        bindings.Should().Contain("CONTROL+N");
        bindings.Should().Contain("CONTROL+O");
        bindings.Should().Contain("CONTROL+S");
        bindings.Should().Contain("CONTROL+SHIFT+S");
    }

    [Fact]
    public void MajorViews_ExposeAutomationNamesAndExplicitTabOrder()
    {
        var files = new[]
        {
            "MainWindow.xaml",
            "GroundModelEditorView.xaml",
            "SlopeAnalysisSettingsView.xaml",
            "SlopeResultView.xaml",
            "SettlementSettingsView.xaml",
            "SettlementResultView.xaml"
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(RepoPath("src", "GLEM.App", "Views", file));
            text.Should().Contain("AutomationProperties.", $"{file} must expose screen-reader metadata");
            text.Should().Contain("KeyboardNavigation.TabIndex", $"{file} must define a stable tab order");
        }
    }

    [Fact]
    public void Plots_UsePatternsAndHighContrastFallbacksInsteadOfColorOnly()
    {
        var crossSection = File.ReadAllText(RepoPath("src", "GLEM.App", "Plots", "CrossSectionPlotBuilder.cs"));
        var settlement = File.ReadAllText(RepoPath("src", "GLEM.App", "Plots", "SettlementPlotBuilder.cs"));

        crossSection.Should().Contain("SystemParameters.HighContrast");
        settlement.Should().Contain("SystemParameters.HighContrast");
        crossSection.Should().Contain("LinePattern.DenselyDashed");
        crossSection.Should().Contain("plt.Add.Text(layer.Name");
        settlement.Should().Contain("LinePattern.Solid");
        settlement.Should().Contain("LinePattern.Dotted");
        settlement.Should().Contain("LinePattern.Dashed");
        settlement.Should().Contain("LinePattern.DenselyDashed");
    }

    [Fact]
    public void ApplicationIcon_IsConfiguredAndContainsMultiResolutionFrames()
    {
        var project = File.ReadAllText(RepoPath("src", "GLEM.App", "GLEM.App.csproj"));
        project.Should().Contain("<ApplicationIcon>Assets\\GLEM.ico</ApplicationIcon>");

        var icon = File.ReadAllBytes(RepoPath("src", "GLEM.App", "Assets", "GLEM.ico"));
        icon.Length.Should().BeGreaterThan(1_000);
        BitConverter.ToUInt16(icon, 0).Should().Be(0);
        BitConverter.ToUInt16(icon, 2).Should().Be(1);
        BitConverter.ToUInt16(icon, 4).Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void ReleaseAutomation_ProvidesInstallerSigningChecksumsSbomAndSelfTest()
    {
        var workflow = File.ReadAllText(RepoPath(".github", "workflows", "release.yml"));
        var package = File.ReadAllText(RepoPath("scripts", "package-release.ps1"));
        var verifier = File.ReadAllText(RepoPath("scripts", "verify-release.ps1"));
        var installer = File.ReadAllText(RepoPath("installer", "GLEM.iss"));
        var codeql = File.ReadAllText(RepoPath(".github", "workflows", "codeql.yml"));
        var dependabot = File.ReadAllText(RepoPath(".github", "dependabot.yml"));

        workflow.Should().Contain("WINDOWS_CERTIFICATE_BASE64");
        workflow.Should().Contain("verify /pa");
        workflow.Should().Contain("Create final SHA-256 checksums");
        package.Should().Contain("specVersion='1.5'");
        verifier.Should().Contain("--selftest");
        verifier.Should().Contain("README.ja.md");
        installer.Should().Contain("GLEMFile\\shell\\open\\command");
        installer.Should().Contain("recursesubdirs createallsubdirs");
        codeql.Should().Contain("github/codeql-action/analyze@v3");
        dependabot.Should().Contain("package-ecosystem: nuget");
        dependabot.Should().Contain("package-ecosystem: github-actions");
    }

    [Fact]
    public void MethodDocumentation_IsBilingualAndDisclosesJanbuApproximation()
    {
        var english = File.ReadAllText(RepoPath("docs", "METHODS.md"));
        var japanese = File.ReadAllText(RepoPath("docs", "METHODS.ja.md"));

        english.Should().Contain("not a full reproduction of Janbu's general procedure");
        english.Should().Contain("λc = min(2.0");
        japanese.Should().Contain("完全なヤンブ一般法の再現ではありません");
        japanese.Should().Contain("λc = min(2.0");
    }

    private static string RepoPath(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(new[] { root }.Concat(parts).ToArray());
    }
}
