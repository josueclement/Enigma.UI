using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Controls.InfoBar;
using Carbon.Avalonia.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Cryptography.PublicKey;
using Enigma.Cryptography.Utils;
using Enigma.Cryptography.X509;
using Enigma.UI.Controls;
using Enigma.UI.Models;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.OpenSsl;

namespace Enigma.UI.ViewModels;

public class GenerateCertificatePageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly IOverlayService _overlayService;
    private readonly PublicKeyServiceFactory _publicKeyServiceFactory;
    private readonly X509CertificateServiceFactory _x509ServiceFactory;
    private readonly DefaultPathsOptions _defaultPaths;

    public GenerateCertificatePageViewModel(
        IFileDialogService fileDialogService,
        IInfoBarService infoBarService,
        IOverlayService overlayService,
        PublicKeyServiceFactory publicKeyServiceFactory,
        X509CertificateServiceFactory x509ServiceFactory,
        IOptions<DefaultPathsOptions> defaultPaths)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
        _overlayService = overlayService;
        _publicKeyServiceFactory = publicKeyServiceFactory;
        _x509ServiceFactory = x509ServiceFactory;
        _defaultPaths = defaultPaths.Value;

        BrowseCertificateOutputCommand = new AsyncRelayCommand(BrowseCertificateOutputAsync);
        BrowsePrivateKeyOutputCommand = new AsyncRelayCommand(BrowsePrivateKeyOutputAsync);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
    }

    public AsyncRelayCommand BrowseCertificateOutputCommand { get; }
    public AsyncRelayCommand BrowsePrivateKeyOutputCommand { get; }
    public AsyncRelayCommand GenerateCommand { get; }

    // --- Mode & Algorithm ---

    public string[] ModeOptions { get; } = ["Self-Signed Certificate", "Certificate Signing Request (CSR)"];

    private int _selectedModeIndex;
    public int SelectedModeIndex
    {
        get => _selectedModeIndex;
        set
        {
            if (SetProperty(ref _selectedModeIndex, value))
                OnPropertyChanged(nameof(IsSelfSigned));
        }
    }

    public bool IsSelfSigned => SelectedModeIndex == 0;

    public string[] SignatureAlgorithmOptions { get; } = ["SHA256WithRSA", "SHA384WithRSA", "SHA512WithRSA"];

    private int _selectedSignatureAlgorithmIndex;
    public int SelectedSignatureAlgorithmIndex
    {
        get => _selectedSignatureAlgorithmIndex;
        set => SetProperty(ref _selectedSignatureAlgorithmIndex, value);
    }

    public string[] KeySizeOptions { get; } = ["2048", "3072", "4096"];

    private int _selectedKeySizeIndex;
    public int SelectedKeySizeIndex
    {
        get => _selectedKeySizeIndex;
        set => SetProperty(ref _selectedKeySizeIndex, value);
    }

    // --- Subject Fields ---

    private string? _commonName;
    public string? CommonName
    {
        get => _commonName;
        set
        {
            if (SetProperty(ref _commonName, value))
                CommonNameHasError = false;
        }
    }

    private bool _commonNameHasError;
    public bool CommonNameHasError
    {
        get => _commonNameHasError;
        set => SetProperty(ref _commonNameHasError, value);
    }

    private string? _organization;
    public string? Organization
    {
        get => _organization;
        set => SetProperty(ref _organization, value);
    }

    private string? _organizationalUnit;
    public string? OrganizationalUnit
    {
        get => _organizationalUnit;
        set => SetProperty(ref _organizationalUnit, value);
    }

    private string? _country;
    public string? Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    private string? _state;
    public string? State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    private string? _locality;
    public string? Locality
    {
        get => _locality;
        set => SetProperty(ref _locality, value);
    }

    // --- Certificate Options ---

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

    // --- Output Files ---

    private string? _certificateOutputPath;
    public string? CertificateOutputPath
    {
        get => _certificateOutputPath;
        set
        {
            if (SetProperty(ref _certificateOutputPath, value))
                CertificateOutputPathHasError = false;
        }
    }

    private bool _certificateOutputPathHasError;
    public bool CertificateOutputPathHasError
    {
        get => _certificateOutputPathHasError;
        set => SetProperty(ref _certificateOutputPathHasError, value);
    }

    private string? _privateKeyOutputPath;
    public string? PrivateKeyOutputPath
    {
        get => _privateKeyOutputPath;
        set
        {
            if (SetProperty(ref _privateKeyOutputPath, value))
                PrivateKeyOutputPathHasError = false;
        }
    }

    private bool _privateKeyOutputPathHasError;
    public bool PrivateKeyOutputPathHasError
    {
        get => _privateKeyOutputPathHasError;
        set => SetProperty(ref _privateKeyOutputPathHasError, value);
    }

    private string? _privateKeyPassword;
    public string? PrivateKeyPassword
    {
        get => _privateKeyPassword;
        set => SetProperty(ref _privateKeyPassword, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // --- Browse Commands ---

    private async Task BrowseCertificateOutputAsync()
    {
        var title = IsSelfSigned ? "Save Certificate" : "Save CSR";
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            title, _defaultPaths.Certificates, "certificate.pem", ".pem", true, null);
        if (path is not null)
            CertificateOutputPath = path;
    }

    private async Task BrowsePrivateKeyOutputAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Private Key", _defaultPaths.Certificates, "private.pem", ".pem", true, null);
        if (path is not null)
            PrivateKeyOutputPath = path;
    }

    // --- Validation ---

    private bool ValidateInputs()
    {
        CommonNameHasError = string.IsNullOrWhiteSpace(CommonName);
        CertificateOutputPathHasError = string.IsNullOrWhiteSpace(CertificateOutputPath);
        PrivateKeyOutputPathHasError = string.IsNullOrWhiteSpace(PrivateKeyOutputPath);
        return !(CommonNameHasError || CertificateOutputPathHasError || PrivateKeyOutputPathHasError);
    }

    // --- Generate ---

    private async Task GenerateAsync()
    {
        if (IsBusy) return;
        if (!ValidateInputs()) return;

        IsBusy = true;
        var overlayTitle = IsSelfSigned ? "Generating Certificate" : "Generating CSR";
        await _overlayService.ShowAsync(new ProgressOverlayCard
        {
            Title = overlayTitle,
            Message = "This may take a moment...",
            IsIndeterminate = true
        });

        try
        {
            var keySize = int.Parse(KeySizeOptions[SelectedKeySizeIndex]);
            var algorithmName = SignatureAlgorithmOptions[SelectedSignatureAlgorithmIndex];

            var keyPair = await Task.Run(() =>
                _publicKeyServiceFactory.CreateRsaService().GenerateKeyPair(keySize));

            var service = _x509ServiceFactory.CreateService(algorithmName);

            var attrs = new List<DerObjectIdentifier> { X509Name.CN };
            var valueDict = new Dictionary<DerObjectIdentifier, string> { { X509Name.CN, CommonName! } };
            if (!string.IsNullOrWhiteSpace(Organization)) { attrs.Add(X509Name.O); valueDict[X509Name.O] = Organization; }
            if (!string.IsNullOrWhiteSpace(OrganizationalUnit)) { attrs.Add(X509Name.OU); valueDict[X509Name.OU] = OrganizationalUnit; }
            if (!string.IsNullOrWhiteSpace(Country)) { attrs.Add(X509Name.C); valueDict[X509Name.C] = Country; }
            if (!string.IsNullOrWhiteSpace(State)) { attrs.Add(X509Name.ST); valueDict[X509Name.ST] = State; }
            if (!string.IsNullOrWhiteSpace(Locality)) { attrs.Add(X509Name.L); valueDict[X509Name.L] = Locality; }
            var subject = new X509Name(attrs, valueDict);

            if (IsSelfSigned)
            {
                int? keyUsageInt = null;
                var ku = 0;
                if (KeyUsageDigitalSignature) ku |= KeyUsage.DigitalSignature;
                if (KeyUsageKeyEncipherment) ku |= KeyUsage.KeyEncipherment;
                if (KeyUsageKeyCertSign) ku |= KeyUsage.KeyCertSign;
                if (KeyUsageCrlSign) ku |= KeyUsage.CrlSign;
                if (ku != 0) keyUsageInt = ku;

                GeneralNames? sans = null;
                if (!string.IsNullOrWhiteSpace(SubjectAlternativeNames))
                {
                    var dnsNames = SubjectAlternativeNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var generalNames = new GeneralName[dnsNames.Length];
                    for (var i = 0; i < dnsNames.Length; i++)
                        generalNames[i] = new GeneralName(GeneralName.DnsName, dnsNames[i]);
                    sans = new GeneralNames(generalNames);
                }

                var cert = await Task.Run(() =>
                    service.GenerateSelfSignedCertificate(
                        keyPair, subject,
                        ValidFrom!.Value, ValidTo!.Value,
                        keyUsageInt, IsCaCertificate, sans));

                await using var certStream = File.Create(CertificateOutputPath!);
                X509Utils.SaveCertificateToPem(cert, certStream);
            }
            else
            {
                var csr = await Task.Run(() => service.GenerateCsr(keyPair, subject));

                await using var csrStream = File.Create(CertificateOutputPath!);
                using var writer = new StreamWriter(csrStream, Encoding.UTF8, 1024, true);
                var pemWriter = new PemWriter(writer);
                pemWriter.WriteObject(csr);
            }

            if (!string.IsNullOrEmpty(PrivateKeyPassword))
            {
                await using var privStream = File.Create(PrivateKeyOutputPath!);
                PemUtils.SavePrivateKey(keyPair.Private, privStream, PrivateKeyPassword, "AES-256-CBC");
            }
            else
            {
                await using var privStream = File.Create(PrivateKeyOutputPath!);
                PemUtils.SaveKey(keyPair.Private, privStream);
            }

            await _overlayService.HideAsync();
            var successMessage = IsSelfSigned
                ? "Certificate generated successfully."
                : "CSR generated successfully.";
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = successMessage;
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
