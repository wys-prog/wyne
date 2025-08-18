using Godot;
using System;
using System.Diagnostics;

namespace Wyne.Source;

public partial class GameExecutor : RichTextLabel
{
  private void Println(string str)
  {
    Text += str + "\n";
  }

  private void PrintlnDef(string str)
  {
    CallDeferred("append_text", str + "\n");
  }

  private static string Color(string name, string on) => $"[color={name}]{on}[/color]";

  public void Execute(string cmd)
  {
    Println(Color("blue", $"{cmd}"));

    var psi = new ProcessStartInfo
    {
      FileName = Globals.osName == "Windows" ? "cmd.exe" :
                Globals.osName == "OSX" ? "/bin/zsh" :
                                      "/bin/bash",

      Arguments = Globals.isWindows ? $"/C {cmd}" :
                  Globals.osName == "OSX" ? $"-c \"{cmd}\"" :
                                    $"-c \"{cmd}\"",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    var process = new Process { StartInfo = psi };
    string path = System.Environment.GetEnvironmentVariable("PATH") ?? "";

    if (Globals.osName == "OSX")
    {
      path += ":/usr/local/bin:/usr/local/sbin:/opt/homebrew/bin:/opt/homebrew/sbin";
    }
    else if (Globals.osName == "Linux")
    {
      path += ":/usr/bin:/usr/local/bin:/usr/sbin:/usr/local/sbin";
    }

    psi.EnvironmentVariables["PATH"] = path;

    process.OutputDataReceived += (sender, e) =>
    {
      if (e.Data != null) PrintlnDef(e.Data);
    };

    process.ErrorDataReceived += (sender, e) =>
    {
      if (e.Data != null) PrintlnDef(Color("red", e.Data));
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
  }
}
