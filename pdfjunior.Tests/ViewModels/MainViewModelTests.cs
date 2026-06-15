using pdfjunior.ViewModels;
using Xunit;

namespace pdfjunior.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Files_StartsEmpty()
    {
        var vm = new MainViewModel();

        Assert.Empty(vm.Files);
    }

    [Fact]
    public void HasFiles_DefaultFalse()
    {
        var vm = new MainViewModel();

        Assert.False(vm.HasFiles);
    }

    [Fact]
    public void SelectedFile_DefaultNull()
    {
        var vm = new MainViewModel();

        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void CanMerge_AlwaysFalse()
    {
        var vm = new MainViewModel();

        Assert.False(vm.CanMerge);
    }
}
