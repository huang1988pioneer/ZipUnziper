using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZipUnziper.Models;
using ZipUnziper.Services;

namespace ZipUnziper.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IZipService _zipService;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<ZipEntryItem> _allEntries = [];
    private CancellationTokenSource? _operationCts;

    public MainWindowViewModel() : this(new ZipService(), new NullDialogService())
    {
    }

    public MainWindowViewModel(IZipService zipService, IDialogService dialogs)
    {
        _zipService = zipService;
        _dialogs = dialogs;
    }

    public ObservableCollection<ZipEntryItem> VisibleEntries { get; } = [];
    public ObservableCollection<ZipEntryItem> SelectedEntries { get; } = [];

    [ObservableProperty]
    private string _windowTitle = "ZipUnziper";

    [ObservableProperty]
    private string? _archivePath;

    [ObservableProperty]
    private string _currentFolder = "";

    [ObservableProperty]
    private string _pathBarText = "";

    [ObservableProperty]
    private string _statusText = "就緒 — 開啟 ZIP 以預覽內容，或開始壓縮檔案";

    [ObservableProperty]
    private string _selectionStatus = "";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isProgressVisible;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private ZipEntryItem? _selectedEntry;

    public bool HasArchive => !string.IsNullOrEmpty(ArchivePath);
    public bool CanGoUp => !string.IsNullOrEmpty(CurrentFolder);
    public string ArchiveName => string.IsNullOrEmpty(ArchivePath) ? "未開啟壓縮檔" : Path.GetFileName(ArchivePath);

    partial void OnArchivePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasArchive));
        OnPropertyChanged(nameof(ArchiveName));
        WindowTitle = string.IsNullOrEmpty(value)
            ? "ZipUnziper"
            : $"{Path.GetFileName(value)} — ZipUnziper";
        OpenArchiveCommand.NotifyCanExecuteChanged();
        CloseArchiveCommand.NotifyCanExecuteChanged();
        ExtractAllCommand.NotifyCanExecuteChanged();
        ExtractSelectedCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentFolderChanged(string value)
    {
        OnPropertyChanged(nameof(CanGoUp));
        UpdatePathBar();
        GoUpCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => RefreshVisibleEntries();

    partial void OnIsBusyChanged(bool value)
    {
        OpenArchiveCommand.NotifyCanExecuteChanged();
        CompressFilesCommand.NotifyCanExecuteChanged();
        CompressFolderCommand.NotifyCanExecuteChanged();
        ExtractAllCommand.NotifyCanExecuteChanged();
        ExtractSelectedCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        CloseArchiveCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEntryChanged(ZipEntryItem? value)
    {
        ExtractSelectedCommand.NotifyCanExecuteChanged();
        UpdateSelectionStatus();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task OpenArchiveAsync()
    {
        var path = await _dialogs.PickOpenArchiveAsync();
        if (path is null) return;
        await LoadArchiveAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanUseArchive))]
    private void CloseArchive()
    {
        ArchivePath = null;
        _allEntries = [];
        VisibleEntries.Clear();
        SelectedEntries.Clear();
        CurrentFolder = "";
        PathBarText = "";
        StatusText = "已關閉壓縮檔";
        SelectionStatus = "";
    }

    [RelayCommand(CanExecute = nameof(CanUseArchive))]
    private async Task RefreshAsync()
    {
        if (ArchivePath is null) return;
        await LoadArchiveAsync(ArchivePath, keepFolder: true);
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task CompressFilesAsync()
    {
        var files = await _dialogs.PickFilesToCompressAsync();
        if (files.Count == 0) return;
        await CompressPathsAsync(files);
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task CompressFolderAsync()
    {
        var folders = await _dialogs.PickFoldersToCompressAsync();
        if (folders.Count == 0) return;
        await CompressPathsAsync(folders);
    }

    [RelayCommand(CanExecute = nameof(CanUseArchive))]
    private async Task ExtractAllAsync()
    {
        if (ArchivePath is null) return;

        var dest = await _dialogs.PickExtractFolderAsync();
        if (dest is null) return;

        await RunOperationAsync("解壓中…", async (progress, ct) =>
        {
            await _zipService.ExtractAsync(ArchivePath, dest, null, progress, ct);
        }, successMessage: $"已全部解壓到: {dest}");

        if (!IsBusy)
        {
            var open = await _dialogs.ConfirmAsync("解壓完成", "是否在 Finder 中顯示目的資料夾？");
            if (open) await _dialogs.OpenInFinderAsync(dest);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExtractSelected))]
    private async Task ExtractSelectedAsync()
    {
        if (ArchivePath is null) return;

        var selected = GetEffectiveSelection();
        if (selected.Count == 0)
        {
            await _dialogs.ShowMessageAsync("提示", "請先選擇要解壓的項目");
            return;
        }

        var dest = await _dialogs.PickExtractFolderAsync();
        if (dest is null) return;

        var paths = selected.Select(e => e.FullPath).ToList();
        await RunOperationAsync("解壓選取項目…", async (progress, ct) =>
        {
            await _zipService.ExtractAsync(ArchivePath, dest, paths, progress, ct);
        }, successMessage: $"已解壓 {selected.Count} 個項目到: {dest}");
    }

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void GoUp()
    {
        if (string.IsNullOrEmpty(CurrentFolder)) return;
        var trimmed = CurrentFolder.TrimEnd('/');
        var idx = trimmed.LastIndexOf('/');
        CurrentFolder = idx >= 0 ? trimmed[..(idx + 1)] : "";
        RefreshVisibleEntries();
    }

    [RelayCommand]
    private void GoHome()
    {
        CurrentFolder = "";
        RefreshVisibleEntries();
    }

    [RelayCommand]
    private async Task OpenEntryAsync(ZipEntryItem? entry)
    {
        entry ??= SelectedEntry;
        if (entry is null) return;

        if (entry.IsDirectory)
        {
            CurrentFolder = entry.FullPath.EndsWith('/') ? entry.FullPath : entry.FullPath + "/";
            RefreshVisibleEntries();
            return;
        }

        if (ArchivePath is null) return;

        try
        {
            IsBusy = true;
            StatusText = $"正在提取預覽: {entry.Name}";
            var temp = await _zipService.ExtractToTempAsync(ArchivePath, entry.FullPath);
            if (temp is null)
            {
                await _dialogs.ShowMessageAsync("無法開啟", "找不到該檔案項目");
                return;
            }

            await _dialogs.OpenFileAsync(temp);
            StatusText = $"已開啟: {entry.Name}";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("開啟失敗", ex.Message);
            StatusText = "開啟失敗";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void CancelOperation()
    {
        _operationCts?.Cancel();
        StatusText = "正在取消…";
    }

    /// <summary>
    /// Open archive from external path (drag-drop / command line).
    /// </summary>
    public async Task LoadArchiveAsync(string path, bool keepFolder = false)
    {
        if (!File.Exists(path))
        {
            await _dialogs.ShowMessageAsync("錯誤", $"找不到檔案:\n{path}");
            return;
        }

        try
        {
            IsBusy = true;
            IsProgressVisible = true;
            ProgressValue = 0;
            StatusText = $"正在讀取: {Path.GetFileName(path)}";

            var entries = await _zipService.ListEntriesAsync(path);
            ArchivePath = path;
            _allEntries = entries;
            if (!keepFolder) CurrentFolder = "";
            RefreshVisibleEntries();

            var fileCount = entries.Count(e => !e.IsDirectory);
            var totalSize = entries.Where(e => !e.IsDirectory).Sum(e => e.UncompressedSize);
            StatusText = $"已開啟 {Path.GetFileName(path)} — {fileCount} 個檔案，共 {ZipEntryItem.FormatBytes(totalSize)}";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("無法開啟壓縮檔", ex.Message);
            StatusText = "開啟失敗";
        }
        finally
        {
            IsBusy = false;
            IsProgressVisible = false;
        }
    }

    /// <summary>
    /// Compress arbitrary paths (used by drag-drop of non-zip files).
    /// </summary>
    public async Task CompressPathsAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        var suggested = paths.Count == 1
            ? Path.GetFileName(paths[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + ".zip"
            : "archive.zip";

        var savePath = await _dialogs.PickSaveArchiveAsync(suggested);
        if (savePath is null) return;

        await RunOperationAsync("壓縮中…", async (progress, ct) =>
        {
            await _zipService.CompressAsync(savePath, paths, progress, ct);
        }, successMessage: $"壓縮完成: {savePath}");

        var open = await _dialogs.ConfirmAsync("壓縮完成", "是否開啟剛建立的壓縮檔？");
        if (open) await LoadArchiveAsync(savePath);
    }

    public void SetSelection(IEnumerable<ZipEntryItem> items)
    {
        SelectedEntries.Clear();
        foreach (var item in items)
            SelectedEntries.Add(item);
        SelectedEntry = SelectedEntries.FirstOrDefault();
        ExtractSelectedCommand.NotifyCanExecuteChanged();
        UpdateSelectionStatus();
    }

    private void RefreshVisibleEntries()
    {
        VisibleEntries.Clear();
        var folder = CurrentFolder ?? "";

        IEnumerable<ZipEntryItem> query = _allEntries.Where(e => IsDirectChild(e.FullPath, folder, e.IsDirectory));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = _allEntries.Where(e =>
                e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query
                     .OrderByDescending(e => e.IsDirectory)
                     .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            VisibleEntries.Add(item);
        }

        UpdatePathBar();
        UpdateSelectionStatus();
    }

    private static bool IsDirectChild(string fullPath, string folder, bool isDirectory)
    {
        folder ??= "";
        var path = fullPath;
        if (isDirectory && !path.EndsWith('/'))
            path += "/";

        if (string.IsNullOrEmpty(folder))
        {
            // top level: no slash in remaining name (except trailing for dirs)
            var trimmed = path.TrimEnd('/');
            return !trimmed.Contains('/');
        }

        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = path[folder.Length..].TrimEnd('/');
        return rest.Length > 0 && !rest.Contains('/');
    }

    private void UpdatePathBar()
    {
        if (string.IsNullOrEmpty(ArchivePath))
        {
            PathBarText = "";
            return;
        }

        PathBarText = string.IsNullOrEmpty(CurrentFolder)
            ? ArchiveName
            : $"{ArchiveName}/{CurrentFolder.TrimEnd('/')}";
    }

    private void UpdateSelectionStatus()
    {
        var selected = GetEffectiveSelection();
        if (selected.Count == 0)
        {
            SelectionStatus = $"{VisibleEntries.Count} 個項目";
            return;
        }

        var size = selected.Where(e => !e.IsDirectory).Sum(e => e.UncompressedSize);
        SelectionStatus = $"已選 {selected.Count} 項" +
                          (size > 0 ? $"，{ZipEntryItem.FormatBytes(size)}" : "");
    }

    private List<ZipEntryItem> GetEffectiveSelection()
    {
        if (SelectedEntries.Count > 0)
            return SelectedEntries.ToList();
        if (SelectedEntry is not null)
            return [SelectedEntry];
        return [];
    }

    private bool CanInteract() => !IsBusy;
    private bool CanUseArchive() => !IsBusy && HasArchive;
    private bool CanExtractSelected() => !IsBusy && HasArchive && (SelectedEntry is not null || SelectedEntries.Count > 0);

    private async Task RunOperationAsync(
        string busyMessage,
        Func<IProgress<ZipOperationProgress>, CancellationToken, Task> work,
        string successMessage)
    {
        _operationCts?.Cancel();
        _operationCts = new CancellationTokenSource();
        var ct = _operationCts.Token;

        try
        {
            IsBusy = true;
            IsProgressVisible = true;
            ProgressValue = 0;
            StatusText = busyMessage;

            var progress = new Progress<ZipOperationProgress>(p =>
            {
                ProgressValue = p.Percent;
                if (!string.IsNullOrEmpty(p.Message))
                    StatusText = p.Message;
            });

            await work(progress, ct);
            StatusText = successMessage;
            ProgressValue = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = "操作已取消";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("操作失敗", ex.Message);
            StatusText = "操作失敗";
        }
        finally
        {
            IsBusy = false;
            IsProgressVisible = false;
            _operationCts = null;
        }
    }
}

/// <summary>
/// Placeholder used by the designer / default ctor.
/// </summary>
file sealed class NullDialogService : IDialogService
{
    public Task<string?> PickOpenArchiveAsync() => Task.FromResult<string?>(null);
    public Task<IReadOnlyList<string>> PickFilesToCompressAsync() =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<IReadOnlyList<string>> PickFoldersToCompressAsync() =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<string?> PickSaveArchiveAsync(string suggestedName = "archive.zip") => Task.FromResult<string?>(null);
    public Task<string?> PickExtractFolderAsync() => Task.FromResult<string?>(null);
    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
    public Task OpenInFinderAsync(string path) => Task.CompletedTask;
    public Task OpenFileAsync(string path) => Task.CompletedTask;
}
