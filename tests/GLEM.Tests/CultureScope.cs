using System.Globalization;
using GLEM.App.Properties;

namespace GLEM.Tests;

/// <summary>
/// Temporarily applies a culture to the current thread and to newly created threads for the duration of a test.
/// On Dispose (even when the test body throws) it restores CurrentCulture, CurrentUICulture,
/// DefaultThreadCurrentCulture, DefaultThreadCurrentUICulture, and AppResources.Culture to their previous values.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _originalCulture;
    private readonly CultureInfo _originalUICulture;
    private readonly CultureInfo? _originalDefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultThreadCurrentUICulture;
    private readonly CultureInfo? _originalAppResourcesCulture;

    public CultureScope(CultureInfo culture) : this(culture, culture)
    {
    }

    public CultureScope(CultureInfo culture, CultureInfo uiCulture)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;
        _originalDefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
        _originalDefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        _originalAppResourcesCulture = AppResources.Culture;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUICulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultThreadCurrentUICulture;
        AppResources.Culture = _originalAppResourcesCulture;
    }
}
