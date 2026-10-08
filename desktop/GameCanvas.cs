using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace DinoDash.Desktop;

internal sealed class GameCanvas : Control
{
    private readonly ActEngine engine;
    private readonly ActVisualFx fx = new();
    private readonly ActParallaxBackdrop backdrop = new();
    private readonly ActHudRenderer hud = new();
    private readonly ActRegionPresentation region = new();
    private readonly ToolTip evolutionTip = new();
    private bool sceneOnly;
    public event Action? FormLibraryRequested;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ActBindings Bindings { get; set; } = new();

    public void UpdateHud(bool snap = false) => hud.Sync(engine, snap);

    internal Bitmap RenderRewardScene()
    {
        var bitmap = new Bitmap(Width, Height);
        sceneOnly = true;
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            OnPaint(new PaintEventArgs(graphics, ClientRectangle));
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
        finally { sceneOnly = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hud.Dispose(); region.Dispose(); evolutionTip.Dispose(); }
        base.Dispose(disposing);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Dimmed { get; set; } = true;

    // Time accumulator for environment animations that run independent of
    // hit-stop (backdrop and decorations should never freeze).
    private float wallClock;

    public GameCanvas(ActEngine game)
    {
        engine = game;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(18, 18, 18);
        TabStop = true;
        ImeMode = ImeMode.Disable;
        evolutionTip.OwnerDraw=true;
        evolutionTip.BackColor=ActUiTheme.Ink;evolutionTip.ForeColor=ActUiTheme.Paper;
        evolutionTip.Popup+=(_,args)=>
        {
            using var font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel);
            var measured=TextRenderer.MeasureText(evolutionTip.GetToolTip(this),font,new Size(330,int.MaxValue),TextFormatFlags.WordBreak);
            args.ToolTipSize=new Size(measured.Width+24,measured.Height+20);
        };
        evolutionTip.Draw+=(_,args)=>
        {
            args.Graphics.Clear(ActUiTheme.Ink);ActUiTheme.Frame(args.Graphics,new RectangleF(1,1,args.Bounds.Width-3,args.Bounds.Height-3));
            ActUiTheme.Text(args.Graphics,args.ToolTipText??"",new RectangleF(12,8,args.Bounds.Width-24,args.Bounds.Height-16),14);
        };
        MouseClick+=(_,args)=>
        {
            float scale=ActHudRenderer.UiScale(ClientSize,DeviceDpi);
            if(args.Button==MouseButtons.Left && ActFormVisual.HudBounds(Height/scale).Contains(args.X/scale,args.Y/scale))FormLibraryRequested?.Invoke();
        };
        MouseMove += (_, args) =>
        {
            EvolutionDefinition? found = ActHudRenderer.EvolutionAt(args.Location,
                ClientSize, DeviceDpi, engine.Run);
            string tip = found is null ? "" : $"{(found.Rarity==EvolutionRarity.Rare?"稀有进化 · ":"")}{found.Name}：{found.Description}";
            float scale=ActHudRenderer.UiScale(ClientSize,DeviceDpi);
            bool portrait=ActFormVisual.HudBounds(Height/scale).Contains(args.X/scale,args.Y/scale);
            Cursor=portrait?Cursors.Hand:Cursors.Default;
            if(portrait)
            {
                var form=ActPrimalCatalog.Form(engine.Run.CurrentForm);
                tip=$"{form.Name}：{form.Description}\n点击打开形态库 · {SettingsDialog.KeyName((Keys)Bindings.PreviousForm)} / {SettingsDialog.KeyName((Keys)Bindings.NextForm)} 切换形态";
                if(engine.Run.FormCooldown>0)tip+=$"\n切换冷却：{engine.Run.FormCooldown:0.0} 秒";
            }
            if (evolutionTip.GetToolTip(this) != tip)
                evolutionTip.SetToolTip(this, tip);
        };
    }

    // Called by GameForm each tick so the FX layer can observe state changes
    // even when the engine itself is paused (hit-stop, overlay, etc.).
    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        wallClock += dt;
        hud.Tick(dt);
        region.Update(engine.PlayerX, dt);
        fx.NoteEngineShake(engine.ShakeRemaining);
        fx.Update(engine, dt);
    }

    // Called by GameForm when the engine is reset so FX state is also cleared.
    public void ResetFx()
    {
        wallClock = 0f;
        fx.Reset();
        hud.Sync(engine, true);
        region.SetZones(engine.MapIndex >= 2 || engine.IsRoomJourney || engine.IsResting ? engine.Layout.Zones : ActLevel.Zones);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        if (engine.IsResting)
        {
            ActRestVisual.DrawScene(g, engine, ClientSize, wallClock, Bindings);
            if (!sceneOnly) hud.Draw(g, engine, ClientSize, DeviceDpi, Bindings);
            if (Dimmed)
            {
                using var restVeil = new SolidBrush(Color.FromArgb(155,12,15,21));
                g.FillRectangle(restVeil, ClientRectangle);
            }
            return;
        }
        g.Clear(Color.FromArgb(18, 18, 18));

        float scale = Math.Min(ClientSize.Width  / ActTuning.ViewWidth,
                               ClientSize.Height / ActTuning.ViewHeight);
        if (scale <= 0f) return;

        float ox = (ClientSize.Width  - ActTuning.ViewWidth  * scale) / 2f;
        // Preserve the entire 960 x 360 combat view. Extra height extends scenery
        // above and below it instead of creating thick empty window borders.
        float extraHeight = Math.Max(0, ClientSize.Height / scale - ActTuning.ViewHeight);
        float skyExtension = extraHeight * .82f;
        float oy = skyExtension * scale;

        GraphicsState outerSaved = g.Save();
        g.TranslateTransform(ox, oy);
        g.ScaleTransform(scale, scale);
        g.SmoothingMode     = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        // --- background fill (paper tone, not pure white) ----------------
        g.FillRectangle(ActVisualTheme.PaperBrush,
            0, 0, ActTuning.ViewWidth, ActTuning.ViewHeight);

        // --- shake offset (visual FX layer owns the shake math) ----------
        float shakeX = fx.ShakeX;
        float shakeY = fx.ShakeY;
        // Fall back to the engine's built-in shake when fx hasn't seeded it
        // yet (first frame after reset).
        if (shakeX == 0f && shakeY == 0f && engine.ShakeRemaining > 0f)
        {
            float mag = engine.ShakeRemaining > .15f
                ? ActTuning.HeavyShake : ActTuning.LightShake;
            shakeX = (int)(engine.AnimationClock * 76f) % 2 == 0 ?  mag : -mag;
            shakeY = (int)(engine.AnimationClock * 91f) % 2 == 0 ?  2f  : -2f;
        }

        GraphicsState worldSaved = g.Save();
        g.TranslateTransform(shakeX, shakeY);

        // ---- backdrop (drawn in screen space, not world) ----------------
        var skySaved = g.Save();
        g.TranslateTransform(0, -skyExtension);
        g.ScaleTransform(1, (ActTuning.GroundY + skyExtension) / ActTuning.GroundY);
        if (engine.MapIndex >= 3)
            ActAdvancedMapVisual.DrawBackdrop(g, engine.MapIndex, engine.CameraX, engine.CameraY, wallClock);
        else if (engine.MapIndex == 2)
            ActSecondMapVisual.DrawBackdrop(g, engine.CameraX, wallClock);
        else backdrop.Draw(g, engine.CameraX, wallClock);
        g.Restore(skySaved);

        // ---- world-space camera transform --------------------------------
        g.TranslateTransform(-engine.CameraX, -engine.CameraY);

        // ---- ground body + props (behind everything) --------------------
        if (engine.MapIndex >= 3)
        {
            ActAdvancedMapVisual.DrawBackgroundProps(g, engine, wallClock);
            ActAdvancedMapVisual.DrawPlatformSupports(g, engine);
        }
        if (engine.MapIndex >= 2 || engine.IsRoomJourney)
            ActSecondMapVisual.DrawTerrain(g, engine.Layout.Terrain, engine.CameraX, engine.MapIndex);
        else
        {
            ActLevelDecor.DrawGroundBody(g, engine.CameraX);
            g.FillRectangle(ActVisualTheme.GroundBrush, engine.CameraX - 40,
                ActTuning.ViewHeight - 1, ActTuning.ViewWidth + 80, extraHeight + 10);
        }
        foreach (RectangleF solid in engine.MapIndex >= 3 ? engine.Layout.Solids : engine.Solids)
            if (engine.MapIndex >= 2 || engine.IsRoomJourney || solid.Top < ActTuning.GroundY)
            {
                if (engine.MapIndex >= 3) ActAdvancedMapVisual.DrawPlatform(g, solid, engine.MapIndex);
                else if (engine.MapIndex == 2) ActSecondMapVisual.DrawPlatform(g, solid);
                else ActLevelDecor.DrawPlatform(g, solid, wallClock);
            }
        if (engine.MapIndex >= 3)
            ActAdvancedMapVisual.DrawEnvironment(g, engine, wallClock);
        else if (engine.MapIndex == 2)
            ActSecondMapVisual.DrawProps(g, engine.Layout, engine.CameraX, wallClock);
        else if(engine.IsRoomJourney)ActChapterRoomScenery.DrawWetlandProps(g,engine);
        else ActLevelDecor.DrawGroundProps(g, engine.CameraX, wallClock, engine.Supplies);

        foreach (ActSupply supply in engine.Supplies)
            if (supply.Right >= engine.CameraX && supply.Left <= engine.CameraX + ActTuning.ViewWidth)
                ActSupplyVisual.Draw(g, supply, engine.MapIndex, wallClock);

        // ---- level chrome (gates, zones, hazards, rewards, supplies) ----
        DrawLevelChrome(g);
        ActRestVisual.DrawBones(g, engine, wallClock);
        ActBossVisual.DrawHazards(g, engine);

        // ---- FX behind characters ---------------------------------------
        ActFxRenderer.DrawBehind(g, fx);

        // ---- enemies ----------------------------------------------------
        DrawEnemies(g);

        // ---- projectiles ------------------------------------------------
        foreach (BoneSpike spike in engine.Projectiles)
            ActSpriteAnimator.DrawSpike(g, spike);

        // ---- enemy shots ------------------------------------------------
        foreach (EnemyShot shot in engine.EnemyShots)
            if (shot.Source is { Kind: ActEnemyKind.Boss }) ActBossVisual.DrawProjectile(g, shot);
            else DrawEnemyShot(g, shot);

        // ---- player -----------------------------------------------------
        ActSpriteAnimator.DrawDinosaur(g, engine, fx);
        ActFormVisual.Draw(g,engine);

        // ---- FX in front ------------------------------------------------
        ActFxRenderer.DrawFront(g, fx);
        ActPowerVisuals.Draw(g, engine, wallClock);

        // ---- impact flash overlay (screen-space, applied after world) ---
        if (fx.ImpactFlash > 0f)
            g.FillRectangle(
                ActVisualTheme.Fade(Color.White, (int)(fx.ImpactFlash * 60f)),
                engine.CameraX, 0, ActTuning.ViewWidth, ActTuning.ViewHeight);
        if (fx.HeavyFlash > 0f)
            g.FillRectangle(
                ActVisualTheme.Fade(Color.White, (int)(fx.HeavyFlash * 38f)),
                engine.CameraX, 0, ActTuning.ViewWidth, ActTuning.ViewHeight);

        // ---- foreground fringe ------------------------------------------
        if (engine.MapIndex == 1 && !engine.IsRoomJourney)
            ActLevelDecor.DrawForeground(g, engine.CameraX, wallClock);

        g.Restore(worldSaved);

        // ---- dim overlay for menus/overlays ------------------------------
        g.Restore(outerSaved);
        if (!sceneOnly)
        {
            hud.Draw(g, engine, ClientSize, DeviceDpi, Bindings);
            region.Draw(g, ClientSize, DeviceDpi);
        }
        if (Dimmed)
        {
            using var veil = new SolidBrush(Color.FromArgb(155, 12, 15, 21));
            g.FillRectangle(veil, ClientRectangle);
        }
    }

    // ---------------------------------------------------------------- level

    private void DrawLevelChrome(Graphics g)
    {
        // Hazards
        foreach (ActHazard hazard in engine.Hazards)
            DrawHazard(g, hazard, engine.RunTime);

        // Rewards
        foreach (ActReward reward in engine.Rewards)
        {
            if (reward.Collected || !engine.IsRewardUnlocked(reward)) continue;
            ActRewardIcons.DrawPickup(g, reward.X, reward.Y, reward.Kind, wallClock);
        }

        // Supplies (health crosses)
        foreach (ActSupply supply in engine.Supplies)
        {
            if (supply.Collected) continue;
            ActRewardIcons.DrawPickup(g, supply.X, supply.Y, ActRewardKind.Heal, wallClock);
        }

        // Exit door
        if(engine.IsRoomJourney && engine.Run.RoomJourney!.Stage!=RoomJourneyStage.Boss)
        {ActRoomJourneyVisual.DrawWorld(g,engine,wallClock,Bindings);return;}
        float exit = engine.ExitX;
        float exitGround = engine.TerrainYAt(exit) ?? ActTuning.GroundY;
        g.FillRectangle(ActVisualTheme.InkBrush,
            exit, exitGround - 94f, 7f, 94f);
        bool open = engine.ExitUnlocked;
        g.FillRectangle(open ? ActVisualTheme.PaperBrush : ActVisualTheme.InkBrush,
            exit + 7f, exitGround - 92f, 42f, 26f);
        using var doorPen = new Pen(ActVisualTheme.Ink, 3f);
        g.DrawRectangle(doorPen, exit + 7f, exitGround - 92f, 42f, 26f);
        if (open)
        {
            // Glowing doorway when boss is dead.
            float glow = .5f + .5f * MathF.Sin(wallClock * 6f);
            g.FillRectangle(
                ActVisualTheme.Fade(Color.FromArgb(240, 230, 180), (int)(120f * glow)),
                exit + 7f, exitGround - 92f, 42f, 26f);
            ActRestVisual.DrawPortal(g, engine, exit, exitGround, false);
        }
    }

    private static void DrawHazard(Graphics g, ActHazard hazard, float time)
    {
        RectangleF bounds = hazard.Bounds(time);
        bool active = hazard.IsActive(time);
        switch (hazard.Kind)
        {
            case ActHazardKind.Spikes:
                for (int i = 0; i < 3; i++)
                {
                    float x = hazard.X + i * 13f;
                    g.FillPolygon(ActVisualTheme.InkBrush,
                    [new PointF(x, hazard.SurfaceY),
                     new PointF(x + 6f, hazard.SurfaceY - 17f),
                     new PointF(x + 13f, hazard.SurfaceY)]);
                    g.FillRectangle(ActVisualTheme.Fade(Color.FromArgb(210, 210, 220), 200),
                        x + 2f, hazard.SurfaceY - 16f, 2f, 10f);
                }
                break;
            case ActHazardKind.Vent:
            {
                g.FillRectangle(ActVisualTheme.InkBrush, hazard.X - 3f,
                    hazard.SurfaceY - 7f, 36f, 7f);
                using var nozzle = new Pen(ActVisualTheme.Ink, 2f);
                g.DrawLine(nozzle, hazard.X - 1f, hazard.SurfaceY - 13f,
                    hazard.X + 31f, hazard.SurfaceY - 13f);
                if (active)
                {
                    using var hot = new Pen(Color.FromArgb(211, 191, 145), 3f);
                    for (int i = 0; i < 3; i++)
                    {
                        float sx = hazard.X + 4f + i * 10f;
                        g.DrawLines(hot, [new PointF(sx, hazard.SurfaceY - 14f),
                            new PointF(sx + 4f, hazard.SurfaceY - 31f),
                            new PointF(sx - 1f, hazard.SurfaceY - 49f),
                            new PointF(sx + 5f, hazard.SurfaceY - 65f)]);
                    }
                }
                else
                {
                    using var pen = new Pen(ActVisualTheme.Ink, 1f);
                    g.DrawRectangle(pen, hazard.X, hazard.SurfaceY - 17f, 30f, 4f);
                }
                break;
            }
            case ActHazardKind.Rock:
            {
                using var guidePen = new Pen(ActVisualTheme.Fade(ActVisualTheme.Ink, 60), 1f);
                g.DrawLine(guidePen, hazard.X + 13f, hazard.SurfaceY - 275f,
                    hazard.X + 13f, hazard.SurfaceY);
                if (active)
                {
                    g.FillRectangle(ActVisualTheme.InkBrush, bounds);
                    g.FillRectangle(ActVisualTheme.PaperBrush,
                        bounds.X + 6f, bounds.Y + 4f, 5f, 4f);
                }
                else
                {
                    using var pen = new Pen(ActVisualTheme.Ink, 1f);
                    g.DrawRectangle(pen, hazard.X, hazard.SurfaceY - 4f, 27f, 4f);
                }
                break;
            }
        }
    }

    // ---------------------------------------------------------------- enemies

    private void DrawEnemies(Graphics g)
    {
        foreach (ActEnemy enemy in engine.Enemies)
            ActSpriteAnimator.DrawEnemySprite(g, enemy, fx);
    }

    private static void DrawEnemyShot(Graphics g, EnemyShot shot)
    {
        // More readable shot: elongated crystal shape with bright core.
        float vlen = MathF.Sqrt(shot.Vx * shot.Vx + shot.Vy * shot.Vy);
        float nx = vlen > 0 ? shot.Vx / vlen : 1f;
        float ny = vlen > 0 ? shot.Vy / vlen : 0f;
        g.FillPolygon(ActVisualTheme.InkBrush,
        [
            new PointF(shot.X       + nx * 14f, shot.Y       + ny * 14f),
            new PointF(shot.X - ny * 4f, shot.Y + nx * 4f),
            new PointF(shot.X       - nx * 3f,  shot.Y       - ny * 3f),
            new PointF(shot.X + ny * 4f, shot.Y - nx * 4f),
        ]);
        g.FillRectangle(ActVisualTheme.PaperBrush,
            shot.X + nx * 4f - 2f, shot.Y + ny * 4f - 1f, 5f, 2f);
    }

}
