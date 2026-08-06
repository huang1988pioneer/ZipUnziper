using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZipUnziper.Models;

namespace ZipUnziper.Services;

public interface IZipService
{
    /// <summary>
    /// Lists all entries in a ZIP archive (flat list with full paths).
    /// </summary>
    Task<IReadOnlyList<ZipEntryItem>> ListEntriesAsync(string archivePath, CancellationToken ct = default);

    /// <summary>
    /// Extracts the entire archive or selected entry paths into destination directory.
    /// </summary>
    Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        IReadOnlyList<string>? entryPaths = null,
        IProgress<ZipOperationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a ZIP archive from files and/or directories.
    /// </summary>
    Task CompressAsync(
        string archivePath,
        IReadOnlyList<string> sourcePaths,
        IProgress<ZipOperationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts a single file entry to a temporary path for preview.
    /// Returns null if entry is a directory or not found.
    /// </summary>
    Task<string?> ExtractToTempAsync(string archivePath, string entryPath, CancellationToken ct = default);
}
