using Xunit;

namespace GLEM.Tests;

public sealed class ReleaseSelfTestTests
{
    [Fact]
    public void ReleaseSelfTest_C01ThroughC03_PassesWithExplicitDrivingLoad() =>
        Assert.True(GLEM.App.ReleaseVerification.RunSelfTest());
}
