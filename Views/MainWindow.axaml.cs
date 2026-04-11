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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using G33kColony.ViewModels;
using JetBrains.Annotations;

namespace G33kColony.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel m_viewModel;

    [UsedImplicitly]
    public MainWindow()
        : this(new MainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        m_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = m_viewModel;
    }

    private void OnAboutButtonClick(object sender, RoutedEventArgs e) =>
        ShowAboutDialog();

    private void OnAboutMenuItemClick(object sender, RoutedEventArgs e) =>
        ShowAboutDialog();

    private static void ShowAboutDialog() =>
        (Application.Current?.DataContext as AppViewModel)?.AboutCommand.Execute(null);
}
