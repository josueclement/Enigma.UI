using System;
using System.IO;

namespace Enigma.UI;

public static class ConfigurationSetup
{
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
