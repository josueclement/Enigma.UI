using Carbon.Avalonia.Desktop.Services;
using Enigma.Cryptography.DataEncryption;
using Enigma.Cryptography.PQC;
using Enigma.Cryptography.PublicKey;
using Enigma.Cryptography.X509;
using Enigma.LicenseManager;
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

        public void AddCryptographyServices()
        {
            _ = services.AddSingleton<PublicKeyServiceFactory>();
            _ = services.AddSingleton<MLKemServiceFactory>();
            _ = services.AddSingleton<MLDsaServiceFactory>();
            _ = services.AddSingleton<Pbkdf2DataEncryptionService>();
            _ = services.AddSingleton<Argon2DataEncryptionService>();
            _ = services.AddSingleton<RsaDataEncryptionService>();
            _ = services.AddSingleton<MLKemDataEncryptionService>();
            _ = services.AddSingleton<LicenseService>();
            _ = services.AddSingleton<X509CertificateServiceFactory>();
        }

        public void AddPagesAndViewModels()
        {
            _ = services.AddSingleton<MainWindow>();
            _ = services.AddTransient<GenerateKeysPageView>();
            _ = services.AddTransient<EncryptDecryptFilesPageView>();
            _ = services.AddTransient<GenerateLicensesPageView>();
            _ = services.AddTransient<ValidateLicensesPageView>();
            _ = services.AddTransient<GenerateCertificatePageView>();
            _ = services.AddTransient<SignCertificatePageView>();
            _ = services.AddTransient<CertificateToolsPageView>();
            _ = services.AddTransient<CertificateInfoPageView>();

            _ = services.AddSingleton<MainWindowViewModel>();
            _ = services.AddSingleton<GenerateKeysPageViewModel>();
            _ = services.AddSingleton<EncryptDecryptFilesPageViewModel>();
            _ = services.AddSingleton<GenerateLicensesPageViewModel>();
            _ = services.AddSingleton<ValidateLicensesPageViewModel>();
            _ = services.AddSingleton<GenerateCertificatePageViewModel>();
            _ = services.AddSingleton<SignCertificatePageViewModel>();
            _ = services.AddSingleton<CertificateToolsPageViewModel>();
            _ = services.AddSingleton<CertificateInfoPageViewModel>();
        }
    }
}
