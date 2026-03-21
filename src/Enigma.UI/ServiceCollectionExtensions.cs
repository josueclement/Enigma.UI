using Carbon.Avalonia.Desktop.Services;
using Enigma.UI.Models;
using Enigma.UI.ViewModels;
using Enigma.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.UI;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddAppConfiguration(IConfiguration configuration)
        {
            _ = services.Configure<DefaultPathsOptions>(
                configuration.GetSection(DefaultPathsOptions.SectionName));
        }

        public void AddCarbonServices()
        {
            _ = services.AddSingleton<IFileDialogService, FileDialogService>();
            _ = services.AddSingleton<IFolderDialogService, FolderDialogService>();
            _ = services.AddSingleton<INavigationService, NavigationService>();
            _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
            _ = services.AddSingleton<IInfoBarService, InfoBarService>();
            _ = services.AddSingleton<IOverlayService, OverlayService>();
        }

        public void AddPagesAndViewModels()
        {
            _ = services.AddSingleton<MainWindow>();
            _ = services.AddTransient<GenerateKeysPageView>();
            _ = services.AddTransient<EncryptDecryptFilesPageView>();
            _ = services.AddTransient<GenerateLicensesPageView>();

            _ = services.AddSingleton<MainWindowViewModel>();
            _ = services.AddSingleton<GenerateKeysPageViewModel>();
            _ = services.AddSingleton<EncryptDecryptFilesPageViewModel>();
            _ = services.AddSingleton<GenerateLicensesPageViewModel>();
        }
    }
}
