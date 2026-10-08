using System.Drawing;

namespace DinoDash.Desktop;

// World-space decoration drawn at scroll factor 1, i.e. in the same space as the
// solids. Everything here is procedurally placed from a stable hash of the world
// cell, so it never moves and never needs storing -- and critically, it is never
// added to ActEngine.Solids, so none of it can be stood on or collided with.
//
// Placement rules that keep the route readable:
//   * Nothing is drawn inside the 44px band above the ground line where the
//     player's feet and enemy hitboxes live, except flat ground texture.
//   * Tall props are pushed behind the play line by tone, not by position.
//   * Platform trim is derived from the solid rectangles themselves, so trim can
//     never disagree with collision.
internal static class ActLevelDecor
{
    // Ground surface: banded strata, grit and a lit top edge.
    public static void DrawGroundBody(Graphics g, float cameraX)
    {
        float left = cameraX - 40f;
        float right = cameraX + ActTuning.ViewWidth + 40f;
        float top = ActTuning.GroundY;
        g.FillRectangle(ActVisualTheme.GroundBrush, left, top, right - left, 60f);
        // Two strata bands give the cut face depth without adding contrast to
        // the silhouette the player reads for footing.
        g.FillRectangle(ActVisualTheme.GroundFaceBrush, left, top + 13f, right - left, 6f);
        g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(48, 49, 58), 255),
            left, top + 30f, right - left, 4f);

        int firstCell = (int)MathF.Floor(left / 24f);
        int lastCell = (int)MathF.Ceiling(right / 24f);
        for (int cell = firstCell; cell <= lastCell; cell++)
        {
            float x = cell * 24f;
            float jitter = ActVisualTheme.Hash01(cell, 61);
            // Lit top edge, broken up so it does not read as a drawn line.
            if (jitter > .18f)
                g.FillRectangle(ActVisualTheme.GroundGritBrush,
                    x + jitter * 10f, top + 2f, 9f + jitter * 9f, 2f);
            if (ActVisualTheme.Hash01(cell, 67) > .62f)
                g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(70, 71, 82), 255),
                    x + ActVisualTheme.Hash01(cell, 71) * 16f,
                    top + 7f + ActVisualTheme.Hash01(cell, 73) * 18f, 3f, 3f);
            if (ActVisualTheme.Hash01(cell, 79) > .8f)
                g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(60, 61, 72), 255),
                    x + 4f, top + 22f + ActVisualTheme.Hash01(cell, 83) * 12f, 12f, 2f);
        }
    }

    // Scenery that sits behind the play line: rocks, bones, ruins, plants. The
    // 44px exclusion band above the ground keeps footing unambiguous.
    public static void DrawGroundProps(Graphics g, float cameraX, float time,
        IReadOnlyList<ActSupply>? camps = null)
    {
        float left = cameraX - 60f;
        float right = cameraX + ActTuning.ViewWidth + 60f;
        int firstCell = (int)MathF.Floor(left / 118f);
        int lastCell = (int)MathF.Ceiling(right / 118f);
        for (int cell = firstCell; cell <= lastCell; cell++)
        {
            float slot = ActVisualTheme.Hash01(cell, 907);
            if (slot > .88f) continue;
            float x = cell * 118f + ActVisualTheme.Hash01(cell, 911) * 76f;
            if (camps?.Any(camp => camp.Contains(x)) == true) continue;
            float baseY = ActTuning.GroundY;
            int kind = ActVisualTheme.HashInt(cell, 919, 6);
            switch (kind)
            {
                case 0: DrawBoulder(g, x, baseY, cell); break;
                case 1: DrawRibcage(g, x, baseY, cell); break;
                case 2: DrawSkull(g, x, baseY, cell); break;
                case 3: DrawShrub(g, x, baseY, cell, time); break;
                case 4: DrawStoneMarker(g, x, baseY, cell); break;
                default: DrawPebbleField(g, x, baseY, cell); break;
            }
        }
    }

    private static void DrawBoulder(Graphics g, float x, float baseY, int cell)
    {
        float w = 22f + ActVisualTheme.Hash01(cell, 101) * 26f;
        float h = 13f + ActVisualTheme.Hash01(cell, 103) * 16f;
        Brush body = ActVisualTheme.Fade(ActVisualTheme.Decor, 240);
        Brush lit = ActVisualTheme.Fade(Color.FromArgb(146, 151, 161), 230);
        Brush dark = ActVisualTheme.Fade(ActVisualTheme.DecorDeep, 235);
        g.FillPolygon(body,
        [
            new PointF(x, baseY),
            new PointF(x + w * .16f, baseY - h * .78f),
            new PointF(x + w * .48f, baseY - h),
            new PointF(x + w * .82f, baseY - h * .66f),
            new PointF(x + w, baseY)
        ]);
        g.FillPolygon(lit,
        [
            new PointF(x + w * .16f, baseY - h * .78f),
            new PointF(x + w * .48f, baseY - h),
            new PointF(x + w * .5f, baseY - h * .5f),
            new PointF(x + w * .24f, baseY - h * .44f)
        ]);
        g.FillRectangle(dark, x + w * .1f, baseY - 3f, w * .8f, 3f);
        // Small companion stone so the silhouette is not a lone lump.
        if (ActVisualTheme.Hash01(cell, 107) > .5f)
            g.FillPolygon(body,
            [
                new PointF(x + w + 5f, baseY),
                new PointF(x + w + 9f, baseY - 8f),
                new PointF(x + w + 17f, baseY)
            ]);
    }

    private static void DrawRibcage(Graphics g, float x, float baseY, int cell)
    {
        Brush bone = ActVisualTheme.Fade(Color.FromArgb(206, 202, 190), 235);
        Brush shade = ActVisualTheme.Fade(Color.FromArgb(168, 163, 150), 225);
        int ribs = 4 + ActVisualTheme.HashInt(cell, 113, 3);
        float spine = baseY - 4f;
        g.FillRectangle(bone, x, spine, ribs * 11f + 8f, 3f);
        for (int rib = 0; rib < ribs; rib++)
        {
            float rx = x + 5f + rib * 11f;
            float rh = 12f + MathF.Sin(rib / (float)ribs * 3.14f) * 14f;
            g.FillRectangle(bone, rx, spine - rh, 3f, rh);
            g.FillRectangle(shade, rx + 3f, spine - rh, 1f, rh);
            g.FillRectangle(bone, rx, spine - rh, 6f, 3f);
        }
        // Loose vertebra trailing off the spine.
        g.FillRectangle(bone, x - 9f, spine - 1f, 6f, 4f);
        g.FillRectangle(bone, x - 17f, spine, 5f, 3f);
    }

    private static void DrawSkull(Graphics g, float x, float baseY, int cell)
    {
        Brush bone = ActVisualTheme.Fade(Color.FromArgb(210, 206, 194), 240);
        Brush socket = ActVisualTheme.Fade(Color.FromArgb(92, 90, 86), 235);
        float scale = .9f + ActVisualTheme.Hash01(cell, 127) * .6f;
        float w = 30f * scale;
        float h = 17f * scale;
        g.FillRectangle(bone, x, baseY - h, w, h);
        g.FillRectangle(bone, x + w, baseY - h * .72f, w * .42f, h * .52f);
        g.FillRectangle(socket, x + w * .58f, baseY - h * .78f, 5f * scale, 5f * scale);
        // Jaw dropped open, teeth suggested by two notches.
        g.FillRectangle(bone, x + w * .3f, baseY - 3f, w * .9f, 3f);
        g.FillRectangle(socket, x + w * .82f, baseY - 6f, 2f, 3f);
        g.FillRectangle(socket, x + w * 1.05f, baseY - 6f, 2f, 3f);
    }

    private static void DrawShrub(Graphics g, float x, float baseY, int cell, float time)
    {
        float phase = ActVisualTheme.Hash01(cell, 131) * 6.283f;
        float sway = MathF.Sin(time * 1.35f + phase) * 2.6f;
        int blades = 5 + ActVisualTheme.HashInt(cell, 137, 4);
        for (int blade = 0; blade < blades; blade++)
        {
            float bx = x + blade * 5f;
            float h = 11f + ActVisualTheme.Hash01(cell, blade * 17) * 17f;
            float lean = (blade - blades * .5f) * 1.3f + sway * (h / 28f);
            g.DrawLine(ActVisualTheme.FadePen(ActVisualTheme.Scrub, 230, 2f),
                bx, baseY, bx + lean, baseY - h);
        }
        // Occasional seed head to catch the eye.
        if (ActVisualTheme.Hash01(cell, 139) > .62f)
            g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(150, 146, 132), 230),
                x + blades * 2.4f + sway, baseY - 27f, 3f, 5f);
    }

    private static void DrawStoneMarker(Graphics g, float x, float baseY, int cell)
    {
        Brush stone = ActVisualTheme.Fade(ActVisualTheme.DecorDeep, 235);
        Brush lit = ActVisualTheme.Fade(ActVisualTheme.Decor, 230);
        float h = 24f + ActVisualTheme.Hash01(cell, 149) * 16f;
        float lean = (ActVisualTheme.Hash01(cell, 151) - .5f) * 6f;
        g.FillPolygon(stone,
        [
            new PointF(x, baseY),
            new PointF(x + 2f + lean, baseY - h),
            new PointF(x + 10f + lean, baseY - h + 2f),
            new PointF(x + 12f, baseY)
        ]);
        g.FillPolygon(lit,
        [
            new PointF(x + 2f + lean, baseY - h),
            new PointF(x + 10f + lean, baseY - h + 2f),
            new PointF(x + 8f + lean, baseY - h * .6f),
            new PointF(x + 4f + lean, baseY - h * .62f)
        ]);
        // Carved notches hint at an old trail marker.
        for (int notch = 0; notch < 3; notch++)
            g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(58, 60, 70), 220),
                x + 4f + lean * .6f, baseY - h * .5f + notch * 5f, 5f, 1f);
    }

    private static void DrawPebbleField(Graphics g, float x, float baseY, int cell)
    {
        for (int pebble = 0; pebble < 6; pebble++)
        {
            float px = x + pebble * 9f + ActVisualTheme.Hash01(cell, pebble * 23) * 6f;
            float size = 2f + ActVisualTheme.Hash01(cell, pebble * 29) * 3f;
            g.FillRectangle(ActVisualTheme.Fade(ActVisualTheme.Decor, 210),
                px, baseY - size, size, size);
        }
    }

    // Platform trim derived straight from the collision rectangle: capped ends,
    // a lit top edge, an underside shadow and hanging growth. Because every
    // value comes from `solid`, the art can never drift from the hitbox.
    public static void DrawPlatform(Graphics g, RectangleF solid, float time)
    {
        g.FillRectangle(ActVisualTheme.GroundBrush, solid);
        g.FillRectangle(ActVisualTheme.GroundFaceBrush,
            solid.Left, solid.Top + 4f, solid.Width, 3f);
        // Lit lip: the one high-contrast cue for where the player can land.
        g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(226, 228, 236), 245),
            solid.Left + 2f, solid.Top + 1f, solid.Width - 4f, 2f);
        g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(104, 108, 120), 235),
            solid.Left, solid.Top, 3f, solid.Height);
        g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(104, 108, 120), 235),
            solid.Right - 3f, solid.Top, 3f, solid.Height);
        // Rock corbels under the ends so platforms look supported.
        g.FillPolygon(ActVisualTheme.Fade(Color.FromArgb(52, 54, 63), 235),
        [
            new PointF(solid.Left, solid.Bottom),
            new PointF(solid.Left + 11f, solid.Bottom),
            new PointF(solid.Left + 4f, solid.Bottom + 7f)
        ]);
        g.FillPolygon(ActVisualTheme.Fade(Color.FromArgb(52, 54, 63), 235),
        [
            new PointF(solid.Right - 11f, solid.Bottom),
            new PointF(solid.Right, solid.Bottom),
            new PointF(solid.Right - 4f, solid.Bottom + 7f)
        ]);

        int cell = (int)(solid.Left * .17f) + (int)(solid.Top * .11f);
        // Hanging vines, swaying. Purely decorative -- they hang below the
        // platform where nothing is ever tested for collision.
        int vines = 1 + ActVisualTheme.HashInt(cell, 601, 3);
        for (int vine = 0; vine < vines; vine++)
        {
            float vx = solid.Left + 14f +
                ActVisualTheme.Hash01(cell, vine * 31) * MathF.Max(6f, solid.Width - 28f);
            float len = 9f + ActVisualTheme.Hash01(cell, vine * 37) * 17f;
            float phase = ActVisualTheme.Hash01(cell, vine * 41) * 6.283f;
            float sway = MathF.Sin(time * 1.15f + phase) * 2.2f;
            g.DrawLine(ActVisualTheme.FadePen(ActVisualTheme.Scrub, 200, 2f),
                vx, solid.Bottom, vx + sway, solid.Bottom + len);
            g.FillRectangle(ActVisualTheme.Fade(ActVisualTheme.Grove, 205),
                vx + sway - 2f, solid.Bottom + len, 4f, 3f);
        }
        // Tufts on top, kept to 5px so they never hide an enemy's feet.
        int tufts = ActVisualTheme.HashInt(cell, 607, 3);
        for (int tuft = 0; tuft < tufts; tuft++)
        {
            float tx = solid.Left + 8f +
                ActVisualTheme.Hash01(cell, tuft * 43) * MathF.Max(4f, solid.Width - 16f);
            float sway = MathF.Sin(time * 1.5f + tx * .1f) * 1.6f;
            g.DrawLine(ActVisualTheme.FadePen(ActVisualTheme.Scrub, 215, 1.6f),
                tx, solid.Top, tx + sway, solid.Top - 5f);
            g.DrawLine(ActVisualTheme.FadePen(ActVisualTheme.Scrub, 215, 1.6f),
                tx + 3f, solid.Top, tx + 3f + sway, solid.Top - 4f);
        }
    }

    // Foreground fringe: a few dark blades drawn after everything else, at a
    // slightly faster scroll than the world, to seal the bottom of the frame.
    public static void DrawForeground(Graphics g, float cameraX, float time)
    {
        float scroll = cameraX * 1.12f;
        const float spacing = 152f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 2;
        for (int cell = first; cell <= last; cell++)
        {
            if (ActVisualTheme.Hash01(cell, 701) > .52f) continue;
            float x = cell * spacing - scroll;
            float phase = ActVisualTheme.Hash01(cell, 709) * 6.283f;
            float sway = MathF.Sin(time * .92f + phase) * 4.5f;
            int blades = 4 + ActVisualTheme.HashInt(cell, 719, 4);
            for (int blade = 0; blade < blades; blade++)
            {
                float bx = x + blade * 7f;
                float h = 24f + ActVisualTheme.Hash01(cell, blade * 53) * 30f;
                g.DrawLine(ActVisualTheme.FadePen(ActVisualTheme.Fore, 150, 3f),
                    bx, ActTuning.ViewHeight + 4f,
                    bx + sway + (blade - blades * .5f) * 2f,
                    ActTuning.ViewHeight - h);
            }
        }
    }
}
