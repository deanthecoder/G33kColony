// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using G33kColony.ViewModels;

namespace G33kColony.Tests;

public class MainWindowViewModelTests
{
    [Test]
    public void MainWindowViewModelExposesProductText()
    {
        var viewModel = new MainWindowViewModel();

        Assert.That(viewModel.AppName, Is.EqualTo("G33kColony"));
        Assert.That(viewModel.WindowTitle, Is.EqualTo("G33kColony"));
        Assert.That(viewModel.Tagline, Does.Contain("Ant colony"));
    }
}
