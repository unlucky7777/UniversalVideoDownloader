#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.UI;

public sealed class CleanHomeAndLinkPreviewTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static string ReadXaml()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "N_m3u8DL_RE_GUI", "MainWindow.xaml"));
        return File.ReadAllText(source);
    }

    [Fact]
    public void NoAutomaticQuestionMarkWidgetsRemain()
    {
        var text = ReadXaml();
        Assert.DoesNotContain("Text=\"?\"", text);
        Assert.DoesNotContain("[?]", text);
    }

    [Theory]
    [InlineData("Engine")]
    [InlineData("Threads")]
    [InlineData("Max quality")]
    public void TechnicalLabelsHaveRealWpfTooltips(string label)
    {
        var root = XDocument.Parse(ReadXaml());
        var nodes = root.Descendants(Ns + "TextBlock")
            .Where(x => (string?)x.Attribute("Text") == label);
        Assert.Contains(nodes,
            node => !string.IsNullOrWhiteSpace((string?)node.Attribute("ToolTip")));
    }

    [Fact]
    public void DropdownAndThreadsBoxHaveWorkingTooltipTargets()
    {
        var root = XDocument.Parse(ReadXaml());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var name in new[] { "ComboBox_DownloadEngine", "ComboBox_WebsiteQuality", "TextBox_Max" })
        {
            var node = root.Descendants().Single(e => (string?)e.Attribute(x + "Name") == name);
            Assert.False(string.IsNullOrWhiteSpace((string?)node.Attribute("ToolTip")));
        }
    }

    [Fact]
    public void QualityChangeRerunsMetadataPreview()
    {
        var root = XDocument.Parse(ReadXaml());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var select = root.Descendants(Ns + "ComboBox")
            .Single(e => (string?)e.Attribute(x + "Name") == "ComboBox_WebsiteQuality");
        Assert.Equal("WebsiteQuality_SelectionChanged",
            (string?)select.Attribute("SelectionChanged"));
    }
}
