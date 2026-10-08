using DinoDash.Desktop;

internal static class AdvancedMapChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("ACT complete rest clearings are flat and free of platforms", () =>
        {
            foreach (int chapter in new[] { 2, 3, 4 })
            foreach (int seed in Enumerable.Range(1, 40))
            {
                var map = chapter == 2 ? ActSecondMap.Generate(seed) : ActAdvancedMaps.Generate(chapter, seed);
                foreach (var camp in map.Supplies)
                {
                    for (float x = camp.X - 449; x <= camp.X + 449; x += 17)
                        Require(ActSecondMap.GroundAt(map, x) == 300, "Rest clearing has a slope");
                    Require(camp.Y == 272, "Supply does not match the clearing floor");
                    Require(!map.Solids.Any(s => s.Left < camp.X + 449 && s.Right > camp.X - 449),
                        "Rest clearing contains platforms");
                }
            }
        });
        check("ACT advanced chapters have deterministic authored sockets, lengths and safe camps", () =>
        {
            foreach (int chapter in new[] { 3, 4 })
            {
                var signatures = new HashSet<string>();
                var families = new HashSet<string>();
                var camps = new HashSet<string>();
                for (int seed = 1; seed <= 80; seed++)
                {
                    var map = ActAdvancedMaps.Generate(chapter, seed);
                    Require(map.Width == (chapter == 3 ? 32700 : 43600));
                    Require(map.Signature == ActAdvancedMaps.Generate(chapter, seed).Signature);
                    signatures.Add(map.Signature);
                    var roomNames = map.Rooms.Select(r => r.Variant.Id.Split(' ', 2)[1]).ToArray();
                    foreach (string name in roomNames) families.Add(name);
                    foreach (var camp in map.Supplies) camps.Add(camp.Name);
                    Require(roomNames.Distinct().Count() == roomNames.Length,
                        $"Repeated terrain family in chapter {chapter}, seed {seed}");
                    Require(map.Rooms.Count(r =>
                        !r.Variant.Id.Contains("-C") &&
                        r.Variant.Id.Split(' ', 2)[0].Count(c => c == '-') == 1) == 1,
                        "More than one copy of the old ground silhouette");
                    Require(map.Supplies.Select(c => c.Name).Distinct().Count() == map.Supplies.Count,
                        "Transition camps repeated in one run");
                    Require(map.Enemies[^1].Kind == ActEnemyKind.Boss && map.Enemies[^1].X > map.Width - 1000);
                    foreach (var room in map.Rooms)
                    {
                        Require(ActSecondMap.GroundAt(map, room.Node.StartX + 1) is float a && a < 301 && a > 299);
                        Require(ActSecondMap.GroundAt(map, room.Node.EndX - 1) is float b &&
                            Math.Abs(b - 300) < 1.5f, $"Module socket mismatch chapter {chapter} seed {seed}");
                    }
                    foreach (var camp in map.Supplies)
                    {
                        Require(!map.Enemies.Any(e => camp.Contains(e.HomeX)));
                        Require(!map.Mechanisms.Any(m => m.X < camp.Right && m.X + m.Width > camp.Left));
                        Require(ActSecondMap.GroundAt(map, camp.Left) == 300 && ActSecondMap.GroundAt(map, camp.Right) == 300);
                    }
                    foreach (var enemy in map.Enemies.Where(e => e.Kind != ActEnemyKind.Flying))
                    {
                        float foot = enemy.Y + enemy.Height, x = enemy.X + enemy.Width / 2;
                        Require(ActSecondMap.GroundAt(map, x) is float y && Math.Abs(y - foot) < 2 ||
                            map.Solids.Any(s => x >= s.Left && x <= s.Right && Math.Abs(s.Top - foot) < 2),
                            $"Enemy spawn has no footing chapter={chapter} seed={seed} x={x} foot={foot} floor={ActSecondMap.GroundAt(map,x)} kind={enemy.Kind}");
                    }
                }
                Require(signatures.Count > 50, "Insufficient route/state variety");
                Require(families.Count >= 10 && camps.Count == 4,
                    $"Authored terrain or transitions missing in chapter {chapter}: {families.Count}/{camps.Count}");
            }
        });
        check("ACT authored platforms clear slopes and never overlap through full travel", () =>
        {
            foreach (int chapter in new[] { 3, 4 })
            foreach (int seed in Enumerable.Range(1, 300))
            {
                var map = ActAdvancedMaps.Generate(chapter, seed);
                var boxes = map.Solids.Concat(map.MovingPlatforms.Select(ActPlatformLayout.TravelBounds)).ToArray();
                for (int i = 0; i < boxes.Length; i++)
                {
                    var box = boxes[i];
                    for (float sx = box.Left + 1; sx < box.Right; sx += 5)
                    {
                        if (ActSecondMap.GroundAt(map, sx) is float floor)
                            Require(floor - box.Bottom >= ActPlatformLayout.MinimumGroundClearance - .1f,
                                $"Too-low platform chapter={chapter} seed={seed} x={sx} gap={floor-box.Bottom}");
                    }
                    for (int j = 0; j < i; j++)
                    {
                        var intersection = System.Drawing.RectangleF.Intersect(box, boxes[j]);
                        Require(intersection.Width <= .02f || intersection.Height <= .02f,
                            $"Overlapping platforms chapter={chapter} seed={seed} {i}/{j}");
                    }
                }
            }
        });
        check("ACT forest lower routes walk end to end and camera follows without snaps", () =>
        {
            // All three authored route states and three structural variants.
            for (int seed = 1; seed <= 18; seed++)
            {
                var game = Quiet(3, seed);
                game.RightHeld = true;
                float lastCamera = game.CameraY;
                for (int tick = 0; tick < 20000 && game.PlayerX < game.ExitX; tick++)
                {
                    game.Update(ActTuning.Step);
                    Require(Math.Abs(game.CameraY - lastCamera) <= 2.01f, "Camera jumped");
                    lastCamera = game.CameraY;
                    Require(!game.IsDead, "Forest lower route killed player");
                }
                Require(game.PlayerX >= game.ExitX, $"Forest trapped at {game.PlayerX}, {game.PlayerY}");
            }
        });
        check("ACT canopy branch can be climbed and crossed with base jumps", () =>
        {
            foreach (float phase in new[] { 0f, 1.2f, 3f })
            {
                int seed = Enumerable.Range(1, 200).First(s =>
                    ActAdvancedMaps.Generate(3, s).Rooms[0].Variant.Id.StartsWith("T1-0 "));
                var game = Quiet(3, seed);
                float x = game.Layout.Rooms[0].Node.StartX;
                foreach (var p in game.Layout.MovingPlatforms) p.Update(phase, 0, false);
                game.PlacePlayerForTest(x + 400, 245 - game.PlayerHeight);
                game.RightHeld = true; game.JumpHeld = true;
                bool reachedCrown = false;
                for (int tick = 0; tick < 1500 && game.PlayerX < x + 2130; tick++)
                {
                    if (game.Grounded)
                    {
                        var support = game.Solids.FirstOrDefault(s => game.PlayerX + 27 >= s.Left && game.PlayerX + 27 < s.Right &&
                            Math.Abs(game.PlayerY + game.PlayerHeight - s.Top) < 3);
                        if (support.Width > 0 && game.PlayerX >= support.Right - 48) game.JumpRequested = true;
                        if (game.PlayerX >= x + 940 && game.PlayerY + game.PlayerHeight <= 112) reachedCrown = true;
                    }
                    game.Update(ActTuning.Step);
                }
                Require(reachedCrown && game.PlayerX > x + 2130 && game.Health == game.MaximumHealth,
                    $"Canopy route failed at phase {phase}: {game.PlayerX - x}, crown={reachedCrown}");
            }
        });
        check("ACT seven new forest route families offer traversable raised branches", () =>
        {
            for (int shape = 3; shape <= 9; shape++)
            {
                int localShape = shape;
                int seed = Enumerable.Range(1, 900).First(s =>
                    ActAdvancedMaps.Generate(3, s).Rooms[0].Variant.Id.StartsWith($"T1-{localShape}-1 "));
                var game = Quiet(3, seed);
                float x = game.Layout.Rooms[0].Node.StartX;
                var first = game.Layout.Solids.Where(s => s.Left >= x + 400 && s.Left < x + 620)
                    .OrderBy(s => s.Left).First();
                game.PlacePlayerForTest(first.Left + 30, first.Top - game.PlayerHeight);
                game.RightHeld = game.JumpHeld = true;
                var visited = new HashSet<float>();
                for (int tick = 0; tick < 1700 && game.PlayerX < x + 2600; tick++)
                {
                    if (game.Grounded)
                    {
                        var support = game.Solids.FirstOrDefault(s =>
                            game.PlayerX + 27 >= s.Left && game.PlayerX + 27 < s.Right &&
                            Math.Abs(game.PlayerY + game.PlayerHeight - s.Top) < 3);
                        if (support.Width > 0)
                        {
                            visited.Add(support.Left);
                            if (game.PlayerX >= support.Right - 48) game.JumpRequested = true;
                        }
                    }
                    game.Update(ActTuning.Step);
                }
                Require(game.PlayerX > x + 2500 && visited.Count >= 3 &&
                    game.Health == game.MaximumHealth,
                    $"New forest branch {shape} failed at {game.PlayerX-x}; ledges={visited.Count}");
            }
        });
        check("ACT moving platforms carry a stationary rider and falling platforms recover", () =>
        {
            foreach (var motion in new[] { ActPlatformMotion.Horizontal, ActPlatformMotion.Vertical })
            {
                var game = Quiet(3, 1);
                var platform = new ActDynamicPlatform(new(500, 180, 170, 14), motion, 55, 4);
                game.Layout.MovingPlatforms.Clear(); game.Layout.MovingPlatforms.Add(platform);
                game.PlacePlayerForTest(550, 90);
                Tick(game, .6f);
                Require(game.Grounded && Math.Abs(game.PlayerY + game.PlayerHeight - platform.Bounds.Top) < 2, "Rider did not land");
                float offset = game.PlayerX - platform.Bounds.X;
                for (int i = 0; i < 360; i++)
                {
                    game.Update(ActTuning.Step);
                    Require(game.Grounded && Math.Abs(game.PlayerY + game.PlayerHeight - platform.Bounds.Top) < 2, "Platform lost rider");
                    Require(Math.Abs(game.PlayerX - platform.Bounds.X - offset) < 2);
                }
            }
            var fall = new ActDynamicPlatform(new(0, 0, 150, 14), ActPlatformMotion.Falling);
            fall.Update(.1f, 0, true);
            Require(fall.Warning && fall.Bounds.Y == 0 && fall.Active);
            for (int i = 0; i < 200; i++) fall.Update(.01f, 0, false);
            Require(!fall.Active && fall.Bounds.Y > 100);
            for (int i = 0; i < 310; i++) fall.Update(.01f, 0, false);
            Require(fall.Active && fall.Bounds == fall.Origin && fall.Age < 0);
        });
        check("ACT mushroom launches without holding jump and lower shaft can be exited", () =>
        {
            var game = Quiet(3, 1);
            var mushroom = game.Layout.Mechanisms.First(m => m.Kind == ActMechanismKind.Mushroom);
            game.PlacePlayerForTest(mushroom.X + 8, mushroom.Y - game.PlayerHeight);
            float start = game.PlayerY;
            Tick(game, .45f);
            Require(game.PlayerY < start - 170 && !game.JumpHeld, "Bounce was cut by jump release");
        });
        check("ACT volcano authored lava stones clear with base jump at varied phases", () =>
        {
            foreach (int seed in Enumerable.Range(1, 18))
            foreach (float phase in new[] { 0f, 1.1f, 2.8f })
            {
                var game = Quiet(4, seed);
                foreach (var room in game.Layout.Rooms.Where(r => !r.Variant.Id.Contains("-C")))
                {
                    float x = room.Node.StartX;
                    foreach (var pool in game.Layout.Mechanisms.Where(m =>
                        m.Kind == ActMechanismKind.Lava && m.X >= x && m.X < room.Node.EndX))
                    {
                        foreach (var p in game.Layout.MovingPlatforms) p.Update(phase, 0, false);
                        float lip = pool.X, bank = lip + pool.Width;
                        float start = lip - 140;
                        game.PlacePlayerForTest(start,
                            game.TerrainYAt(start + 27)!.Value - game.PlayerHeight);
                        game.RightHeld = true; game.JumpHeld = true;
                        int hp = game.Health;
                        for (int i = 0; i < 680 && game.PlayerX < bank + 50; i++)
                        {
                            if (game.Grounded && game.PlayerX >= lip - 72)
                                game.JumpRequested = true;
                            game.Update(ActTuning.Step);
                        }
                        Require(game.PlayerX > bank + 20 && game.Health == hp,
                            $"Lava crossing failed seed {seed} phase {phase} module {room.Variant.Id} " +
                            $"lip={lip-x} width={pool.Width} x={game.PlayerX-x} hp={game.Health}");
                        game.ClearInput();
                    }
                }
            }
        });
        check("ACT collapse bridge allows basic running with 12 second pursuit and safe rescue", () =>
        {
            var game = Quiet(4, 3);
            var room = game.Layout.Rooms.Single(r => r.Variant.Id.Contains("-C"));
            game.PlacePlayerForTest(room.Node.StartX + 100, 248);
            game.RightHeld = true;
            Tick(game, 14.5f);
            Require(game.PlayerX > room.Node.StartX + 3400 && game.Health == game.MaximumHealth, "Collapse outran basic movement");
            Require(game.Layout.MovingPlatforms.Any(p => p.Motion == ActPlatformMotion.Collapse && !p.Active));
            game.ClearInput();
            game.PlacePlayerForTest(room.Node.StartX + 200, 248); Tick(game, .1f);
            game.PlacePlayerForTest(room.Node.StartX + 1000, 455); Tick(game, .1f);
            Require(game.PlayerX < room.Node.StartX + 400 && game.Health == game.MaximumHealth - 1, "Lava did not rescue to entrance");
        });
        check("ACT eruption and rock warn before damage, rocks hit enemies, camps stop danger", () =>
        {
            foreach (var kind in new[] { ActMechanismKind.Eruption, ActMechanismKind.Rock })
            {
                var game = Quiet(4, 1);
                game.Layout.Mechanisms.Clear();
                var m = new ActMechanism(kind, 300, 300, 70);
                game.Layout.Mechanisms.Add(m);
                game.PlacePlayerForTest(305, 248);
                Tick(game, 2.7f);
                Require(m.Warning && !m.Active && game.Health == game.MaximumHealth);
                Tick(game, .9f);
                Require(game.Health == game.MaximumHealth && m.Warning);
                Tick(game, 1.3f);
                Require(game.Health == game.MaximumHealth - 1, "Telegraphed hazard failed to hit");
            }
            var rockGame = Quiet(4, 1);
            rockGame.Layout.Mechanisms.Clear();
            var rock = new ActMechanism(ActMechanismKind.Rock, 600, 300, 80);
            rockGame.Layout.Mechanisms.Add(rock);
            var enemy = rockGame.Enemies[0]; enemy.X = 610; enemy.Y = 258; enemy.Health = 5; enemy.State = ActEnemyState.Patrol; enemy.Dormant = true;
            rockGame.PlacePlayerForTest(800, 248);
            Tick(rockGame, 5.1f);
            Require(enemy.Health == 3, "Rock must damage enemy once per cycle");
        });
        check("ACT layered ranged enemies do not aim through floors", () =>
        {
            var game = Quiet(3, 1);
            game.Layout.Solids.Add(new(200, 180, 600, 38));
            var enemy = game.Enemies.First(e => e.Kind == ActEnemyKind.Ranged);
            enemy.X = 350; enemy.Y = 138; enemy.State = ActEnemyState.Chase;
            game.PlacePlayerForTest(600, 248);
            Tick(game, 2f);
            Require(game.EnemyShots.Count == 0, "Cross-floor targeting");
        });
        check("ACT chapters 3 and 4 preserve RunState and complete real boss pickup/exit", () =>
        {
            var game = new ActEngine(7);
            game.Run.AddArt(BattleArtId.BoneArmor);
            game.Run.AddEvolution(EvolutionId.HollowBones);
            for (int chapter = 2; chapter <= 4; chapter++)
            {
                game.EnterNextMap(7);
                Require(game.MapIndex == chapter && game.Run.BattleArtCount == 1 && game.Run.Evolutions.Count >= 1);
                if (chapter == 2) continue;
                foreach (var e in game.Enemies) e.State = ActEnemyState.Dead;
                game.PlacePlayerForTest(game.Boss.X + 35, 248);
                Tick(game, 1.3f);
                Require(game.PendingPowerReward == ActRewardChoice.Evolution);
                game.ChooseEvolution(0);
                game.PlacePlayerForTest(game.ExitX, 248);
                Tick(game, 3.1f);
                Require(game.IsComplete);
            }
        });
    }

    private static ActEngine Quiet(int chapter, int seed)
    {
        var game = new ActEngine(1);
        game.LoadMapForDebug(chapter, seed, false);
        foreach (var e in game.Enemies) e.State = ActEnemyState.Dead;
        game.Layout.Rewards.Clear();
        game.Boss.State = ActEnemyState.Patrol; game.Boss.Dormant = true;
        return game;
    }
    private static void Tick(ActEngine game, float seconds)
    { for (int i = 0; i < (int)Math.Ceiling(seconds / ActTuning.Step); i++) game.Update(ActTuning.Step); }
    private static void Require(bool condition, string message = "advanced map assertion failed")
    { if (!condition) throw new InvalidOperationException(message); }
}
