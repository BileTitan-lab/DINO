using System.Drawing;
using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// Renders whatever ActVisualFx has produced. Split into a "behind" pass and a
// "front" pass so trails and shockwaves can sit on the correct side of the
// characters. Draw-only: it mutates nothing.
internal static class ActFxRenderer
{
    // Behind characters: afterimages, flat ground rings, ambient motes.
    public static void DrawBehind(Graphics g, ActVisualFx fx)
    {
        foreach (FxCorpse corpse in fx.Corpses) DrawCorpse(g, corpse);
        foreach (FxAfterimage ghost in fx.Afterimages) DrawAfterimage(g, ghost);
        foreach (FxShockwave wave in fx.Shockwaves)
            if (wave.Flat) DrawShockwave(g, wave);
        foreach (FxParticle p in fx.Particles)
            if (p.Kind is FxKind.Leaf or FxKind.Ember) DrawParticle(g, p);
    }

    // In front of characters: swing arcs, airborne rings, impact debris.
    public static void DrawFront(Graphics g, ActVisualFx fx)
    {
        foreach(FxClash clash in fx.Clashes)DrawClash(g,clash);
        foreach (FxTrail trail in fx.Trails) DrawTrail(g, trail);
        foreach (FxShockwave wave in fx.Shockwaves)
            if (!wave.Flat) DrawShockwave(g, wave);
        foreach (FxParticle p in fx.Particles)
            if (p.Kind is not (FxKind.Leaf or FxKind.Ember)) DrawParticle(g, p);
    }

    private static void DrawClash(Graphics g,FxClash clash)
    {
        float fade=Math.Clamp(clash.Life/.32f,0,1),spread=(1-fade)*13f;
        var save=g.Save();g.SmoothingMode=SmoothingMode.None;
        // FadePen returns a shared cached pen; disposing it breaks the next frame.
        Pen dark=ActVisualTheme.FadePen(ActUiTheme.Ink,(int)(210*fade),5);
        Pen light=ActVisualTheme.FadePen(ActUiTheme.Gold,(int)(255*fade),3);
        g.DrawLine(dark,clash.X-16-spread,clash.Y-18-spread,clash.X+16+spread,clash.Y+18+spread);
        g.DrawLine(dark,clash.X-16-spread,clash.Y+18+spread,clash.X+16+spread,clash.Y-18-spread);
        g.DrawLine(light,clash.X-13-spread,clash.Y-15-spread,clash.X+13+spread,clash.Y+15+spread);
        g.DrawLine(light,clash.X-13-spread,clash.Y+15+spread,clash.X+13+spread,clash.Y-15-spread);
        using var center=new SolidBrush(Color.FromArgb((int)(240*fade),Color.White));
        g.FillRectangle(center,clash.X-3,clash.Y-3,6,6);
        g.Restore(save);
    }

    private static void DrawParticle(Graphics g, FxParticle p)
    {
        float fade = p.Fade;
        int alpha = (int)(255f * (p.Kind switch
        {
            FxKind.Dust => fade * .5f,
            FxKind.Leaf => MathF.Min(1f, fade * 2.2f) * .55f,
            FxKind.Ember => MathF.Min(1f, fade * 2.6f) * .7f,
            FxKind.Streak => fade * .62f,
            _ => MathF.Min(1f, fade * 1.5f)
        }));
        if (alpha <= 2) return;
        float x = p.X;
        float y = p.Y;
        switch (p.Kind)
        {
            case FxKind.Dust:
            {
                // Dust puffs grow and soften as they age.
                float size = p.Size * (1f + (1f - fade) * 1.9f);
                g.FillRectangle(ActVisualTheme.Fade(p.Tone, alpha),
                    x - size * .5f, y - size * .5f, size, size);
                break;
            }
            case FxKind.Spark:
            {
                // Sparks stretch along their velocity.
                float len = MathF.Min(13f, MathF.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy) * .035f + 2f);
                float inv = 1f / MathF.Max(1f, MathF.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy));
                g.DrawLine(ActVisualTheme.FadePen(p.Tone, alpha, p.Size),
                    x, y, x - p.Vx * inv * len, y - p.Vy * inv * len);
                break;
            }
            case FxKind.Streak:
            {
                float len = 9f + p.Size * 3f;
                g.DrawLine(ActVisualTheme.FadePen(p.Tone, alpha, p.Size),
                    x, y,
                    x - MathF.Cos(p.Angle) * len, y - MathF.Sin(p.Angle) * len);
                break;
            }
            case FxKind.Shard:
            {
                // Tumbling chip of rock or hide.
                GraphicsState saved = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(p.Angle * 57.2958f);
                g.FillRectangle(ActVisualTheme.Fade(p.Tone, alpha),
                    -p.Size * .5f, -p.Size * .35f, p.Size, p.Size * .7f);
                g.Restore(saved);
                break;
            }
            case FxKind.Bone:
            {
                GraphicsState saved = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(p.Angle * 57.2958f);
                Brush tone = ActVisualTheme.Fade(p.Tone, alpha);
                g.FillRectangle(tone, -p.Size, -1f, p.Size * 2f, 2f);
                g.FillRectangle(tone, -p.Size - 1f, -2f, 2f, 4f);
                g.FillRectangle(tone, p.Size - 1f, -2f, 2f, 4f);
                g.Restore(saved);
                break;
            }
            case FxKind.Leaf:
            {
                GraphicsState saved = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform(p.Angle * 57.2958f);
                g.FillPolygon(ActVisualTheme.Fade(p.Tone, alpha),
                [
                    new PointF(-p.Size, 0f), new PointF(0f, -p.Size * .55f),
                    new PointF(p.Size, 0f), new PointF(0f, p.Size * .55f)
                ]);
                g.Restore(saved);
                break;
            }
            default:
                g.FillRectangle(ActVisualTheme.Fade(p.Tone, alpha),
                    x - p.Size * .5f, y - p.Size * .5f, p.Size, p.Size);
                break;
        }
    }

    // Flat waves are squashed to read as travelling along the ground; round
    // waves are used for mid-air impacts.
    private static void DrawShockwave(Graphics g, FxShockwave wave)
    {
        float fade = wave.Fade;
        int alpha = (int)(190f * fade * fade);
        if (alpha <= 3) return;
        float rx = wave.Radius;
        float ry = wave.Flat ? wave.Radius * .26f : wave.Radius;
        Pen pen = ActVisualTheme.FadePen(wave.Tone, alpha,
            MathF.Max(1f, wave.Thickness * fade));
        g.DrawEllipse(pen, wave.X - rx, wave.Y - ry, rx * 2f, ry * 2f);
        if (!wave.Flat) return;
        // A brighter leading crest on the ground ring.
        g.DrawLine(ActVisualTheme.FadePen(wave.Tone, alpha / 2, 2f),
            wave.X - rx, wave.Y, wave.X - rx * .82f, wave.Y);
        g.DrawLine(ActVisualTheme.FadePen(wave.Tone, alpha / 2, 2f),
            wave.X + rx * .82f, wave.Y, wave.X + rx, wave.Y);
    }

    // Swing arcs are drawn as a set of concentric bands that thin out over the
    // trail's life, which reads as a blade smear at 120Hz.
    private static void DrawTrail(Graphics g, FxTrail trail)
    {
        float fade = trail.Fade;
        int alpha = (int)(200f * fade);
        if (alpha <= 4) return;
        // The arc sweeps as it fades: the visible wedge travels From -> To.
        float travel = 1f - fade;
        float span = trail.To - trail.From;
        float head = trail.From + span * Math.Clamp(travel + .34f, 0f, 1f);
        float tail = trail.From + span * Math.Clamp(travel - .12f, 0f, 1f);
        int steps = 9;
        for (int band = 0; band < 3; band++)
        {
            float radius = trail.Inner + (trail.Outer - trail.Inner) * (band / 2f);
            float width = trail.Width * (1f - band * .26f) * fade;
            if (width < .6f) continue;
            Pen pen = ActVisualTheme.FadePen(trail.Tone,
                (int)(alpha * (1f - band * .3f)), width);
            var points = new PointF[steps];
            for (int i = 0; i < steps; i++)
            {
                float t = i / (steps - 1f);
                float angle = tail + (head - tail) * t;
                points[i] = new PointF(
                    trail.X + MathF.Cos(angle) * radius,
                    trail.Y + MathF.Sin(angle) * radius * .82f);
            }
            g.DrawLines(pen, points);
        }
        // Bright leading tip so the direction of the swing is unmistakable.
        float tipX = trail.X + MathF.Cos(head) * trail.Outer;
        float tipY = trail.Y + MathF.Sin(head) * trail.Outer * .82f;
        g.FillRectangle(ActVisualTheme.Fade(Color.White, (int)(alpha * .7f)),
            tipX - 2f, tipY - 2f, 4f, 4f);
    }

    // Enemy death: a stagger frame in the hurt pose, then the body folds down
    // onto its feet in pixel steps and blinks out.
    private static void DrawCorpse(Graphics g, FxCorpse corpse)
    {
        float t = corpse.Elapsed;
        float stagger = corpse.Kind is ActEnemyKind.Boss or ActEnemyKind.Heavy ? .22f : .12f;
        float fold = Math.Clamp((t - stagger) / .28f, 0f, 1f);
        // Four discrete collapse steps keep it reading as frame animation.
        float scaleY = 1f - MathF.Floor(fold * 4f) / 4f * .62f;
        float remaining = corpse.Life / corpse.MaxLife;
        if (remaining < .35f && (int)(corpse.Life * 24f) % 2 == 0) return;

        bool flash = t < stagger && (int)(t * 40f) % 2 == 0;
        Brush ink = ActVisualTheme.Fade(flash ? ActVisualTheme.Decor : ActVisualTheme.Ink,
            (int)(255f * MathF.Min(1f, remaining * 2.2f)));
        Brush paper = ActVisualTheme.Fade(Color.White, (int)(255f * MathF.Min(1f, remaining * 2.2f)));

        float x = MathF.Round(corpse.X);
        float y = MathF.Round(corpse.Y);
        float feet = y + corpse.Height;
        GraphicsState saved = g.Save();
        if (scaleY < 1f)
        {
            g.TranslateTransform(0f, feet);
            g.ScaleTransform(1f, scaleY);
            g.TranslateTransform(0f, -feet);
        }
        if (corpse.Facing < 0)
        {
            g.TranslateTransform(x + corpse.Width, 0f);
            g.ScaleTransform(-1f, 1f);
            x = 0f;
        }
        ActSpriteAnimator.DrawEnemyBody(g, corpse.Kind, x, y, ActEnemyState.Hurt, t,
            corpse.Move, 0, corpse.MaxHealth, ink, paper, corpse.BossId);
        g.Restore(saved);
    }

    private static void DrawAfterimage(Graphics g, FxAfterimage ghost)
    {
        int alpha = (int)(120f * ghost.Fade * ghost.Fade);
        if (alpha <= 3) return;
        GraphicsState saved = g.Save();
        float x = MathF.Round(ghost.X);
        if (ghost.Facing < 0)
        {
            g.TranslateTransform(x + 54f, 0f);
            g.ScaleTransform(-1f, 1f);
            x = 0f;
        }
        PixelSprites.DrawDinoTinted(g, x, MathF.Round(ghost.Y) + (ghost.Compact ? 22f : 0f),
            ghost.Frame, ghost.Compact, ghost.Airborne,
            ActVisualTheme.Fade(ActVisualTheme.Ink, alpha),
            ActVisualTheme.Fade(ActVisualTheme.Paper, alpha / 3));
        g.Restore(saved);
    }
}
