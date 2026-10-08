using System.Drawing;

namespace DinoDash.Desktop;

// Pure scenery. Every layer is drawn from the camera position and a stable hash,
// so nothing here holds state that could drift out of sync with the simulation
// and nothing here is ever consulted by movement, collision or combat code.
//
// Layer order, far to near:
//   sky gradient -> sun haze -> cloud bands (2 depths) -> far ridges ->
//   mid ridges + ruins -> grove silhouettes -> distant fauna -> dust haze
//
// Scroll factors are all < 1 so the backdrop trails the camera. The ground-level
// decoration in ActLevelDecor draws at factor 1 with the solids.
internal sealed class ActParallaxBackdrop
{
    private const float Horizon = 214f;

    public void Draw(Graphics g, float cameraX, float time)
    {
        float progress = Math.Clamp(cameraX / ActTuning.LevelWidth, 0f, 1f);
        DrawSky(g, progress);
        DrawSunHaze(g, cameraX, progress);
        DrawClouds(g, cameraX, time, .045f, 34f, 92, 168f, 3);
        DrawClouds(g, cameraX, time, .095f, 21f, 132, 96f, 2);
        DrawFarRidges(g, cameraX, progress);
        DrawMidRidges(g, cameraX, progress);
        DrawRuins(g, cameraX, progress);
        DrawGrove(g, cameraX, time);
        DrawDistantFauna(g, cameraX, time);
        DrawHaze(g);
    }

    private static void DrawSky(Graphics g, float progress)
    {
        using Brush sky = ActVisualTheme.SkyBrush(progress);
        g.FillRectangle(sky, 0f, -4f, ActTuning.ViewWidth, ActTuning.ViewHeight + 8f);
    }

    // A single soft sun that drifts across the level, giving each zone its own
    // light direction without any per-zone authoring.
    private static void DrawSunHaze(Graphics g, float cameraX, float progress)
    {
        float sunX = ActTuning.ViewWidth * (.82f - progress * .62f)
            - cameraX * .012f % ActTuning.ViewWidth * 0f;
        float sunY = 62f + progress * 26f;
        Color glow = ActVisualTheme.Lerp(
            Color.FromArgb(255, 246, 224), Color.FromArgb(255, 226, 198), progress);
        for (int ring = 5; ring >= 0; ring--)
        {
            float radius = 26f + ring * 17f;
            g.FillEllipse(ActVisualTheme.Fade(glow, 16 + (5 - ring) * 9),
                sunX - radius, sunY - radius, radius * 2f, radius * 2f);
        }
    }

    // Two cloud bands at different depths. Banding (no anti-aliasing) keeps the
    // shapes consistent with the pixel dinosaur.
    private static void DrawClouds(Graphics g, float cameraX, float time,
        float factor, float drift, int alpha, float spacing, int puffs)
    {
        float scroll = cameraX * factor + time * drift;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 3;
        for (int cell = first; cell <= last; cell++)
        {
            float seedA = ActVisualTheme.Hash01(cell, (int)(factor * 1000f));
            if (seedA > .78f) continue;
            float x = cell * spacing - scroll + seedA * spacing * .5f;
            float y = 26f + ActVisualTheme.Hash01(cell, 77) * 84f;
            float scale = .62f + ActVisualTheme.Hash01(cell, 131) * .95f;
            DrawCloud(g, x, y, scale, alpha, puffs, cell);
        }
    }

    private static void DrawCloud(Graphics g, float x, float y, float scale,
        int alpha, int puffs, int cell)
    {
        Brush body = ActVisualTheme.Fade(Color.White, alpha);
        Brush shade = ActVisualTheme.Fade(Color.FromArgb(214, 220, 232), alpha / 2);
        float width = 46f * scale;
        float height = 11f * scale;
        g.FillRectangle(body, x, y, width, height);
        for (int puff = 0; puff < puffs; puff++)
        {
            float px = x + width * (.14f + puff * .32f);
            float pw = width * (.24f + ActVisualTheme.Hash01(cell, puff * 13) * .2f);
            float ph = height * (.85f + ActVisualTheme.Hash01(cell, puff * 29) * .95f);
            g.FillRectangle(body, px, y - ph, pw, ph + 2f);
        }
        g.FillRectangle(shade, x, y + height - 2f, width, 2f);
    }

    // Far ridge line: low contrast, very slow scroll, silhouette only.
    private static void DrawFarRidges(Graphics g, float cameraX, float progress)
    {
        float scroll = cameraX * .14f;
        const float spacing = 118f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 3;
        Color tone = ActVisualTheme.Lerp(ActVisualTheme.Ridge,
            Color.FromArgb(188, 190, 204), progress);
        for (int cell = first; cell <= last; cell++)
        {
            float x = cell * spacing - scroll;
            float peak = 96f + ActVisualTheme.Hash01(cell, 5) * 72f;
            float half = spacing * (.62f + ActVisualTheme.Hash01(cell, 9) * .5f);
            g.FillPolygon(ActVisualTheme.Fade(tone, 210),
            [
                new PointF(x - half, Horizon + 6f),
                new PointF(x - half * .34f, peak + 18f),
                new PointF(x, peak),
                new PointF(x + half * .46f, peak + 26f),
                new PointF(x + half, Horizon + 6f)
            ]);
        }
        g.FillRectangle(ActVisualTheme.Fade(tone, 210), 0f, Horizon,
            ActTuning.ViewWidth, 12f);
    }

    // Mid ridges carry a lit face so the light direction from the sun reads.
    private static void DrawMidRidges(Graphics g, float cameraX, float progress)
    {
        float scroll = cameraX * .26f;
        const float spacing = 86f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 3;
        bool lightFromLeft = progress > .45f;
        for (int cell = first; cell <= last; cell++)
        {
            float x = cell * spacing - scroll;
            float peak = 140f + ActVisualTheme.Hash01(cell, 41) * 52f;
            float half = spacing * (.7f + ActVisualTheme.Hash01(cell, 47) * .42f);
            PointF apex = new(x, peak);
            PointF left = new(x - half, Horizon + 20f);
            PointF right = new(x + half, Horizon + 20f);
            g.FillPolygon(ActVisualTheme.Fade(ActVisualTheme.RidgeNear, 235),
                [left, apex, right]);
            PointF flank = lightFromLeft ? left : right;
            g.FillPolygon(ActVisualTheme.Fade(Color.FromArgb(206, 211, 221), 190),
                [apex, flank, new PointF(x, Horizon + 20f)]);
            // A dusting of snow or bone-white scree on the taller peaks.
            if (peak < 158f)
                g.FillPolygon(ActVisualTheme.Fade(Color.FromArgb(234, 236, 243), 170),
                [
                    apex,
                    new PointF(x - half * .2f, peak + 15f),
                    new PointF(x + half * .18f, peak + 13f)
                ]);
        }
        g.FillRectangle(ActVisualTheme.Fade(ActVisualTheme.RidgeNear, 235),
            0f, Horizon + 14f, ActTuning.ViewWidth, 16f);
    }

    // Broken columns and arches. Placement is sparse and hashed per cell, which
    // is what breaks up the "same rock every screen" repetition.
    private static void DrawRuins(Graphics g, float cameraX, float progress)
    {
        float scroll = cameraX * .38f;
        const float spacing = 224f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 2;
        for (int cell = first; cell <= last; cell++)
        {
            float pick = ActVisualTheme.Hash01(cell, 211);
            if (pick > .74f) continue;
            float x = cell * spacing - scroll + ActVisualTheme.Hash01(cell, 217) * 90f;
            float baseY = Horizon + 34f;
            Brush stone = ActVisualTheme.Fade(ActVisualTheme.Ruin, 228);
            Brush crack = ActVisualTheme.Fade(Color.FromArgb(146, 152, 162), 200);
            int kind = ActVisualTheme.HashInt(cell, 223, 3);
            if (kind == 0)
            {
                // Toppled column: three drums plus the broken stump.
                float h = 34f + ActVisualTheme.Hash01(cell, 229) * 26f;
                g.FillRectangle(stone, x, baseY - h, 13f, h);
                g.FillRectangle(crack, x + 9f, baseY - h, 4f, h);
                g.FillRectangle(stone, x - 4f, baseY - h - 5f, 21f, 5f);
                for (int drum = 0; drum < 3; drum++)
                    g.FillRectangle(stone, x + 22f + drum * 17f,
                        baseY - 9f - drum % 2 * 3f, 15f, 9f);
            }
            else if (kind == 1)
            {
                // Standing arch with the keystone missing.
                float h = 46f + ActVisualTheme.Hash01(cell, 233) * 22f;
                g.FillRectangle(stone, x, baseY - h, 11f, h);
                g.FillRectangle(stone, x + 44f, baseY - h + 6f, 11f, h - 6f);
                g.FillRectangle(stone, x, baseY - h, 24f, 8f);
                g.FillRectangle(stone, x + 38f, baseY - h + 3f, 17f, 8f);
                g.FillRectangle(crack, x + 7f, baseY - h + 8f, 4f, h - 10f);
            }
            else
            {
                // Rib cage half-buried in the slope: a landmark, not a hazard.
                float h = 26f + ActVisualTheme.Hash01(cell, 239) * 14f;
                g.FillRectangle(stone, x, baseY - 5f, 62f, 5f);
                for (int rib = 0; rib < 5; rib++)
                {
                    float rx = x + 6f + rib * 12f;
                    float rh = h - MathF.Abs(rib - 2) * 5f;
                    g.FillRectangle(stone, rx, baseY - 5f - rh, 4f, rh);
                    g.FillRectangle(stone, rx, baseY - 5f - rh, 8f, 4f);
                }
            }
        }
        if (progress > .78f) DrawNestSpires(g, scroll);
    }

    // The boss end of the level gets taller spires so the silhouette changes as
    // the player approaches the nest.
    private static void DrawNestSpires(Graphics g, float scroll)
    {
        const float spacing = 132f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 2;
        for (int cell = first; cell <= last; cell++)
        {
            if (ActVisualTheme.Hash01(cell, 307) > .5f) continue;
            float x = cell * spacing - scroll;
            float h = 74f + ActVisualTheme.Hash01(cell, 311) * 54f;
            g.FillPolygon(ActVisualTheme.Fade(Color.FromArgb(150, 152, 164), 215),
            [
                new PointF(x, Horizon + 34f),
                new PointF(x + 7f, Horizon + 34f - h),
                new PointF(x + 15f, Horizon + 34f)
            ]);
        }
    }

    // Grove silhouettes sway as a whole. The sway is a function of time and
    // position only -- no wind state is stored.
    private static void DrawGrove(Graphics g, float cameraX, float time)
    {
        float scroll = cameraX * .52f;
        const float spacing = 96f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 2;
        for (int cell = first; cell <= last; cell++)
        {
            float pick = ActVisualTheme.Hash01(cell, 401);
            if (pick > .82f) continue;
            float x = cell * spacing - scroll + ActVisualTheme.Hash01(cell, 409) * 52f;
            float baseY = Horizon + 44f;
            float phase = ActVisualTheme.Hash01(cell, 419) * 6.283f;
            float sway = MathF.Sin(time * 1.05f + phase) * 3.4f;
            if (pick < .46f) DrawFern(g, x, baseY, cell, sway);
            else DrawCycad(g, x, baseY, cell, sway);
        }
    }

    private static void DrawFern(Graphics g, float x, float baseY, int cell, float sway)
    {
        Brush leaf = ActVisualTheme.Fade(ActVisualTheme.Grove, 225);
        float h = 30f + ActVisualTheme.Hash01(cell, 421) * 20f;
        g.FillRectangle(leaf, x, baseY - h * .5f, 3f, h * .5f);
        for (int frond = 0; frond < 5; frond++)
        {
            float angle = -2.55f + frond * .62f;
            float len = h * (.62f + ActVisualTheme.Hash01(cell, frond * 7) * .5f);
            float tipX = x + 1.5f + MathF.Cos(angle) * len + sway * (frond - 2) * .5f;
            float tipY = baseY - h * .5f + MathF.Sin(angle) * len * .74f;
            g.DrawLine(ActVisualTheme.FadePen(ActVisualTheme.Grove, 225, 2.4f),
                x + 1.5f, baseY - h * .5f, tipX, tipY);
        }
    }

    private static void DrawCycad(Graphics g, float x, float baseY, int cell, float sway)
    {
        Brush trunk = ActVisualTheme.Fade(ActVisualTheme.Scrub, 200);
        Brush crown = ActVisualTheme.Fade(ActVisualTheme.Grove, 230);
        float h = 42f + ActVisualTheme.Hash01(cell, 431) * 30f;
        for (int seg = 0; seg * 8f < h; seg++)
            g.FillRectangle(trunk, x + sway * (seg * 8f / h) * .5f,
                baseY - (seg + 1) * 8f, 7f, 8f);
        float topX = x + sway * .5f + 3.5f;
        float topY = baseY - h;
        for (int frond = 0; frond < 6; frond++)
        {
            float angle = -2.9f + frond * .49f;
            float len = 17f + ActVisualTheme.Hash01(cell, frond * 11) * 11f;
            g.FillPolygon(crown,
            [
                new PointF(topX, topY + 3f),
                new PointF(topX + MathF.Cos(angle) * len,
                    topY + MathF.Sin(angle) * len * .8f),
                new PointF(topX + MathF.Cos(angle + .2f) * len * .82f,
                    topY + MathF.Sin(angle + .2f) * len * .8f + 3f)
            ]);
        }
    }

    // Long-necks and pterosaur flocks on the horizon. Cosmetic only: they have
    // no bounds, cannot be hit and never interact with the player.
    private static void DrawDistantFauna(Graphics g, float cameraX, float time)
    {
        float scroll = cameraX * .2f;
        const float spacing = 620f;
        int first = (int)MathF.Floor(scroll / spacing) - 1;
        int last = first + (int)(ActTuning.ViewWidth / spacing) + 2;
        for (int cell = first; cell <= last; cell++)
        {
            float pick = ActVisualTheme.Hash01(cell, 503);
            if (pick > .62f) continue;
            float x = cell * spacing - scroll + ActVisualTheme.Hash01(cell, 509) * 240f;
            if (pick < .3f) DrawLongNeck(g, x, Horizon + 18f, cell, time);
            else DrawFlock(g, x, 58f + ActVisualTheme.Hash01(cell, 521) * 46f, cell, time);
        }
    }

    private static void DrawLongNeck(Graphics g, float x, float baseY, int cell, float time)
    {
        Brush tone = ActVisualTheme.Fade(Color.FromArgb(172, 178, 190), 200);
        float scale = .8f + ActVisualTheme.Hash01(cell, 523) * .5f;
        float bob = MathF.Sin(time * .62f + cell) * 1.6f;
        // Slow browsing sweep of the neck, looping every few seconds.
        float reach = MathF.Sin(time * .38f + cell * .7f) * 7f;
        float bw = 30f * scale;
        float bh = 13f * scale;
        g.FillRectangle(tone, x, baseY - bh + bob, bw, bh);
        g.FillRectangle(tone, x + bw * .78f, baseY - bh - 19f * scale + bob + reach * .3f,
            5f * scale, 20f * scale);
        g.FillRectangle(tone, x + bw * .74f + reach, baseY - bh - 24f * scale + bob,
            9f * scale, 5f * scale);
        g.FillRectangle(tone, x - 13f * scale, baseY - bh + 2f + bob, 14f * scale, 4f * scale);
        g.FillRectangle(tone, x + bw * .18f, baseY + bob, 4f * scale, 6f * scale);
        g.FillRectangle(tone, x + bw * .6f, baseY + bob, 4f * scale, 6f * scale);
    }

    private static void DrawFlock(Graphics g, float x, float y, int cell, float time)
    {
        int count = 3 + ActVisualTheme.HashInt(cell, 541, 4);
        float glide = time * 13f % (ActTuning.ViewWidth + 220f);
        for (int bird = 0; bird < count; bird++)
        {
            float bx = x + glide + bird * 19f;
            if (bx > ActTuning.ViewWidth + 20f) bx -= ActTuning.ViewWidth + 220f;
            float by = y + MathF.Sin(time * 1.5f + bird * .9f) * 3f + bird % 2 * 6f;
            float flap = MathF.Sin(time * 6.5f + bird * 1.3f) * 3f;
            Pen wing = ActVisualTheme.FadePen(Color.FromArgb(120, 126, 140), 185, 1.6f);
            g.DrawLine(wing, bx - 5f, by + flap, bx, by);
            g.DrawLine(wing, bx, by, bx + 5f, by + flap);
        }
    }

    // A warm wash just above the play line so the near ground reads as closer
    // than the ridges without darkening the collision silhouettes.
    private static void DrawHaze(Graphics g)
    {
        g.FillRectangle(ActVisualTheme.Fade(ActVisualTheme.Paper, 54),
            0f, Horizon + 10f, ActTuning.ViewWidth, 74f);
    }
}
