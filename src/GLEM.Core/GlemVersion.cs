using System.Reflection;

namespace GLEM.Core;

/// <summary>
/// Exposes the current GLEM product version at runtime.
/// The value is derived from this assembly's informational version, which is set
/// centrally in Directory.Build.props (single source of truth), so no literal
/// version string is duplicated here.
/// </summary>
public static class GlemVersion
{
    /// <summary>Last-resort fallback if neither version attribute is available.</summary>
    private const string Fallback = "1.0.0";

    /// <summary>Current product version (e.g., "1.1.0"), without any +build metadata.</summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = typeof(GlemVersion).Assembly;

        // Primary source: AssemblyInformationalVersionAttribute (set from $(InformationalVersion)).
        if (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is { InformationalVersion: { Length: > 0 } informational })
        {
            // Strip optional +build metadata (e.g., "1.1.0+abc123" -> "1.1.0").
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        // Safe fallback: the four-part assembly version, reported as major.minor.build.
        if (assembly.GetName().Version is { } v)
        {
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }

        return Fallback;
    }
}
