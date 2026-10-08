using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

internal static class ActAdvancedMapVisual
{
    public static void DrawBackdrop(Graphics g, int chapter, float cameraX, float cameraY, float time)
    {
        bool forest = chapter == 3;
        using var sky = new LinearGradientBrush(new Rectangle(0, -300, 960, 1100),
            forest ? Color.FromArgb(131, 155, 124) : Color.FromArgb(135, 87, 79),
            forest ? Color.FromArgb(35, 57, 51) : Color.FromArgb(40, 29, 42), 90f);
        g.FillRectangle(sky, 0, -300, 960, 1100);
        for (int layer = 0; layer < 3; layer++)
        {
            float spacing = forest ? 245 : 310;
            float shift = cameraX * (.08f + layer * .08f) % spacing;
            using var body = new SolidBrush(forest
                ? Color.FromArgb(65 + layer * 18, 89 + layer * 6, 72 + layer * 5)
                : Color.FromArgb(98 - layer * 15, 66 - layer * 7, 67 - layer * 5));
            using var edge = new Pen(Color.FromArgb(45, forest ? Color.LightGoldenrodYellow : Color.Coral), 3);
            for (int i = -2; i < 6; i++)
            {
                float x = i * spacing - shift, y = -cameraY * .15f;
                if (forest)
                {
                    g.FillPolygon(body, [new PointF(x, -300), new PointF(x + 47, -300), new PointF(x + 73, 620), new PointF(x - 33, 620)]);
                    g.DrawLine(edge, x + 13, -120, x + 15, 540);
                    g.FillPolygon(body, [new PointF(x + 20, y + 170), new PointF(x + 127, y + 90),
                        new PointF(x + 142, y + 87), new PointF(x + 37, y + 198)]);
                    g.FillEllipse(body, x - 96, y - 30 - layer * 27, 270, 120);
                }
                else
                {
                    g.FillPolygon(body, [new PointF(x - 65, 560), new PointF(x + 20, y + 185),
                        new PointF(x + 73, y + 80 + layer * 25), new PointF(x + 140, y + 110), new PointF(x + 270, 560)]);
                    g.DrawLines(edge, [new PointF(x + 73, y + 115), new PointF(x + 97, y + 215), new PointF(x + 80, y + 262)]);
                }
            }
        }
        using var mote = new SolidBrush(Color.FromArgb(130, forest ? Color.FromArgb(207, 221, 141) : Color.FromArgb(255, 163, 92)));
        for (int i = 0; i < 32; i++)
        {
            float x = ((i * 97 - cameraX * .16f + time * 8) % 1000 + 1000) % 1000;
            float y = ((i * 67 - time * (forest ? 3 : 13)) % 450 + 450) % 450;
            g.FillRectangle(mote, x, y, 2, 3);
        }
    }

    public static void DrawPlatform(Graphics g, RectangleF rect, int chapter, bool warning = false)
    {
        using var body = new SolidBrush(chapter == 3 ? Color.FromArgb(64, 56, 40) : Color.FromArgb(61, 44, 46));
        using var lip = new Pen(warning ? Color.FromArgb(255, 195, 106) : chapter == 3
            ? Color.FromArgb(166, 184, 106) : Color.FromArgb(186, 145, 128), 3);
        using var grain = new Pen(chapter == 3 ? Color.FromArgb(112, 100, 60) : Color.FromArgb(110, 76, 68), 1);
        g.FillRectangle(body, rect);
        g.DrawLine(lip, rect.Left, rect.Top, rect.Right, rect.Top);
        for (float x = rect.Left + 12; x < rect.Right - 10; x += 28)
        {
            g.DrawLine(grain, x, rect.Top + 6, x + 18, rect.Top + 9);
            if (chapter == 3) g.DrawLine(grain, x + 5, rect.Bottom, x + 9, rect.Bottom + 9);
            if (warning) g.DrawLines(lip, [new PointF(x, rect.Top), new PointF(x + 6, rect.Top + 5), new PointF(x + 1, rect.Bottom)]);
        }
    }

    public static void DrawBackgroundProps(Graphics g, ActEngine game, float time)
    {
        float left = game.CameraX - 100, right = game.CameraX + 1060;
        DrawRoomLandmarks(g, game, left, right, time);
        foreach (var camp in game.Supplies)
            if (camp.Right > left && camp.Left < right)
                DrawTransition(g, camp, game.MapIndex, time);
    }

    public static void DrawPlatformSupports(Graphics g, ActEngine game)
    {
        if (game.MapIndex != 3) return;
        float left = game.CameraX - 100, right = game.CameraX + 1060;
        using var wood = new Pen(Color.FromArgb(75, 99, 65), 5);
        using var vine = new Pen(Color.FromArgb(119, 148, 84), 2);
        foreach (var shelf in game.Layout.Solids.Where(s => s.Height <= 16 &&
            s.Right > left && s.Left < right))
        {
            float center = shelf.Left + shelf.Width / 2;
            if (game.TerrainYAt(center) is not float floor ||
                floor - shelf.Bottom is < 25 or > 330) continue;
            float endX = center + 15;
            g.DrawBezier(wood, new PointF(center, shelf.Bottom),
                new PointF(center - 4, shelf.Bottom + 30),
                new PointF(endX + 22, floor - 35), new PointF(endX, floor));
            g.DrawLine(vine, shelf.Left + 18, shelf.Bottom,
                shelf.Left + 25, Math.Min(floor, shelf.Bottom + 34));
        }
    }

    public static void DrawEnvironment(Graphics g, ActEngine game, float time)
    {
        float left = game.CameraX - 100, right = game.CameraX + 1060;
        bool forest = game.MapIndex == 3;
        using var stem = new Pen(Color.FromArgb(101, 128, 77), 2);
        using var stone = new SolidBrush(Color.FromArgb(90, forest ? Color.FromArgb(140, 167, 107) : Color.FromArgb(211, 120, 85)));
        for (int cell = (int)(left / 160); cell <= (int)(right / 160); cell++)
        {
            float x = cell * 160 + 31;
            if (game.Supplies.Any(c => c.Contains(x))) continue;
            if (game.TerrainYAt(x) is not float y) continue;
            if (forest)
            {
                for (int n = 0; n < 3; n++)
                {
                    float rootX = x + 8 + n * 12;
                    float rootY = game.TerrainYAt(rootX) ?? y;
                    g.DrawLine(stem, rootX, rootY, rootX - 13, rootY - 18 - n * 6);
                    g.FillEllipse(stone, rootX - 19, rootY - 25 - n * 6, 17, 7);
                }
            }
            else
            {
                float rightY = game.TerrainYAt(x + 29) ?? y;
                g.FillPolygon(stone, [new PointF(x, y), new PointF(x + 8, y - 21),
                    new PointF(x + 20, rightY - 28), new PointF(x + 29, rightY)]);
            }
        }
        foreach (var p in game.Layout.MovingPlatforms)
        {
            if (p.Origin.Right < left - 150 || p.Origin.Left > right + 150) continue;
            if (!p.Active)
            {
                using var ghost = new Pen(Color.FromArgb(60, Color.Wheat), 1) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(ghost, p.Origin.X, p.Origin.Y, p.Origin.Width, p.Origin.Height);
                continue;
            }
            RectangleF rect = p.Bounds;
            if (p.Warning) rect.Offset(MathF.Sin(time * 45) * 1.5f, 0);
            if (p.Motion is ActPlatformMotion.Falling or ActPlatformMotion.Collapse)
            {
                using var clip = new GraphicsPath();
                float leftFloor = game.TerrainYAt(rect.Left + 1) ?? rect.Bottom + 20;
                float rightFloor = game.TerrainYAt(rect.Right - 1) ?? rect.Bottom + 20;
                clip.AddPolygon([new PointF(rect.Left, rect.Top - 4),
                    new PointF(rect.Right, rect.Top - 4),
                    new PointF(rect.Right, rightFloor - 2),
                    new PointF(rect.Left, leftFloor - 2)]);
                var saved = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                DrawPlatform(g, rect, game.MapIndex, p.Warning);
                g.Restore(saved);
            }
            else DrawPlatform(g, rect, game.MapIndex, p.Warning);
        }
        foreach (var m in game.Layout.Mechanisms)
        {
            if (m.X + m.Width < left || m.X > right) continue;
            DrawMechanism(g, game, m, time);
        }
    }

    private static void DrawRoomLandmarks(Graphics g, ActEngine game,
        float left, float right, float time)
    {
        string[] forestNames = ["巨根阶庭", "叶影断廊", "空心古树",
            "双峰藤谷", "幽荧菌谷", "鸟巢断枝", "沉根回声"];
        string[] volcanoNames = ["阶叠熔湖", "黑曜石台地", "脉动喷口",
            "倾斜玄武岩", "灰烬竖井", "断桥回廊", "余烬阶桥"];
        foreach (var room in game.Layout.Rooms)
        {
            if (room.Node.EndX < left || room.Node.StartX > right) continue;
            string label = room.Variant.Id[(room.Variant.Id.IndexOf(' ') + 1)..];
            int motif = Array.IndexOf(game.MapIndex == 3 ? forestNames : volcanoNames, label);
            if (motif < 0) continue;
            foreach (float offset in new[] { 460f, 2250f, 3550f })
            {
                float x = room.Node.StartX + offset;
                if (x < left - 90 || x > right + 90 ||
                    game.TerrainYAt(x) is not float y) continue;
                if (game.MapIndex == 3)
                {
                    using var dark = new SolidBrush(Color.FromArgb(61, 83, 54));
                    using var light = new Pen(Color.FromArgb(126, 158, 93), 3);
                    switch (motif)
                    {
                        case 0: case 6:
                            g.DrawArc(light, x - 58, y - 130, 115, 160, 188, 174);
                            g.DrawArc(light, x - 35, y - 105, 77, 120, 183, 165);
                            break;
                        case 1: case 5:
                            // A broad V-shaped prop looked like a walkable
                            // branch. Use fine background vines instead.
                            for (int branch = -1; branch <= 1; branch++)
                            {
                                float bx = x + branch * 37;
                                g.DrawBezier(light, new PointF(bx, y),
                                    new PointF(bx - 10, y - 34),
                                    new PointF(bx + 25, y - 72),
                                    new PointF(bx + 13, y - 116));
                            }
                            break;
                        case 2:
                            float rootLeft = game.TerrainYAt(x - 48) ?? y;
                            float rootRight = game.TerrainYAt(x + 48) ?? y;
                            float hollowBase = Math.Min(rootLeft, rootRight) - 5;
                            g.FillPolygon(dark, [new PointF(x - 48, rootLeft + 3),
                                new PointF(x - 32, y - 64), new PointF(x - 28, y - 147),
                                new PointF(x - 9, y - 159), new PointF(x + 6, y - 145),
                                new PointF(x + 26, y - 151), new PointF(x + 31, y - 58),
                                new PointF(x + 48, rootRight + 3)]);
                            using (var hollow = new SolidBrush(Color.FromArgb(30, 50, 43)))
                                g.FillEllipse(hollow, x - 20, hollowBase - 78, 40, 78);
                            g.DrawArc(light, x - 21, hollowBase - 79, 42, 78, 190, 160);
                            using (var bark = new Pen(Color.FromArgb(76, 99, 64), 2))
                            {
                                g.DrawLine(bark, x - 23, y - 137, x - 28, hollowBase - 22);
                                g.DrawLine(bark, x + 22, y - 131, x + 29, hollowBase - 12);
                            }
                            break;
                        case 3:
                            for (int i = -1; i <= 1; i++)
                                g.DrawBezier(light, new PointF(x + i * 36, y),
                                    new PointF(x + i * 38 + 25, y - 75),
                                    new PointF(x + i * 38 - 20, y - 119),
                                    new PointF(x + i * 38, y - 185));
                            break;
                        default:
                            using (var glow = new SolidBrush(Color.FromArgb(85, 181, 224, 128)))
                                for (int i = -2; i <= 2; i++)
                                {
                                    float sx = x + i * 25;
                                    float sy = y - 35 - i % 2 * 15;
                                    g.FillEllipse(glow, sx - 12, sy - 10, 25, 20);
                                    g.DrawLine(light, sx, sy, sx, y);
                                }
                            break;
                    }
                }
                else
                {
                    using var rock = new SolidBrush(Color.FromArgb(64, 50, 52));
                    using var edge = new Pen(Color.FromArgb(motif % 2 == 0 ? 218 : 148,
                        motif % 2 == 0 ? 120 : 132, 96), 2);
                    if (motif is 0 or 1 or 3)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            float sx = x + i * 38;
                            float h = 55 + (i + motif + 3) % 3 * 25;
                            g.FillPolygon(rock, [new PointF(sx - 16, y),
                                new PointF(sx - 7, y - h), new PointF(sx + 9, y - h - 12),
                                new PointF(sx + 23, y)]);
                            g.DrawLine(edge, sx - 7, y - h, sx + 9, y - h - 12);
                        }
                    }
                    else if (motif is 2 or 4)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            float sx = x + i * 31;
                            g.DrawArc(edge, sx - 17, y - 90, 34, 60, 180, 160);
                            using var ash = new SolidBrush(Color.FromArgb(100, 210, 168, 134));
                            g.FillEllipse(ash, sx - 11 + MathF.Sin(time + i) * 6,
                                y - 125 - (time * 12 + i * 20) % 30, 14, 7);
                        }
                    }
                    else
                    {
                        g.FillPolygon(rock, [new PointF(x - 70, y),
                            new PointF(x - 42, y - 52), new PointF(x + 23, y - 42),
                            new PointF(x + 78, y - 11), new PointF(x + 64, y)]);
                        g.DrawLines(edge, [new PointF(x - 42, y - 52),
                            new PointF(x - 5, y - 29), new PointF(x + 23, y - 42)]);
                    }
                }
            }
        }
    }

    private static void DrawTransition(Graphics g, ActSupply camp, int chapter, float time)
    {
        float x = camp.X, y = camp.Y + 28;
        if (chapter == 3)
        {
            bool fungi = camp.Name.Contains("菌光");
            bool roots = camp.Name.Contains("盘根");
            using var trunk = new Pen(roots ? Color.FromArgb(80, 71, 49) :
                Color.FromArgb(71, 94, 59), roots ? 11 : 6);
            using var leaf = new SolidBrush(fungi ? Color.FromArgb(157, 195, 111) :
                Color.FromArgb(77, 117, 78));
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = x + side * 302;
                g.DrawBezier(trunk, new PointF(sx, y), new PointF(sx - side * 45, y - 80),
                    new PointF(sx + side * 15, y - 120), new PointF(sx - side * 55, y - 185));
                for (int i = 0; i < 4; i++)
                {
                    float px = sx - side * (i * 23 + 15);
                    float py = y - 25 - i % 2 * 13;
                    if (fungi)
                    {
                        using var glow = new SolidBrush(Color.FromArgb(38 + (int)(Math.Abs(Math.Sin(time * 2 + i)) * 30), 177, 219, 148));
                        g.FillEllipse(glow, px - 12, py - 21, 30, 29);
                        g.FillEllipse(leaf, px - 6, py - 13, 16, 8);
                    }
                    else
                    {
                        using var stem = new Pen(Color.FromArgb(99, 121, 73), 2);
                        g.DrawLine(stem, px + side * 7, y + 2, px, py - 4);
                        g.FillEllipse(leaf, px - 7, py - 6, 17, 7);
                    }
                }
            }
            if (camp.Name.Contains("静藤"))
            {
                using var vine = new Pen(Color.FromArgb(100, 147, 112), 3);
                for (int i = 0; i < 6; i++)
                {
                    float vx = x - 240 + i * 95;
                    g.DrawBezier(vine, new PointF(vx, y - 220), new PointF(vx - 18, y - 178),
                        new PointF(vx + 17, y - 157), new PointF(vx, y - 120));
                }
            }
        }
        else
        {
            bool obsidian = camp.Name.Contains("黑曜");
            bool embers = camp.Name.Contains("余烬");
            using var rock = new SolidBrush(obsidian ? Color.FromArgb(31, 33, 39) :
                Color.FromArgb(79, 59, 59));
            using var crack = new Pen(embers ? Color.FromArgb(232, 127, 70) :
                Color.FromArgb(129, 113, 107), 2);
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = x + side * 305;
                g.FillPolygon(rock, [new PointF(sx - 31, y), new PointF(sx - 18, y - 71),
                    new PointF(sx + 7, y - 119), new PointF(sx + 32, y)]);
                g.DrawLines(crack, [new PointF(sx + 4, y - 100),
                    new PointF(sx - 7, y - 54), new PointF(sx + 8, y - 17)]);
                if (embers)
                {
                    using var ember = new SolidBrush(Color.FromArgb(180, 245, 149, 88));
                    for (int i = 0; i < 8; i++)
                        g.FillRectangle(ember, sx + MathF.Sin(time * 2 + i) * 35,
                            y - 25 - (time * 17 + i * 17) % 92, 2, 3);
                }
            }
            if (camp.Name.Contains("灰岩"))
            {
                using var rib = new Pen(Color.FromArgb(169, 156, 134), 7);
                for (int i = -2; i <= 2; i++)
                    g.DrawArc(rib, x + i * 88 - 39, y - 85, 79, 90, 180, 155);
            }
        }
    }

    private static void DrawMechanism(Graphics g, ActEngine game, ActMechanism m, float time)
    {
        using var warm = new SolidBrush(Color.FromArgb(240, 156, 84));
        using var bright = new Pen(Color.FromArgb(255, 213, 129), 2);
        if (m.Kind == ActMechanismKind.Lava)
        {
            using var glow = new LinearGradientBrush(new RectangleF(m.X, m.Y - 90, m.Width, 90),
                Color.FromArgb(0, 250, 108, 40), Color.FromArgb(115, 250, 108, 40), 90f);
            g.FillRectangle(glow, m.X, m.Y - 90, m.Width, 90);
            using var pool = new LinearGradientBrush(new RectangleF(m.X, m.Y, m.Width, 250),
                Color.FromArgb(237, 106, 49), Color.FromArgb(72, 33, 37), 90f);
            g.FillRectangle(pool, m.X, m.Y, m.Width, 600);
            for (float x = m.X; x < m.X + m.Width; x += 30)
                g.DrawLine(bright, x, m.Y + MathF.Sin(x + time * 3) * 2, Math.Min(x + 22, m.X + m.Width), m.Y);
            return;
        }
        if (m.Kind == ActMechanismKind.Mushroom)
        {
            float floor = game.TerrainYAt(m.X + m.Width / 2) ?? m.Y;
            using var stalk = new SolidBrush(Color.FromArgb(200, 195, 141));
            using var cap = new SolidBrush(Color.FromArgb(144, 176, 94));
            g.FillRectangle(stalk, m.X + m.Width * .4f, floor - 15, m.Width * .2f, 15);
            g.FillEllipse(cap, m.X, floor - 24, m.Width, 19);
            for (int i = 1; i <= 3; i++) g.FillEllipse(stalk, m.X + i * m.Width / 5, floor - 20, 7, 5);
            using var pulse = new Pen(Color.FromArgb(150, 203, 231, 151), 2);
            float y = floor - 32 - (time * 17 % 16);
            g.DrawLines(pulse, [new PointF(m.X + m.Width / 2 - 5, y + 4), new PointF(m.X + m.Width / 2, y), new PointF(m.X + m.Width / 2 + 5, y + 4)]);
            return;
        }
        bool air = m.Kind == ActMechanismKind.Updraft;
        float leftY = game.TerrainYAt(m.X + 2) ?? m.Y;
        float rightY = game.TerrainYAt(m.X + m.Width - 2) ?? m.Y;
        float markerY = (leftY + rightY) / 2;
        using var rim = new Pen(air ? Color.FromArgb(141, 204, 197) : Color.FromArgb(161, 91, 74), 3);
        g.DrawLine(rim, m.X, leftY - 3, m.X + m.Width, rightY - 3);
        if (m.Warning || m.Active)
        {
            using var zone = new SolidBrush(Color.FromArgb(m.Warning ? 50 + (int)(Math.Abs(Math.Sin(time * 8)) * 60) : 100,
                air ? Color.Aquamarine : Color.Orange));
            for (int i = 0; i < 5; i++)
            {
                float ax = m.X + i * m.Width / 5;
                float bx = m.X + (i + 1) * m.Width / 5;
                float ay = game.TerrainYAt(ax) ?? markerY;
                float by = game.TerrainYAt(bx) ?? markerY;
                g.FillPolygon(zone, [new PointF(ax, ay - 8), new PointF(bx, by - 8),
                    new PointF(bx, by + 2), new PointF(ax, ay + 2)]);
            }
            if (m.Warning && !air)
            {
                // A persistent ground marker and overhead chevron precede damage.
                g.DrawLines(bright, [new PointF(m.X + m.Width / 2 - 7, markerY - 48),
                    new PointF(m.X + m.Width / 2, markerY - 38), new PointF(m.X + m.Width / 2 + 7, markerY - 48)]);
                g.DrawLine(bright, m.X, leftY - 3, m.X + m.Width, rightY - 3);
            }
        }
        if (!m.Active) return;
        if (m.Kind == ActMechanismKind.Rock)
        {
            var r = m.Bounds;
            using var rock = new SolidBrush(Color.FromArgb(107, 82, 73));
            using var trail = new Pen(Color.FromArgb(130, 234, 164, 107), 4);
            g.DrawLine(trail, r.X + r.Width / 2, r.Y - 35, r.X + r.Width / 2, r.Y);
            g.FillPolygon(rock, [new PointF(r.X, r.Y + 9), new PointF(r.X + 11, r.Y), new PointF(r.Right - 5, r.Y + 2),
                new PointF(r.Right, r.Bottom - 9), new PointF(r.X + 9, r.Bottom)]);
            g.DrawLine(bright, r.X + 11, r.Y + 3, r.Right - 8, r.Y + 5);
        }
        else
        {
            using var plume = new SolidBrush(Color.FromArgb(air ? 90 : 130, air ? Color.PaleTurquoise : Color.OrangeRed));
            var outline = new List<PointF> { new(m.X, leftY) };
            for (int i = 0; i <= 8; i++)
                outline.Add(new PointF(m.X + m.Width * i / 8,
                    MathF.Min(leftY, rightY) - 150 + (i % 2 == 0 ? 0 : 12 + MathF.Sin(time * 15 + i) * 9)));
            outline.Add(new PointF(m.X + m.Width, rightY));
            g.FillPolygon(plume, outline.ToArray());
            using var streak = new Pen(air ? Color.PaleTurquoise : Color.FromArgb(255, 205, 111), 2);
            for (int i = 0; i < 6; i++)
            {
                float y = markerY - (time * 160 + i * 25) % 145;
                g.DrawLine(streak, m.X + 8 + i * (m.Width - 16) / 6, y,
                    m.X + 8 + i * (m.Width - 16) / 6, y - 12);
            }
        }
    }
}

