using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Wyne.Source;

public partial class GameObject
{
  public string Id { get; set; }
  public string Name { get; set; }
  public string Publisher { get; set; }
  public string Version { get; set; }
  public List<string> Languages { get; set; } = [];
  public string Runner { get; set; }
  public string RunnerCommand { get; set; }
  public string Exe { get; set; }
  public List<string> Tags { get; set; } = [];
  public string Description { get; set; }
  public string InstallPath { get; set; }
  public string GameCoverPath { get; set; }

  // -------------------------------------------------
  public static GameObject LoadFromFolder(string folderPath)
  {
    string infoPath = Path.Combine(folderPath, "Info.json");
    if (!File.Exists(infoPath))
    {
      GD.PrintErr($"[WARN] No Info.json found in {folderPath}");
      return null;
    }

    try
    {
      string raw = File.ReadAllText(infoPath);
      var parsed = Json.ParseString(raw);

      if (parsed.VariantType != Variant.Type.Dictionary)
      {
        GD.PrintErr($"[ERROR] Info.json is not a dictionary in {folderPath}");
        return null;
      }

      var dict = parsed.AsGodotDictionary();

      var game = new GameObject
      {
        Id = dict.TryGetValue("id", out var id) ? id.AsString() : Path.GetFileName(folderPath),
        Name = dict.TryGetValue("name", out var name) ? name.AsString() : Path.GetFileName(folderPath),
        Publisher = dict.TryGetValue("publisher", out var pub) ? pub.AsString() : "Unknown",
        Version = dict.TryGetValue("version", out var ver) ? ver.AsString() : "1.0",
        Runner = dict.TryGetValue("runner", out var run) ? run.AsString() : "",
        RunnerCommand = dict.TryGetValue("runcmd", out var runcmd) ? runcmd.AsString() : "",
        Exe = dict.TryGetValue("exe", out var exe) ? exe.AsString() : "",
        Description = dict.TryGetValue("description", out var desc) ? desc.AsString() : "",
        GameCoverPath = dict.TryGetValue("cover", out var cov) ? cov.AsString() : "",
        InstallPath = folderPath
      };

      game.Runner = game.Runner
        .Replace("$WYNE_PREFIX", WyneSystem.WYNE_PREFIX)
        .Replace("$WYNE_SYSBIN", WyneSystem.WYNE_SYSBIN);

      if (dict.TryGetValue("languages", out var langs) && langs.VariantType == Variant.Type.Array)
      {
        foreach (var l in langs.AsGodotArray<Variant>())
          game.Languages.Add(l.AsString());
      }

      if (dict.TryGetValue("tags", out var tags) && tags.VariantType == Variant.Type.Array)
      {
        foreach (var t in tags.AsGodotArray<Variant>())
          game.Tags.Add(t.AsString());
      }

      GD.Print($"[INFO] Loaded game: {game.Name} ({game.Version}) from {folderPath}");
      return game;
    }
    catch (Exception e)
    {
      GD.PrintErr($"[ERROR] Failed to load game from {folderPath}: {e.Message}");
      return null;
    }
  }

  // -------------------------------------------------
  public string GetExecutablePath()
  {
    if (string.IsNullOrEmpty(Exe))
      return "";
    return Path.Combine(InstallPath, Exe);
  }

  public ImageTexture GetImage()
  {
    if (File.Exists($"{InstallPath}/{GameCoverPath}"))
      return ImageTexture.CreateFromImage(Image.LoadFromFile($"{InstallPath}/{GameCoverPath}"));
    
    return new();
  }
}