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

namespace Enigma.UI.ViewModels;

public partial class EncryptDecryptFilesPageViewModel : ObservableObject
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IInfoBarService _infoBarService;
    private CancellationTokenSource? _cts;

    public EncryptDecryptFilesPageViewModel(IFileDialogService fileDialogService, IInfoBarService infoBarService)
    {
        _fileDialogService = fileDialogService;
        _infoBarService = infoBarService;
    }

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
        set => SetProperty(ref _password, value);
    }

    private string? _confirmPassword;
    public string? ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

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
        set => SetProperty(ref _keyFilePath, value);
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
        set => SetProperty(ref _inputFilePath, value);
    }

    private string? _outputFilePath;
    public string? OutputFilePath
    {
        get => _outputFilePath;
        set => SetProperty(ref _outputFilePath, value);
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

    [RelayCommand]
    private async Task BrowseKeyFileAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Key File", false, "", "", null);
        if (paths.Any())
            KeyFilePath = paths.First();
    }

    [RelayCommand]
    private async Task BrowseInputFileAsync()
    {
        var paths = await _fileDialogService.ShowOpenFileDialogAsync(
            "Select Input File", false, "", "", null);
        if (paths.Any())
            InputFilePath = paths.First();
    }

    [RelayCommand]
    private async Task BrowseOutputFileAsync()
    {
        var path = await _fileDialogService.ShowSaveFileDialogAsync(
            "Save Output File", "", "", "", true, null);
        if (path is not null)
            OutputFilePath = path;
    }

    [RelayCommand]
    private async Task EncryptAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(InputFilePath) || string.IsNullOrWhiteSpace(OutputFilePath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select input and output file paths.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (IsPasswordBased)
        {
            if (string.IsNullOrEmpty(Password))
            {
                await _infoBarService.ShowAsync(bar =>
                {
                    bar.Title = "Validation";
                    bar.Message = "Please enter a password.";
                    bar.Severity = InfoBarSeverity.Warning;
                });
                return;
            }

            if (Password != ConfirmPassword)
            {
                await _infoBarService.ShowAsync(bar =>
                {
                    bar.Title = "Validation";
                    bar.Message = "Passwords do not match.";
                    bar.Severity = InfoBarSeverity.Warning;
                });
                return;
            }
        }
        else if (string.IsNullOrWhiteSpace(KeyFilePath))
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
                    await new Pbkdf2DataEncryptionService().EncryptAsync(
                        input, output, cipher, Password!, Iterations, progress, _cts.Token);
                    break;
                case 1: // Argon2
                    await new Argon2DataEncryptionService().EncryptAsync(
                        input, output, cipher, Encoding.UTF8.GetBytes(Password!),
                        Argon2Iterations, Argon2Parallelism, Argon2MemoryPowOfTwo,
                        progress, _cts.Token);
                    break;
                case 2: // RSA
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var publicKey = PemUtils.LoadKey(keyStream);
                    await new RsaDataEncryptionService().EncryptAsync(
                        input, output, cipher, publicKey, progress, _cts.Token);
                    break;
                }
                case 3: // ML-KEM
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var publicKey = PemUtils.LoadKey(keyStream);
                    await new MLKemDataEncryptionService().EncryptAsync(
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

    [RelayCommand]
    private async Task DecryptAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(InputFilePath) || string.IsNullOrWhiteSpace(OutputFilePath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select input and output file paths.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (IsPasswordBased && string.IsNullOrEmpty(Password))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please enter a password.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

        if (IsKeyBased && string.IsNullOrWhiteSpace(KeyFilePath))
        {
            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Validation";
                bar.Message = "Please select a private key file.";
                bar.Severity = InfoBarSeverity.Warning;
            });
            return;
        }

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
                    await new Pbkdf2DataEncryptionService().DecryptAsync(
                        input, output, Password!, progress, _cts.Token);
                    break;
                case 1: // Argon2
                    await new Argon2DataEncryptionService().DecryptAsync(
                        input, output, Encoding.UTF8.GetBytes(Password!),
                        progress, _cts.Token);
                    break;
                case 2: // RSA
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var privateKey = !string.IsNullOrEmpty(KeyPassword)
                        ? PemUtils.LoadPrivateKey(keyStream, KeyPassword)
                        : PemUtils.LoadKey(keyStream);
                    await new RsaDataEncryptionService().DecryptAsync(
                        input, output, privateKey, progress, _cts.Token);
                    break;
                }
                case 3: // ML-KEM
                {
                    await using var keyStream = File.OpenRead(KeyFilePath!);
                    var privateKey = !string.IsNullOrEmpty(KeyPassword)
                        ? PemUtils.LoadPrivateKey(keyStream, KeyPassword)
                        : PemUtils.LoadKey(keyStream);
                    await new MLKemDataEncryptionService().DecryptAsync(
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

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
