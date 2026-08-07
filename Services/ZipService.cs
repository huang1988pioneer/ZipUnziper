using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZipUnziper.Models;

namespace ZipUnziper.Services;

public sealed class ZipService : IZipService
{
    public Task<IReadOnlyList<ZipEntryItem>> ListEntriesAsync(string archivePath, CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<ZipEntryItem>>(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("找不到壓縮檔", archivePath);

            using var archive = ZipFile.OpenRead(archivePath);
            var list = new List<ZipEntryItem>(archive.Entries.Count);

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var isDir = IsDirectoryEntry(entry);
                var fullPath = NormalizeEntryPath(entry.FullName);
                var name = isDir
                    ? GetDirectoryName(fullPath)
                    : Path.GetFileName(fullPath.TrimEnd('/'));

                if (string.IsNullOrEmpty(name) && isDir)
                    continue;

                list.Add(new ZipEntryItem
                {
                    Name = name,
                    FullPath = fullPath,
                    IsDirectory = isDir,
                    UncompressedSize = isDir ? 0 : entry.Length,
                    CompressedSize = isDir ? 0 : entry.CompressedLength,
                    LastModified = entry.LastWriteTime,
                    Crc32 = isDir ? 0 : entry.Crc32,
                    CompressionMethod = isDir ? null : "Deflate"
                });
            }

            // Ensure parent folders exist even if the zip only has files
            EnsureVirtualFolders(list);
            return list
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, ct);
    }

    public Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        IReadOnlyList<string>? entryPaths = null,
        IProgress<ZipOperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(destinationDirectory);

            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries.Where(e => !IsDirectoryEntry(e)).ToList();

            if (entryPaths is { Count: > 0 })
            {
                var set = new HashSet<string>(
                    entryPaths.Select(NormalizeEntryPath),
                    StringComparer.OrdinalIgnoreCase);

                entries = entries.Where(e =>
                {
                    var path = NormalizeEntryPath(e.FullName);
                    if (set.Contains(path)) return true;
                    // include children of selected folders
                    return set.Any(sel =>
                        sel.EndsWith('/') && path.StartsWith(sel, StringComparison.OrdinalIgnoreCase)
                        || !sel.EndsWith('/') && path.StartsWith(sel + "/", StringComparison.OrdinalIgnoreCase));
                }).ToList();
            }

            var total = Math.Max(entries.Count, 1);
            var done = 0;

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var relative = NormalizeEntryPath(entry.FullName).TrimEnd('/');
                var destPath = Path.Combine(destinationDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

                // Path traversal guard
                var fullDest = Path.GetFullPath(destPath);
                var fullRoot = Path.GetFullPath(destinationDirectory);
                if (!fullDest.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !string.Equals(fullDest, fullRoot, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"不安全的壓縮路徑: {entry.FullName}");
                }

                var dir = Path.GetDirectoryName(fullDest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(fullDest, overwrite: true);
                done++;
                progress?.Report(new ZipOperationProgress
                {
                    Message = $"正在解壓: {relative}",
                    Percent = done * 100.0 / total
                });
            }

            progress?.Report(new ZipOperationProgress
            {
                Message = $"解壓完成（{done} 個檔案）",
                Percent = 100
            });
        }, ct);
    }

    public Task CompressAsync(
        string archivePath,
        IReadOnlyList<string> sourcePaths,
        IProgress<ZipOperationProgress>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (sourcePaths.Count == 0)
                throw new ArgumentException("請至少選擇一個要壓縮的檔案或資料夾", nameof(sourcePaths));

            var parentDir = Path.GetDirectoryName(archivePath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);

            if (File.Exists(archivePath))
                File.Delete(archivePath);

            // Collect all files first for progress
            var files = new List<(string FullPath, string EntryName)>();
            foreach (var source in sourcePaths)
            {
                ct.ThrowIfCancellationRequested();
                if (File.Exists(source))
                {
                    files.Add((source, Path.GetFileName(source)));
                }
                else if (Directory.Exists(source))
                {
                    var rootName = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(source, file);
                        var entryName = Path.Combine(rootName, relative).Replace('\\', '/');
                        files.Add((file, entryName));
                    }
                }
            }

            if (files.Count == 0)
                throw new InvalidOperationException("所選路徑中沒有可壓縮的檔案");

            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            var total = files.Count;
            var done = 0;

            foreach (var (fullPath, entryName) in files)
            {
                ct.ThrowIfCancellationRequested();
                archive.CreateEntryFromFile(fullPath, entryName, CompressionLevel.Optimal);
                done++;
                progress?.Report(new ZipOperationProgress
                {
                    Message = $"正在壓縮: {entryName}",
                    Percent = done * 100.0 / total
                });
            }

            progress?.Report(new ZipOperationProgress
            {
                Message = $"壓縮完成（{done} 個檔案）",
                Percent = 100
            });
        }, ct);
    }

    public Task<string?> ExtractToTempAsync(string archivePath, string entryPath, CancellationToken ct = default)
    {
        return Task.Run<string?>(() =>
        {
            ct.ThrowIfCancellationRequested();
            var normalized = NormalizeEntryPath(entryPath);
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.Entries.FirstOrDefault(e =>
                NormalizeEntryPath(e.FullName).Equals(normalized, StringComparison.OrdinalIgnoreCase));

            if (entry is null || IsDirectoryEntry(entry))
                return null;

            var tempRoot = Path.Combine(Path.GetTempPath(), "ZipUnziper", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var fileName = Path.GetFileName(normalized.TrimEnd('/'));
            var dest = Path.Combine(tempRoot, fileName);
            entry.ExtractToFile(dest, overwrite: true);
            return dest;
        }, ct);
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        string.IsNullOrEmpty(entry.Name)
        || entry.FullName.EndsWith('/')
        || entry.FullName.EndsWith('\\');

    private static string NormalizeEntryPath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string GetDirectoryName(string fullPath)
    {
        var trimmed = fullPath.TrimEnd('/');
        var idx = trimmed.LastIndexOf('/');
        return idx >= 0 ? trimmed[(idx + 1)..] : trimmed;
    }

    private static void EnsureVirtualFolders(List<ZipEntryItem> list)
    {
        var existing = new HashSet<string>(list.Select(e => e.FullPath.TrimEnd('/') + (e.IsDirectory ? "/" : "")),
            StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<ZipEntryItem>();
        foreach (var item in list.Where(e => !e.IsDirectory).ToList())
        {
            var parts = item.FullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            for (var i = 0; i < parts.Length - 1; i++)
            {
                current += parts[i] + "/";
                if (existing.Add(current))
                {
                    toAdd.Add(new ZipEntryItem
                    {
                        Name = parts[i],
                        FullPath = current,
                        IsDirectory = true
                    });
                }
            }
        }

        list.AddRange(toAdd);
    }
}
