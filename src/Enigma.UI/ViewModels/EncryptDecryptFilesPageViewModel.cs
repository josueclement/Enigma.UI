using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Controls.InfoBar;
using Carbon.Avalonia.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Cryptography.DataEncryption;
using Enigma.Cryptography.Utils;
using Enigma.UI.Models;
using Microsoft.Extensions.Options;

namespace Enigma.UI.ViewModels;

public class EncryptDecryptFilesPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly Pbkdf2DataEncryptionService _pbkdf2Service;
    private readonly Argon2DataEncryptionService _argon2Service;
    private readonly RsaDataEncryptionService _rsaService;
    private readonly MLKemDataEncryptionService _mlKemService;
    private readonly DefaultPathsOptions _defaultPaths;
    private CancellationTokenSource? _cts;

    public EncryptDecryptFilesPageViewModel(
        IFileDialogService fileDialogService,
        IInfoBarService infoBarService,
        Pbkdf2DataEncryptionService pbkdf2Service,
        Argon2DataEncryptionService argon2Service,
        RsaDataEncryptionService rsaService,
        MLKemDataEncryptionService mlKemService,
        IOptions<DefaultPathsOptions> defaultPaths)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
        _pbkdf2Service = pbkdf2Service;
        _argon2Service = argon2Service;
        _rsaService = rsaService;
        _mlKemService = mlKemService;
        _defaultPaths = defaultPaths.Value;

        BrowseKeyFileCommand = new AsyncRelayCommand(BrowseKeyFileAsync);
        BrowseInputFileCommand = new AsyncRelayCommand(BrowseInputFileAsync);
        BrowseOutputFileCommand = new AsyncRelayCommand(BrowseOutputFileAsync);
        EncryptCommand = new AsyncRelayCommand(EncryptAsync);
        DecryptCommand = new AsyncRelayCommand(DecryptAsync);
        CancelCommand = new RelayCommand(Cancel);
    }

    public AsyncRelayCommand BrowseKeyFileCommand { get; }
    public AsyncRelayCommand BrowseInputFileCommand { get; }
    public AsyncRelayCommand BrowseOutputFileCommand { get; }
    public AsyncRelayCommand EncryptCommand { get; }
    public AsyncRelayCommand DecryptCommand { get; }
    public RelayCommand CancelCommand { get; }

    public string[] EncryptionTypeOptions { get; } = ["PBKDF2", "Argon2", "RSA", "ML-KEM"];

    private int _selectedEncryptionTypeIndex;
    public int SelectedEncryptionTypeIndex
    {
        get => _selectedEncryptionTypeIndex;
        set
        {
            if (SetProperty(ref _selectedEncryptionTypeIndex, value))
            {
                OnPropertyChanged(nameof(IsPasswordBased));
                OnPropertyChanged(nameof(IsKeyBased));
                OnPropertyChanged(nameof(IsArgon2));
                OnPropertyChanged(nameof(IsPbkdf2));
            }
        }
    }

    public bool IsPasswordBased => SelectedEncryptionTypeIndex is 0 or 1;
    public bool IsKeyBased => SelectedEncryptionTypeIndex is 2 or 3;
    public bool IsArgon2 => SelectedEncryptionTypeIndex == 1;
    public bool IsPbkdf2 => SelectedEncryptionTypeIndex == 0;

    public string[] CipherOptions { get; } = ["AES-256-GCM", "Twofish-256-GCM", "Serpent-256-GCM", "Camellia-256-GCM"];

    private int _selectedCipherIndex;
    public int SelectedCipherIndex
    {
        get => _selectedCipherIndex;
        set => SetProperty(ref _selectedCipherIndex, value);
    }

    private string? _password;
    public string? Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                PasswordHasError = false;
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

    private int _iterations = 100000;
    public int Iterations
    {
        get => _iterations;
        set => SetProperty(ref _iterations, value);
    }

    private int _argon2Iterations = 10;
    public int Argon2Iterations
    {
        get => _argon2Iterations;
        set => SetProperty(ref _argon2Iterations, value);
    }

    private int _argon2Parallelism = 4;
    public int Argon2Parallelism
    {
        get => _argon2Parallelism;
        set => SetProperty(ref _argon2Parallelism, value);
    }

    private int _argon2MemoryPowOfTwo = 16;
    public int Argon2MemoryPowOfTwo
    {
        get => _argon2MemoryPowOfTwo;
        set => SetProperty(ref _argon2MemoryPowOfTwo, value);
    }

    private string? _keyFilePath;
    public string? KeyFilePath
    {
        get => _keyFilePath;
        set
        {
            if (SetProperty(ref _keyFilePath, value))
                KeyFileHasError = false;
        }
    }

    private string? _keyPassword;
    public string? KeyPassword
    {
        get => _keyPassword;
        set => SetProperty(ref _keyPassword, value);
    }

    private string? _inputFilePath;
    public string? InputFilePath
    {
        get => _inputFilePath;
        set
        {
            if (SetProperty(ref _inputFilePath, value))
                InputFileHasError = false;
        }
    }

    private string? _outputFilePath;
    public string? OutputFilePath
    {
        get => _outputFilePath;
        set
        {
            if (SetProperty(ref _outputFilePath, value))
                OutputFileHasError = false;
        }
    }

    // --- Validation error flags ---

    private bool _passwordHasError;
    public bool PasswordHasError
    {
        get => _passwordHasError;
        set => SetProperty(ref _passwordHasError, value);
    }

    private bool _keyFileHasError;
    public bool KeyFileHasError
    {
        get => _keyFileHasError;
        set => SetProperty(ref _keyFileHasError, value);
    }

    private bool _inputFileHasError;
    public bool InputFileHasError
    {
        get => _inputFileHasError;
        set => SetProperty(ref _inputFileHasError, value);
    }

    private bool _outputFileHasError;
    public bool OutputFileHasError
    {
        get => _outputFileHasError;
        set => SetProperty(ref _outputFileHasError, value);
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private Cipher GetSelectedCipher() => SelectedCipherIndex switch
    {
        0 => Cipher.Aes256Gcm,
        1 => Cipher.Twofish256Gcm,
        2 => Cipher.Serpent256Gcm,
        3 => Cipher.Camellia256Gcm,
        _ => Cipher.Aes256Gcm
    };

    private async Task BrowseKeyFileAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Key File", false, _defaultPaths.Keys, "", null);
        if (paths.Any())
            KeyFilePath = paths.First();
    }

    private async Task BrowseInputFileAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Input File", false, _defaultPaths.EncryptedFiles, "", null);
        if (paths.Any())
            InputFilePath = paths.First();
    }

    private async Task BrowseOutputFileAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Output File", _defaultPaths.EncryptedFiles, "", "", true, null);
        if (path is not null)
            OutputFilePath = path;
    }

    private bool ValidateEncryptInputs()
    {
        InputFileHasError = string.IsNullOrWhiteSpace(InputFilePath);
        OutputFileHasError = string.IsNullOrWhiteSpace(OutputFilePath);

        if (IsPasswordBased)
        {
            PasswordHasError = string.IsNullOrEmpty(Password);
        }
        else
        {
            KeyFileHasError = string.IsNullOrWhiteSpace(KeyFilePath);
        }

        return !(InputFileHasError || OutputFileHasError || PasswordHasError
            || KeyFileHasError || HasPasswordMismatch);
    }

    private async Task EncryptAsync()
    {
        if (IsBusy) return;
        if (!ValidateEncryptInputs()) return;

        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
        var progress = new Progress<int>(p => Progress = p);

        try
        {
            await using var input = File.OpenRead(InputFilePath);
            await using var output = File.Create(OutputFilePath);
            var cipher = GetSelectedCipher();

            switch (SelectedEncryptionTypeIndex)
            {
                case 0: // PBKDF2
                    await _pbkdf2Service.EncryptAsync(
                        input, output, cipher, Password!, Iterations, progress, _cts.Token);
                    break;
                case 1: // Argon2
                    await _argon2Service.EncryptAsync(
                        input, output, cipher, Encoding.UTF8.GetBytes(Password!),
                        Argon2Iterations, Argon2Parallelism, Argon2MemoryPowOfTwo,
                        progress, _cts.Token);
                    break;
                case 2: // RSA
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var publicKey = PemUtils.LoadKey(keyStream);
                    await _rsaService.EncryptAsync(
                        input, output, cipher, publicKey, progress, _cts.Token);
                    break;
                }
                case 3: // ML-KEM
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var publicKey = PemUtils.LoadKey(keyStream);
                    await _mlKemService.EncryptAsync(
                        input, output, cipher, publicKey, progress, _cts.Token);
                    break;
                }
            }

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "File encrypted successfully.";
                bar.Severity = InfoBarSeverity.Success;
            });
        }
        catch (OperationCanceledException)
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Cancelled";
                bar.Message = "Encryption was cancelled.";
                bar.Severity = InfoBarSeverity.Info;
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
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool ValidateDecryptInputs()
    {
        InputFileHasError = string.IsNullOrWhiteSpace(InputFilePath);
        OutputFileHasError = string.IsNullOrWhiteSpace(OutputFilePath);

        if (IsPasswordBased)
            PasswordHasError = string.IsNullOrEmpty(Password);
        else
            KeyFileHasError = string.IsNullOrWhiteSpace(KeyFilePath);

        return !(InputFileHasError || OutputFileHasError || PasswordHasError || KeyFileHasError);
    }

    private async Task DecryptAsync()
    {
        if (IsBusy) return;
        if (!ValidateDecryptInputs()) return;

        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
        var progress = new Progress<int>(p => Progress = p);

        try
        {
            await using var input = File.OpenRead(InputFilePath);
            await using var output = File.Create(OutputFilePath);

            switch (SelectedEncryptionTypeIndex)
            {
                case 0: // PBKDF2
                    await _pbkdf2Service.DecryptAsync(
                        input, output, Password!, progress, _cts.Token);
                    break;
                case 1: // Argon2
                    await _argon2Service.DecryptAsync(
                        input, output, Encoding.UTF8.GetBytes(Password!),
                        progress, _cts.Token);
                    break;
                case 2: // RSA
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var privateKey = !string.IsNullOrEmpty(KeyPassword)
                        ? PemUtils.LoadPrivateKey(keyStream, KeyPassword)
                        : PemUtils.LoadKey(keyStream);
                    await _rsaService.DecryptAsync(
                        input, output, privateKey, progress, _cts.Token);
                    break;
                }
                case 3: // ML-KEM
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var privateKey = !string.IsNullOrEmpty(KeyPassword)
                        ? PemUtils.LoadPrivateKey(keyStream, KeyPassword)
                        : PemUtils.LoadKey(keyStream);
                    await _mlKemService.DecryptAsync(
                        input, output, privateKey, progress, _cts.Token);
                    break;
                }
            }

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Success";
                bar.Message = "File decrypted successfully.";
                bar.Severity = InfoBarSeverity.Success;
            });
        }
        catch (OperationCanceledException)
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Cancelled";
                bar.Message = "Decryption was cancelled.";
                bar.Severity = InfoBarSeverity.Info;
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
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }
}
