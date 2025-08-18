using System.IO;
using Godot;

namespace Wyne.Source;

public partial class Home : Control
{
  private TabContainer tab;
  private GameExecutor executor;

  private void AddGamesUI()
  {
    Globals.Games = [];

    string[] subDirs = Directory.GetDirectories(WyneSystem.WYNE_GAMES);
    GD.Print($"Folders in WYNE_GAMES: \n{subDirs.Join()}");

    foreach (var dir in subDirs)
    {
      var obj = GameObject.LoadFromFolder(dir);
      if (obj == null) continue;
      Globals.Games.Add(obj);

      VBoxContainer gameBox = new()
      {
        Name = obj.Name,
        CustomMinimumSize = new Vector2(400, 400)
      };

      var cover = new TextureRect
      {
        Texture = obj.GetImage(),
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
        CustomMinimumSize = new Vector2(256, 256)
      };
      gameBox.AddChild(cover);

      var nameLbl = new Label { Text = obj.Name, HorizontalAlignment = HorizontalAlignment.Center };
      gameBox.AddChild(nameLbl);

      void AddMeta(string label, string value)
      {
        var h = new HBoxContainer();
        h.AddChild(new Label { Text = $"{label}:", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd });
        h.AddChild(new Label { Text = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        gameBox.AddChild(h);
      }

      AddMeta("Publisher", obj.Publisher);
      AddMeta("Version", obj.Version);
      AddMeta("Runner", obj.Runner);
      AddMeta("Executable", obj.Exe);
      AddMeta("Languages", string.Join(", ", obj.Languages));
      AddMeta("Tags", string.Join(", ", obj.Tags));

      if (!string.IsNullOrEmpty(obj.Description))
        gameBox.AddChild(new Label { Text = obj.Description, AutowrapMode = TextServer.AutowrapMode.Word });

      var launchBtn = new Button { Text = "▶ Play" };
      launchBtn.Pressed += () =>
      {
        GD.Print($"[ACTION] Launching {obj.Name} from {obj.GetExecutablePath()} with runner {obj.Runner}");
        executor.Visible = true;
        executor.Execute($"'{obj.Runner}' '{obj.RunnerCommand}' '{obj.GetExecutablePath()}'");
      };
      gameBox.AddChild(launchBtn);

      tab.AddChild(gameBox);
      tab.SetTabTitle(tab.GetChildCount() - 1, obj.Name);
    }
  }


  public override void _Ready()
  {
    tab = GetNode<TabContainer>("TabContainer");
    executor = GetNode<GameExecutor>("TabContainer/GameExecutor");
    AddGamesUI();
  }

  public static void AskForInstancesRefresh() => RefreshAsked = true;

  private static bool RefreshAsked = false;

  public override void _Process(double delta)
  {
    if (RefreshAsked)
    {
      RefreshAsked = false;
      var children = tab.GetChildren();

      for (int i = 2; i < children.Count; i++) children[i].QueueFree();

      AddGamesUI();
    }
  }
}
