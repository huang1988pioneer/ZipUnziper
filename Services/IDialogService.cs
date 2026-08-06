using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZipUnziper.Services;

public interface IDialogService
{
    Task<string?> PickOpenArchiveAsync();
    Task<IReadOnlyList<string>> PickFilesToCompressAsync();
    Task<IReadOnlyList<string>> PickFoldersToCompressAsync();
    Task<string?> PickSaveArchiveAsync(string suggestedName = "archive.zip");
    Task<string?> PickExtractFolderAsync();
    Task ShowMessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task OpenInFinderAsync(string path);
    Task OpenFileAsync(string path);
}
