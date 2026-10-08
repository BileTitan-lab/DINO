using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// World-space camp scenery, shared by both biomes. The supply's state is read
// directly so the empty basket and caption persist after its one-time use.
internal static class ActSupplyVisual
{
    public static void Draw(Graphics g, ActSupply camp, int map, float time)
    {
        float x = camp.X, y = camp.Y + 28f;
        using var wood = new SolidBrush(Color.FromArgb(66, 55, 46));
        using var cloth = new SolidBrush(map == 4 ? Color.FromArgb(104, 82, 78) : Color.FromArgb(73, 91, 78));
        using var lining = new SolidBrush(map == 4 ? Color.FromArgb(153, 116, 91) : Color.FromArgb(118, 135, 107));
        using var moss = new SolidBrush(map == 4 ? Color.FromArgb(83, 72, 66) :
            map == 1 ? Color.FromArgb(99, 112, 85) : Color.FromArgb(102, 104, 77));
        using var stone = new SolidBrush(Color.FromArgb(133, 128, 103));
        using var shadow = new SolidBrush(Color.FromArgb(75, 26, 36, 32));
        using var rope = new Pen(Color.FromArgb(183, 165, 119), 2f);
        using var leaf = new Pen(map == 4 ? Color.FromArgb(133, 104, 77) : Color.FromArgb(91, 111, 78), 3f);

        // Soft pool of light and a greener ground surface mark the change of pace.
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(x - 270, y - 185, 540, 210);
            using var glow = new PathGradientBrush(path) { CenterColor =
                Color.FromArgb(75, 243, 218, 149), SurroundColors = [Color.Transparent] };
            g.FillPath(glow, path);
        }
        g.FillPolygon(moss, new PointF[] { new(x - 260, y), new(x - 213, y - 4),
            new(x - 90, y - 1), new(x + 30, y - 5), new(x + 205, y - 3),
            new(x + 260, y), new(x + 205, y + 7), new(x - 220, y + 7) });
        for (int i = 0; i < 12; i++)
            g.FillRectangle(stone, x - 224 + i * 39, y + 4 + i % 2 * 3, 25, 3);

        // Low woven canopy, tied to two timber supports; all props are scenery.
        g.FillRectangle(wood, x - 112, y - 110, 5, 109);
        g.FillRectangle(wood, x + 95, y - 102, 5, 101);
        g.DrawLine(rope, x - 155, y - 1, x - 108, y - 113);
        g.DrawLine(rope, x + 97, y - 105, x + 140, y - 1);
        g.FillPolygon(cloth, new PointF[] { new(x - 130, y - 109), new(x - 35, y - 140),
            new(x + 116, y - 104), new(x + 95, y - 88), new(x - 18, y - 115),
            new(x - 108, y - 90) });
        g.DrawLines(rope, new PointF[] { new(x - 130, y - 109), new(x - 35, y - 140),
            new(x + 116, y - 104) });
        for (int i = 0; i < 5; i++)
            g.FillRectangle(lining, x - 89 + i * 34, y - 105 + Math.Abs(i - 2) * 3, 10, 4);
        g.FillEllipse(shadow, x - 130, y - 8, 265, 15);

        // Food basket and timber bench beneath the pickup.
        g.FillRectangle(wood, x - 35, y - 19, 70, 7);
        g.FillRectangle(wood, x - 29, y - 12, 5, 12);
        g.FillRectangle(wood, x + 24, y - 12, 5, 12);
        g.DrawArc(rope, x - 17, y - 32, 34, 22, 0, 180);
        for (int i = 0; i < 4; i++)
            g.DrawLine(rope, x - 12 + i * 8, y - 22, x - 10 + i * 7, y - 17);

        // Water basin and a cluster of reeds differentiate this from dry arenas.
        g.FillEllipse(stone, x + 145, y - 13, 59, 17);
        using var water = new SolidBrush(Color.FromArgb(109, 163, 156));
        g.FillEllipse(water, x + 151, y - 13, 47, 8);
        for (int i = 0; i < 7; i++)
        {
            float plantX = x + 217 + i * 4;
            float height = 17 + i % 3 * 7;
            g.DrawLine(leaf, plantX, y, plantX - 8 + i * 2, y - height);
            g.DrawLine(leaf, plantX, y - height / 2, plantX + 8, y - height + 3);
        }

        float fireX = x - 191;
        for (int i = 0; i < 5; i++)
            g.FillRectangle(stone, fireX - 22 + i * 9, y - 5 + i % 2 * 2, 8, 6);
        g.FillRectangle(wood, fireX - 16, y - 8, 33, 4);
        using var ember = new SolidBrush(Color.FromArgb(217, 130, 65));
        using var flame = new SolidBrush(Color.FromArgb(242, 209, 128));
        float flicker = MathF.Sin(time * 8f) * 3f;
        g.FillPolygon(ember, new PointF[] { new(fireX - 13, y - 8),
            new(fireX - 8, y - 24), new(fireX - 2, y - 17),
            new(fireX + 5, y - 35 + flicker), new(fireX + 12, y - 8) });
        g.FillPolygon(flame, new PointF[] { new(fireX - 6, y - 8),
            new(fireX + 2, y - 23 - flicker), new(fireX + 6, y - 8) });
        for (int i = 0; i < 4; i++)
        {
            float rise = (time * 17 + i * 13) % 58;
            g.FillRectangle(flame, fireX + MathF.Sin(time + i) * 9, y - 25 - rise, 2, 2);
        }

        // Compact diegetic sign; the gameplay HUD remains unchanged.
        using var sign = new SolidBrush(Color.FromArgb(215, 40, 48, 43));
        using var text = new SolidBrush(Color.FromArgb(226, 222, 195));
        using var title = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var detail = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center };
        g.FillRectangle(sign, x - 91, y - 87, 182, 42);
        g.DrawString(camp.Name, title, text, new RectangleF(x - 90, y - 85, 180, 20), format);
        g.DrawString(camp.Collected ? "补给已取用" : "生命 +2  ·  骨刺补满", detail, text,
            new RectangleF(x - 90, y - 66, 180, 18), format);
    }
}
