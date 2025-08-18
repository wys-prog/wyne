using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Wyne.Source
{
  public static class Globals
  {
    public static List<GameObject> Games = new();

    public static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static readonly string osName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
                                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "OSX"
                                    : "Linux";

    public static readonly Architecture OsArchitecture = RuntimeInformation.OSArchitecture;
    public static readonly Architecture ProcessArchitecture = RuntimeInformation.ProcessArchitecture;

    public static readonly Dictionary<string, string> Variables = BuildVariables();

    private static Dictionary<string, string> BuildVariables()
    {
      var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      // Basic OS / user info
      dict["OS"] = osName.ToUpperInvariant();
      dict["HOME"] = Environment.GetEnvironmentVariable("HOME") ?? "";
      dict["USERPROFILE"] = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
      dict["USER"] = Environment.GetEnvironmentVariable("USER") ?? Environment.GetEnvironmentVariable("USERNAME") ?? "";
      dict["SHELL"] = Environment.GetEnvironmentVariable("SHELL") ?? "";
      dict["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "";

      // Architecture
      dict["OS_ARCH"] = OsArchitecture.ToString();
      dict["PROCESS_ARCH"] = ProcessArchitecture.ToString();
      dict["IS_ARM"] = (OsArchitecture == Architecture.Arm || OsArchitecture == Architecture.Arm64).ToString();
      dict["IS_X64"] = (OsArchitecture == Architecture.X64).ToString();
      dict["IS_X86"] = (OsArchitecture == Architecture.X86).ToString();

      // WSL detection (Linux only)
      dict["IS_WSL"] = IsWsl().ToString();

      // Common package managers / important executables (platform-specific fallbacks included)
      if (isWindows)
      {
        dict["CHOCOLATEY"] = TryFindExecutable("choco") ?? "";
        dict["SCOOP"] = TryFindExecutable("scoop") ?? "";
        dict["POWERSHELL"] = TryFindExecutable("pwsh") ?? TryFindExecutable("powershell") ?? "";
        // Windows might have wine installed under some shim - likely not native, but we try
        dict["WINE"] = TryFindExecutable("wine") ?? "";
      }
      else // macOS / Linux
      {
        // Wine
        dict["WINE"] = TryFindExecutable("wine") ?? FindCommonUnixPaths(new[] { "/usr/bin/wine", "/usr/local/bin/wine", "/opt/homebrew/bin/wine", "/opt/wine/bin/wine" }) ?? "";
        dict["WINE64"] = TryFindExecutable("wine64") ?? "";

        // Homebrew (macOS common locations)
        dict["BREW"] = TryFindExecutable("brew") ?? FindCommonUnixPaths(new[] { "/opt/homebrew/bin/brew", "/usr/local/bin/brew" }) ?? "";

        // MacPorts
        dict["PORT"] = TryFindExecutable("port") ?? "";

        // Linux package managers
        dict["APT"] = TryFindExecutable("apt") ?? TryFindExecutable("apt-get") ?? "";
        dict["DNF"] = TryFindExecutable("dnf") ?? "";
        dict["PACMAN"] = TryFindExecutable("pacman") ?? "";
        dict["ZYPPER"] = TryFindExecutable("zypper") ?? "";
        dict["FLATPAK"] = TryFindExecutable("flatpak") ?? "";
        dict["SNAP"] = TryFindExecutable("snap") ?? "";
        dict["APTITUDE"] = TryFindExecutable("aptitude") ?? "";
      }

      // Generic helpers/tools
      dict["CURL"] = TryFindExecutable("curl") ?? "";
      dict["WGET"] = TryFindExecutable("wget") ?? "";
      dict["GIT"] = TryFindExecutable("git") ?? "";

      // Wine-specific env hints
      dict["WINEPREFIX"] = Environment.GetEnvironmentVariable("WINEPREFIX") ?? Path.Combine(dict["HOME"], ".wine");
      dict["DEFAULT_WINEPREFIX_EXISTS"] = (Directory.Exists(dict["WINEPREFIX"]) ? "true" : "false");

      // Short summary entry for quick display/debug
      dict["SUMMARY"] = $"{dict["OS"]} | OS_ARCH={dict["OS_ARCH"]} | PROCESS_ARCH={dict["PROCESS_ARCH"]} | WINE={(string.IsNullOrEmpty(dict["WINE"]) ? "no" : dict["WINE"])} | BREW={(string.IsNullOrEmpty(dict["BREW"]) ? "no" : dict["BREW"])}";

      return dict;
    }

    /// <summary>
    /// Try to find an executable using system tools ("which" on Unix, "where" on Windows).
    /// Returns the full path if found, or null.
    /// </summary>
    private static string TryFindExecutable(string exeName)
    {
      if (string.IsNullOrWhiteSpace(exeName))
        return null;

      try
      {
        if (isWindows)
        {
          // 'where' returns multiple lines possibly; we take first non-empty
          string output = RunProcessCaptureOutput("where", exeName);
          if (!string.IsNullOrWhiteSpace(output))
          {
            var first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (File.Exists(first)) return first;
          }
        }
        else
        {
          string output = RunProcessCaptureOutput("which", exeName);
          if (!string.IsNullOrWhiteSpace(output))
          {
            var first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (File.Exists(first)) return first;
          }
        }
      }
      catch
      {
        // ignore errors, will fallback to common paths
      }

      return null;
    }

    /// <summary>
    /// Check a list of common absolute paths and return the first existing one (or null).
    /// </summary>
    private static string FindCommonUnixPaths(string[] paths)
    {
      foreach (var p in paths)
      {
        try
        {
          if (!string.IsNullOrEmpty(p) && File.Exists(p))
            return p;
        }
        catch { }
      }
      return null;
    }

    private static string RunProcessCaptureOutput(string command, string arguments)
    {
      var psi = new ProcessStartInfo
      {
        FileName = command,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var proc = Process.Start(psi);
      if (proc == null) return null;
      string output = proc.StandardOutput.ReadToEnd();
      proc.WaitForExit(3000); // short timeout
      return output;
    }

    private static bool IsWsl()
    {
      try
      {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
          // check /proc/version for "Microsoft" (common WSL indicator)
          var path = "/proc/version";
          if (File.Exists(path))
          {
            var txt = File.ReadAllText(path);
            if (txt.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
              return true;
          }

          // also environment variable WSLENV or WSL_DISTRO_NAME
          if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSLENV")) ||
              !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME")))
            return true;
        }
      }
      catch { }
      return false;
    }
  }
}
