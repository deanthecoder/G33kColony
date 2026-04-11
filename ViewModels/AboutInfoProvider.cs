// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DTC.Core.UI;

namespace G33kColony.ViewModels;

/// <summary>
/// Provides static metadata used by the app menu and About dialog.
/// </summary>
internal static class AboutInfoProvider
{
    public static AboutInfo Info => CreateInfo(LoadIcon());

    internal static AboutInfo CreateInfo(IImage icon) => new()
    {
        Title = "G33kColony",
        Version = typeof(AboutInfoProvider).Assembly.GetName().Version?.ToString() ?? "Unknown",
        Copyright = "Copyright (c) 2026 Dean Edis (DeanTheCoder).",
        WebsiteUrl = "https://github.com/deanthecoder/G33kColony",
        Icon = icon
    };

    private static Bitmap LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://G33kColony/Assets/app.ico"));
        return new Bitmap(stream);
    }
}
