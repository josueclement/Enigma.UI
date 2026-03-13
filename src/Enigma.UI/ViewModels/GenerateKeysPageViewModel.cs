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

namespace Enigma.UI.ViewModels;

public partial class GenerateKeysPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;

    public GenerateKeysPageViewModel(IFileDialogService fileDialogService, IInfoBarService infoBarService)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
        UpdateParameterOptions();
    }

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
        set => SetProperty(ref _password, value);
    }

    private string? _confirmPassword;
    public string? ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    private string? _publicKeyPath;
    public string? PublicKeyPath
    {
        get => _publicKeyPath;
        set => SetProperty(ref _publicKeyPath, value);
    }

    private string? _privateKeyPath;
    public string? PrivateKeyPath
    {
        get => _privateKeyPath;
        set => SetProperty(ref _privateKeyPath, value);
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
            0 => new[] { "2048", "3072", "4096" },
            1 => new[] { "ML-KEM-512", "ML-KEM-768", "ML-KEM-1024" },
            2 => new[] { "ML-DSA-44", "ML-DSA-65", "ML-DSA-87" },
            _ => Array.Empty<string>()
        };
        foreach (var opt in options)
            ParameterOptions.Add(opt);
        SelectedParameterIndex = 0;
    }

    [RelayCommand]
    private async Task BrowsePublicKeyPathAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Public Key", "", "public.pem", ".pem", true, null);
        if (path is not null)
            PublicKeyPath = path;
    }

    [RelayCommand]
    private async Task BrowsePrivateKeyPathAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Private Key", "", "private.pem", ".pem", true, null);
        if (path is not null)
            PrivateKeyPath = path;
    }

    [RelayCommand]
    private async Task GenerateKeysAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(PublicKeyPath) || string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select output file paths for both keys.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword)
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Passwords do not match.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        IsBusy = true;
        try
        {
            var keyPair = await Task.Run(() => SelectedAlgorithmIndex switch
            {
                0 => new PublicKeyServiceFactory().CreateRsaService()
                    .GenerateKeyPair(int.Parse(ParameterOptions[SelectedParameterIndex])),
                1 => SelectedParameterIndex switch
                {
                    0 => new MLKemServiceFactory().CreateKem512().GenerateKeyPair(),
                    1 => new MLKemServiceFactory().CreateKem768().GenerateKeyPair(),
                    _ => new MLKemServiceFactory().CreateKem1024().GenerateKeyPair()
                },
                2 => SelectedParameterIndex switch
                {
                    0 => new MLDsaServiceFactory().CreateDsa44Service().GenerateKeyPair(),
                    1 => new MLDsaServiceFactory().CreateDsa65Service().GenerateKeyPair(),
                    _ => new MLDsaServiceFactory().CreateDsa87Service().GenerateKeyPair()
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

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "Keys generated successfully.";
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
