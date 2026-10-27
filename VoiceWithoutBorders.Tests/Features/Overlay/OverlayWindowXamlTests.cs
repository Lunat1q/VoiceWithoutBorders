using FluentAssertions;
using System.Xml.Linq;

namespace VoiceWithoutBorders.Tests.Features.Overlay;

/// <summary>Reads the overlay's XAML source: the window itself needs a desktop and is not created in tests.</summary>
public class OverlayWindowXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Background")]
    [InlineData("BorderBrush")]
    public void Pill_HasABrushLockedAndUnlocked(string property)
    {
        // Regression: 0.2.2 shipped with the pill's brushes removed and no style setters, so it was invisible.
        var pill = Load().Descendants(Wpf + "Border").Single(b => (string?)b.Attribute(X + "Name") == "Pill");
        var style = pill.Element(Wpf + "Border.Style")!.Element(Wpf + "Style")!;

        pill.Attribute(property).Should().BeNull("a local value would beat the unlocked trigger");
        Setters(style).Should().Contain(property, "the locked pill needs it");
        Setters(style.Descendants(Wpf + "DataTrigger").Single()).Should().Contain(property, "unlocked recolours it");
    }

    private static IEnumerable<string?> Setters(XElement parent) =>
        parent.Elements(Wpf + "Setter").Select(s => (string?)s.Attribute("Property"));

    private static XDocument Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "VoiceWithoutBorders.slnx")))
            dir = dir.Parent ?? throw new InvalidOperationException("Repository root not found.");
        return XDocument.Load(Path.Combine(dir.FullName, "VoiceWithoutBorders", "Features", "Overlay", "OverlayWindow.xaml"));
    }
}
