using System;
using System.IO;

namespace Enigma.UI;

public static class ConfigurationSetup
{
    // Current: per-user config at ~/.config/EnigmaUI/ (Linux & Windows).
    // For a system-wide config (all users), two options:
    //   1. CommonApplicationData — cross-platform, no runtime check:
    //        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
    //        Linux: /usr/share/EnigmaUI   |   Windows: C:\ProgramData\EnigmaUI
    //   2. /etc on Linux (traditional config location) with runtime check:
    //        OperatingSystem.IsLinux()
    //            ? Path.Combine("/etc", "EnigmaUI")
    //            : Path.Combine(Environment.GetFolderPath(
    //                  Environment.SpecialFolder.CommonApplicationData), "EnigmaUI")
    public static string GetConfigFilePath()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "EnigmaUI");
        return Path.Combine(configDir, "config.json");
    }

    public static void EnsureConfigFileExists(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, """
                {
                  "DefaultPaths": {
                    "Keys": "",
                    "Licenses": "",
                    "EncryptedFiles": ""
                  }
                }
                """);
        }
    }
}
