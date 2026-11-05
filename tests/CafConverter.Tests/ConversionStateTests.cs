using CafConverter.Models;
using CafConverter.ViewModels;

namespace CafConverter.Tests;

public sealed class ConversionStateTests
{
    [Fact]
    public void CancellationUpdatesVisibleState()
    {
        var vm = new MediaFileItemViewModel(new MediaFileItem { FilePath = "test.mp3" });
        vm.Status = ConversionStatus.Converting;
        vm.MarkCancelled();
        Assert.Equal(ConversionStatus.Cancelled, vm.Status);
        Assert.Equal("已取消", vm.StatusText);
    }
}
