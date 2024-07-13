using System;
using Microsoft.Win32;

namespace Version;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Installed .NET Framework versions:");
        CheckNetFrameworkVersion();
    }

    static void CheckNetFrameworkVersion()
    {
        string[] versions = { "4.5", "4.5.1", "4.5.2", "4.6", "4.6.1", "4.6.2", "4.7", "4.7.1", "4.7.2", "4.8", "4.8.1" };
        string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

        using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
        {
            if (ndpKey != null && ndpKey.GetValue("Release") != null)
            {
                int releaseKey = (int)ndpKey.GetValue("Release");
                Console.WriteLine($".NET Framework Version: {CheckFor45PlusVersion(releaseKey)}");
            }
            else
            {
                Console.WriteLine(".NET Framework Version 4.5 or later is not detected.");
            }
        }

        // Kiểm tra các phiên bản cũ hơn
        foreach (string version in versions)
        {
            if (IsNetFrameworkInstalled(version))
            {
                Console.WriteLine($".NET Framework {version} is installed.");
            }
        }
    }

    static string CheckFor45PlusVersion(int releaseKey)
    {
        if (releaseKey >= 533320)
            return "4.8.1 or later";
        if (releaseKey >= 528040)
            return "4.8";
        if (releaseKey >= 461808)
            return "4.7.2";
        if (releaseKey >= 461308)
            return "4.7.1";
        if (releaseKey >= 460798)
            return "4.7";
        if (releaseKey >= 394802)
            return "4.6.2";
        if (releaseKey >= 394254)
            return "4.6.1";
        if (releaseKey >= 393295)
            return "4.6";
        if (releaseKey >= 379893)
            return "4.5.2";
        if (releaseKey >= 378675)
            return "4.5.1";
        if (releaseKey >= 378389)
            return "4.5";
        return "No 4.5 or later version detected";
    }

    static bool IsNetFrameworkInstalled(string version)
    {
        string subkey = $@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v{version}";
        using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
        {
            return ndpKey != null && ndpKey.GetValue("Install") != null;
        }
    }
}
