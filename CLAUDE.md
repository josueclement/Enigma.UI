# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
dotnet build src/Enigma.UI/Enigma.UI.csproj

# Run
dotnet run --project src/Enigma.UI/Enigma.UI.csproj

# Publish release
dotnet publish -c Release src/Enigma.UI/Enigma.UI.csproj
```

No test projects exist in this repository. Solution file is `Enigma.UI.slnx` (modern slnx format).

## Architecture

**Avalonia 11 desktop app** (.NET 10, C# 14) using **MVVM** with CommunityToolkit.Mvvm and a custom Carbon design system (`Carbon.Avalonia.Desktop`).

### Key patterns

- **DI via Microsoft.Extensions.Hosting** — services registered in `ServiceCollectionExtensions.cs` using C# 14 extension blocks. `Program.cs` builds the host, starts it, then launches Avalonia.
- **Navigation** — `MainWindowViewModel` configures a `PageFactory` that resolves views (transient) and ViewModels (singleton) from DI. Pages are registered as `NavigationItem` entries with Phosphor icons.
- **ViewModels** extend `ObservableObject`. Properties use `SetProperty()` for change notification.

### Conventions

- Do not use `[RelayCommand]` — use explicit `RelayCommand`/`AsyncRelayCommand` properties initialized in the constructor.
- Do not use `[ObservableProperty]` — use manual properties with `SetProperty()`.
- **Compiled bindings** are enabled by default (`AvaloniaUseCompiledBindingsByDefault=true`).
- **Carbon.Avalonia.Desktop services**: `IFileDialogService`, `IFolderDialogService`, `INavigationService`, `IContentDialogService`, `IInfoBarService`, `IOverlayService` — all registered as singletons.

### Source layout

All source lives under `src/Enigma.UI/`:
- `ViewModels/` — one ViewModel per page plus `MainWindowViewModel`
- `Views/` — `.axaml` markup + `.axaml.cs` code-behind; `MainWindow` hosts the NavigationView

### Cryptography libraries (NuGet)

- **Enigma.Cryptography** — RSA key generation, PEM utilities
- **Enigma.Cryptography.DataEncryption** — PBKDF2, Argon2, RSA, ML-KEM encryption with AES/Twofish/Serpent/Camellia ciphers (all 256-bit GCM)
- **Enigma.LicenseManager** — license generation/validation with RSA and ML-DSA signing

### Theme & styling

Fluent Dark theme with Carbon Avalonia overlay. Inter font. Phosphor Icons (regular).

## Platform notes

- `Program.cs` swallows `TaskCanceledException` from `Tmds.DBus` on Linux for clean shutdown.
- `Avalonia.Diagnostics` is only included in Debug builds.
- `App.axaml.cs` has a fallback `ServiceProvider` for the XAML designer when `AppHost` is null.
