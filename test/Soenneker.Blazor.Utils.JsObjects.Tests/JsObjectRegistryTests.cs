using Soenneker.Blazor.Utils.JsObjects.Abstract;
using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Blazor.Utils.JsObjects.Tests;

[Collection("Collection")]
public sealed class JsObjectRegistryTests : FixturedUnitTest
{
    private readonly IJsObjectRegistry _blazorlibrary;

    public JsObjectRegistryTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _blazorlibrary = Resolve<IJsObjectRegistry>(true);
    }

    [Fact]
    public void Default()
    {

    }
}
