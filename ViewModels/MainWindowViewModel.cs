// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.ViewModels;

namespace G33kColony.ViewModels;

/// <summary>
/// Provides the first-run shell state for G33kColony.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    public string AppName { get; } = "G33kColony";

    public string WindowTitle { get; } = "G33kColony";

    public string Tagline { get; } = "Ant colony simulation workspace.";
}
