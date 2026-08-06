using System;

namespace ZipUnziper.Models;

/// <summary>
/// Represents a file or folder entry inside a ZIP archive (or a virtual folder view).
/// </summary>
public sealed class ZipEntryItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long UncompressedSize { get; init; }
    public long CompressedSize { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public uint Crc32 { get; init; }
    public string? CompressionMethod { get; init; }

    public double CompressionRatio =>
        UncompressedSize <= 0 ? 0 : Math.Clamp(1.0 - (double)CompressedSize / UncompressedSize, 0, 1);

    public string SizeDisplay => IsDirectory ? string.Empty : FormatBytes(UncompressedSize);
    public string PackedDisplay => IsDirectory ? string.Empty : FormatBytes(CompressedSize);
    public string RatioDisplay => IsDirectory || UncompressedSize <= 0
        ? string.Empty
        : $"{CompressionRatio * 100:0.0}%";
    public string ModifiedDisplay => LastModified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    public string CrcDisplay => IsDirectory || Crc32 == 0 ? string.Empty : Crc32.ToString("X8");
    public string TypeDisplay => IsDirectory ? "資料夾" : GetExtensionLabel(Name);
    public string IconGlyph => IsDirectory ? "📁" : GetFileGlyph(Name);

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double value = bytes;
        string[] units = ["KB", "MB", "GB", "TB"];
        int unit = -1;
        do
        {
            value /= 1024;
            unit++;
        } while (value >= 1024 && unit < units.Length - 1);

        return $"{value:0.##} {units[unit]}";
    }

    private static string GetExtensionLabel(string name)
    {
        var ext = System.IO.Path.GetExtension(name);
        return string.IsNullOrEmpty(ext) ? "檔案" : ext.TrimStart('.').ToUpperInvariant() + " 檔案";
    }

    private static string GetFileGlyph(string name)
    {
        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "📦",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => "🖼",
            ".txt" or ".md" or ".log" or ".csv" => "📄",
            ".pdf" => "📕",
            ".cs" or ".js" or ".ts" or ".py" or ".json" or ".xml" or ".html" or ".css" => "💻",
            ".mp3" or ".wav" or ".flac" or ".aac" => "🎵",
            ".mp4" or ".mov" or ".avi" or ".mkv" => "🎬",
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "📑",
            _ => "📄"
        };
    }
}
