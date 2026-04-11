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

public class MainWindowTests
{
    [Test]
    public void AboutInfoProviderUsesProductIdentity()
    {
        var aboutInfo = AboutInfoProvider.CreateInfo(null);

        Assert.That(aboutInfo.Title, Is.EqualTo("G33kColony"));
        Assert.That(aboutInfo.Version, Is.EqualTo("1.0.0.0"));
        Assert.That(aboutInfo.Copyright, Is.EqualTo("Copyright (c) 2026 Dean Edis (DeanTheCoder)."));
        Assert.That(aboutInfo.WebsiteUrl, Is.EqualTo("https://github.com/deanthecoder/G33kColony"));
    }
}
