using System;
using Avalonia.Controls;
using Carbon.Avalonia.Desktop.Controls.Navigation;
using Carbon.Avalonia.Desktop.Services;
using Enigma.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using PhosphorIconsAvalonia;

namespace Enigma.UI.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public MainWindowViewModel(
        IServiceProvider services,
        INavigationService navigation,
        IContentDialogService dialogService,
        IOverlayService overlayService,
        IInfoBarService infoBarService)
    {
        _services = services;
        Navigation = navigation;
        DialogService = dialogService;
        OverlayService = overlayService;
        InfoBarService = infoBarService;

        Navigation.PageFactory = navItem =>
        {
            var page = _services.GetRequiredService(navItem.PageType);
            if (page is not Control ctrl)
                throw new InvalidOperationException($"Page type {navItem.PageType} is not a Control");
            ctrl.DataContext = _services.GetRequiredService(navItem.PageViewModelType);
            return ctrl;
        };

        Navigation.Items.Add(new NavigationItem
        {
            Header = "Generate Keys",
            IconData = IconService.CreateGeometry(Icon.key, IconType.regular),
            PageType = typeof(GenerateKeysPageView),
            PageViewModelType = typeof(GenerateKeysPageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Encrypt/Decrypt Files",
            IconData = IconService.CreateGeometry(Icon.lock_simple, IconType.regular),
            PageType = typeof(EncryptDecryptFilesPageView),
            PageViewModelType = typeof(EncryptDecryptFilesPageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Generate Licenses",
            IconData = IconService.CreateGeometry(Icon.certificate, IconType.regular),
            PageType = typeof(GenerateLicensesPageView),
            PageViewModelType = typeof(GenerateLicensesPageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Validate Licenses",
            IconData = IconService.CreateGeometry(Icon.seal_check, IconType.regular),
            PageType = typeof(ValidateLicensesPageView),
            PageViewModelType = typeof(ValidateLicensesPageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Generate Certificate",
            IconData = IconService.CreateGeometry(Icon.seal, IconType.regular),
            PageType = typeof(GenerateCertificatePageView),
            PageViewModelType = typeof(GenerateCertificatePageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Sign Certificate",
            IconData = IconService.CreateGeometry(Icon.pen_nib, IconType.regular),
            PageType = typeof(SignCertificatePageView),
            PageViewModelType = typeof(SignCertificatePageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Certificate Tools",
            IconData = IconService.CreateGeometry(Icon.file_magnifying_glass, IconType.regular),
            PageType = typeof(CertificateToolsPageView),
            PageViewModelType = typeof(CertificateToolsPageViewModel)
        });
        Navigation.Items.Add(new NavigationItem
        {
            Header = "Certificate Info",
            IconData = IconService.CreateGeometry(Icon.info, IconType.regular),
            PageType = typeof(CertificateInfoPageView),
            PageViewModelType = typeof(CertificateInfoPageViewModel)
        });

        var firstPage = _services.GetRequiredService<GenerateKeysPageView>();
        firstPage.DataContext = _services.GetRequiredService<GenerateKeysPageViewModel>();
        Navigation.NavigateToAsync(firstPage).GetAwaiter().GetResult();
    }

    public INavigationService Navigation { get; }
    public IContentDialogService DialogService { get; }
    public IOverlayService OverlayService { get; }
    public IInfoBarService InfoBarService { get; }
}
