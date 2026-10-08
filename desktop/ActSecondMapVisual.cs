using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// The canyon uses the same authored terrain segments as collision. There is no
// flat painted floor beneath a pit, so the silhouette tells the truth.
internal static class ActSecondMapVisual
{
    private static readonly Color Sky = Color.FromArgb(210, 195, 166);
    private static readonly Color Haze = Color.FromArgb(163, 151, 139);
    private static readonly Color Distant = Color.FromArgb(115, 109, 105);
    private static readonly Color Rock = Color.FromArgb(50, 47, 51);
    private static readonly Color Face = Color.FromArgb(72, 62, 59);
    private static readonly Color Rim = Color.FromArgb(181, 143, 99);

    public static void DrawBackdrop(Graphics g, float cameraX, float time)
    {
        using var gradient = new LinearGradientBrush(new Rectangle(0, 0, 960, 600),
            Sky, Color.FromArgb(69, 64, 67), LinearGradientMode.Vertical);
        g.FillRectangle(gradient, 0, 0, 960, 600);
        using var mist = new SolidBrush(Color.FromArgb(60, 229, 211, 178));
        using var far = new SolidBrush(Haze);
        using var near = new SolidBrush(Distant);
        float farShift = cameraX * .09f;
        for (int i = -2; i < 10; i++)
        {
            float x = i * 185f - farShift % 185f;
            g.FillPolygon(far, [new PointF(x - 75, 320), new PointF(x - 55, 245),
                new PointF(x - 21, 245), new PointF(x + 5, 184 + i % 3 * 13),
                new PointF(x + 66, 184 + i % 3 * 13), new PointF(x + 78, 313)]);
            g.FillRectangle(mist, x - 82, 276 + (i % 3) * 7, 170, 5);
        }
        float nearShift = cameraX * .22f;
        for (int i = -2; i < 10; i++)
        {
            float x = i * 275f - nearShift % 275f;
            g.FillPolygon(near, [new PointF(x - 80, 370), new PointF(x - 45, 295),
                new PointF(x + 7, 278), new PointF(x + 24, 235 + i % 2 * 20),
                new PointF(x + 105, 235 + i % 2 * 20), new PointF(x + 165, 370)]);
        }
        using var dust = new SolidBrush(Color.FromArgb(90, 238, 220, 179));
        for (int i = 0; i < 25; i++)
        {
            float x = (i * 157 - cameraX * .13f + time * (i % 3 + 1) * 5f) % 1100;
            if (x < -10) x += 1100;
            g.FillRectangle(dust, x, 65 + (i * 71) % 250, i % 4 == 0 ? 3 : 2, 2);
        }
    }

    public static void DrawTerrain(Graphics g, IReadOnlyList<ActTerrainSegment> terrain,
        float cameraX, int chapter = 2)
    {
        using var body = new SolidBrush(chapter == 1 ? Color.FromArgb(39,42,48) : chapter == 3 ? Color.FromArgb(28, 42, 36) : chapter == 4 ? Color.FromArgb(40, 31, 36) : Rock);
        using var face = new SolidBrush(chapter == 1 ? Color.FromArgb(55,58,61) : chapter == 3 ? Color.FromArgb(48, 62, 43) : chapter == 4 ? Color.FromArgb(77, 43, 40) : Face);
        using var top = new Pen(chapter == 1 ? Color.FromArgb(141,146,132) : chapter == 3 ? Color.FromArgb(140, 163, 93) : chapter == 4 ? Color.FromArgb(207, 127, 86) : Rim, 3f);
        using var seam = new Pen(Color.FromArgb(82, 97, 79, 74), 1f);
        float left = cameraX - 40, right = cameraX + 1000;
        // Fill each uninterrupted stretch as one polygon. Separate anti-aliased
        // polygons leave bright one-pixel slits where two sloped pieces meet.
        for (int start = 0; start < terrain.Count;)
        {
            int end = start;
            while (end + 1 < terrain.Count &&
                Math.Abs(terrain[end].Right - terrain[end + 1].Left) < .1f)
                end++;
            if (terrain[end].Right >= left && terrain[start].Left <= right)
            {
                var outline = new List<PointF> { new(terrain[start].Left, terrain[start].LeftY) };
                var band = new List<PointF> { new(terrain[start].Left, terrain[start].LeftY + 11) };
                for (int i = start; i <= end; i++)
                {
                    outline.Add(new PointF(terrain[i].Right, terrain[i].RightY));
                    band.Add(new PointF(terrain[i].Right, terrain[i].RightY + 11));
                }
                outline.Add(new PointF(terrain[end].Right, chapter >= 3 ? 1100 : 660));
                outline.Add(new PointF(terrain[start].Left, chapter >= 3 ? 1100 : 660));
                g.FillPolygon(body, outline.ToArray());
                for (int i = end; i >= start; i--)
                    band.Add(new PointF(terrain[i].Right, terrain[i].RightY + 31));
                band.Add(new PointF(terrain[start].Left, terrain[start].LeftY + 31));
                g.FillPolygon(face, band.ToArray());
            }
            start = end + 1;
        }
        foreach (ActTerrainSegment segment in terrain)
        {
            if (segment.Right < left || segment.Left > right) continue;
            g.DrawLine(top, segment.Left, segment.LeftY, segment.Right, segment.RightY);
            for (int i = 0; i < 3; i++)
                g.DrawLine(seam, segment.Left + 12 + i * 5, segment.LeftY + 43 + i * 34,
                    segment.Right - 9, segment.RightY + 44 + i * 34);
            // Broken edge marking helps the player read a jump before reaching it.
            if (!terrain.Any(next => Math.Abs(next.Left - segment.Right) < .1f))
            {
                g.DrawLine(top, segment.Right - 12, segment.RightY - 1,
                    segment.Right + 5, segment.RightY + 13);
                using var crack = new Pen(Color.FromArgb(185, 113, 84), 2f);
                g.DrawLine(crack, segment.Right - 15, segment.RightY + 1,
                    segment.Right - 2, segment.RightY + 25);
            }
        }
    }

    public static void DrawPlatform(Graphics g, RectangleF solid)
    {
        using var dark = new SolidBrush(Rock);
        using var lit = new SolidBrush(Face);
        using var rim = new Pen(Rim, 2f);
        g.FillRectangle(dark, solid);
        if (solid.Height > 20)
        {
            for (float x = solid.Left + 16; x < solid.Right; x += 40)
                g.FillRectangle(lit, x, solid.Top + solid.Height - 12, 25, 4);
            g.DrawLine(rim, solid.Left, solid.Bottom, solid.Right, solid.Bottom);
        }
        else
        {
            g.FillRectangle(lit, solid.Left + 3, solid.Top + 5, solid.Width - 6, 4);
            g.DrawLine(rim, solid.Left, solid.Top, solid.Right, solid.Top);
            using var brace = new Pen(Color.FromArgb(90, 85, 73), 2f);
            g.DrawLine(brace, solid.Left + 9, solid.Bottom, solid.Left + 16, solid.Bottom + 11);
            g.DrawLine(brace, solid.Right - 9, solid.Bottom, solid.Right - 16, solid.Bottom + 11);
        }
    }

    public static void DrawProps(Graphics g, ActLevelLayout layout, float cameraX, float time)
    {
        using var pillar = new SolidBrush(Color.FromArgb(135, 96, 82, 78));
        using var highlight = new Pen(Color.FromArgb(110, Rim), 1f);
        for (int cell = (int)MathF.Floor((cameraX - 80) / 170);
            cell <= (int)MathF.Ceiling((cameraX + 1040) / 170); cell++)
        {
            float x = cell * 170 + 31;
            if (layout.Supplies.Any(camp => camp.Contains(x))) continue;
            float? baseY = ActSecondMap.GroundAt(layout, x);
            if (baseY is null) continue;
            int h = 19 + (cell * 17 & 23);
            g.FillPolygon(pillar, [new PointF(x, baseY.Value),
                new PointF(x + 5, baseY.Value - h),
                new PointF(x + 15, baseY.Value - h - 5),
                new PointF(x + 22, baseY.Value)]);
            g.DrawLine(highlight, x + 6, baseY.Value - h, x + 14, baseY.Value - h - 4);
            using var grit = new SolidBrush(Color.FromArgb(85, 214, 179, 126));
            g.FillRectangle(grit, x + 34, baseY.Value - 3, 9, 2);
        }
    }
}
