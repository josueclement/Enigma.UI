using System;
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
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace Enigma.UI.ViewModels;

public class SignCertificatePageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly IOverlayService _overlayService;
    private readonly X509CertificateServiceFactory _x509ServiceFactory;
    private readonly DefaultPathsOptions _defaultPaths;

    public SignCertificatePageViewModel(
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

        BrowseCsrFileCommand = new AsyncRelayCommand(BrowseCsrFileAsync);
        BrowseCaPrivateKeyCommand = new AsyncRelayCommand(BrowseCaPrivateKeyAsync);
        VerifyCsrCommand = new AsyncRelayCommand(VerifyCsrAsync);
        BrowseCertificateOutputCommand = new AsyncRelayCommand(BrowseCertificateOutputAsync);
        IssueCertificateCommand = new AsyncRelayCommand(IssueCertificateAsync);
    }

    public AsyncRelayCommand BrowseCsrFileCommand { get; }
    public AsyncRelayCommand BrowseCaPrivateKeyCommand { get; }
    public AsyncRelayCommand VerifyCsrCommand { get; }
    public AsyncRelayCommand BrowseCertificateOutputCommand { get; }
    public AsyncRelayCommand IssueCertificateCommand { get; }

    public string[] SignatureAlgorithmOptions { get; } = ["SHA256WithRSA", "SHA384WithRSA", "SHA512WithRSA"];

    private string? _csrFilePath;
    public string? CsrFilePath
    {
        get => _csrFilePath;
        set
        {
            if (SetProperty(ref _csrFilePath, value))
                CsrFileHasError = false;
        }
    }

    private bool _csrFileHasError;
    public bool CsrFileHasError
    {
        get => _csrFileHasError;
        set => SetProperty(ref _csrFileHasError, value);
    }

    private string? _caPrivateKeyPath;
    public string? CaPrivateKeyPath
    {
        get => _caPrivateKeyPath;
        set
        {
            if (SetProperty(ref _caPrivateKeyPath, value))
                CaPrivateKeyHasError = false;
        }
    }

    private bool _caPrivateKeyHasError;
    public bool CaPrivateKeyHasError
    {
        get => _caPrivateKeyHasError;
        set => SetProperty(ref _caPrivateKeyHasError, value);
    }

    private string? _caPrivateKeyPassword;
    public string? CaPrivateKeyPassword
    {
        get => _caPrivateKeyPassword;
        set => SetProperty(ref _caPrivateKeyPassword, value);
    }

    private string? _issuerCommonName;
    public string? IssuerCommonName
    {
        get => _issuerCommonName;
        set
        {
            if (SetProperty(ref _issuerCommonName, value))
                IssuerNameHasError = false;
        }
    }

    private bool _issuerNameHasError;
    public bool IssuerNameHasError
    {
        get => _issuerNameHasError;
        set => SetProperty(ref _issuerNameHasError, value);
    }

    private int _selectedSignatureAlgorithmIndex;
    public int SelectedSignatureAlgorithmIndex
    {
        get => _selectedSignatureAlgorithmIndex;
        set => SetProperty(ref _selectedSignatureAlgorithmIndex, value);
    }

    private DateTime? _validFrom = DateTime.Now;
    public DateTime? ValidFrom
    {
        get => _validFrom;
        set => SetProperty(ref _validFrom, value);
    }

    private DateTime? _validTo = DateTime.Now.AddYears(1);
    public DateTime? ValidTo
    {
        get => _validTo;
        set => SetProperty(ref _validTo, value);
    }

    private bool _isCaCertificate;
    public bool IsCaCertificate
    {
        get => _isCaCertificate;
        set => SetProperty(ref _isCaCertificate, value);
    }

    private bool _keyUsageDigitalSignature = true;
    public bool KeyUsageDigitalSignature
    {
        get => _keyUsageDigitalSignature;
        set => SetProperty(ref _keyUsageDigitalSignature, value);
    }

    private bool _keyUsageKeyEncipherment = true;
    public bool KeyUsageKeyEncipherment
    {
        get => _keyUsageKeyEncipherment;
        set => SetProperty(ref _keyUsageKeyEncipherment, value);
    }

    private bool _keyUsageKeyCertSign;
    public bool KeyUsageKeyCertSign
    {
        get => _keyUsageKeyCertSign;
        set => SetProperty(ref _keyUsageKeyCertSign, value);
    }

    private bool _keyUsageCrlSign;
    public bool KeyUsageCrlSign
    {
        get => _keyUsageCrlSign;
        set => SetProperty(ref _keyUsageCrlSign, value);
    }

    private string? _subjectAlternativeNames;
    public string? SubjectAlternativeNames
    {
        get => _subjectAlternativeNames;
        set => SetProperty(ref _subjectAlternativeNames, value);
    }

    private string? _certificateOutputPath;
    public string? CertificateOutputPath
    {
        get => _certificateOutputPath;
        set
        {
            if (SetProperty(ref _certificateOutputPath, value))
                CertificateOutputHasError = false;
        }
    }

    private bool _certificateOutputHasError;
    public bool CertificateOutputHasError
    {
        get => _certificateOutputHasError;
        set => SetProperty(ref _certificateOutputHasError, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private async Task BrowseCsrFileAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select CSR File", false, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            CsrFilePath = paths.First();
    }

    private async Task BrowseCaPrivateKeyAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select CA Private Key", false, _defaultPaths.Keys, "", null);
        if (paths.Any())
            CaPrivateKeyPath = paths.First();
    }

    private async Task BrowseCertificateOutputAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Certificate", _defaultPaths.Certificates, "certificate.pem", ".pem", true, null);
        if (path is not null)
            CertificateOutputPath = path;
    }

    private async Task VerifyCsrAsync()
    {
        if (string.IsNullOrWhiteSpace(CsrFilePath))
        {
            CsrFileHasError = true;
            return;
        }

        try
        {
            await using var csrStream = File.OpenRead(CsrFilePath);
            using var reader = new StreamReader(csrStream, Encoding.UTF8, true, 1024, true);
            var pemReader = new PemReader(reader);
            var csr = (Pkcs10CertificationRequest)pemReader.ReadObject();

            var service = _x509ServiceFactory.CreateService(SignatureAlgorithmOptions[SelectedSignatureAlgorithmIndex]);
            var isValid = service.VerifyCsr(csr);

            if (isValid)
            {
                await _infoBarService.ShowAsync(bar =>
                {
                    bar.Title = "Valid";
                    bar.Message = "CSR signature is valid.";
                    bar.Severity = InfoBarSeverity.Success;
                });
            }
            else
            {
                await _infoBarService.ShowAsync(bar =>
                {
                    bar.Title = "Invalid";
                    bar.Message = "CSR signature is invalid.";
                    bar.Severity = InfoBarSeverity.Error;
                });
            }
        }
        catch (Exception ex)
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Error";
                bar.Message = ex.Message;
                bar.Severity = InfoBarSeverity.Error;
            });
        }
    }

    private async Task IssueCertificateAsync()
    {
        if (IsBusy) return;

        CsrFileHasError = string.IsNullOrWhiteSpace(CsrFilePath);
        CaPrivateKeyHasError = string.IsNullOrWhiteSpace(CaPrivateKeyPath);
        IssuerNameHasError = string.IsNullOrWhiteSpace(IssuerCommonName);
        CertificateOutputHasError = string.IsNullOrWhiteSpace(CertificateOutputPath);

        if (CsrFileHasError || CaPrivateKeyHasError || IssuerNameHasError || CertificateOutputHasError)
            return;

        IsBusy = true;
        await _overlayService.ShowAsync(new ProgressOverlayCard
        {
            Title = "Issuing Certificate",
            Message = "This may take a moment...",
            IsIndeterminate = true
        });

        try
        {
            await Task.Run(async () =>
            {
                // Load CSR from PEM
                await using var csrStream = File.OpenRead(CsrFilePath!);
                using var reader = new StreamReader(csrStream, Encoding.UTF8, true, 1024, true);
                var pemReader = new PemReader(reader);
                var csr = (Pkcs10CertificationRequest)pemReader.ReadObject();

                // Load CA private key
                await using var keyStream = File.OpenRead(CaPrivateKeyPath!);
                var caPrivateKey = !string.IsNullOrEmpty(CaPrivateKeyPassword)
                    ? PemUtils.LoadPrivateKey(keyStream, CaPrivateKeyPassword)
                    : PemUtils.LoadKey(keyStream);

                // Reconstruct the key pair from the private key
                AsymmetricCipherKeyPair issuerKeyPair;
                if (caPrivateKey is Org.BouncyCastle.Crypto.Parameters.RsaPrivateCrtKeyParameters rsaPriv)
                {
                    var rsaPub = new Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters(false, rsaPriv.Modulus, rsaPriv.PublicExponent);
                    issuerKeyPair = new AsymmetricCipherKeyPair(rsaPub, rsaPriv);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported key type. Only RSA keys are supported for certificate signing.");
                }

                // Build issuer name
                var issuerName = new X509Name("CN=" + IssuerCommonName);

                // Create X509 service
                var service = _x509ServiceFactory.CreateService(SignatureAlgorithmOptions[SelectedSignatureAlgorithmIndex]);

                // Build key usage
                var keyUsage = 0;
                if (KeyUsageDigitalSignature) keyUsage |= KeyUsage.DigitalSignature;
                if (KeyUsageKeyEncipherment) keyUsage |= KeyUsage.KeyEncipherment;
                if (KeyUsageKeyCertSign) keyUsage |= KeyUsage.KeyCertSign;
                if (KeyUsageCrlSign) keyUsage |= KeyUsage.CrlSign;

                // Build SANs
                GeneralNames? sans = null;
                if (!string.IsNullOrWhiteSpace(SubjectAlternativeNames))
                {
                    var names = SubjectAlternativeNames
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(n => new GeneralName(GeneralName.DnsName, n))
                        .ToArray();
                    if (names.Length > 0)
                        sans = new GeneralNames(names);
                }

                // Determine validity dates
                var validFrom = ValidFrom ?? DateTime.Now;
                var validTo = ValidTo ?? DateTime.Now.AddYears(1);

                // Issue the certificate
                var cert = service.IssueCertificate(csr, issuerKeyPair, issuerName, validFrom, validTo, keyUsage, IsCaCertificate, sans);

                // Save certificate to PEM
                await using var outputStream = File.Create(CertificateOutputPath!);
                X509Utils.SaveCertificateToPem(cert, outputStream);
            });

            await _overlayService.HideAsync();
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "Certificate issued successfully.";
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
