using System.Collections.Generic;

namespace Soenneker.Blazor.Utils.JsObjects.Demo.Pages;

internal sealed class DemoSnapshot
{
    public int InstanceId { get; set; }
    public string Kind { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string Headline { get; set; } = "";
    public IReadOnlyList<string> Items { get; set; } = [];
}
