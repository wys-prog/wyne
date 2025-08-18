using Godot;
using System.IO;

namespace Wyne.Source;

public partial class Main : Control
{
  private void GCall_on_pressed()
  {
    GetNode<FileDialog>("Button/FileDialog").Visible = true;
  }

  private static void GCall_on_file_dialog_dir_selected(string dir)
  {
    var infoPath = Path.Combine(dir, "Info.json");
    if (!File.Exists(infoPath))
    {
      GD.PrintErr($"Info.json not found in {dir}");
      return;
    }

    var obj = GameObject.LoadFromFolder(dir);
    if (obj == null)
    {
      GD.PrintErr($"Failed to load game from '{dir}'");
      return;
    }

    var destDir = Path.Combine(WyneSystem.WYNE_GAMES, obj.Name);
    if (!Directory.Exists(destDir))
      Directory.CreateDirectory(destDir);

    foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
    {
      var relativePath = Path.GetRelativePath(dir, file);
      var destFile = Path.Combine(destDir, relativePath);
      var destFileDir = Path.GetDirectoryName(destFile);
      if (!Directory.Exists(destFileDir))
        Directory.CreateDirectory(destFileDir);
      File.Copy(file, destFile, overwrite: true);
    }

    GD.Print($"Copied game '{obj.Name}' to {destDir}");
    Home.AskForInstancesRefresh();
  }
}
