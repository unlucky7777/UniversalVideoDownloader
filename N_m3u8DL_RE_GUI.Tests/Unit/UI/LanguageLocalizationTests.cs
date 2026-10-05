using System.Collections.Generic;
using System.Reflection;
using N_m3u8DL_RE_GUI;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.UI;

public sealed class LanguageLocalizationTests
{
    [Fact]
    public void KoreanAndEnglishResourceTable_InitializesWithoutDuplicateKeys()
    {
        var field = typeof(MainWindow).GetField("KoreanUi", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var translations = Assert.IsType<Dictionary<string, string>>(field!.GetValue(null));
        Assert.True(translations.Count >= 120);
        Assert.Equal("영상 주소", translations["Input URL"]);
        Assert.Equal("지원 사이트", translations["Supported Sites"]);
    }
}
