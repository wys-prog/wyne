using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;

namespace Wyne.Scenes;

public partial class GameObject
{
  public string GameName;
  public string GamePath;
  public string CoverImage;
  public string About;
  public Dictionary<string, string> ExecutionCommands;
  public string VersionName;
  public string SourceServer;
  public string Developpers;
  public string WebPage;
  public bool ValidInstance;
}

public partial class Home : Control
{
  public static string GetWynePath()
  {
    string basePath;

    if (OS.HasFeature("windows"))
      basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
    else if (OS.HasFeature("osx"))
      basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) 
          + "/Library/Application Support";
    else // Linux / BSD
      basePath = System.Environment.GetEnvironmentVariable("XDG_DATA_HOME") 
          ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "/.local/share";

    return Path.Combine(basePath, "Wyne");
  }


  public static readonly string WYNE_PREFIX = GetWynePath() + "/Application Data/";
  public static readonly string WYNE_GAMES = WYNE_PREFIX + "Games";
  public static readonly string WYNE_SETTINGS = WYNE_PREFIX + "Settings";
  public static readonly string WYNE_SYSTEM = WYNE_PREFIX + "System";
  public static readonly bool isWindows = System.Runtime.InteropServices.RuntimeInformation
              .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

  private VBoxContainer GamesLists;
  private TextureRect GameCover;
  private TextEdit GameAbout;
  private Button LaunchBtn;
  private OptionButton ChooseCfgBtn;
  private Button EditBtn;
  private Button RemoveBtn;
  private TextEdit OutputLabel;
  private Window OutputWindow;
  private FileDialog ChooseGameToImport;
  private AcceptDialog ErrorDialog;
  private ConfirmationDialog ComfirmGameDelete;
  private Dictionary<string, string> CurrentExecutableCommand = [];
  private List<string> CurrentExecutableCommandIndexed = [];
  private string ToExec = "echo hello world -- LOL.";
  private string CurrentGamePath;
  private string CurrentGameName;

  private void DamnitError(string whatFuck)
  {
    ErrorDialog.DialogText = whatFuck;
    ErrorDialog.Visible = true;
  }

  private static string[] GetGames()
  {
    GD.Print($"Searching games in folder: '{WYNE_GAMES}'");
    var games = new List<string>();

    if (Directory.Exists(WYNE_GAMES))
    {
      foreach (var dir in Directory.GetDirectories(WYNE_GAMES))
      {
        games.Add(dir);
        GD.Print($"Game folder found: '{dir}'");
      }
    }

    return [.. games];
  }

  private static GameObject GetGameObject(string path)
  {
    GameObject obj = new()
    {
      GamePath = path,
      ValidInstance = false,
      GameName = $"Game_{System.Guid.NewGuid()}",
      About = "",
      Developpers = "No one X)",
      SourceServer = "This computer (maybe)",
      VersionName = "1 1 1 1 1 1",
      ExecutionCommands = []
    };

    if (!File.Exists(path + "/Info")) return obj;

    StreamReader reader = new(path + "/Info");

    while (!reader.EndOfStream)
    {
      var line = reader.ReadLine().Trim();
      if (line.StartsWith("Name:")) obj.GameName = line["Name:".Length..].Trim();
      else if (line.StartsWith("GameCover:"))
        obj.CoverImage = $"{obj.GamePath}/{line["GameCover:".Length..].Trim()}";
      else if (line.StartsWith("About:")) obj.About = line["About:".Length..].Trim();
      else if (line.StartsWith("Profiles:"))
      {
        obj.ExecutionCommands = [];
        var profilesLine = line["Profiles:".Length..].Trim();
        // Expecting format: {NAME: COMMAND, ...}
        if (profilesLine.StartsWith('{') && profilesLine.EndsWith('}'))
        {
          profilesLine = profilesLine[1..^1].Trim(); // Remove braces
          var pairs = profilesLine.Split(',');
          foreach (var pair in pairs)
          {
            var kv = pair.Split(':', 2);
            if (kv.Length == 2)
            {
              var key = kv[0].Trim();
              var value = kv[1].Trim();
              obj.ExecutionCommands[key] = value;
            }
          }
        }
      }
      else if (line.StartsWith("Version:"))
      {
        obj.VersionName = line["Version:".Length..].Trim().ToUpper()
          .Replace(' ', '_')
          .Replace('\n', '_')
          .Replace('\t', '_');
      }
      else if (line.StartsWith("Source:")) obj.SourceServer = line["Source:".Length..].Trim();
      else if (line.StartsWith("Developpers:")) obj.Developpers = line["Developpers:".Length..].Trim();
      else if (line.StartsWith("WebPage:")) obj.WebPage = line["WebPage:".Length..].Trim();
    }

    reader.Close();

    obj.ValidInstance = true;

    return obj;
  }

  private static ImageTexture LoadGameCover(string path) => ImageTexture.CreateFromImage(Image.LoadFromFile(path));

  private void ExecuteGame(string cmd)
  {
    var psi = new ProcessStartInfo
    {
      FileName = isWindows ? "cmd.exe" : "/bin/bash",
      Arguments = isWindows ? $"/C {cmd}" : $"-c \"{cmd}\"",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    var process = new Process { StartInfo = psi };

    process.OutputDataReceived += (sender, e) =>
    {
      if (e.Data != null)
      {
        OutputLabel.CallDeferred("insert_text_at_caret", e.Data + "\n");
      }
    };

    process.ErrorDataReceived += (sender, e) =>
    {
      if (e.Data != null)
      {
        OutputLabel.CallDeferred("insert_text_at_caret", e.Data + "\n");
      }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    OutputWindow.Visible = true;
  }

  private void GodotCallback_on_btn_launch_item_selected(int i)
  {
    ToExec = CurrentExecutableCommand[CurrentExecutableCommandIndexed[i]];
    LaunchBtn.Text = CurrentExecutableCommandIndexed[i];
  }

  private void GodotCallback_on_btn_launch_pressed()
  {
    ExecuteGame(ToExec);
  }

  private void GodotCallback_on_add_game_pressed()
  {
    ChooseGameToImport.Visible = true;
  }

  private void GodotCallback_on_choose_game_to_import_dir_selected(string path)
  {
    ChooseGameToImport.Visible = false;
    if (!File.Exists(path + "/Info"))
    {
      DamnitError($"Folder '{path}' does not contains file 'Info'.");
      return;
    }

    var obj = GetGameObject(path);

    try
    {
      string MyDir = Path.Combine(WYNE_GAMES, obj.GameName);
      Directory.CreateDirectory(MyDir);
      foreach (var file in Directory.GetFiles(path))
      {
        var destFile = Path.Combine(MyDir, Path.GetFileName(file));
        File.Copy(file, destFile, overwrite: true);
        GD.Print($"Copied {destFile}");
      }
      foreach (var dir in Directory.GetDirectories(path))
      {
        var destDir = Path.Combine(MyDir, Path.GetFileName(dir));
        Directory.CreateDirectory(destDir);
        foreach (var subFile in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
          var relativePath = Path.GetRelativePath(dir, subFile);
          var destSubFile = Path.Combine(destDir, relativePath);
          Directory.CreateDirectory(Path.GetDirectoryName(destSubFile)!);
          File.Copy(subFile, destSubFile, overwrite: true);
          GD.Print($"Copied {destSubFile}");
        }
      }
    }
    catch (System.Exception e)
    {
      DamnitError($"{e.Message}\n\nStacktrace:\n{e.StackTrace}");
    }

    Refresh();
  }

  private void Refresh()
  {
    foreach (var node in GamesLists.GetChildren()) node.QueueFree();

    var games = GetGames();
    List<GameObject> gameObjects = [];
    int i = 0;

    foreach (var game in games)
    {
      int indexCopy = i++;
      var obj = GetGameObject(game);
      gameObjects.Add(obj);

      Button btn = new() { Text = obj.GameName };
      btn.Pressed += () =>
      {
        GameObject MyGame = gameObjects[indexCopy];
        GameAbout.Text = $"{MyGame.GameName} - V_{MyGame.VersionName}\n{MyGame.About}\nFrom {MyGame.SourceServer} - {MyGame.Developpers}";
        GameCover.Texture = LoadGameCover(MyGame.CoverImage);
      };

      GamesLists.AddChild(btn);
    }
  }

  private void GodotCallback_on_btn_remove_pressed()
  {
    ComfirmGameDelete.DialogText = $"Do you really want to delete the game '{CurrentGameName}' ?";
    ComfirmGameDelete.Visible = true;
  }

  private void GodotCallback_on_comfirm_game_delete_confirmed()
  {
    Directory.Delete(CurrentGamePath, true);

    Refresh();
  }






  public override void _Ready()
  {
   //DisplayServer.WindowSetSize((Vector2I)(DisplayServer.ScreenGetSize() / new Vector2(1.3f, 1.3f)));

    GetTree().Root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
    GetTree().Root.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;

    GamesLists = GetNode<VBoxContainer>("GamesPanel/GamesList");
    GameCover = GetNode<TextureRect>("WinInf/TextureRect");
    GameAbout = GetNode<TextEdit>("WinInf/TextEdit");
    ChooseCfgBtn = GetNode<OptionButton>("BtnChooseCfg");
    LaunchBtn = GetNode<Button>("BtnLaunch");
    EditBtn = GetNode<Button>("BtnEdit");
    RemoveBtn = GetNode<Button>("BtnRemove");
    OutputLabel = GetNode<TextEdit>("StdOutWin/StdOut");
    OutputWindow = GetNode<Window>("StdOutWin");
    ChooseGameToImport = GetNode<FileDialog>("ChooseGameToImport");
    ErrorDialog = GetNode<AcceptDialog>("ErrorDialog");
    ComfirmGameDelete = GetNode<ConfirmationDialog>("ComfirmGameDelete");

    var games = GetGames();
    List<GameObject> gameObjects = [];
    int i = 0;

    foreach (var game in games)
    {
      int indexCopy = i++;
      var obj = GetGameObject(game);
      gameObjects.Add(obj);

      Button btn = new() { Text = obj.GameName };
      btn.Pressed += () =>
      {
        GameObject MyGame = gameObjects[indexCopy];
        GameAbout.Text = $"{MyGame.GameName} - V_{MyGame.VersionName}\n{MyGame.About}\nFrom {MyGame.SourceServer} - {MyGame.Developpers}";
        GameCover.Texture = LoadGameCover(MyGame.CoverImage);
        CurrentGameName = obj.GameName;
        CurrentGamePath = obj.GamePath;
        CurrentExecutableCommand = obj.ExecutionCommands;
        CurrentExecutableCommandIndexed = [];

        foreach (var item in CurrentExecutableCommand) CurrentExecutableCommandIndexed.Add(item.Key);
      };

      GamesLists.AddChild(btn);
    }
  }
}
