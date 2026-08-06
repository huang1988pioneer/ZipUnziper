using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ZipUnziper.Services;

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Func<Window?> _windowProvider;

    public AvaloniaDialogService(Func<Window?> windowProvider)
    {
        _windowProvider = windowProvider;
    }

    private Window RequireWindow() =>
        _windowProvider() ?? throw new InvalidOperationException("主視窗尚未就緒");

    public async Task<string?> PickOpenArchiveAsync()
    {
        var window = RequireWindow();
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "開啟壓縮檔",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ZIP 壓縮檔")
                {
                    Patterns = ["*.zip"],
                    AppleUniformTypeIdentifiers = ["public.zip-archive"],
                    MimeTypes = ["application/zip"]
                },
                FilePickerFileTypes.All
            ]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<IReadOnlyList<string>> PickFilesToCompressAsync()
    {
        var window = RequireWindow();
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "選擇要壓縮的檔案",
            AllowMultiple = true
        });

        return files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();
    }

    public async Task<IReadOnlyList<string>> PickFoldersToCompressAsync()
    {
        var window = RequireWindow();
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "選擇要壓縮的資料夾",
            AllowMultiple = true
        });

        return folders
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Cast<string>()
            .ToList();
    }

    public async Task<string?> PickSaveArchiveAsync(string suggestedName = "archive.zip")
    {
        var window = RequireWindow();
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "儲存壓縮檔",
            SuggestedFileName = suggestedName,
            DefaultExtension = "zip",
            FileTypeChoices =
            [
                new FilePickerFileType("ZIP 壓縮檔")
                {
                    Patterns = ["*.zip"],
                    AppleUniformTypeIdentifiers = ["public.zip-archive"],
                    MimeTypes = ["application/zip"]
                }
            ]
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickExtractFolderAsync()
    {
        var window = RequireWindow();
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "選擇解壓目的資料夾",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var window = RequireWindow();
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "確定",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        MinWidth = 80
                    }
                }
            }
        };

        var button = (Button)((StackPanel)dialog.Content!).Children[1];
        button.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(window);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var window = RequireWindow();
        var result = false;
        var ok = new Button { Content = "確定", MinWidth = 80 };
        var cancel = new Button { Content = "取消", MinWidth = 80, Margin = new Avalonia.Thickness(8, 0, 0, 0) };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { ok, cancel }
                    }
                }
            }
        };

        ok.Click += (_, _) => { result = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(window);
        return result;
    }

    public Task OpenInFinderAsync(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-R \"{path}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            var dir = System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Process.Start("xdg-open", dir);
        }

        return Task.CompletedTask;
    }

    public Task OpenFileAsync(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", $"\"{path}\"");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else
            Process.Start("xdg-open", path);

        return Task.CompletedTask;
    }
}
