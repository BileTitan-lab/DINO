using System.Text.Json;

namespace DinoDash.Desktop;

internal sealed class GameSettings
{
    // Preserve the previous record and bindings in settings.json.
    public int HighScore { get; set; }
    public Keys JumpKey { get; set; } = Keys.Space;
    public Keys CrouchKey { get; set; } = Keys.Down;
    public ActBindings Bindings { get; set; } = new();
}

internal static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new GameSettings();
            GameSettings? settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath));
            if (settings is null) return new GameSettings();
            settings.Bindings ??= new ActBindings();
            RepairBindings(settings.Bindings);
            return settings;
        }
        catch { return new GameSettings(); }
    }

    private static void RepairBindings(ActBindings bindings)
    {
        ActAction[] previous =
            [ActAction.Left, ActAction.Right, ActAction.Jump,
                ActAction.Dash, ActAction.Attack, ActAction.Pause];
        var used = new HashSet<int>();
        foreach (ActAction action in previous)
        {
            int key = bindings.Get(action);
            if (!SettingsDialog.IsBindable((Keys)key) || !used.Add(key))
            {
                ActBindings defaults = new();
                foreach (ActAction item in ActBindings.Actions)
                    bindings.Set(item, defaults.Get(item));
                return;
            }
        }
        int[] candidates =
            [75, 83, 76, 81, 69, 82, 84, 89, 85, 73, 79, 80, 70, 71, 72,
                90, 88, 67, 86, 66, 78, 77];
        foreach (ActAction action in new[]
            { ActAction.Tail, ActAction.Stomp, ActAction.Ranged,
                ActAction.BattleArt1, ActAction.BattleArt2, ActAction.Interact,
                ActAction.PreviousForm, ActAction.NextForm, ActAction.FormLibrary })
        {
            int key = bindings.Get(action);
            if (!SettingsDialog.IsBindable((Keys)key) || used.Contains(key))
                key = candidates.First(candidate => !used.Contains(candidate));
            bindings.Set(action, key);
            used.Add(key);
        }
    }

    public static bool Save(GameSettings settings)
    {
        try
        {
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, true);
            return true;
        }
        catch { return false; }
    }
}
