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
        BrowseValidateLicenseCommand = new AsyncRelayCommand(BrowseValidateLicenseAsync);
        BrowseValidatePublicKeyCommand = new AsyncRelayCommand(BrowseValidatePublicKeyAsync);
        GenerateLicenseCommand = new AsyncRelayCommand(GenerateLicenseAsync);
        ValidateLicenseCommand = new AsyncRelayCommand(ValidateLicenseAsync);
    }

    public AsyncRelayCommand BrowseSigningKeyCommand { get; }
    public AsyncRelayCommand BrowseLicenseOutputCommand { get; }
    public AsyncRelayCommand BrowseValidateLicenseCommand { get; }
    public AsyncRelayCommand BrowseValidatePublicKeyCommand { get; }
    public AsyncRelayCommand GenerateLicenseCommand { get; }
    public AsyncRelayCommand ValidateLicenseCommand { get; }

    // --- Generation properties ---

    private string? _productId;
    public string? ProductId
    {
        get => _productId;
        set => SetProperty(ref _productId, value);
    }

    private string? _owner;
    public string? Owner
    {
        get => _owner;
        set => SetProperty(ref _owner, value);
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

    private DateTimeOffset? _expirationDate;
    public DateTimeOffset? ExpirationDate
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
        set => SetProperty(ref _signingKeyPath, value);
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
        set => SetProperty(ref _licenseOutputPath, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // --- Validation properties ---

    private string? _validateLicensePath;
    public string? ValidateLicensePath
    {
        get => _validateLicensePath;
        set => SetProperty(ref _validateLicensePath, value);
    }

    private string? _validatePublicKeyPath;
    public string? ValidatePublicKeyPath
    {
        get => _validatePublicKeyPath;
        set => SetProperty(ref _validatePublicKeyPath, value);
    }

    private string? _validateProductId;
    public string? ValidateProductId
    {
        get => _validateProductId;
        set => SetProperty(ref _validateProductId, value);
    }

    private string? _validateDeviceId;
    public string? ValidateDeviceId
    {
        get => _validateDeviceId;
        set => SetProperty(ref _validateDeviceId, value);
    }

    private string? _validationResult;
    public string? ValidationResult
    {
        get => _validationResult;
        set
        {
            if (SetProperty(ref _validationResult, value))
                OnPropertyChanged(nameof(HasValidationResult));
        }
    }

    private bool? _isValidationSuccess;
    public bool? IsValidationSuccess
    {
        get => _isValidationSuccess;
        set => SetProperty(ref _isValidationSuccess, value);
    }

    public bool HasValidationResult => ValidationResult is not null;

    // --- Browse commands (Generation) ---

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

    // --- Browse commands (Validation) ---

    private async Task BrowseValidateLicenseAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select License File", false, _defaultPaths.Licenses, "", null);
        if (paths.Any())
            ValidateLicensePath = paths.First();
    }

    private async Task BrowseValidatePublicKeyAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Public Key", false, _defaultPaths.Keys, "", null);
        if (paths.Any())
            ValidatePublicKeyPath = paths.First();
    }

    // --- Generate License ---

    private async Task GenerateLicenseAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(ProductId))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please enter a Product ID.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(Owner))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please enter an Owner.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(SigningKeyPath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select a signing key file.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(LicenseOutputPath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select a license output path.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

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
                builder.SetExpirationDate(ExpirationDate.Value.DateTime);

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

    // --- Validate License ---

    private async Task ValidateLicenseAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(ValidateLicensePath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select a license file.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(ValidatePublicKeyPath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select a public key file.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        IsBusy = true;
        try
        {
            await using var licenseStream = File.OpenRead(ValidateLicensePath);
            var license = await License.LoadAsync(licenseStream);

            await using var keyStream = File.OpenRead(ValidatePublicKeyPath);
            var publicKey = PemUtils.LoadKey(keyStream);

            var (isValid, errorMessage) = new LicenseService()
                .IsValid(license!, publicKey, ValidateProductId ?? "", ValidateDeviceId);

            IsValidationSuccess = isValid;
            ValidationResult = isValid
                ? "License is valid."
                : $"License is invalid: {errorMessage}";
        }
        catch (Exception ex)
        {
            IsValidationSuccess = false;
            ValidationResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
