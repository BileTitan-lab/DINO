using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using DinoDash.Desktop;

// Offline raster checks: no game window, keyboard automation or screen capture.
internal static class VisualChecks
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        KeyboardChecks.Run();
        MainMenuChecks.Run();
        AdvancedMapVisualChecks.Run();
        RestVisualChecks.Run();
        BossVisualChecks.Run();
        PrimalVisualChecks.Run();
        UiThemeChecks.Run();
        CheckRegionPresentation();
        CheckClashPresentation();
        ChapterRoomVisualChecks.Run();
        ExpandedBuildVisualChecks.Run();
        string output = Path.Combine(AppContext.BaseDirectory, "animation-check.png");
        using var atlas = new Bitmap(1080, 650);
        using Graphics sheet = Graphics.FromImage(atlas);
        sheet.Clear(ActVisualTheme.Paper);
        using var font = new Font("Consolas", 11f);
        var tail = typeof(ActSpriteAnimator).GetMethod("DrawTailSweep",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        // Sample 501 timings rather than only animation keyframes. The shadow
        // region outside the torso must stay above the player's feet.
        for (int i = 0; i <= 500; i++)
        {
            using var frame = new Bitmap(300, 110);
            using Graphics g = Graphics.FromImage(frame);
            g.Clear(Color.White);
            tail.Invoke(null, [g, 130f, 20f, i / 1000f]);
            for (int y = 72; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    if ((x < 130 || x >= 185) && frame.GetPixel(x, y).R < 80)
                        throw new InvalidOperationException($"Tail crossed feet at {i / 1000f:0.000}s.");
        }

        sheet.DrawString("FLYING: collider = red outline; decorative wing tips extend outside", font,
            Brushes.Black, 15, 8);
        ActEnemyState[] states = [ActEnemyState.Patrol, ActEnemyState.Windup,
            ActEnemyState.Attack, ActEnemyState.Recover];
        for (int i = 0; i < 8; i++)
        {
            float x = 25f + i * 130f;
            var saved = sheet.Save();
            sheet.TranslateTransform(x, 55f);
            sheet.ScaleTransform(2f, 2f);
            ActSpriteAnimator.DrawEnemyBody(sheet, ActEnemyKind.Flying, 0f, 0f,
                states[i / 2], (i % 2) * .22f, BossMove.Charge, 2, 2,
                Brushes.Black, Brushes.White);
            sheet.DrawRectangle(Pens.IndianRed, 0, 0, 44, 28);
            sheet.Restore(saved);
            sheet.DrawString(states[i / 2].ToString(), font, Brushes.Black, x, 137f);
        }

        sheet.DrawString("TAIL: contact samples / bottom line = feet", font, Brushes.Black, 15, 173);
        for (int i = 0; i < 4; i++)
        {
            float x = 130 + i * 265;
            tail.Invoke(null, [sheet, x, 215f, .17f + i * .06f]);
            sheet.DrawLine(Pens.IndianRed, x - 115f, 267f, x + 135f, 267f);
        }

        sheet.DrawString("BOSS TAIL: windup / strike / follow-through / recover", font,
            Brushes.Black, 15, 300);
        ActEnemyState[] bossStates = [ActEnemyState.Windup, ActEnemyState.Attack,
            ActEnemyState.Attack, ActEnemyState.Recover];
        float[] times = [.89f, .18f, .38f, .1f];
        for (int i = 0; i < 4; i++)
            ActSpriteAnimator.DrawEnemyBody(sheet, ActEnemyKind.Boss, 95 + i * 265,
                345, bossStates[i], times[i], BossMove.Tail, 24, 24,
                Brushes.Black, Brushes.White);

        using (var attack = new Bitmap(110, 130))
        using (var recover = new Bitmap(110, 130))
        {
            using Graphics a = Graphics.FromImage(attack);
            using Graphics b = Graphics.FromImage(recover);
            a.Clear(Color.White); b.Clear(Color.White);
            PixelSprites.DrawBossEnemy(a, 90, 10, ActEnemyState.Attack, .38f,
                BossMove.Tail, 24, 24, Brushes.Black, Brushes.White);
            PixelSprites.DrawBossEnemy(b, 90, 10, ActEnemyState.Recover, 0f,
                BossMove.Tail, 24, 24, Brushes.Black, Brushes.White);
            for (int y = 0; y < attack.Height; y++)
                for (int x = 0; x < 86; x++)
                    if (attack.GetPixel(x, y) != recover.GetPixel(x, y))
                        throw new InvalidOperationException("Boss tail jumps at recovery boundary.");
        }

        sheet.DrawString("HUD: health lost / energy recovered", font, Brushes.Black, 15, 470);
        using var hp = new ActHudMeter("生命", true) { Size = new Size(300, 66) };
        using var energy = new ActHudMeter("骨刺能量", false) { Size = new Size(300, 66) };
        hp.SetValue(5, 5, true); hp.SetValue(3, 5); hp.Tick(.25f);
        energy.SetValue(2, 5, true); energy.SetValue(4, 5); energy.Tick(.25f);
        using var hpImage = new Bitmap(300, 66);
        using var energyImage = new Bitmap(300, 66);
        hp.DrawToBitmap(hpImage, new Rectangle(0, 0, 300, 66));
        energy.DrawToBitmap(energyImage, new Rectangle(0, 0, 300, 66));
        sheet.DrawImageUnscaled(hpImage, 25, 510);
        sheet.DrawImageUnscaled(energyImage, 360, 510);
        atlas.Save(output, ImageFormat.Png);
        var sceneEngine = new ActEngine(12345);
        sceneEngine.PlacePlayerForTest(17650f,
            ActTuning.GroundY - sceneEngine.PlayerHeight);
        using var canvas = new GameCanvas(sceneEngine)
        {
            Size = new Size(1440, 540), Dimmed = false
        };
        using var scene = new Bitmap(1440, 540);
        canvas.DrawToBitmap(scene, new Rectangle(0, 0, 1440, 540));
        scene.Save(Path.Combine(AppContext.BaseDirectory, "scene-check.png"), ImageFormat.Png);
        CheckWindowLayouts();
        CheckSettingsLayout();
        CheckMapDebugLayout();
        CheckPowerRewardLayout();
        CheckSecondMapScene();
        Console.WriteLine("PASS 501 tail samples; boss recovery boundary; offscreen sprite/HUD rendering.");
        Console.WriteLine(output);
    }

    private static void CheckClashPresentation()
    {
        var game = new ActEngine(42);
        var enemy = game.Enemy;
        game.PlacePlayerForTest(enemy.X - 66f, ActTuning.GroundY - game.PlayerHeight);
        enemy.State = ActEnemyState.Windup;
        enemy.StateTime = ActEnemyTuning.MeleeWindup - .05f;
        game.AttackRequested = true;
        game.Update(ActTuning.Step);
        var fx = new ActVisualFx();
        fx.Update(game, ActTuning.Step);
        if (fx.Clashes.Count != 1 || fx.Particles.Count(p => p.Kind == FxKind.Spark) < 18)
            throw new InvalidOperationException("Clash did not produce slash and spark effects.");
        using var frame = new Bitmap(180, 160);
        using Graphics g = Graphics.FromImage(frame);
        g.Clear(Color.Transparent);
        g.TranslateTransform(70 - game.ClashX, 70 - game.ClashY);
        ActFxRenderer.DrawFront(g, fx);
        if (frame.GetPixel(70, 70).A == 0)
            throw new InvalidOperationException("Clash slash did not render at the impact point.");
        using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
        for (int i = 0; i < 50; i++)
        {
            canvas.Tick(ActTuning.Step);
            using var scene = canvas.RenderRewardScene();
            game.Update(ActTuning.Step);
        }
        Console.WriteLine("PASS claw clash slash, sparks and impact point rendered offscreen.");
    }

    private static void CheckWindowLayouts()
    {
        foreach (Size size in new[] { new Size(780, 540), new Size(1280, 720), new Size(1920, 1080) })
        {
            using var form = new GameForm(12345);
            form.ClientSize = size;
            // Handles/layout only; never Show(), pump messages, or capture a screen.
            _ = form.Handle;
            form.PerformLayout();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var canvas = (GameCanvas)typeof(GameForm).GetField("canvas", flags)!.GetValue(form)!;
            var overlay = (Panel)typeof(GameForm).GetField("overlay", flags)!.GetValue(form)!;
            if (form.ImeMode != ImeMode.Disable || canvas.ImeMode != ImeMode.Disable)
                throw new Exception("Gameplay controls permit IME composition.");
            typeof(GameForm).GetMethod("LayoutInterface", flags)!.Invoke(form, null);
            if (canvas.Size != size) throw new Exception("External UI stole viewport space.");
            if (!canvas.ClientRectangle.Contains(overlay.Bounds)) throw new Exception("Menu clipped.");
            foreach (string name in new[] { "pauseButton", "settingsButton", "mapDebugButton", "overlayButton" })
            {
                var button = (Button)typeof(GameForm).GetField(name, flags)!.GetValue(form)!;
                Size text = TextRenderer.MeasureText(button.Text, button.Font);
                if (button.Width < text.Width + 8 || button.Height < text.Height + 8 ||
                    !button.Parent!.ClientRectangle.Contains(button.Bounds))
                    throw new Exception($"{name} text/rectangle clipped at {size}.");
            }
            var pause = (Button)typeof(GameForm).GetField("pauseButton", flags)!.GetValue(form)!;
            float ui = ActHudRenderer.UiScale(size, form.DeviceDpi);
            if (pause.Left < 288 * ui) throw new Exception("Top controls overlap the HUD.");
            Control host = canvas.Parent!;
            // A hidden Form causes WinForms to skip all children in DrawToBitmap.
            // Detach only for rasterization, without showing any native window.
            canvas.Parent = null;
            using var menu = new Bitmap(size.Width, size.Height);
            canvas.DrawToBitmap(menu, new Rectangle(Point.Empty, size));
            menu.Save(Path.Combine(AppContext.BaseDirectory, $"menu-{size.Width}.png"), ImageFormat.Png);
            typeof(GameForm).GetMethod("BeginRun", flags)!.Invoke(form, [false]);
            using var run = new Bitmap(size.Width, size.Height);
            canvas.DrawToBitmap(run, new Rectangle(Point.Empty, size));
            run.Save(Path.Combine(AppContext.BaseDirectory, $"hud-{size.Width}.png"), ImageFormat.Png);
            if (size.Width == 1280)
            {
                var engine = (ActEngine)typeof(GameForm).GetField("engine", flags)!.GetValue(form)!;
                engine.Run.AddArt(BattleArtId.RegenerativeSpores);
                engine.Run.AddArt(BattleArtId.AmberLag);
                engine.Run.AddEvolution(EvolutionId.SharpClaws);
                engine.Run.AddEvolution(EvolutionId.HeavyScales);
                engine.Run.StartEnemySlow(4.5f);
                using var powers = new Bitmap(size.Width, size.Height);
                canvas.DrawToBitmap(powers, new Rectangle(Point.Empty, size));
                powers.Save(Path.Combine(AppContext.BaseDirectory, "power-hud.png"), ImageFormat.Png);
                foreach (EvolutionDefinition definition in ActPowerCatalog.Evolutions)
                    engine.Run.AddEvolution(definition.Id,EvolutionSource.BossReward);
                float scale = ActHudRenderer.UiScale(size, form.DeviceDpi);
                float start = size.Width / scale - 256 + 52;
                int lastIndex=engine.Run.Evolutions.Count-1;
                var lastIcon = new Point((int)((start + lastIndex%6*28 + 10) * scale),
                    (int)((166+lastIndex/6*29 + 10) * scale));
                if (ActHudRenderer.EvolutionAt(lastIcon, size, form.DeviceDpi,
                    engine.Run)?.Id != ActPowerCatalog.Evolutions[^1].Id)
                    throw new Exception("Last evolution icon clipped or not hoverable.");
                using var fullPowers = new Bitmap(size.Width, size.Height);
                canvas.DrawToBitmap(fullPowers, new Rectangle(Point.Empty, size));
                fullPowers.Save(Path.Combine(AppContext.BaseDirectory, "power-hud-full.png"), ImageFormat.Png);
            }
            // The same card must fit the longer death message, including stats.
            typeof(GameForm).GetField("state", flags)!.SetValue(form, ActPlayState.Dead);
            typeof(GameForm).GetMethod("ShowOverlay", flags)!.Invoke(form,
                ["远征未竟", "本次用时 12:34  ·  击败 42 个敌人\n重新出发将生成新的地图布局。", "重新出发"]);
            foreach (Control label in overlay.Controls.OfType<Label>())
            {
                Size measured = TextRenderer.MeasureText(label.Text, label.Font,
                    new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
                if (measured.Height > label.Height)
                    throw new Exception($"Menu text clipped: {label.Text}, {size}.");
            }
            if (size.Width == 1280)
            {
                using var death = new Bitmap(size.Width, size.Height);
                canvas.DrawToBitmap(death, new Rectangle(Point.Empty, size));
                death.Save(Path.Combine(AppContext.BaseDirectory, "death-menu.png"), ImageFormat.Png);
            }
            host.Controls.Add(canvas);
        }
        Console.WriteLine("PASS three window sizes: full client viewport, menu bounds and measured button text.");
    }

    private static void CheckSettingsLayout()
    {
        if (SettingsDialog.KeyName(Keys.D1) != "1" ||
            SettingsDialog.KeyName(Keys.D2) != "2")
            throw new Exception("Battle-art defaults are mislabeled.");
        using var form = new SettingsDialog(new ActBindings());
        if (form.ImeMode != ImeMode.Disable) throw new Exception("Binding menu permits IME composition.");
        _ = form.Handle;
        form.PerformLayout();
        Control root = form.Controls[0];
        root.Parent = null;
        root.PerformLayout();
        foreach (Button button in root.Controls.OfType<Button>())
        {
            Size text = TextRenderer.MeasureText(button.Text, button.Font);
            if (button.Height < text.Height + 4 || button.Bottom > root.ClientSize.Height)
                throw new Exception("Key-binding row clipped.");
        }
        using var bitmap = new Bitmap(root.Width, root.Height);
        root.DrawToBitmap(bitmap, root.ClientRectangle);
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, "settings-menu.png"), ImageFormat.Png);
        form.Controls.Add(root);
        Console.WriteLine("PASS key-binding menu layout.");
    }

    private static void CheckMapDebugLayout()
    {
        using var dialog = new ActMapDebugDialog(1, 12345);
        _ = dialog.Handle;
        dialog.PerformLayout();
        var cards = dialog.Controls.OfType<Panel>().OrderBy(panel => panel.Top).ThenBy(panel => panel.Left).ToArray();
        if (cards.Length != 4 || cards.Any(card => !dialog.ClientRectangle.Contains(card.Bounds)))
            throw new Exception("Map choice cards are clipped.");
        var choices = cards.Select(card => card.Controls.OfType<RadioButton>().Single()).ToArray();
        choices[1].Checked = true;
        if (choices[0].Checked || !dialog.TryApplySelection() ||
            dialog.SelectedMap != 2 || dialog.SelectedSeed != 12345)
            throw new Exception("Second map selection did not replace the first.");
        choices[2].Checked = true;
        if (!dialog.TryApplySelection() || dialog.SelectedMap != 3 || choices[1].Checked) throw new Exception("Forest selection failed");
        choices[3].Checked = true;
        if (!dialog.TryApplySelection() || dialog.SelectedMap != 4 || choices[2].Checked) throw new Exception("Volcano selection failed");
        var seed = dialog.Controls.OfType<TextBox>().Single();
        seed.Text = "0";
        if (dialog.TryApplySelection()) throw new Exception("Invalid debug seed was accepted.");
        dialog.Controls.OfType<CheckBox>().Single(box => box.Text.Contains("随机")).Checked = true;
        if (!dialog.TryApplySelection() || dialog.SelectedSeed is not null)
            throw new Exception("Random debug layout selection failed.");
        choices[0].Checked = true;
        if (choices[1].Checked || !dialog.TryApplySelection() || dialog.SelectedMap != 1)
            throw new Exception("Debug selector could not return to the tutorial.");
        foreach (Button button in dialog.Controls.OfType<Button>())
            if (!dialog.ClientRectangle.Contains(button.Bounds) ||
                button.Width < TextRenderer.MeasureText(button.Text, button.Font).Width + 8)
                throw new Exception("Map debug action button is clipped.");

        dialog.Controls.OfType<CheckBox>().Single(box => box.Text.Contains("随机")).Checked = false;
        seed.Text = "12345";

        using var root = new Panel { Size = dialog.ClientSize, BackColor = dialog.BackColor };
        Control[] children = dialog.Controls.Cast<Control>().ToArray();
        foreach (Control child in children) root.Controls.Add(child);
        using var image = new Bitmap(root.Width, root.Height);
        root.DrawToBitmap(image, root.ClientRectangle);
        image.Save(Path.Combine(AppContext.BaseDirectory, "map-debug-menu.png"), ImageFormat.Png);
        foreach (Control child in children) dialog.Controls.Add(child);
        Console.WriteLine("PASS map debug selector layout, both destinations and seed validation.");
    }

    private static void CheckPowerRewardLayout()
    {
        var engine = new ActEngine(24680);
        engine.PlacePlayerForTest(3300, ActTuning.GroundY - engine.PlayerHeight);
        using var canvas = new GameCanvas(engine) { Size = new Size(1280, 720), Dimmed = false };
        using var backdrop = new Bitmap(1280, 720);
        canvas.DrawToBitmap(backdrop, canvas.ClientRectangle);
        using var cleanScene = canvas.RenderRewardScene();
        engine.Run.AddArt(BattleArtId.RegenerativeSpores);
        engine.Run.AddArt(BattleArtId.BoneArmor);
        engine.OfferBattleArts();
        using (var dialog = new ActPowerRewardDialog(engine, backdrop, cleanScene))
            SaveReward(dialog, "battle-art-reward.png");
        engine.DeclineBattleArt();
        engine.OfferEvolutions();
        using (var dialog = new ActPowerRewardDialog(engine, backdrop, cleanScene))
            SaveReward(dialog, "evolution-reward.png");
        using (var small = new ActPowerRewardDialog(engine, backdrop, cleanScene) { ClientSize = new Size(780, 540) })
            SaveReward(small, "evolution-reward-small.png");
        using (var transition = new ActPowerRewardDialog(engine, backdrop))
        {
            transition.AdvanceTransition(.16f);
            if (Math.Abs(transition.Reveal - .5f) > .01f)
                throw new Exception("Reward fade-in skipped its midpoint.");
            transition.AdvanceTransition(.16f);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ActPowerRewardDialog).GetMethod("Choose", flags)!.Invoke(transition, [0]);
            typeof(ActPowerRewardDialog).GetMethod("Choose", flags)!.Invoke(transition, [1]);
            if (!transition.IsFadingOut || engine.Run.Evolutions.Count != 1)
                throw new Exception("Reward accepted twice during fade-out.");
            transition.AdvanceTransition(.12f);
            if (transition.Reveal is < .49f or > .51f || transition.DialogResult == DialogResult.OK)
                throw new Exception("Reward closed before fade-out completed.");
            transition.AdvanceTransition(.13f);
            if (transition.DialogResult != DialogResult.OK)
                throw new Exception("Reward failed to close after fading out.");
        }
        using var icons = new Bitmap(760, 160);
        using (var g = Graphics.FromImage(icons))
        {
            g.Clear(ActVisualTheme.Paper);
            int i = 0;
            foreach (ActRewardKind kind in Enum.GetValues<ActRewardKind>())
                ActRewardIcons.DrawPickup(g, 60 + i++ * 125, 85, kind, 0);
        }
        icons.Save(Path.Combine(AppContext.BaseDirectory, "reward-pickups.png"), ImageFormat.Png);
        Console.WriteLine("PASS battle-art replacement and evolution reward layout.");

        static void SaveReward(ActPowerRewardDialog dialog, string name)
        {
            dialog.AdvanceTransition(1f);
            _ = dialog.Handle;
            dialog.PerformLayout();
            Control root = dialog.Controls[0];
            root.Parent = null;
            root.PerformLayout();
            using var bitmap = new Bitmap(root.Width, root.Height);
            root.DrawToBitmap(bitmap, root.ClientRectangle);
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, name), ImageFormat.Png);
            dialog.Controls.Add(root);
        }
    }

    private static void CheckRegionPresentation()
    {
        using var region = new ActRegionPresentation();
        region.Update(0, .5f);
        if (region.Title != "晨雾湿地" || region.Opacity != 1f)
            throw new Exception("Starting region discovery missing.");
        region.Update(2999, 4f);
        if (region.Opacity != 0f) throw new Exception("Region name stays on screen.");
        region.Update(3000, .5f);
        if (region.Title != "芦苇沼泽" || region.Opacity != 1f)
            throw new Exception("Region changed at wrong boundary.");
        region.Update(2999, .1f);
        region.Update(3000, .5f);
        if (region.Opacity != 0f) throw new Exception("Backtracking spams region banners.");
        region.Reset();
        region.Update(3000, .5f);
        if (region.Opacity != 1f) throw new Exception("New run did not reset discovery.");
        var engine = new ActEngine(12345);
        engine.PlacePlayerForTest(3300, ActTuning.GroundY - engine.PlayerHeight);
        using var canvas = new GameCanvas(engine) { Size = new Size(1280, 720), Dimmed = false };
        canvas.Tick(.5f);
        using var picture = new Bitmap(1280, 720);
        canvas.DrawToBitmap(picture, new Rectangle(0, 0, 1280, 720));
        picture.Save(Path.Combine(AppContext.BaseDirectory, "region-marsh.png"), ImageFormat.Png);
        Console.WriteLine("PASS region entry, fade-out, backtracking and new-run reset; marsh scene rendered.");
    }

    private static void CheckSecondMapScene()
    {
        var variants = new Dictionary<string, (int Seed, float X)>();
        for (int seed = 1; seed <= 100 && variants.Count < 6; seed++)
        {
            var candidate = ActSecondMap.Generate(seed);
            foreach (RoomSelection room in candidate.Rooms)
            {
                float x = room.Variant.Id switch
                {
                    "A1 山顶攻防" => 1510,
                    "A2 双层裂谷" => 1960,
                    "B1 沉岩洞穴" => 4600,
                    "B2 断脊石桥" => 4370,
                    "C1 回旋高地" => 5990,
                    _ => 5990
                };
                variants.TryAdd(room.Variant.Id, (seed, ActSecondMap.WorldX(x)));
            }
        }
        if (variants.Count != 6) throw new Exception("Canyon variants are missing.");
        foreach (var (name, sample) in variants)
        {
            var game = new ActEngine(1);
            game.EnterNextMap(sample.Seed, roomAdventure: false);
            float feet = game.TerrainYAt(sample.X + game.PlayerWidth / 2f) ??
                game.Layout.Solids.Where(s => sample.X + 27 >= s.Left && sample.X + 27 < s.Right)
                    .Select(s => s.Top).DefaultIfEmpty(300f).Min();
            game.PlacePlayerForTest(sample.X, feet - game.PlayerHeight);
            using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
            canvas.ResetFx(); canvas.Tick(.15f);
            using var picture = new Bitmap(1280, 720);
            canvas.DrawToBitmap(picture, canvas.ClientRectangle);
            picture.Save(Path.Combine(AppContext.BaseDirectory,
                $"canyon-{name[..2]}.png"), ImageFormat.Png);
        }
        Console.WriteLine("PASS six canyon modules rendered with terrain-aligned collision and camera.");
        var extended = new Dictionary<string, (int Seed, float X)>();
        for (int seed = 1; seed <= 120 && extended.Count < 6; seed++)
        {
            var layout = ActSecondMap.Generate(seed);
            foreach (RoomSelection room in layout.Rooms.Skip(3))
                extended.TryAdd(room.Variant.Id,
                    (seed, (room.Node.StartX + room.Node.EndX) / 2f));
        }
        if (extended.Count != 6) throw new Exception("Extended canyon variants are missing.");
        foreach (var (name, sample) in extended)
        {
            var game = new ActEngine(1);
            game.EnterNextMap(sample.Seed, roomAdventure: false);
            float sampleX = sample.X;
            float feet = game.TerrainYAt(sampleX + 27f) ??
                game.Layout.Solids.Where(s => sampleX + 27f >= s.Left &&
                    sampleX + 27f < s.Right).Select(s => s.Top)
                    .DefaultIfEmpty(300f).Min();
            game.PlacePlayerForTest(sampleX, feet - game.PlayerHeight);
            using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
            canvas.ResetFx(); canvas.Tick(.15f);
            using var picture = new Bitmap(1280, 720);
            canvas.DrawToBitmap(picture, canvas.ClientRectangle);
            picture.Save(Path.Combine(AppContext.BaseDirectory,
                $"canyon-{name[..2]}.png"), ImageFormat.Png);
        }
        var seamLayout = ActSecondMap.Generate(42);
        foreach (var pair in seamLayout.Terrain.Zip(seamLayout.Terrain.Skip(1)))
        {
            if (Math.Abs(pair.First.Right - pair.Second.Left) > .1f) continue;
            using var image = new Bitmap(64, 560);
            using Graphics g = Graphics.FromImage(image);
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(32.35f - pair.First.Right, 0f);
            ActSecondMapVisual.DrawTerrain(g, seamLayout.Terrain, pair.First.Right - 32f);
            for (int px = 31; px <= 33; px++)
                if (image.GetPixel(px, 500).R > 100)
                    throw new Exception($"Bright terrain seam at {pair.First.Right}");
        }
        Console.WriteLine("PASS six extended modules and fractional-camera terrain seams rendered.");
        foreach (int map in new[] { 1, 2 })
        foreach (bool used in new[] { false, true })
        {
            var game = new ActEngine(12);
            if (map == 2) game.EnterNextMap(12, roomAdventure: false);
            ActSupply camp = game.Supplies[0];
            camp.Collected = used;
            game.PlacePlayerForTest(camp.X - 90, camp.Y + 28 - game.PlayerHeight);
            using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
            canvas.ResetFx(); canvas.Tick(.2f);
            using var picture = new Bitmap(1280, 720);
            canvas.DrawToBitmap(picture, canvas.ClientRectangle);
            picture.Save(Path.Combine(AppContext.BaseDirectory,
                $"camp-map{map}-{(used ? "used" : "ready")}.png"), ImageFormat.Png);
        }
        Console.WriteLine("PASS both biomes' ready/used supply camps rendered offscreen.");
    }
}
