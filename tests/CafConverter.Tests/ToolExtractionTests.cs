using CafConverter.Services;

namespace CafConverter.Tests;

public sealed class ToolExtractionTests
{
    [Fact]
    public void EmbeddedFfmpegAndFfprobeResourcesAreDiscoverable()
    {
        var names = typeof(ToolExtractionService).Assembly.GetManifestResourceNames();
        var tools = ToolExtractionService.FindEmbeddedResourceNames(names);
        Assert.NotNull(tools.Ffmpeg);
        Assert.NotNull(tools.Ffprobe);
    }
}
