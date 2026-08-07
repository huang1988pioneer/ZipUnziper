using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ZipUnziper.Services;
using ZipUnziper.ViewModels;
using ZipUnziper.Views;

namespace ZipUnziper;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            MainWindow? mainWindow = null;
            var dialogs = new AvaloniaDialogService(() => mainWindow);
            var zipService = new ZipService();
            var vm = new MainWindowViewModel(zipService, dialogs);

            mainWindow = new MainWindow
            {
                DataContext = vm
            };
            desktop.MainWindow = mainWindow;

            // Open ZIP from command-line args if provided
            var args = desktop.Args;
            if (args is { Length: > 0 } && System.IO.File.Exists(args[0]))
            {
                mainWindow.Opened += async (_, _) =>
                {
                    await vm.LoadArchiveAsync(args[0]);
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
