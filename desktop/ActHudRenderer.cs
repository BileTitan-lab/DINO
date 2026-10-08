using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// Screen-space presentation only. Never moves with camera shake or writes to the engine.
internal sealed class ActHudRenderer : IDisposable
{
    internal static readonly Color Panel = ActUiTheme.Panel;
    internal static readonly Color Gold = ActUiTheme.Gold;
    internal static readonly Color Muted = ActUiTheme.Muted;
    private static readonly Color HudInk=Color.FromArgb(39,47,57);
    private static readonly Color HudEmpty=Color.FromArgb(139,148,157);
    private readonly Font small = new("Microsoft YaHei UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font strong = new("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font iconFont = new("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);
    private int previousHp = -1, previousEnergy = -1;
    private int hp, energy;
    private float hpFade, energyFade, time;

    internal static float UiScale(Size size, int dpi) => Math.Max(.5f,
        Math.Min(dpi / 96f, Math.Min(size.Width / 780f, size.Height / 460f)));

    public void Sync(ActEngine engine, bool snap = false)
    {
        if (snap || previousHp < 0)
        {
            previousHp = hp = engine.Health;
            previousEnergy = energy = engine.RangedEnergy;
            hpFade = energyFade = 0;
        }
        if (hp != engine.Health) { previousHp = hp; hp = engine.Health; hpFade = .5f; }
        if (energy != engine.RangedEnergy)
        { previousEnergy = energy; energy = engine.RangedEnergy; energyFade = .5f; }
    }

    public void Tick(float dt)
    {
        time += dt;
        hpFade = Math.Max(0, hpFade - dt);
        energyFade = Math.Max(0, energyFade - dt);
    }

    public void Draw(Graphics g, ActEngine engine, Size size, int dpi,
        ActBindings bindings)
    {
        Sync(engine);
        float scale = UiScale(size, dpi);
        var saved = g.Save();
        g.ScaleTransform(scale, scale);
        float width = size.Width / scale, height = size.Height / scale;
        g.SmoothingMode = SmoothingMode.None;
        // No plate or border: all HUD marks are drawn directly over the scene.
        var formBox=ActFormVisual.HudBounds(height);
        ActFormVisual.DrawHudPortrait(g,formBox,engine.Run.CurrentForm);
        Meter(g, "生命", engine.Run.ExactHealth, engine.MaximumHealth, previousHp, hpFade, 22, true);
        Meter(g, "骨刺", energy, engine.MaximumEnergy, previousEnergy, energyFade, 49, false);
        if(engine.Run.FormCooldown>0)
        {
            g.FillRectangle(ActVisualTheme.Fade(Gold,240),formBox.X+6,formBox.Y+70,
                (formBox.Width-12)*Math.Clamp(1-engine.Run.FormCooldown/(ActPrimalCatalog.SwitchCooldown*engine.Run.Resolve(PowerStat.FormSwitchCooldown,1)),0,1),2);
            Text(g,$"{engine.Run.FormCooldown:0.0}s",small,HudInk,new RectangleF(formBox.X,formBox.Y+75,formBox.Width,18),true);
        }
        DrawBone(g,108,81);
        Text(g, $"骨片  {engine.Run.BoneShards}", strong, HudInk, new RectangleF(133,73,155,22));
        if(engine.DashCooldownRemaining>0)
            Text(g,$"冲刺恢复 {engine.DashCooldownRemaining:0.0}s",small,HudInk,new RectangleF(94,102,185,20));
        string message = engine.ExitCountdown > 0 ?
            $"{(engine.IsRoomJourney && engine.Run.RoomJourney!.Stage!=RoomJourneyStage.Boss ? "前往下一房间" : engine.IsResting ? engine.MapIndex == ActChapters.Count ? "结束远征" : "前往下一章" : "前往休息处")}  {Math.Max(0,ActRestTuning.ExitSeconds-engine.ExitCountdown):0.0} 秒 · 离开范围取消" : engine.RestNotice;
        if (message.Length > 0)
        {
            g.FillRectangle(ActVisualTheme.Fade(Panel,230), width/2-215,height-91,430,30);
            Text(g,message,small,Gold,new RectangleF(width/2-210,height-87,420,24),true);
        }

        // Temporary effects remain visible, without permanently occupying another HUD row.
        string[] buffs = new[] {
            engine.DamageBuffRemaining > 0 ? $"攻击增强  {engine.DamageBuffRemaining:0}s" : "",
            engine.MoveBuffRemaining > 0 ? $"移速增强  {engine.MoveBuffRemaining:0}s" : "",
            engine.Run.RageRemaining > 0f ? $"狂暴 {engine.Run.RageRemaining:0.0}s" : "",
            engine.Run.EnemySlowRemaining > 0f ? $"时滞 {engine.Run.EnemySlowRemaining:0.0}s" : "",
            engine.Run.HuntRemaining > 0f ? $"狩猎 {engine.Run.HuntRemaining:0.0}s" : "",
            engine.Run.ShieldCharges > 0 ? $"骨甲 ×{engine.Run.ShieldCharges}" : ""
        }.Where(s => s.Length > 0).Concat(engine.Run.BuildEffects.Select(pair=>
            $"{pair.Value.Name}{(pair.Key==BuildBuff.Barrier?$" ×{engine.Run.BarrierCharges}":"")} {pair.Value.Remaining:0.0}s"))
            .Concat(engine.Run.ArmedClawMultiplier>1?new[]{"霸王重击 · 下一次爪击"}:Array.Empty<string>()).ToArray();
        if (buffs.Length > 0)
        {
            for (int i = 0; i < buffs.Length; i++)
                Text(g, buffs[i], small, HudInk,
                    new RectangleF(22 + (i % 2) * 175, 131 + i / 2 * 23, 173, 20));
        }

        DrawPowerInventory(g, engine, width, bindings);

        var boss = engine.Boss;
        if (boss.State != ActEnemyState.Dead && Math.Abs(engine.PlayerX - boss.X) <= 800f)
        {
            // Anchored to screen bottom: no camera offset, shake or overlap with player HUD.
            float w = Math.Min(480, width - 80), x = (width - w) / 2, y = height - 59;
            g.FillRectangle(ActVisualTheme.Fade(Panel, 230), x - 12, y - 8, w + 24, 51);
            var identity = ActBossCatalog.Get(boss);
            Text(g, $"{identity.Name} · {identity.Title}", small, Gold, new RectangleF(x, y - 4, w, 21), true);
            g.FillRectangle(ActVisualTheme.Fade(Muted, 55), x, y + 23, w, 7);
            Color tone = boss.Health <= boss.MaxHealth / 2 ? Color.FromArgb(224, 136, 113) : Gold;
            g.FillRectangle(ActVisualTheme.Fade(tone, 245), x, y + 23,
                w * Math.Clamp((float)boss.Health / boss.MaxHealth, 0, 1), 7);
        }
        g.Restore(saved);
    }

    private void DrawPowerInventory(Graphics g, ActEngine engine, float width,
        ActBindings bindings)
    {
        float x = width - 256f;
        for (int i = 0; i < ActPowerCatalog.BattleArtCapacity; i++)
        {
            float y = 77f + i * 43f;
            ActUiTheme.Frame(g,new RectangleF(x,y,238,38),alpha:185);
            string key = SettingsDialog.KeyName((Keys)(i == 0
                ? bindings.BattleArt1 : bindings.BattleArt2));
            Text(g, key, strong, Gold, new RectangleF(x + 8, y + 4, 42, 30));
            if (engine.Run.BattleArts[i] is BattleArtId id)
            {
                BattleArtDefinition art = ActPowerCatalog.Art(id);
                ActUiTheme.Icon(g,new RectangleF(x+49,y+6,26,26),ActRewardIcons.For(id));
                Text(g, art.Name, strong, ActVisualTheme.Paper,
                    new RectangleF(x + 80, y + 4, 150, 30));
            }
            else Text(g, "空战技槽", small, Muted,
                new RectangleF(x + 52, y + 4, 165, 30));
        }
        if (engine.Run.Evolutions.Count == 0) return;
        Text(g, "进化", small, Muted, new RectangleF(x + 1, 166, 50, 20));
        int index = 0;
        foreach (EvolutionId id in engine.Run.Evolutions.Order())
        {
            float iconX = x + 52 + index % 6 * 28;
            float iconY = 166 + index / 6 * 29;
            g.FillRectangle(ActVisualTheme.Fade(Panel, 220), iconX, iconY, 24, 24);
            bool isRare=ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare;
            ActUiTheme.Icon(g,new RectangleF(iconX+2,iconY+2,20,20),ActRewardIcons.For(id),isRare?Gold:null);
            if(ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare)
            {using var rareBorder=new Pen(Gold,1);g.DrawRectangle(rareBorder,iconX,iconY,24,24);}
            index++;
        }
    }

    public static EvolutionDefinition? EvolutionAt(Point point, Size size, int dpi,
        ActRunState run)
    {
        float scale = UiScale(size, dpi);
        float x = point.X / scale, y = point.Y / scale;
        int row = (int)MathF.Floor((y - 166) / 29f);
        if (row < 0 || row >= (run.Evolutions.Count+5)/6 || y > 166 + row * 29 + 24) return null;
        float start = size.Width / scale - 256 + 52;
        int column = (int)MathF.Floor((x - start) / 28f);
        int index = row * 6 + column;
        if (column < 0 || column >= 6 || index >= run.Evolutions.Count ||
            x < start + column * 28 || x > start + column * 28 + 24) return null;
        return ActPowerCatalog.Evolution(run.Evolutions.Order().ElementAt(index));
    }

    private static void DrawBone(Graphics g,float x,float y)
    {
        var state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(-42);
        using var outline=new Pen(Color.Black,2);
        PointF[] bone=[new(-7,-3),new(-9,-5),new(-12,-5),new(-14,-3),new(-14,0),new(-12,2),new(-12,5),new(-9,6),new(-6,3),new(5,3),new(8,5),new(11,4),new(12,1),new(10,-1),new(10,-4),new(7,-5),new(5,-3)];
        g.FillPolygon(ActVisualTheme.Fade(Gold,255),bone);g.DrawPolygon(outline,bone);g.Restore(state);
    }
    private void Meter(Graphics g,string title,float value,int max,int previous,float fade,float y,bool health)
    {
        Text(g,title,strong,HudInk,new RectangleF(94,y-5,38,23));
        Text(g,$"{value:0.#} / {max}",small,HudInk,new RectangleF(248,y-5,52,23));
        float gap=3,cell=(105-gap*(max-1))/max;
        Color tone=health?hp==1&&(int)(time*3)%2==0?Color.FromArgb(167,67,65):HudInk:Gold;
        for(int i=0;i<max;i++)
        {
            float x=133+i*(cell+gap);
            if(health)
            {
                Heart(g,x,y-1,HudEmpty,180,true);
                if(i<value)
                {
                    var clip=g.Save();g.SetClip(new RectangleF(x,y-1,16*Math.Clamp(value-i,0,1),16),CombineMode.Intersect);
                    Heart(g,x,y-1,tone,255);g.Restore(clip);
                }
                else if(i<previous&&fade>0)Heart(g,x,y-1,tone,(int)(fade*400));
            }
            else
            {
                g.FillRectangle(ActVisualTheme.Fade(ActUiTheme.Ink,255),x,y+4,cell,6);
                if(i<value)g.FillRectangle(ActVisualTheme.Fade(tone,255),x,y+4,cell,6);
                else if(i<previous&&fade>0)g.FillRectangle(ActVisualTheme.Fade(tone,(int)(fade*400)),x,y+4,cell,6);
            }
        }
    }
    private static void Heart(Graphics g, float x, float y, Color color, int alpha, bool outline = false)
    {
        PointF[] pixels = [new(2, 0), new(6, 0), new(8, 2), new(10, 0),
            new(14, 0), new(16, 2), new(16, 6), new(14, 6), new(14, 8),
            new(12, 8), new(12, 10), new(10, 10), new(10, 12), new(8, 14),
            new(6, 12), new(6, 10), new(4, 10), new(4, 8), new(2, 8),
            new(2, 6), new(0, 6), new(0, 2)];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new PointF(x + pixels[i].X, y + pixels[i].Y);
        if(!outline)g.FillPolygon(ActVisualTheme.Fade(color, alpha), pixels);
        g.DrawPolygon(ActVisualTheme.FadePen(Color.FromArgb(222,220,209),Math.Min(alpha,200),2f),pixels);
        g.DrawPolygon(ActVisualTheme.FadePen(color,alpha,.8f),pixels);
    }

    private static void Text(Graphics g, string text, Font font, Color color, RectangleF rect, bool center = false)
    {
        using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        bool dark=color.GetBrightness()<.5f;
        var shadow=rect;shadow.Offset(1,1);
        g.DrawString(text,font,ActVisualTheme.Fade(dark?Color.FromArgb(222,220,209):Color.Black,dark?220:165),shadow,format);
        g.DrawString(text, font, ActVisualTheme.Fade(color, 255), rect, format);
    }

    public void Dispose() { small.Dispose(); strong.Dispose(); iconFont.Dispose(); }
}

