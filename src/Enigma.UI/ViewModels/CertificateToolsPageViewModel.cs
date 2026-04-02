using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Controls.InfoBar;
using Carbon.Avalonia.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Cryptography.Utils;
using Enigma.Cryptography.X509;
using Enigma.UI.Controls;
using Enigma.UI.Models;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.X509;

namespace Enigma.UI.ViewModels;

public class CertificateToolsPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly IOverlayService _overlayService;
    private readonly X509CertificateServiceFactory _x509ServiceFactory;
    private readonly DefaultPathsOptions _defaultPaths;

    public CertificateToolsPageViewModel(
        IFileDialogService fileDialogService,
        IInfoBarService infoBarService,
        IOverlayService overlayService,
        X509CertificateServiceFactory x509ServiceFactory,
        IOptions<DefaultPathsOptions> defaultPaths)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
        _overlayService = overlayService;
        _x509ServiceFactory = x509ServiceFactory;
        _defaultPaths = defaultPaths.Value;

        // Inspect
        BrowseInspectCertificateCommand = new AsyncRelayCommand(BrowseInspectCertificateAsync);
        InspectCommand = new AsyncRelayCommand(InspectAsync);

        // Validate Chain
        BrowseValidateCertificateCommand = new AsyncRelayCommand(BrowseValidateCertificateAsync);
        BrowseTrustedCaCertificatesCommand = new AsyncRelayCommand(BrowseTrustedCaCertificatesAsync);
        ValidateChainCommand = new AsyncRelayCommand(ValidateChainAsync);

        // Export PFX
        BrowseExportCertificateCommand = new AsyncRelayCommand(BrowseExportCertificateAsync);
        BrowseExportPrivateKeyCommand = new AsyncRelayCommand(BrowseExportPrivateKeyAsync);
        BrowsePfxOutputCommand = new AsyncRelayCommand(BrowsePfxOutputAsync);
        ExportPfxCommand = new AsyncRelayCommand(ExportPfxAsync);

        // Import PFX
        BrowseImportPfxCommand = new AsyncRelayCommand(BrowseImportPfxAsync);
        BrowseImportCertificateOutputCommand = new AsyncRelayCommand(BrowseImportCertificateOutputAsync);
        BrowseImportPrivateKeyOutputCommand = new AsyncRelayCommand(BrowseImportPrivateKeyOutputAsync);
        ImportPfxCommand = new AsyncRelayCommand(ImportPfxAsync);
    }

    // === Commands ===

    public AsyncRelayCommand BrowseInspectCertificateCommand { get; }
    public AsyncRelayCommand InspectCommand { get; }
    public AsyncRelayCommand BrowseValidateCertificateCommand { get; }
    public AsyncRelayCommand BrowseTrustedCaCertificatesCommand { get; }
    public AsyncRelayCommand ValidateChainCommand { get; }
    public AsyncRelayCommand BrowseExportCertificateCommand { get; }
    public AsyncRelayCommand BrowseExportPrivateKeyCommand { get; }
    public AsyncRelayCommand BrowsePfxOutputCommand { get; }
    public AsyncRelayCommand ExportPfxCommand { get; }
    public AsyncRelayCommand BrowseImportPfxCommand { get; }
    public AsyncRelayCommand BrowseImportCertificateOutputCommand { get; }
    public AsyncRelayCommand BrowseImportPrivateKeyOutputCommand { get; }
    public AsyncRelayCommand ImportPfxCommand { get; }

    // =====================
    // === Inspect Section ===
    // =====================

    private string? _inspectCertificatePath;
    public string? InspectCertificatePath
    {
        get => _inspectCertificatePath;
        set
        {
            if (SetProperty(ref _inspectCertificatePath, value))
                InspectCertificatePathHasError = false;
        }
    }

    private bool _inspectCertificatePathHasError;
    public bool InspectCertificatePathHasError
    {
        get => _inspectCertificatePathHasError;
        set => SetProperty(ref _inspectCertificatePathHasError, value);
    }

    private string? _inspectResult;
    public string? InspectResult
    {
        get => _inspectResult;
        set
        {
            if (SetProperty(ref _inspectResult, value))
                OnPropertyChanged(nameof(HasInspectResult));
        }
    }

    public bool HasInspectResult => InspectResult is not null;

    // ============================
    // === Validate Chain Section ===
    // ============================

    private string? _validateCertificatePath;
    public string? ValidateCertificatePath
    {
        get => _validateCertificatePath;
        set
        {
            if (SetProperty(ref _validateCertificatePath, value))
                ValidateCertificatePathHasError = false;
        }
    }

    private bool _validateCertificatePathHasError;
    public bool ValidateCertificatePathHasError
    {
        get => _validateCertificatePathHasError;
        set => SetProperty(ref _validateCertificatePathHasError, value);
    }

    private string? _trustedCaCertificatesPath;
    public string? TrustedCaCertificatesPath
    {
        get => _trustedCaCertificatesPath;
        set
        {
            if (SetProperty(ref _trustedCaCertificatesPath, value))
                TrustedCaCertificatesPathHasError = false;
        }
    }

    private bool _trustedCaCertificatesPathHasError;
    public bool TrustedCaCertificatesPathHasError
    {
        get => _trustedCaCertificatesPathHasError;
        set => SetProperty(ref _trustedCaCertificatesPathHasError, value);
    }

    private string? _chainValidationResult;
    public string? ChainValidationResult
    {
        get => _chainValidationResult;
        set
        {
            if (SetProperty(ref _chainValidationResult, value))
                OnPropertyChanged(nameof(HasChainValidationResult));
        }
    }

    public bool HasChainValidationResult => ChainValidationResult is not null;

    // ==========================
    // === Export PFX Section ===
    // ==========================

    private string? _exportCertificatePath;
    public string? ExportCertificatePath
    {
        get => _exportCertificatePath;
        set
        {
            if (SetProperty(ref _exportCertificatePath, value))
                ExportCertificatePathHasError = false;
        }
    }

    private bool _exportCertificatePathHasError;
    public bool ExportCertificatePathHasError
    {
        get => _exportCertificatePathHasError;
        set => SetProperty(ref _exportCertificatePathHasError, value);
    }

    private string? _exportPrivateKeyPath;
    public string? ExportPrivateKeyPath
    {
        get => _exportPrivateKeyPath;
        set
        {
            if (SetProperty(ref _exportPrivateKeyPath, value))
                ExportPrivateKeyPathHasError = false;
        }
    }

    private bool _exportPrivateKeyPathHasError;
    public bool ExportPrivateKeyPathHasError
    {
        get => _exportPrivateKeyPathHasError;
        set => SetProperty(ref _exportPrivateKeyPathHasError, value);
    }

    private string? _exportPrivateKeyPassword;
    public string? ExportPrivateKeyPassword
    {
        get => _exportPrivateKeyPassword;
        set => SetProperty(ref _exportPrivateKeyPassword, value);
    }

    private string? _pfxAlias;
    public string? PfxAlias
    {
        get => _pfxAlias;
        set
        {
            if (SetProperty(ref _pfxAlias, value))
                PfxAliasHasError = false;
        }
    }

    private bool _pfxAliasHasError;
    public bool PfxAliasHasError
    {
        get => _pfxAliasHasError;
        set => SetProperty(ref _pfxAliasHasError, value);
    }

    private string? _pfxPassword;
    public string? PfxPassword
    {
        get => _pfxPassword;
        set
        {
            if (SetProperty(ref _pfxPassword, value))
                PfxPasswordHasError = false;
        }
    }

    private bool _pfxPasswordHasError;
    public bool PfxPasswordHasError
    {
        get => _pfxPasswordHasError;
        set => SetProperty(ref _pfxPasswordHasError, value);
    }

    private string? _pfxOutputPath;
    public string? PfxOutputPath
    {
        get => _pfxOutputPath;
        set
        {
            if (SetProperty(ref _pfxOutputPath, value))
                PfxOutputPathHasError = false;
        }
    }

    private bool _pfxOutputPathHasError;
    public bool PfxOutputPathHasError
    {
        get => _pfxOutputPathHasError;
        set => SetProperty(ref _pfxOutputPathHasError, value);
    }

    // ==========================
    // === Import PFX Section ===
    // ==========================

    private string? _importPfxPath;
    public string? ImportPfxPath
    {
        get => _importPfxPath;
        set
        {
            if (SetProperty(ref _importPfxPath, value))
                ImportPfxPathHasError = false;
        }
    }

    private bool _importPfxPathHasError;
    public bool ImportPfxPathHasError
    {
        get => _importPfxPathHasError;
        set => SetProperty(ref _importPfxPathHasError, value);
    }

    private string? _importPfxPassword;
    public string? ImportPfxPassword
    {
        get => _importPfxPassword;
        set
        {
            if (SetProperty(ref _importPfxPassword, value))
                ImportPfxPasswordHasError = false;
        }
    }

    private bool _importPfxPasswordHasError;
    public bool ImportPfxPasswordHasError
    {
        get => _importPfxPasswordHasError;
        set => SetProperty(ref _importPfxPasswordHasError, value);
    }

    private string? _importCertificateOutputPath;
    public string? ImportCertificateOutputPath
    {
        get => _importCertificateOutputPath;
        set
        {
            if (SetProperty(ref _importCertificateOutputPath, value))
                ImportCertificateOutputPathHasError = false;
        }
    }

    private bool _importCertificateOutputPathHasError;
    public bool ImportCertificateOutputPathHasError
    {
        get => _importCertificateOutputPathHasError;
        set => SetProperty(ref _importCertificateOutputPathHasError, value);
    }

    private string? _importPrivateKeyOutputPath;
    public string? ImportPrivateKeyOutputPath
    {
        get => _importPrivateKeyOutputPath;
        set
        {
            if (SetProperty(ref _importPrivateKeyOutputPath, value))
                ImportPrivateKeyOutputPathHasError = false;
        }
    }

    private bool _importPrivateKeyOutputPathHasError;
    public bool ImportPrivateKeyOutputPathHasError
    {
        get => _importPrivateKeyOutputPathHasError;
        set => SetProperty(ref _importPrivateKeyOutputPathHasError, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // === Browse Commands ===

    private async Task BrowseInspectCertificateAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Certificate", false, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            InspectCertificatePath = paths.First();
    }

    private async Task BrowseValidateCertificateAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Certificate to Validate", false, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            ValidateCertificatePath = paths.First();
    }

    private async Task BrowseTrustedCaCertificatesAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Trusted CA Certificates", true, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            TrustedCaCertificatesPath = string.Join("; ", paths);
    }

    private async Task BrowseExportCertificateAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Certificate", false, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            ExportCertificatePath = paths.First();
    }

    private async Task BrowseExportPrivateKeyAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Private Key", false, _defaultPaths.Keys, "", null);
        if (paths.Any())
            ExportPrivateKeyPath = paths.First();
    }

    private async Task BrowsePfxOutputAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save PFX File", _defaultPaths.Certificates, "certificate.pfx", ".pfx", true, null);
        if (path is not null)
            PfxOutputPath = path;
    }

    private async Task BrowseImportPfxAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select PFX File", false, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            ImportPfxPath = paths.First();
    }

    private async Task BrowseImportCertificateOutputAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Certificate", _defaultPaths.Certificates, "certificate.pem", ".pem", true, null);
        if (path is not null)
            ImportCertificateOutputPath = path;
    }

    private async Task BrowseImportPrivateKeyOutputAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Private Key", _defaultPaths.Certificates, "private.pem", ".pem", true, null);
        if (path is not null)
            ImportPrivateKeyOutputPath = path;
    }

    // === Inspect Certificate ===

    private async Task InspectAsync()
    {
        if (string.IsNullOrWhiteSpace(InspectCertificatePath))
        {
            InspectCertificatePathHasError = true;
            return;
        }

        try
        {
            await using var stream = File.OpenRead(InspectCertificatePath);
            var cert = X509Utils.LoadCertificateFromPem(stream);
            var info = X509Utils.GetCertificateInfo(cert);

            var sb = new StringBuilder();
            sb.AppendLine($"Subject: {info.Subject}");
            sb.AppendLine($"Issuer: {info.Issuer}");
            sb.AppendLine($"Serial Number: {info.SerialNumber}");
            sb.AppendLine($"Version: {info.Version}");
            sb.AppendLine($"Signature Algorithm: {info.SignatureAlgorithm}");
            sb.AppendLine($"Valid From: {info.NotBefore:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Valid To: {info.NotAfter:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Is CA: {info.IsCa}");
            sb.AppendLine($"Is Valid Now: {info.IsValidNow}");

            if (info.KeyUsage is not null)
            {
                var usages = new List<string>();
                if (info.KeyUsage.Length > 0 && info.KeyUsage[0]) usages.Add("Digital Signature");
                if (info.KeyUsage.Length > 1 && info.KeyUsage[1]) usages.Add("Non Repudiation");
                if (info.KeyUsage.Length > 2 && info.KeyUsage[2]) usages.Add("Key Encipherment");
                if (info.KeyUsage.Length > 3 && info.KeyUsage[3]) usages.Add("Data Encipherment");
                if (info.KeyUsage.Length > 4 && info.KeyUsage[4]) usages.Add("Key Agreement");
                if (info.KeyUsage.Length > 5 && info.KeyUsage[5]) usages.Add("Key Cert Sign");
                if (info.KeyUsage.Length > 6 && info.KeyUsage[6]) usages.Add("CRL Sign");
                sb.AppendLine($"Key Usage: {(usages.Count > 0 ? string.Join(", ", usages) : "None")}");
            }

            if (info.SubjectAlternativeNames.Count > 0)
                sb.AppendLine($"Subject Alternative Names: {string.Join(", ", info.SubjectAlternativeNames)}");

            InspectResult = sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            InspectResult = $"Error: {ex.Message}";
        }
    }

    // === Validate Chain ===

    private async Task ValidateChainAsync()
    {
        ValidateCertificatePathHasError = string.IsNullOrWhiteSpace(ValidateCertificatePath);
        TrustedCaCertificatesPathHasError = string.IsNullOrWhiteSpace(TrustedCaCertificatesPath);

        if (ValidateCertificatePathHasError || TrustedCaCertificatesPathHasError)
            return;

        try
        {
            await using var certStream = File.OpenRead(ValidateCertificatePath!);
            var certificate = X509Utils.LoadCertificateFromPem(certStream);

            var caPaths = TrustedCaCertificatesPath!
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var trustedCerts = new List<Org.BouncyCastle.X509.X509Certificate>();
            foreach (var caPath in caPaths)
            {
                await using var caStream = File.OpenRead(caPath);
                trustedCerts.Add(X509Utils.LoadCertificateFromPem(caStream));
            }

            var service = _x509ServiceFactory.CreateService();
            var isValid = service.ValidateChain(certificate, trustedCerts);

            ChainValidationResult = isValid
                ? "Certificate chain is valid."
                : "Certificate chain is invalid.";
        }
        catch (Exception ex)
        {
            ChainValidationResult = $"Error: {ex.Message}";
        }
    }

    // === Export PFX ===

    private async Task ExportPfxAsync()
    {
        if (IsBusy) return;

        ExportCertificatePathHasError = string.IsNullOrWhiteSpace(ExportCertificatePath);
        ExportPrivateKeyPathHasError = string.IsNullOrWhiteSpace(ExportPrivateKeyPath);
        PfxAliasHasError = string.IsNullOrWhiteSpace(PfxAlias);
        PfxPasswordHasError = string.IsNullOrWhiteSpace(PfxPassword);
        PfxOutputPathHasError = string.IsNullOrWhiteSpace(PfxOutputPath);

        if (ExportCertificatePathHasError || ExportPrivateKeyPathHasError ||
            PfxAliasHasError || PfxPasswordHasError || PfxOutputPathHasError)
            return;

        IsBusy = true;
        await _overlayService.ShowAsync(new ProgressOverlayCard
        {
            Title = "Exporting PFX",
            Message = "Please wait...",
            IsIndeterminate = true
        });

        try
        {
            await using var certStream = File.OpenRead(ExportCertificatePath!);
            var certificate = X509Utils.LoadCertificateFromPem(certStream);

            await using var keyStream = File.OpenRead(ExportPrivateKeyPath!);
            var privateKey = !string.IsNullOrEmpty(ExportPrivateKeyPassword)
                ? PemUtils.LoadPrivateKey(keyStream, ExportPrivateKeyPassword)
                : PemUtils.LoadKey(keyStream);

            var pfxBytes = X509Utils.ExportToPfx(PfxAlias!, certificate, privateKey, PfxPassword!);
            await File.WriteAllBytesAsync(PfxOutputPath!, pfxBytes);

            await _overlayService.HideAsync();
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "PFX file exported successfully.";
                bar.Severity = InfoBarSeverity.Success;
            });
        }
        catch (Exception ex)
        {
            await _overlayService.HideAsync();
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Error";
                bar.Message = ex.Message;
                bar.Severity = InfoBarSeverity.Error;
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    // === Import PFX ===

    private async Task ImportPfxAsync()
    {
        if (IsBusy) return;

        ImportPfxPathHasError = string.IsNullOrWhiteSpace(ImportPfxPath);
        ImportPfxPasswordHasError = string.IsNullOrWhiteSpace(ImportPfxPassword);
        ImportCertificateOutputPathHasError = string.IsNullOrWhiteSpace(ImportCertificateOutputPath);
        ImportPrivateKeyOutputPathHasError = string.IsNullOrWhiteSpace(ImportPrivateKeyOutputPath);

        if (ImportPfxPathHasError || ImportPfxPasswordHasError ||
            ImportCertificateOutputPathHasError || ImportPrivateKeyOutputPathHasError)
            return;

        IsBusy = true;
        await _overlayService.ShowAsync(new ProgressOverlayCard
        {
            Title = "Importing PFX",
            Message = "Please wait...",
            IsIndeterminate = true
        });

        try
        {
            var pfxBytes = await File.ReadAllBytesAsync(ImportPfxPath!);
            var (certificate, privateKey) = X509Utils.LoadFromPfx(pfxBytes, ImportPfxPassword!);

            await using (var certStream = File.Create(ImportCertificateOutputPath!))
                X509Utils.SaveCertificateToPem(certificate, certStream);

            await using (var keyStream = File.Create(ImportPrivateKeyOutputPath!))
                PemUtils.SaveKey(privateKey, keyStream);

            await _overlayService.HideAsync();
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "PFX file imported successfully.";
                bar.Severity = InfoBarSeverity.Success;
            });
        }
        catch (Exception ex)
        {
            await _overlayService.HideAsync();
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Error";
                bar.Message = ex.Message;
                bar.Severity = InfoBarSeverity.Error;
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
