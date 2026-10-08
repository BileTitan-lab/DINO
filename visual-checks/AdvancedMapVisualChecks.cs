using System.Drawing.Imaging;
using DinoDash.Desktop;

internal static class AdvancedMapVisualChecks
{
    public static void Run()
    {
        foreach (int chapter in new[] { 3, 4 })
        foreach (int sample in Enumerable.Range(0, 4))
        {
            var game = new ActEngine(1);
            game.LoadMapForDebug(chapter, 12, false);
            var room = game.Layout.Rooms[sample == 3 ? 4 : sample];
            float x = room.Node.StartX + (chapter == 3 ? 750 : sample == 3 ? 700 : sample == 0 ? 1280 : 2380);
            float floor = game.TerrainYAt(x + 27) ?? 275;
            game.PlacePlayerForTest(x, floor - game.PlayerHeight);
            foreach (var m in game.Layout.Mechanisms) m.Update(sample == 2 ? 4.35f : 3.3f, m.X);
            foreach (var p in game.Layout.MovingPlatforms) p.Update(.35f, x, true);
            using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
            canvas.ResetFx(); canvas.Tick(.15f);
            using var bitmap = new Bitmap(1280, 720);
            canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, $"chapter-{chapter}-{sample}.png"), ImageFormat.Png);
        }
        foreach (int chapter in new[] { 3, 4 })
        {
            var game = new ActEngine(1);
            game.LoadMapForDebug(chapter, 12, false);
            int count = game.Supplies.Count;
            for (int i = 0; i < count; i++)
            {
                var camp = game.Supplies[i];
                game.PlacePlayerForTest(camp.X - 90, 248);
                using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
                canvas.ResetFx(); canvas.Tick(.15f);
                using var bitmap = new Bitmap(1280, 720);
                canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
                bitmap.Save(Path.Combine(AppContext.BaseDirectory,
                    $"chapter-{chapter}-camp-{i}.png"), ImageFormat.Png);
            }
        }
        var volcano = new ActEngine(1);
        volcano.LoadMapForDebug(4, 12, false);
        var slopeHazard = volcano.Layout.Mechanisms.First(m =>
            m.Kind is ActMechanismKind.Eruption or ActMechanismKind.Rock &&
            volcano.TerrainYAt(m.X) is float a &&
            volcano.TerrainYAt(m.X + m.Width - 1) is float b &&
            Math.Abs(a - b) > 8);
        slopeHazard.Update(3.25f, slopeHazard.X);
        float approach = slopeHazard.X - 185;
        volcano.PlacePlayerForTest(approach,
            volcano.TerrainYAt(approach + 27)!.Value - volcano.PlayerHeight);
        SaveScene(volcano, "slope-hazard-warning.png");
        var forest = new ActEngine(1);
        forest.LoadMapForDebug(3, 12, false);
        var moving = forest.Layout.MovingPlatforms.First(p =>
            p.Motion == ActPlatformMotion.Vertical && p.Amplitude > 30);
        moving.Update(1.4f, -10000, false);
        forest.PlacePlayerForTest(moving.Bounds.X + 24,
            moving.Bounds.Y - forest.PlayerHeight);
        SaveScene(forest, "slope-platform-clearance.png");
        var vineRoom = forest.Layout.Rooms.First(r =>
            r.Variant.Id.Contains("叶影断廊") || r.Variant.Id.Contains("鸟巢断枝"));
        float vineX = vineRoom.Node.StartX + 2250;
        forest.PlacePlayerForTest(vineX,
            (forest.TerrainYAt(vineX + 27) ?? 300) - forest.PlayerHeight);
        SaveScene(forest, "forest-vine-landmark.png");
        Console.WriteLine("PASS forest/volcano terrain, mechanisms, warning and active phases rendered offscreen.");
    }

    private static void SaveScene(ActEngine game, string name)
    {
        using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
        canvas.ResetFx(); canvas.Tick(.15f);
        using var bitmap = new Bitmap(1280, 720);
        canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, name), ImageFormat.Png);
    }
}
