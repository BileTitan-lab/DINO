namespace DinoDash.Desktop;

// Temporary procedural effects. All positions and timers come from public
// engine state; this layer cannot change combat or movement.
internal static class ActPowerVisuals
{
    public static void Draw(Graphics g, ActEngine engine, float clock)
    {
        float x = engine.PlayerX + engine.PlayerWidth / 2f;
        float y = engine.PlayerY + engine.PlayerHeight / 2f;
        DrawBuild(g,engine,clock);
        if (engine.Run.ShieldCharges > 0 || engine.Run.BarrierCharges>0)
        {
            using var shield = new Pen(Color.FromArgb(175, 116, 184, 212), 3f);
            g.DrawEllipse(shield, x - 37, y - 41, 74, 82);
        }
        if (engine.Run.RageRemaining > 0f || engine.Run.Remaining(BuildBuff.BloodRage)>0 || engine.Run.Remaining(BuildBuff.LifeBurn)>0)
        {
            using var rage = new Pen(Color.FromArgb(190, 220, 109, 62), 3f);
            for (int i = 0; i < 5; i++)
            {
                float px = x - 22 + i * 11;
                float flame = MathF.Sin(clock * 9 + i) * 5;
                g.DrawLine(rage, px, engine.PlayerY + 9,
                    px + flame, engine.PlayerY - 4 - i % 2 * 6);
            }
        }
        if (engine.Run.HuntRemaining > 0f)
        {
            using var rush = new Pen(Color.FromArgb(150, 220, 201, 122), 2f);
            for (int i = 0; i < 4; i++)
                g.DrawLine(rush, x - engine.Facing * (22 + i * 6),
                    y - 18 + i * 11, x - engine.Facing * (44 + i * 6),
                    y - 18 + i * 11);
        }
        if (engine.Run.EnemySlowRemaining > 0f)
        {
            int amberAlpha = 115 + (int)(30f * MathF.Sin(clock * 5f));
            using var amber = new Pen(Color.FromArgb(amberAlpha, 224, 178, 64), 2f);
            foreach (ActEnemy enemy in engine.Enemies)
            {
                if (enemy.State == ActEnemyState.Dead || enemy.Dormant ||
                    Math.Abs(enemy.X - engine.CameraX) > ActTuning.ViewWidth + 100f)
                    continue;
                g.DrawEllipse(amber, enemy.X - 5f, enemy.Y - 5f,
                    enemy.Width + 10f, enemy.Height + 10f);
                g.DrawLine(amber, enemy.X - 10f, enemy.Y - 11f,
                    enemy.X + 13f, enemy.Y - 11f);
            }
        }
        if (engine.PowerCueRemaining <= 0f) return;
        Color tone = engine.PowerCueRare?ActUiTheme.Gold:engine.PowerCueEvolution ? Color.FromArgb(170, 127, 220) :
            engine.PowerCueArt switch
            {
                BattleArtId.RegenerativeSpores => Color.FromArgb(95, 202, 121),
                BattleArtId.BoneArmor => Color.FromArgb(116, 184, 212),
                BattleArtId.PrimalRage => Color.FromArgb(220, 109, 62),
                BattleArtId.AmberLag => Color.FromArgb(224, 178, 64),
                BattleArtId.EarthquakeRoar => Color.FromArgb(180, 143, 96),
                BattleArtId.EnergyReturn => Color.FromArgb(110, 179, 230),
                BattleArtId.HuntingInstinct => Color.FromArgb(220, 201, 122),
                _ => Color.FromArgb(222, 221, 204)
            };
        float duration = engine.PowerCueEvolution ? 1.1f : .8f;
        float progress = 1f - engine.PowerCueRemaining / duration;
        int alpha = (int)(Math.Clamp(engine.PowerCueRemaining / duration, 0f, 1f) * 220);
        float radius = 25f + progress * (engine.PowerCueArt == BattleArtId.EarthquakeRoar
            ? ActPowerCatalog.Art(BattleArtId.EarthquakeRoar).Radius : 72f);
        using var ring = new Pen(Color.FromArgb(alpha, tone),
            engine.PowerCueArt == BattleArtId.EarthquakeRoar ? 5f : 3f);
        if (engine.PowerCueArt == BattleArtId.EarthquakeRoar)
            g.DrawEllipse(ring, x - radius, (engine.TerrainYAt(x) ?? ActTuning.GroundY) - 11,
                radius * 2, 22);
        else g.DrawEllipse(ring, x - radius, y - radius, radius * 2,
            radius * 2);
        int count = engine.PowerCueArt == BattleArtId.RegenerativeSpores ? 9 : 12;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2f * MathF.PI / count + clock * .6f;
            float px = x + MathF.Cos(angle) * radius;
            float py = y + MathF.Sin(angle) * radius;
            g.FillRectangle(ActVisualTheme.Fade(tone, alpha), px - 2, py - 2,
                engine.PowerCueArt == BattleArtId.RegenerativeSpores ? 5 : 4, 4);
        }
        if(engine.PowerCueRare)
            ActRewardIcons.Draw(g,new RectangleF(x-18,y-58-progress*14,36,36),RewardEmblem.RareEvolution,tone);
    }

    private static void DrawBuild(Graphics g,ActEngine engine,float clock)
    {
        float x=engine.PlayerX+engine.PlayerWidth/2,y=engine.PlayerY+24;
        foreach(var pulse in engine.BuildPulses)
        {
            float strength=Math.Clamp(pulse.Life/ActBuildTuning.AfterimageLife,0,1);
            using var ring=new Pen(Color.FromArgb((int)(strength*155),ActUiTheme.Gold),2);
            if(pulse.GroundWave)g.DrawArc(ring,pulse.X-26,pulse.Y-12,52,30,185,170);
            else if(pulse.Delay>0)
            {
                using var silhouette=new SolidBrush(Color.FromArgb((int)(strength*70),ActUiTheme.Gold));
                g.FillEllipse(silhouette,pulse.X-18,pulse.Y-27,36,54);
                g.DrawEllipse(ring,pulse.X-23,pulse.Y-32,46,64);
            }
            else g.DrawEllipse(ring,pulse.X-pulse.Radius,pulse.Y-15,pulse.Radius*2,30);
        }
        if(engine.BoneRainWarning>0)
        {
            using var warning=new Pen(Color.FromArgb(150,ActUiTheme.Gold),2);
            for(int i=0;i<9;i++)
            {
                float px=engine.CameraX+55+i*105,py=engine.PlayerY-100;
                g.DrawLines(warning,[new PointF(px-5,py),new(px,py+6),new(px+5,py)]);
            }
        }
        foreach(var enemy in engine.Enemies.Where(e=>e.FearRemaining>0 && e.State!=ActEnemyState.Dead))
        {
            using var fear=new Pen(Color.FromArgb(170,ActUiTheme.Paper),2);
            g.DrawArc(fear,enemy.X+enemy.Width/2-9,enemy.Y-19,18,14,0,180);
            g.DrawLine(fear,enemy.X+enemy.Width/2,enemy.Y-22,enemy.X+enemy.Width/2,enemy.Y-17);
        }
        if(engine.Run.Remaining(BuildBuff.Overload)>0 || engine.Run.Remaining(BuildBuff.GeneRampage)>0 || engine.Run.Remaining(BuildBuff.Counter)>0 || engine.Run.ArmedClawMultiplier>1)
        {
            Color tone=engine.Run.Remaining(BuildBuff.Counter)>0?ActRewardIcons.Jade:ActUiTheme.Gold;
            using var glow=new Pen(Color.FromArgb(120+(int)(35*MathF.Sin(clock*7)),tone),2);
            g.DrawArc(glow,x-31,y-36,62,72,clock*80,225);
            for(int i=0;i<4;i++)g.FillRectangle(ActVisualTheme.Fade(tone,150),x-21+i*14,y-28-(clock*20+i*11)%28,3,3);
        }
    }
}
