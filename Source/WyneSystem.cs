using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Wyne.Source;

public enum SettingType
{
  LineEdit,
  CheckBox,
  ToggleButton,
  SpinBox,
  StringArray
}

public class SettingDefinition
{
  public string Key { get; set; }
  public string Label { get; set; }
  public SettingType Type { get; set; }
  public object DefaultValue { get; set; }

  public override string ToString()
  {
    return
      "{" +
      $"\"key\":\"{Key}\", \"label\":\"{Label}\", \"type\":\"{Type}\", \"default\":\"{DefaultValue}\"" +
      "}";
  }
}

public partial class WyneSystem : Control
{
  public static string GetWynePath()
  {
    string basePath;
    if (OS.HasFeature("windows"))
      basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
    else if (OS.HasFeature("osx"))
      basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal)
          + "/Library/Application Support";
    else
      basePath = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME")
          ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/.local/share";

    return Path.Combine(basePath, "Wyne");
  }

  public static readonly string WYNE_PREFIX = GetWynePath() + "/Application Data/";
  public static readonly string WYNE_GAMES = WYNE_PREFIX + "Games";
  public static readonly string WYNE_SETTINGS = WYNE_PREFIX + "Settings";
  public static readonly string WYNE_SYSTEM = WYNE_PREFIX + "System";
  public static readonly string WYNE_SYSBIN = WYNE_SYSTEM + "/Binary";
  public static readonly string WYNE_EXEINFO = WYNE_SYSTEM + "/Data/WyneExeInfo";

  private VBoxContainer Box;
  private static readonly Dictionary<string, object> Values = [];
  private static List<SettingDefinition> Settings = [];

  public enum LoadMode
  {
    Append,
    Overwrite
  }

  // -------------------------------------------------

  private List<SettingDefinition> LoadSettingsFromJson(string jsonPath, LoadMode mode = LoadMode.Overwrite)
  {
    if (!Godot.FileAccess.FileExists(jsonPath))
    {
      GD.PrintErr($"[ERROR] Settings file not found: {jsonPath}");
      return [];
    }

    string raw = Godot.FileAccess.Open(jsonPath, Godot.FileAccess.ModeFlags.Read).GetAsText();
    var parsed = Json.ParseString(raw);

    if (parsed.VariantType != Variant.Type.Array)
    {
      GD.PrintErr("[ERROR] JSON root is not an array!");
      return [];
    }

    var newDefs = new List<SettingDefinition>();

    foreach (var entry in parsed.AsGodotArray<Variant>())
    {
      if (entry.VariantType != Variant.Type.Dictionary) continue;
      var dict = entry.AsGodotDictionary();

      try
      {
        object defaultValue = "";
        if (dict.ContainsKey("default"))
        {
          var def = dict["default"];

          if (def.VariantType == Variant.Type.Array)
          {
            List<string> arr = [];
            foreach (var v in def.AsGodotArray<Variant>())
              arr.Add(v.AsString().Trim());
            defaultValue = arr;
          }

          else if (def.VariantType == Variant.Type.String && def.AsString().Contains(","))
          {
            var parts = def.AsString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            List<string> arr = new();
            foreach (var p in parts) arr.Add(p.Trim());
            defaultValue = arr;
          }
          else
          {
            defaultValue = def;
          }
        }

        var defObj = new SettingDefinition
        {
          Key = dict["key"].AsString(),
          Label = dict.ContainsKey("label") ? dict["label"].AsString() : dict["key"].AsString(),
          Type = Enum.TryParse(dict["type"].AsString(), out SettingType t) ? t : SettingType.LineEdit,
          DefaultValue = defaultValue
        };

        newDefs.Add(defObj);
        GD.Print($"[INFO] Loaded setting: {defObj.Key} ({defObj.Type}) = {defObj.DefaultValue}");
      }
      catch (Exception e)
      {
        GD.PrintErr($"[WARN] Invalid setting entry skipped: {e.Message}");
      }
    }

    if (mode == LoadMode.Overwrite)
      Settings = newDefs;
    else if (mode == LoadMode.Append)
      Settings.AddRange(newDefs);

    return newDefs;
  }

  // -------------------------------------------------
  public void FixSettings()
  {
    if (!Directory.Exists(WYNE_SETTINGS))
      Directory.CreateDirectory(WYNE_SETTINGS);

    string settingsFile = $"{WYNE_SETTINGS}/Settings.json";

    List<SettingDefinition> required = new()
    {
      new SettingDefinition{ Key="GameSearchDirs", Label="Game Search Directories", Type=SettingType.StringArray, DefaultValue=new List<string>{"~/Games"} },
      new SettingDefinition{ Key="UpdateLinks", Label="Update Links", Type=SettingType.StringArray, DefaultValue=new List<string>{"https://updates.example.com"} },
      new SettingDefinition{ Key="ServerLinks", Label="Server Links", Type=SettingType.StringArray, DefaultValue=new List<string>{"https://server1.example.com","https://server2.example.com"} },
      new SettingDefinition{ Key="NewsPaperLinks", Label="News Links", Type=SettingType.StringArray, DefaultValue=new List<string>{"https://news.example.com"} },
    };

    List<SettingDefinition> loaded = [];
    if (File.Exists(settingsFile))
    {
      loaded = LoadSettingsFromJson(settingsFile, LoadMode.Overwrite);
    }

    foreach (var req in required)
    {
      bool exists = loaded.Exists(x => x.Key == req.Key);
      if (!exists)
      {
        GD.Print($"[FIX] Adding missing setting: {req.Key}");
        loaded.Add(req);
      }
    }

    SaveSettingsToJson(settingsFile, loaded);
  }

  // -------------------------------------------------
  private static void SaveSettingsToJson(string path, List<SettingDefinition> defs)
  {
    Godot.Collections.Array data = new();
    foreach (var def in defs)
    {
      var dict = new Godot.Collections.Dictionary
      {
        { "key", def.Key },
        { "label", def.Label },
        { "type", def.Type.ToString() }
      };

      if (def.DefaultValue is List<string> list)
      {
        var arr = new Godot.Collections.Array();
        foreach (var s in list) arr.Add(s);
        dict["default"] = arr;
      }
      else
      {
        dict["default"] = def.DefaultValue?.ToString() ?? "";
      }

      data.Add(dict);
    }

    string json = Json.Stringify(data, "\t");
    using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
    file.StoreString(json);

    GD.Print($"[INFO] Saved settings to {path}");
  }

  // -------------------------------------------------
  public override void _Ready()
  {
    Box = GetNode<VBoxContainer>("VBoxContainer");

    FixSettings();
    var items = LoadSettingsFromJson($"{WYNE_SETTINGS}/Settings.json");
    foreach (var item in items) AddSetting(item);
  }

  // -------------------------------------------------
  private void AddSetting(SettingDefinition def)
  {
    HBoxContainer row = new();
    Label label = new()
    {
      Text = def.Label,
      SizeFlagsHorizontal = SizeFlags.ExpandFill
    };
    row.AddChild(label);

    Control input;

    switch (def.Type)
    {
      case SettingType.LineEdit:
        var lineEdit = new LineEdit { Text = def.DefaultValue.ToString() };
        lineEdit.TextChanged += (newText) => Values[def.Key] = newText;
        input = lineEdit;
        break;

      case SettingType.CheckBox:
        var checkBox = new CheckBox { ButtonPressed = (bool)def.DefaultValue };
        checkBox.Toggled += (pressed) => Values[def.Key] = pressed;
        input = checkBox;
        break;

      case SettingType.ToggleButton:
        var toggle = new Button { Text = def.Label, ToggleMode = true, ButtonPressed = (bool)def.DefaultValue };
        toggle.Toggled += (pressed) => Values[def.Key] = pressed;
        input = toggle;
        break;

      case SettingType.SpinBox:
        var spin = new SpinBox { Value = Convert.ToDouble(def.DefaultValue), MinValue = 0, MaxValue = 100 };
        spin.ValueChanged += (newValue) => Values[def.Key] = (int)newValue;
        input = spin;
        break;

      case SettingType.StringArray:
        var textArea = new TextEdit { Text = string.Join(", ", (List<string>)def.DefaultValue), CustomMinimumSize = new Vector2(200, 60) };
        textArea.TextChanged += () =>
        {
          var arr = new List<string>(textArea.Text.Split(',', StringSplitOptions.RemoveEmptyEntries));
          for (int i = 0; i < arr.Count; i++) arr[i] = arr[i].Trim();
          Values[def.Key] = arr;
        };
        input = textArea;
        break;

      default:
        GD.PrintErr($"Unknown setting type: {def.Type}");
        return;
    }

    Values[def.Key] = def.DefaultValue;
    input.CustomMinimumSize = new Vector2(720, 50);
    row.AddChild(input);
    Box.AddChild(row);
    Box.AddChild(new HSeparator());

    GD.Print($"[INFO] Added setting {def.Key} with default = {def.DefaultValue}");
  }


  public static T GetSettingValue<T>(string key, T defaultValue = default)
  {
    if (!Values.ContainsKey(key))
    {
      GD.PrintErr($"[WARN] Setting \"{key}\" not found, returning default = {defaultValue}");
      return defaultValue;
    }

    try
    {
      var value = Values[key];

      if (value is T tVal)
        return tVal;

      return (T)Convert.ChangeType(value, typeof(T));
    }
    catch (Exception e)
    {
      GD.PrintErr($"[ERROR] Failed to cast setting \"{key}\" to {typeof(T).Name}: {e.Message}");
      return defaultValue;
    }
  }

}
