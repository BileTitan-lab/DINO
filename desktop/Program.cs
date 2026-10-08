namespace DinoDash.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool debug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
        int? seed = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--seed", StringComparison.OrdinalIgnoreCase)) continue;
            debug = true;
            if (i + 1 >= args.Length || !int.TryParse(args[++i], out int value) ||
                value <= 0)
            {
                MessageBox.Show("用法：恐龙冲刺.exe --debug --seed 正整数", "Seed 参数错误");
                return;
            }
            seed = value;
        }
        Application.Run(new GameForm(seed, debug));
    }
}
