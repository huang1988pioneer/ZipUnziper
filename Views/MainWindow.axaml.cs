using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ZipUnziper.Models;
using ZipUnziper.ViewModels;

namespace ZipUnziper.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles()?.ToList();
        if (files is null || files.Count == 0) return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();

        if (paths.Count == 0) return;

        // Single zip → open preview; otherwise compress
        if (paths.Count == 1 &&
            File.Exists(paths[0]) &&
            Path.GetExtension(paths[0]).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await Vm.LoadArchiveAsync(paths[0]);
            return;
        }

        await Vm.CompressPathsAsync(paths);
    }

    private async void OnEntriesDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Vm?.SelectedEntry is null) return;
        await Vm.OpenEntryCommand.ExecuteAsync(Vm.SelectedEntry);
    }

    private void OnEntriesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || sender is not DataGrid grid) return;
        var items = grid.SelectedItems.OfType<ZipEntryItem>().ToList();
        Vm.SetSelection(items);
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "關於 ZipUnziper",
            Width = 400,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "ZipUnziper", FontSize = 22, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    new TextBlock { Text = "以 Avalonia UI 打造的 macOS ZIP 工具", Opacity = 0.8 },
                    new TextBlock
                    {
                        Text = "功能：預覽 ZIP 內容、壓縮、解壓縮\n採用清楚易用的檔案管理介面",
                        Opacity = 0.7,
                        Margin = new Avalonia.Thickness(0, 8, 0, 0)
                    },
                    new Button
                    {
                        Content = "關閉",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        MinWidth = 80,
                        Margin = new Avalonia.Thickness(0, 12, 0, 0)
                    }
                }
            }
        };

        var btn = (Button)((StackPanel)dialog.Content!).Children[^1];
        btn.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
