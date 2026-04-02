using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Cryptography.Utils;
using Enigma.Cryptography.X509;
using Enigma.UI.Models;
using Microsoft.Extensions.Options;

namespace Enigma.UI.ViewModels;

public class CertificateInfoPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly DefaultPathsOptions _defaultPaths;

    public CertificateInfoPageViewModel(
        IFileDialogService fileDialogService,
        IOptions<DefaultPathsOptions> defaultPaths)
    {
        _fileDialogService = fileDialogService;
        _defaultPaths = defaultPaths.Value;

        BrowseCertificateCommand = new AsyncRelayCommand(BrowseCertificateAsync);
        LoadCertificateCommand = new AsyncRelayCommand(LoadCertificateAsync);
    }

    public AsyncRelayCommand BrowseCertificateCommand { get; }
    public AsyncRelayCommand LoadCertificateCommand { get; }

    private string? _certificatePath;
    public string? CertificatePath
    {
        get => _certificatePath;
        set
        {
            if (SetProperty(ref _certificatePath, value))
                CertificatePathHasError = false;
        }
    }

    private bool _certificatePathHasError;
    public bool CertificatePathHasError
    {
        get => _certificatePathHasError;
        set => SetProperty(ref _certificatePathHasError, value);
    }

    // --- Certificate Info Properties ---

    private bool _hasCertificateInfo;
    public bool HasCertificateInfo
    {
        get => _hasCertificateInfo;
        set => SetProperty(ref _hasCertificateInfo, value);
    }

    private string? _subject;
    public string? Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value);
    }

    private string? _issuer;
    public string? Issuer
    {
        get => _issuer;
        set => SetProperty(ref _issuer, value);
    }

    private string? _serialNumber;
    public string? SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    private string? _version;
    public string? Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    private string? _signatureAlgorithm;
    public string? SignatureAlgorithm
    {
        get => _signatureAlgorithm;
        set => SetProperty(ref _signatureAlgorithm, value);
    }

    private string? _validFrom;
    public string? ValidFrom
    {
        get => _validFrom;
        set => SetProperty(ref _validFrom, value);
    }

    private string? _validTo;
    public string? ValidTo
    {
        get => _validTo;
        set => SetProperty(ref _validTo, value);
    }

    private bool _isCa;
    public bool IsCa
    {
        get => _isCa;
        set => SetProperty(ref _isCa, value);
    }

    private bool _isValidNow;
    public bool IsValidNow
    {
        get => _isValidNow;
        set => SetProperty(ref _isValidNow, value);
    }

    private string? _keyUsage;
    public string? KeyUsage
    {
        get => _keyUsage;
        set => SetProperty(ref _keyUsage, value);
    }

    private string? _subjectAlternativeNames;
    public string? SubjectAlternativeNames
    {
        get => _subjectAlternativeNames;
        set => SetProperty(ref _subjectAlternativeNames, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => ErrorMessage is not null;

    // --- Commands ---

    private async Task BrowseCertificateAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Certificate", false, _defaultPaths.Certificates, "", null);
        if (paths.Any())
            CertificatePath = paths.First();
    }

    private async Task LoadCertificateAsync()
    {
        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            CertificatePathHasError = true;
            return;
        }

        HasCertificateInfo = false;
        ErrorMessage = null;

        try
        {
            await using var stream = File.OpenRead(CertificatePath);
            var cert = X509Utils.LoadCertificateFromPem(stream);
            var info = X509Utils.GetCertificateInfo(cert);

            Subject = info.Subject;
            Issuer = info.Issuer;
            SerialNumber = info.SerialNumber.ToString(16).ToUpperInvariant();
            Version = $"V{info.Version}";
            SignatureAlgorithm = info.SignatureAlgorithm;
            ValidFrom = info.NotBefore.ToString("yyyy-MM-dd HH:mm:ss UTC");
            ValidTo = info.NotAfter.ToString("yyyy-MM-dd HH:mm:ss UTC");
            IsCa = info.IsCa;
            IsValidNow = info.IsValidNow;

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
                KeyUsage = usages.Count > 0 ? string.Join(", ", usages) : "None";
            }
            else
            {
                KeyUsage = "Not specified";
            }

            SubjectAlternativeNames = info.SubjectAlternativeNames.Count > 0
                ? string.Join(", ", info.SubjectAlternativeNames)
                : "None";

            HasCertificateInfo = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
