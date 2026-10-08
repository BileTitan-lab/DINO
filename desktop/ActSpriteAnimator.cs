using System.Drawing;
using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// Maps engine state to sprite poses. Each action has timed anticipation,
// contact and recovery frames keyed to the SAME constants the combat code uses
// for its hit windows, so what the player sees always matches when the hit
// actually lands. Nothing here writes to the engine.
//
// ActVisualFx is optional: without it (e.g. a headless caller) the sprites
// still animate, only squash/stretch and hit-punch are skipped.
internal static class ActSpriteAnimator
{
    // Hit-window constants mirrored from ActCombat for frame timing only.
    private static readonly float[] ComboHitStarts = [.065f, .085f, .14f];
    private static readonly float[] ComboHitEnds = [.145f, .18f, .27f];
    private static readonly float[] ComboDurations = [.25f, .29f, .42f];
    private const float TailSweepStart = .17f;
    private const float TailSweepEnd = .36f;
    private const float TailDuration = .5f;
    private const float StompImpactTime = .27f;

    private static readonly Pen ClawPen = new(Color.Black, 4f);
    private static readonly Pen ClawHeavyPen = new(Color.Black, 6f);

    // ================================================================ player

    internal static void DrawDinosaur(Graphics g, ActEngine game, ActVisualFx? fx = null)
    {
        if (!game.IsDead && game.InvulnerabilityRemaining > 0f &&
            game.Pose != ActPose.Hurt &&
            (int)(game.InvulnerabilityRemaining * 16f) % 2 == 0) return;

        GraphicsState saved = g.Save();
        float worldX = MathF.Round(game.PlayerX);
        float drawY = MathF.Round(game.PlayerY);
        ApplySquashStretch(g, game, fx, worldX, drawY);

        if (game.Facing < 0)
        {
            g.TranslateTransform(worldX + game.PlayerWidth, 0f);
            g.ScaleTransform(-1f, 1f);
            worldX = 0f;
        }

        if (game.Pose == ActPose.Dead)
        {
            float since = fx?.DeathElapsed ?? 1f;
            int stage = since < .12f ? 0 : since < .32f ? 1 : 2;
            PixelSprites.DrawDinoDeath(g, worldX, drawY, stage);
            g.Restore(saved);
            return;
        }

        switch (game.Pose)
        {
            case ActPose.Dash:
                PixelSprites.DrawDinoCompact(g, worldX, drawY + 22f,
                    (int)(game.PoseElapsed * 16f) % 2, Brushes.Black, Brushes.White);
                DrawDashLines(g, worldX, drawY, game.PoseElapsed);
                break;
            case ActPose.Attack1:
            case ActPose.Attack2:
            case ActPose.Attack3:
                DrawClawAttack(g, worldX, drawY, game.ComboStep, game.AttackElapsed);
                break;
            case ActPose.Tail:
                DrawTailSweep(g, worldX, drawY, game.TailElapsed);
                break;
            case ActPose.Stomp:
                DrawStomp(g, worldX, drawY, game.Stomping, game.StompImpactRemaining,
                    game.AnimationClock);
                break;
            case ActPose.Ranged:
                DrawRanged(g, worldX, drawY, game.PoseElapsed);
                break;
            default:
                PixelSprites.DrawDinoPosed(g, worldX, drawY, LocomotionPose(game),
                    Brushes.Black, Brushes.White);
                if (game.Pose == ActPose.Run) DrawRunScuff(g, worldX, drawY, game.PoseElapsed);
                break;
        }
        g.Restore(saved);
    }

    private static void ApplySquashStretch(Graphics g, ActEngine game, ActVisualFx? fx,
        float worldX, float drawY)
    {
        if (fx is null) return;
        float sx = 1f, sy = 1f;
        float pivotX = worldX + game.PlayerWidth * .5f;
        float pivotY = drawY + game.PlayerHeight;
        if (fx.LandSquash > 0f)
        {
            sx = 1f + fx.LandSquash * .2f;
            sy = 1f - fx.LandSquash * .18f;
        }
        else if (fx.JumpStretch > 0f)
        {
            sx = 1f - fx.JumpStretch * .12f;
            sy = 1f + fx.JumpStretch * .16f;
        }
        else if (fx.HurtPunch > 0f)
        {
            sx = 1f - fx.HurtPunch * .1f;
            sy = 1f + fx.HurtPunch * .08f;
        }
        else if (game.Pose == ActPose.Stomp && game.Stomping)
        {
            sx = .9f;
            sy = 1.12f;
        }
        // Quantise so the silhouette steps rather than swims.
        sx = MathF.Round(sx * 25f) / 25f;
        sy = MathF.Round(sy * 25f) / 25f;
        if (sx == 1f && sy == 1f) return;
        g.TranslateTransform(pivotX, pivotY);
        g.ScaleTransform(sx, sy);
        g.TranslateTransform(-pivotX, -pivotY);
    }

    // Idle, run, jump, fall and hurt share the posable body.
    private static DinoPose LocomotionPose(ActEngine game)
    {
        float t = game.PoseElapsed;
        switch (game.Pose)
        {
            case ActPose.Run:
            {
                // 13 fps four-frame stride; the body rises on the passing frames.
                int frame = (int)(t * 13f) % 4;
                return new DinoPose
                {
                    Legs = (DinoLegs)frame,
                    BodyDy = frame is 1 or 3 ? -2f : 0f,
                    HeadDx = 2f,
                    HeadDy = frame is 1 or 3 ? 0f : 2f,
                    TailSwing = frame < 2 ? 2f : -2f,
                    Arm = frame is 0 or 1 ? DinoArm.Rest : DinoArm.Cocked
                };
            }
            case ActPose.Jump:
            {
                // Launch: legs trail; nearing the apex they tuck under.
                bool rising = game.VelocityY < -330f;
                return new DinoPose
                {
                    Legs = rising ? DinoLegs.Dangling : DinoLegs.Tucked,
                    HeadDy = rising ? -2f : 0f,
                    TailSwing = rising ? -2f : 0f,
                    Arm = rising ? DinoArm.Raised : DinoArm.Rest
                };
            }
            case ActPose.Fall:
            {
                // Legs reach for the ground, tail lifts for balance.
                bool fast = game.VelocityY > 420f;
                return new DinoPose
                {
                    Legs = DinoLegs.Dangling,
                    HeadDy = fast ? 2f : 0f,
                    TailSwing = fast ? 4f : 2f,
                    Arm = fast ? DinoArm.Flail : DinoArm.Rest
                };
            }
            case ActPose.Hurt:
                return new DinoPose
                {
                    Legs = game.Grounded ? DinoLegs.Braced : DinoLegs.Dangling,
                    HeadDx = -4f, HeadDy = -2f, TailSwing = 4f,
                    Arm = DinoArm.Flail, MouthOpen = true, EyeHurt = true
                };
            default:
            {
                // Idle: slow four-beat breath plus an occasional blink.
                int breath = (int)(t * 2.2f) % 4;
                bool blink = game.AnimationClock % 3.4f < .12f;
                return new DinoPose
                {
                    Legs = DinoLegs.Run2,
                    BodyDy = breath == 2 ? 2f : 0f,
                    HeadDy = breath is 1 or 2 ? 2f : 0f,
                    TailSwing = breath switch { 0 => 0f, 1 => 2f, 2 => 2f, _ => 0f },
                    EyeClosed = blink
                };
            }
        }
    }

    private static void DrawRunScuff(Graphics g, float x, float y, float t)
    {
        // Heel dust on the two contact frames.
        int frame = (int)(t * 13f) % 4;
        if (frame is not (0 or 2)) return;
        g.FillRectangle(Brushes.Black, x - 12f, y + 48f, 6f, 2f);
        g.FillRectangle(Brushes.Black, x - 22f, y + 50f, 4f, 2f);
    }

    private static void DrawDashLines(Graphics g, float x, float y, float t)
    {
        // Speed lines stream off the back; they shift each frame so they read
        // as motion rather than a static decal.
        float shift = (int)(t * 30f) % 2 == 0 ? 0f : 6f;
        g.FillRectangle(Brushes.Black, x - 22f - shift, y + 32f, 16f, 4f);
        g.FillRectangle(Brushes.Black, x - 42f + shift, y + 42f, 24f, 2f);
        g.FillRectangle(Brushes.Black, x - 58f - shift, y + 36f, 10f, 2f);
        g.FillRectangle(Brushes.Black, x - 34f + shift, y + 26f, 8f, 2f);
    }

    // --------------------------------------------------------- claw combo
    // Three distinct swings: a jab, a rising uppercut and a lunging finisher.
    // Anticipation / contact / recovery boundaries equal the combat hit window.
    private static void DrawClawAttack(Graphics g, float x, float y, int stage, float time)
    {
        int index = Math.Clamp(stage, 1, 3) - 1;
        float start = ComboHitStarts[index];
        float end = ComboHitEnds[index];
        float duration = ComboDurations[index];
        bool windup = time < start;
        bool contact = !windup && time < end;
        float settle = Math.Clamp((time - end) / MathF.Max(.01f, duration - end), 0f, 1f);

        DinoPose pose;
        if (windup)
        {
            pose = stage switch
            {
                3 => new DinoPose { BodyDy = 4, HeadDx = -4, HeadDy = 2, TailSwing = 4,
                    Legs = DinoLegs.Crouched, Arm = DinoArm.Cocked },
                2 => new DinoPose { BodyDy = 2, HeadDx = -2, HeadDy = 2, TailSwing = 2,
                    Legs = DinoLegs.Crouched, Arm = DinoArm.Cocked },
                _ => new DinoPose { HeadDx = -2, TailSwing = 2,
                    Legs = DinoLegs.Run0, Arm = DinoArm.Cocked }
            };
        }
        else if (contact)
        {
            pose = stage switch
            {
                3 => new DinoPose { BodyDy = 2, HeadDx = 6, HeadDy = 2, TailSwing = -4,
                    Legs = DinoLegs.Braced, Arm = DinoArm.Extended, MouthOpen = true },
                2 => new DinoPose { BodyDy = -2, HeadDx = 2, HeadDy = -4, TailSwing = -2,
                    Legs = DinoLegs.Run3, Arm = DinoArm.Raised, MouthOpen = true },
                _ => new DinoPose { HeadDx = 4, TailSwing = -2,
                    Legs = DinoLegs.Run3, Arm = DinoArm.Extended }
            };
        }
        else
        {
            // Recovery eases back toward neutral over the remaining frames.
            float back = 1f - settle;
            pose = new DinoPose
            {
                HeadDx = (stage == 3 ? 4f : 2f) * back,
                BodyDy = stage == 3 ? 2f * back : 0f,
                TailSwing = -2f * back,
                Legs = stage == 3 && settle < .5f ? DinoLegs.Braced : DinoLegs.Run2,
                Arm = settle < .5f ? DinoArm.Extended : DinoArm.Rest
            };
        }
        PixelSprites.DrawDinoPosed(g, x, y, pose, Brushes.Black, Brushes.White);
        if (!contact) return;

        // Claw rakes on the contact frames, matching each swing's direction.
        int reach = stage == 3 ? 87 : stage == 2 ? 75 : 68;
        int lift = stage == 2 ? -8 : stage == 3 ? -2 : 4;
        float progress = (time - start) / MathF.Max(.01f, end - start);
        Pen pen = stage == 3 ? ClawHeavyPen : ClawPen;
        for (int stroke = 0; stroke < 3; stroke++)
        {
            float sy = y + 10f + stroke * 11f + lift;
            // Strokes extend over the first third of contact, then hold.
            float tip = x + 50f + (reach - 50f) * Math.Clamp(progress * 3f, .35f, 1f);
            if (stage == 2)
                g.DrawLine(pen, x + 50f, sy + 22f, tip, sy - 4f);
            else
                g.DrawLine(pen, x + 50f, sy + 17f, tip, sy);
            g.FillRectangle(Brushes.White, tip - 5f, sy - (stage == 2 ? 6f : 2f), 4f, 3f);
        }
        if (stage == 3 && progress < .5f)
        {
            // Finisher impact star at full reach.
            g.FillRectangle(Brushes.Black, x + 82f, y + 8f, 4f, 30f);
            g.FillRectangle(Brushes.Black, x + 70f, y + 21f, 28f, 4f);
            g.FillRectangle(Brushes.White, x + 82f, y + 21f, 4f, 4f);
        }
    }

    // --------------------------------------------------------- tail sweep
    // Coil, four-frame bilateral whip, then a settle. Hit window is .17-.36s.
    private static void DrawTailSweep(Graphics g, float x, float y, float time)
    {
        if (time < TailSweepStart)
        {
            // Coil: weight shifts forward, tail drawn low and tight.
            PixelSprites.DrawDinoPosed(g, x, y, new DinoPose
            {
                BodyDy = 2, HeadDx = 2, HeadDy = 2, TailSwing = -4,
                Legs = DinoLegs.Braced, Arm = DinoArm.Cocked
            }, Brushes.Black, Brushes.White);
            g.FillRectangle(Brushes.Black, x - 10f, y + 38f, 14f, 6f);
            g.FillRectangle(Brushes.Black, x - 18f, y + 42f, 10f, 4f);
            return;
        }
        if (time < TailSweepEnd)
        {
            float phase = Math.Clamp((time - TailSweepStart) /
                (TailSweepEnd - TailSweepStart), 0f, 1f);
            float turn = SmoothStep(Math.Clamp((phase - .32f) / .36f, 0f, 1f));
            bool backSide = turn < .5f;
            PixelSprites.DrawDinoPosed(g, x, y, new DinoPose
            {
                BodyDy = 2, HeadDx = -4f + turn * 8f, HeadDy = 2,
                TailSwing = 2f - turn * 4f,
                Legs = DinoLegs.Braced, Arm = DinoArm.Rest,
                MouthOpen = phase is > .2f and < .55f
            }, Brushes.Black, Brushes.White);
            // The tail whips behind (frames 0-1) and then around under the
            // body to the front (frames 2-3), covering both sides of the hitbox.
            // Every point stays above the ground line (y + 52).
            float px = x + 10f + turn * 28f;
            float py = y + 35f + MathF.Sin(phase * MathF.PI) * 5f;
            float dx = -MathF.Cos(turn * MathF.PI);
            float dy = -.1f + MathF.Sin(phase * MathF.PI * 2f) * .12f;
            float reach = 98f - turn * 18f;
            // Overlapping segments form one continuous, tapering tail.
            int segments = (int)(reach / 5f);
            for (int seg = 0; seg <= segments; seg++)
            {
                float along = seg / (float)segments;
                float size = MathF.Max(2f, MathF.Round((10f - along * 7f) * .5f) * 2f);
                float sx = MathF.Round((px + dx * reach * along) * .5f) * 2f;
                float sy = MathF.Round((py + dy * reach * along) * .5f) * 2f;
                // Clamp the whole segment, including its radius, above the feet.
                sy = MathF.Min(sy, y + 50f - size * .5f);
                g.FillRectangle(Brushes.Black, sx - size * .5f, sy - size * .5f, size, size);
            }
            // Motion smear trailing the whip tip.
            float tipX = px + dx * reach;
            float tipY = py + dy * reach;
            float smearY = MathF.Min(y + 48f,
                MathF.Round((tipY - 6f) * .5f) * 2f);
            g.FillRectangle(Brushes.Black, tipX - dx * 18f - 6f, smearY, 12f, 2f);
            g.FillRectangle(Brushes.Black, tipX - dx * 34f - 4f,
                MathF.Min(y + 48f, smearY + (backSide ? -4f : 4f)), 8f, 2f);
            return;
        }
        // Settle: tail drifts back to rest as the recovery runs out.
        float settle = Math.Clamp((time - TailSweepEnd) / (TailDuration - TailSweepEnd), 0f, 1f);
        PixelSprites.DrawDinoPosed(g, x, y, new DinoPose
        {
            BodyDy = 2f * (1f - SmoothStep(settle)),
            TailSwing = -2f * (1f - SmoothStep(settle)),
            Legs = settle < .5f ? DinoLegs.Braced : DinoLegs.Run2
        }, Brushes.Black, Brushes.White);
    }

    private static float SmoothStep(float amount) => amount * amount * (3f - 2f * amount);

    // --------------------------------------------------------- stomp
    private static void DrawStomp(Graphics g, float x, float y, bool falling,
        float impactRemaining, float clock)
    {
        if (falling)
        {
            // Dive: head tucked, feet driven down, arms up.
            PixelSprites.DrawDinoPosed(g, x, y, new DinoPose
            {
                HeadDy = 4, TailSwing = 4, Legs = DinoLegs.Dangling,
                Arm = DinoArm.Raised, MouthOpen = true
            }, Brushes.Black, Brushes.White);
            // Air streaks above, flickering between two offsets.
            float flick = (int)(clock * 30f) % 2 == 0 ? 0f : 4f;
            g.FillRectangle(Brushes.Black, x + 10f, y - 20f - flick, 4f, 14f);
            g.FillRectangle(Brushes.Black, x + 24f, y - 26f + flick, 2f, 18f);
            g.FillRectangle(Brushes.Black, x + 36f, y - 16f - flick, 4f, 10f);
            return;
        }
        // Impact: deep crouch that springs back up as the impact timer drains.
        float k = Math.Clamp(impactRemaining / StompImpactTime, 0f, 1f);
        PixelSprites.DrawDinoPosed(g, x, y, new DinoPose
        {
            BodyDy = k > .55f ? 4f : k > .25f ? 2f : 0f,
            HeadDy = k > .55f ? 4f : 2f, TailSwing = k > .55f ? -4f : 0f,
            Legs = DinoLegs.Crouched, Arm = DinoArm.Rest, MouthOpen = k > .55f
        }, Brushes.Black, Brushes.White);
        // Ground cracks racing outward.
        float spread = MathF.Round(72f * (1f - k) * .5f) * 2f;
        g.FillRectangle(Brushes.Black, x - 26f - spread, y + 50f, 40f, 2f);
        g.FillRectangle(Brushes.Black, x + 40f + spread, y + 50f, 40f, 2f);
        g.FillRectangle(Brushes.Black, x - 10f - spread * .5f, y + 46f, 22f, 2f);
        g.FillRectangle(Brushes.Black, x + 42f + spread * .5f, y + 46f, 22f, 2f);
    }

    // --------------------------------------------------------- bone spike
    private static void DrawRanged(Graphics g, float x, float y, float time)
    {
        bool release = time < .11f;
        PixelSprites.DrawDinoPosed(g, x, y, new DinoPose
        {
            HeadDx = release ? 2f : 0f, TailSwing = release ? -2f : 0f,
            Legs = DinoLegs.Run2, Arm = DinoArm.Extended, MouthOpen = release
        }, Brushes.Black, Brushes.White);
        if (release)
        {
            // Muzzle flash at the claw tip.
            g.FillRectangle(Brushes.Black, x + 52f, y + 24f, 8f, 2f);
            g.FillRectangle(Brushes.Black, x + 54f, y + 20f, 2f, 10f);
            g.FillRectangle(Brushes.White, x + 54f, y + 24f, 2f, 2f);
        }
    }

    // ================================================================ spike

    internal static void DrawSpike(Graphics g, BoneSpike spike)
    {
        float x = MathF.Round(spike.X);
        float y = MathF.Round(spike.Y);
        if (spike.Special)
        {
            GraphicsState turned = g.Save();
            g.TranslateTransform(x + spike.Bounds.Width/2, y + spike.Bounds.Height/2);
            g.ScaleTransform(spike.Scale,spike.Scale);
            g.RotateTransform(MathF.Atan2(spike.Vy, spike.Vx) * 180f / MathF.PI);
            DrawRightSpike(g, -10f, -4f);
            g.Restore(turned);
            return;
        }
        if (spike.Direction < 0)
        {
            GraphicsState saved = g.Save();
            g.TranslateTransform(x + 20f, 0f);
            g.ScaleTransform(-1f, 1f);
            DrawRightSpike(g, 0f, y);
            g.Restore(saved);
        }
        else DrawRightSpike(g, x, y);
    }

    private static void DrawRightSpike(Graphics g, float x, float y)
    {
        g.FillRectangle(Brushes.Black, x, y + 2f, 14f, 5f);
        g.FillRectangle(Brushes.Black, x + 12f, y + 3f, 5f, 3f);
        g.FillRectangle(Brushes.Black, x + 17f, y + 4f, 3f, 1f);
        g.FillRectangle(Brushes.White, x + 3f, y + 3f, 5f, 1f);
        // Ridge notches along the shaft.
        g.FillRectangle(Brushes.White, x + 10f, y + 2f, 1f, 1f);
        g.FillRectangle(Brushes.Black, x - 3f, y + 3f, 3f, 1f);
    }

    // ================================================================ enemies

    private static readonly SolidBrush FlashInk = new(Color.FromArgb(118, 123, 133));

    internal static void DrawEnemySprite(Graphics g, ActEnemy enemy, ActVisualFx? fx = null)
    {
        if (enemy.State == ActEnemyState.Dead || enemy.Dormant) return;

        // Hit flash alternates solid ink with a lighter tone instead of
        // blinking the sprite out, so the enemy never disappears mid-fight.
        bool flash = enemy.FlashRemaining > 0f &&
            (int)(enemy.FlashRemaining * 35f) % 2 == 0;
        Brush ink = flash ? FlashInk : Brushes.Black;

        float punch = fx?.EnemyPunch(enemy) ?? 0f;
        float x = MathF.Round(enemy.X + MathF.Round(-enemy.Facing * punch * 4f));
        float y = MathF.Round(enemy.Y);

        GraphicsState saved = g.Save();
        if (enemy.Facing < 0)
        {
            g.TranslateTransform(x + enemy.Width, 0f);
            g.ScaleTransform(-1f, 1f);
            x = 0f;
        }
        DrawEnemyBody(g, enemy.Kind, x, y, enemy.State, enemy.StateTime,
            enemy.BossMove, enemy.Health, enemy.MaxHealth, ink, Brushes.White, enemy.BossId);
        g.Restore(saved);

        if(enemy.State==ActEnemyState.Dazed)
        {
            using var daze=new Pen(ActUiTheme.Gold,2);
            float cx=enemy.X+enemy.Width*.5f,top=enemy.Y-12f;
            for(int i=-1;i<=1;i++)
            {
                float sx=cx+i*13f,sy=top+(i==0?-5:0);
                g.DrawLine(daze,sx-4,sy,sx+4,sy);
                g.DrawLine(daze,sx,sy-4,sx,sy+4);
            }
        }

        if (enemy.Kind == ActEnemyKind.Boss && enemy.BossId != ActBossId.Tyrant)
            ActBossVisual.DrawIntent(g, enemy);
        else if (enemy.State == ActEnemyState.Windup) DrawWindupIndicator(g, enemy);
        if (enemy.Kind is ActEnemyKind.Heavy or ActEnemyKind.Boss) DrawEnemyHpBar(g, enemy);
    }

    // Shared by live enemies and the FX death collapse.
    internal static void DrawEnemyBody(Graphics g, ActEnemyKind kind, float x, float y,
        ActEnemyState state, float time, BossMove move, int health, int maxHealth,
        Brush ink, Brush paper, ActBossId bossId = ActBossId.Tyrant)
    {
        switch (kind)
        {
            case ActEnemyKind.Ranged:
                PixelSprites.DrawRangedEnemy(g, x, y, state, time, ink, paper);
                break;
            case ActEnemyKind.Flying:
                PixelSprites.DrawFlyingEnemy(g, x, y, state, time, ink, paper);
                break;
            case ActEnemyKind.Heavy:
                PixelSprites.DrawHeavyEnemy(g, x, y, state, time, health, maxHealth, ink, paper);
                break;
            case ActEnemyKind.Boss:
                ActBossVisual.DrawBody(g, bossId, x, y, state, time, move, health, maxHealth, ink, paper);
                break;
            default:
                PixelSprites.DrawMeleeEnemy(g, x, y, state, time, ink, paper);
                break;
        }
    }

    private static void DrawEnemyHpBar(Graphics g, ActEnemy enemy)
    {
        float ex = MathF.Round(enemy.X);
        float ey = MathF.Round(enemy.Y) - (enemy.Kind == ActEnemyKind.Boss ? 22f : 12f);
        g.FillRectangle(Brushes.White, ex - 1f, ey - 1f, enemy.Width + 2f, 7f);
        g.DrawRectangle(Pens.Black, ex, ey, enemy.Width, 5f);
        g.FillRectangle(Brushes.Black, ex + 1f, ey + 1f,
            (enemy.Width - 1f) * enemy.Health / enemy.MaxHealth, 4f);
    }

    // Range telegraphs are unchanged from the original: they describe the real
    // attack areas. Only the alarm mark above the head is restyled.
    private static void DrawWindupIndicator(Graphics g, ActEnemy enemy)
    {
        float ex = MathF.Round(enemy.X);
        float ey = MathF.Round(enemy.Y);
        using var warning = new Pen(Color.Black, 3f);
        if (enemy.Kind == ActEnemyKind.Boss && enemy.BossMove == BossMove.Quake)
            g.DrawRectangle(warning, ex - ActEnemyTuning.BossQuakeRange,
                ActTuning.GroundY - 9f,
                ActEnemyTuning.BossQuakeRange * 2f + enemy.Width, 8f);
        else if (enemy.Kind == ActEnemyKind.Boss && enemy.BossMove == BossMove.Tail)
            g.DrawRectangle(warning, ex - ActEnemyTuning.BossTailRange,
                ActTuning.GroundY - 36f,
                ActEnemyTuning.BossTailRange * 2f + enemy.Width, 29f);
        else if (enemy.Kind == ActEnemyKind.Flying)
            g.DrawEllipse(warning, enemy.TargetX - 32f, enemy.TargetY - 20f, 64f, 40f);
        else
        {
            float ahead = enemy.Facing > 0 ? ex + enemy.Width : ex - 120f;
            float width = enemy.Kind switch
            {
                ActEnemyKind.Boss => 210f, ActEnemyKind.Heavy => 150f,
                ActEnemyKind.Ranged => 185f, _ => 90f
            };
            g.DrawRectangle(warning, ahead, ey + enemy.Height - 29f, width, 18f);
        }

        // Pixel "!" that blinks faster as the windup nears release.
        float windup = enemy.Kind switch
        {
            ActEnemyKind.Ranged => ActEnemyTuning.RangedWindup,
            ActEnemyKind.Flying => ActEnemyTuning.FlyingWindup,
            ActEnemyKind.Heavy => ActEnemyTuning.HeavyWindup,
            ActEnemyKind.Boss => ActEnemyTuning.BossWindup,
            _ => ActEnemyTuning.MeleeWindup
        };
        float k = Math.Clamp(enemy.StateTime / windup, 0f, 1f);
        if ((int)(enemy.StateTime * (6f + k * 16f)) % 2 == 1 && k < .85f) return;
        float mx = MathF.Round(ex + enemy.Width * .5f) - 2f;
        float my = ey - (enemy.Kind == ActEnemyKind.Boss ? 40f : 28f);
        g.FillRectangle(Brushes.White, mx - 2f, my - 2f, 8f, 16f);
        g.FillRectangle(Brushes.Black, mx, my, 4f, 8f);
        g.FillRectangle(Brushes.Black, mx, my + 10f, 4f, 2f);
    }
}
