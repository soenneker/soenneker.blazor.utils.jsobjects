using Soenneker.Blazor.Utils.JsObjects.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Blazor.Utils.JsObjects.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class JsObjectRegistryTests : HostedUnitTest
{
    private readonly IJsObjectRegistry _blazorlibrary;

    public JsObjectRegistryTests(Host host) : base(host)
    {
        _blazorlibrary = Resolve<IJsObjectRegistry>(true);
    }

    [Test]
    public void Default()
    {

    }
}
