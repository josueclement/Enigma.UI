using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Controls.InfoBar;
using Carbon.Avalonia.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Cryptography.Utils;
using Enigma.LicenseManager;
using Enigma.UI.Models;
using Microsoft.Extensions.Options;

namespace Enigma.UI.ViewModels;

public class GenerateLicensesPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly DefaultPathsOptions _defaultPaths;

    public GenerateLicensesPageViewModel(
        IFileDialogService fileDialogService,
        IInfoBarService infoBarService,
        IOptions<DefaultPathsOptions> defaultPaths)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
        _defaultPaths = defaultPaths.Value;

        BrowseSigningKeyCommand = new AsyncRelayCommand(BrowseSigningKeyAsync);
        BrowseLicenseOutputCommand = new AsyncRelayCommand(BrowseLicenseOutputAsync);
        GenerateLicenseCommand = new AsyncRelayCommand(GenerateLicenseAsync);
    }

    public AsyncRelayCommand BrowseSigningKeyCommand { get; }
    public AsyncRelayCommand BrowseLicenseOutputCommand { get; }
    public AsyncRelayCommand GenerateLicenseCommand { get; }

    // --- Generation properties ---

    private string? _productId;
    public string? ProductId
    {
        get => _productId;
        set
        {
            if (SetProperty(ref _productId, value))
                ProductIdHasError = false;
        }
    }

    private string? _owner;
    public string? Owner
    {
        get => _owner;
        set
        {
            if (SetProperty(ref _owner, value))
                OwnerHasError = false;
        }
    }

    private string? _deviceId;
    public string? DeviceId
    {
        get => _deviceId;
        set => SetProperty(ref _deviceId, value);
    }

    private bool _hasExpiration;
    public bool HasExpiration
    {
        get => _hasExpiration;
        set => SetProperty(ref _hasExpiration, value);
    }

    private DateTime? _expirationDate;
    public DateTime? ExpirationDate
    {
        get => _expirationDate;
        set => SetProperty(ref _expirationDate, value);
    }

    public string[] SigningAlgorithmOptions { get; } = ["RSA", "ML-DSA"];

    private int _selectedSigningAlgorithmIndex;
    public int SelectedSigningAlgorithmIndex
    {
        get => _selectedSigningAlgorithmIndex;
        set => SetProperty(ref _selectedSigningAlgorithmIndex, value);
    }

    private string? _signingKeyPath;
    public string? SigningKeyPath
    {
        get => _signingKeyPath;
        set
        {
            if (SetProperty(ref _signingKeyPath, value))
                SigningKeyHasError = false;
        }
    }

    private string? _signingKeyPassword;
    public string? SigningKeyPassword
    {
        get => _signingKeyPassword;
        set => SetProperty(ref _signingKeyPassword, value);
    }

    private string? _licenseOutputPath;
    public string? LicenseOutputPath
    {
        get => _licenseOutputPath;
        set
        {
            if (SetProperty(ref _licenseOutputPath, value))
                OutputPathHasError = false;
        }
    }

    // --- Validation error flags ---

    private bool _productIdHasError;
    public bool ProductIdHasError
    {
        get => _productIdHasError;
        set => SetProperty(ref _productIdHasError, value);
    }

    private bool _ownerHasError;
    public bool OwnerHasError
    {
        get => _ownerHasError;
        set => SetProperty(ref _ownerHasError, value);
    }

    private bool _signingKeyHasError;
    public bool SigningKeyHasError
    {
        get => _signingKeyHasError;
        set => SetProperty(ref _signingKeyHasError, value);
    }

    private bool _outputPathHasError;
    public bool OutputPathHasError
    {
        get => _outputPathHasError;
        set => SetProperty(ref _outputPathHasError, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // --- Browse commands ---

    private async Task BrowseSigningKeyAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Signing Key", false, _defaultPaths.Keys, "", null);
        if (paths.Any())
            SigningKeyPath = paths.First();
    }

    private async Task BrowseLicenseOutputAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save License", _defaultPaths.Licenses, "license.json", ".json", true, null);
        if (path is not null)
            LicenseOutputPath = path;
    }

    // --- Generate License ---

    private bool ValidateInputs()
    {
        ProductIdHasError = string.IsNullOrWhiteSpace(ProductId);
        OwnerHasError = string.IsNullOrWhiteSpace(Owner);
        SigningKeyHasError = string.IsNullOrWhiteSpace(SigningKeyPath);
        OutputPathHasError = string.IsNullOrWhiteSpace(LicenseOutputPath);
        return !(ProductIdHasError || OwnerHasError || SigningKeyHasError || OutputPathHasError);
    }

    private async Task GenerateLicenseAsync()
    {
        if (IsBusy) return;
        if (!ValidateInputs()) return;

        IsBusy = true;
        try
        {
            await using var keyStream = File.OpenRead(SigningKeyPath);
            var privateKey = !string.IsNullOrEmpty(SigningKeyPassword)
                ? PemUtils.LoadPrivateKey(keyStream, SigningKeyPassword)
                : PemUtils.LoadKey(keyStream);

            var builder = new LicenseBuilder()
                .SetId(Ulid.NewUlid().ToString())
                .SetCreationDate(DateTime.UtcNow)
                .SetProductId(ProductId)
                .SetOwner(Owner);

            if (!string.IsNullOrWhiteSpace(DeviceId))
                builder.SetDeviceId(DeviceId);

            if (HasExpiration && ExpirationDate.HasValue)
                builder.SetExpirationDate(ExpirationDate.Value);

            if (SelectedSigningAlgorithmIndex == 0)
                builder.SignWithRsa(privateKey);
            else
                builder.SignWithMlDsa(privateKey);

            var license = builder.Build();

            await using var outputStream = File.Create(LicenseOutputPath);
            await license.SaveAsync(outputStream);

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "License generated successfully.";
                bar.Severity = InfoBarSeverity.Success;
            });
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
        finally
        {
            IsBusy = false;
        }
    }

}
