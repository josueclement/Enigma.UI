using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Controls.InfoBar;
using Carbon.Avalonia.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Cryptography.PQC;
using Enigma.Cryptography.PublicKey;
using Enigma.Cryptography.Utils;
using Enigma.UI.Controls;
using Enigma.UI.Models;
using Microsoft.Extensions.Options;

namespace Enigma.UI.ViewModels;

public class GenerateKeysPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly IOverlayService _overlayService;
    private readonly PublicKeyServiceFactory _publicKeyServiceFactory;
    private readonly MLKemServiceFactory _mlKemServiceFactory;
    private readonly MLDsaServiceFactory _mlDsaServiceFactory;
    private readonly DefaultPathsOptions _defaultPaths;

    public GenerateKeysPageViewModel(
        IFileDialogService fileDialogService,
        IInfoBarService infoBarService,
        IOverlayService overlayService,
        PublicKeyServiceFactory publicKeyServiceFactory,
        MLKemServiceFactory mlKemServiceFactory,
        MLDsaServiceFactory mlDsaServiceFactory,
        IOptions<DefaultPathsOptions> defaultPaths)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
        _overlayService = overlayService;
        _publicKeyServiceFactory = publicKeyServiceFactory;
        _mlKemServiceFactory = mlKemServiceFactory;
        _mlDsaServiceFactory = mlDsaServiceFactory;
        _defaultPaths = defaultPaths.Value;
        UpdateParameterOptions();

        BrowsePublicKeyPathCommand = new AsyncRelayCommand(BrowsePublicKeyPathAsync);
        BrowsePrivateKeyPathCommand = new AsyncRelayCommand(BrowsePrivateKeyPathAsync);
        GenerateKeysCommand = new AsyncRelayCommand(GenerateKeysAsync);
    }

    public AsyncRelayCommand BrowsePublicKeyPathCommand { get; }
    public AsyncRelayCommand BrowsePrivateKeyPathCommand { get; }
    public AsyncRelayCommand GenerateKeysCommand { get; }

    public string[] AlgorithmOptions { get; } = ["RSA", "ML-KEM", "ML-DSA"];

    private int _selectedAlgorithmIndex;
    public int SelectedAlgorithmIndex
    {
        get => _selectedAlgorithmIndex;
        set
        {
            if (SetProperty(ref _selectedAlgorithmIndex, value))
                UpdateParameterOptions();
        }
    }

    public ObservableCollection<string> ParameterOptions { get; } = [];

    private int _selectedParameterIndex;
    public int SelectedParameterIndex
    {
        get => _selectedParameterIndex;
        set => SetProperty(ref _selectedParameterIndex, value);
    }

    private string? _password;
    public string? Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnPropertyChanged(nameof(HasPasswordMismatch));
                OnPropertyChanged(nameof(PasswordMismatchMessage));
            }
        }
    }

    private string? _confirmPassword;
    public string? ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                OnPropertyChanged(nameof(HasPasswordMismatch));
                OnPropertyChanged(nameof(PasswordMismatchMessage));
            }
        }
    }

    public bool HasPasswordMismatch =>
        !string.IsNullOrEmpty(Password) &&
        !string.IsNullOrEmpty(ConfirmPassword) &&
        Password != ConfirmPassword;

    public string? PasswordMismatchMessage =>
        HasPasswordMismatch ? "Passwords do not match" : null;

    private string? _publicKeyPath;
    public string? PublicKeyPath
    {
        get => _publicKeyPath;
        set
        {
            if (SetProperty(ref _publicKeyPath, value))
                PublicKeyPathHasError = false;
        }
    }

    private string? _privateKeyPath;
    public string? PrivateKeyPath
    {
        get => _privateKeyPath;
        set
        {
            if (SetProperty(ref _privateKeyPath, value))
                PrivateKeyPathHasError = false;
        }
    }

    // --- Validation error flags ---

    private bool _publicKeyPathHasError;
    public bool PublicKeyPathHasError
    {
        get => _publicKeyPathHasError;
        set => SetProperty(ref _publicKeyPathHasError, value);
    }

    private bool _privateKeyPathHasError;
    public bool PrivateKeyPathHasError
    {
        get => _privateKeyPathHasError;
        set => SetProperty(ref _privateKeyPathHasError, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private void UpdateParameterOptions()
    {
        ParameterOptions.Clear();
        var options = SelectedAlgorithmIndex switch
        {
            0 => new[] { "2048", "3072", "4096", "8192" },
            1 => new[] { "ML-KEM-512", "ML-KEM-768", "ML-KEM-1024" },
            2 => new[] { "ML-DSA-44", "ML-DSA-65", "ML-DSA-87" },
            _ => Array.Empty<string>()
        };
        foreach (var opt in options)
            ParameterOptions.Add(opt);
        SelectedParameterIndex = 0;
    }

    private async Task BrowsePublicKeyPathAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Public Key", _defaultPaths.Keys, "public.pem", ".pem", true, null);
        if (path is not null)
            PublicKeyPath = path;
    }

    private async Task BrowsePrivateKeyPathAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Private Key", _defaultPaths.Keys, "private.pem", ".pem", true, null);
        if (path is not null)
            PrivateKeyPath = path;
    }

    private bool ValidateInputs()
    {
        PublicKeyPathHasError = string.IsNullOrWhiteSpace(PublicKeyPath);
        PrivateKeyPathHasError = string.IsNullOrWhiteSpace(PrivateKeyPath);
        return !(PublicKeyPathHasError || PrivateKeyPathHasError || HasPasswordMismatch);
    }

    private async Task GenerateKeysAsync()
    {
        if (IsBusy) return;
        if (!ValidateInputs()) return;

        IsBusy = true;
        await _overlayService.ShowAsync(new ProgressOverlayCard
        {
            Title = "Generating Keys",
            Message = "This may take a moment...",
            IsIndeterminate = true
        });
        try
        {
            var keyPair = await Task.Run(() => SelectedAlgorithmIndex switch
            {
                0 => _publicKeyServiceFactory.CreateRsaService()
                    .GenerateKeyPair(int.Parse(ParameterOptions[SelectedParameterIndex])),
                1 => SelectedParameterIndex switch
                {
                    0 => _mlKemServiceFactory.CreateKem512().GenerateKeyPair(),
                    1 => _mlKemServiceFactory.CreateKem768().GenerateKeyPair(),
                    _ => _mlKemServiceFactory.CreateKem1024().GenerateKeyPair()
                },
                2 => SelectedParameterIndex switch
                {
                    0 => _mlDsaServiceFactory.CreateDsa44Service().GenerateKeyPair(),
                    1 => _mlDsaServiceFactory.CreateDsa65Service().GenerateKeyPair(),
                    _ => _mlDsaServiceFactory.CreateDsa87Service().GenerateKeyPair()
                },
                _ => throw new InvalidOperationException("Invalid algorithm selection")
            });

            await using (var pubStream = File.Create(PublicKeyPath))
                PemUtils.SaveKey(keyPair.Public, pubStream);

            if (!string.IsNullOrEmpty(Password))
            {
                await using var privStream = File.Create(PrivateKeyPath);
                PemUtils.SavePrivateKey(keyPair.Private, privStream, Password, "AES-256-CBC");
            }
            else
            {
                await using var privStream = File.Create(PrivateKeyPath);
                PemUtils.SaveKey(keyPair.Private, privStream);
            }

            await _overlayService.HideAsync();
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "Keys generated successfully.";
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
