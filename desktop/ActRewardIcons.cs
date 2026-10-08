using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

internal enum RewardEmblem
{
    Food, BattleArt, Evolution, Energy, Attack, Speed, Shield, Spore,
    Amber, Storm, Quake, Claws, Tail, Wings, Stomp, TwinSpikes, Hunter, Scales, Tissue, RareEvolution
}

// Shared, resolution-independent emblems for world pickups and reward cards.
internal static class ActRewardIcons
{
    public static readonly Color Bone = Color.FromArgb(239, 230, 204);
    public static readonly Color Gold = Color.FromArgb(218, 176, 100);
    public static readonly Color Jade = Color.FromArgb(132, 207, 183);
    private static readonly Color Ink = Color.FromArgb(29, 34, 38);

    public static RewardEmblem For(ActRewardKind kind) => kind switch
    {
        ActRewardKind.Heal => RewardEmblem.Food,
        ActRewardKind.FullRangedEnergy => RewardEmblem.Energy,
        ActRewardKind.DamageBuff => RewardEmblem.Attack,
        ActRewardKind.MoveBuff => RewardEmblem.Speed,
        ActRewardKind.Evolution => RewardEmblem.Evolution,
        ActRewardKind.Form => RewardEmblem.Hunter,
        _ => RewardEmblem.BattleArt
    };

    public static RewardEmblem For(BattleArtId id) => id switch
    {
        BattleArtId.RegenerativeSpores => RewardEmblem.Spore,
        BattleArtId.BoneArmor => RewardEmblem.Shield,
        BattleArtId.PrimalRage => RewardEmblem.Claws,
        BattleArtId.AmberLag => RewardEmblem.Amber,
        BattleArtId.BoneStorm => RewardEmblem.Storm,
        BattleArtId.EarthquakeRoar => RewardEmblem.Quake,
        BattleArtId.EnergyReturn => RewardEmblem.Energy,
        BattleArtId.TyrantSmash or BattleArtId.BloodRage=>RewardEmblem.Claws,
        BattleArtId.TailResonance=>RewardEmblem.Tail,
        BattleArtId.SkySlam=>RewardEmblem.Stomp,
        BattleArtId.BoneRain or BattleArtId.SeekingSpikes=>RewardEmblem.Storm,
        BattleArtId.PiercingSpear=>RewardEmblem.TwinSpikes,
        BattleArtId.SpikeOverload or BattleArtId.LifeBurn=>RewardEmblem.Energy,
        BattleArtId.CounterInstinct or BattleArtId.PrimalBarrier=>RewardEmblem.Shield,
        BattleArtId.PredationRecovery=>RewardEmblem.Spore,
        BattleArtId.TerrorRoar=>RewardEmblem.Quake,
        BattleArtId.GeneRampage or BattleArtId.EmergencyMolt=>RewardEmblem.Evolution,
        _ => RewardEmblem.Hunter
    };

    public static RewardEmblem For(EvolutionId id) => ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare
        ? RewardEmblem.RareEvolution : RewardEmblem.Evolution;

    public static void DrawPickup(Graphics g, float x, float y, ActRewardKind kind, float clock)
    {
        bool evolution = kind == ActRewardKind.Evolution;
        Color accent = evolution ? Jade : Gold;
        var saved = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float bob = MathF.Sin(clock * 2.4f + x * .03f) * 2.3f;
        using var shadow = new SolidBrush(Color.FromArgb(95, Ink));
        g.FillEllipse(shadow, x - 19, y + 20, 38, 7);
        using var glow = new SolidBrush(Color.FromArgb(25, accent));
        g.FillEllipse(glow, x - 28, y - 28 + bob, 56, 56);
        using var ring = new Pen(Color.FromArgb(125, accent), 1f);
        g.DrawArc(ring, x - 23, y - 23 + bob, 46, 46, clock * 18, 115);
        g.DrawArc(ring, x - 23, y - 23 + bob, 46, 46, clock * 18 + 180, 115);
        using var plate = new SolidBrush(Color.FromArgb(225, Ink));
        PointF[] diamond = [new(x, y - 26 + bob), new(x + 25, y + bob),
            new(x, y + 26 + bob), new(x - 25, y + bob)];
        g.FillPolygon(plate, diamond);
        g.DrawPolygon(ring, diamond);
        Draw(g, new RectangleF(x - 20, y - 20 + bob, 40, 40), For(kind), accent);
        using var spark = new SolidBrush(Color.FromArgb(180, accent));
        for (int i = 0; i < 3; i++)
        {
            float phase = (clock * .35f + i / 3f) % 1f;
            g.FillRectangle(spark, x - 16 + i * 16, y - 10 - phase * 28, 1.5f, 2.5f);
        }
        g.Restore(saved);
    }

    public static void Draw(Graphics g, RectangleF rect, RewardEmblem kind, Color accent)
    {
        var saved = g.Save();
        g.TranslateTransform(rect.X, rect.Y);
        g.ScaleTransform(rect.Width / 64f, rect.Height / 64f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bone = new SolidBrush(Bone);
        using var tone = new SolidBrush(accent);
        using var dark = new SolidBrush(Ink);
        using var line = new Pen(Bone, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var edge = new Pen(Ink, 2.6f) { LineJoin = LineJoin.Round };
        using var gold = new Pen(accent, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        void Poly(Brush fill, params PointF[] points)
        { g.FillPolygon(fill, points); g.DrawPolygon(edge, points); }
        switch (kind)
        {
            case RewardEmblem.RareEvolution:
                using(var halo=new Pen(Color.FromArgb(65,Gold),9))g.DrawEllipse(halo,5,3,54,58);
                for(int strand=0;strand<2;strand++)
                {
                    float center=21+strand*22;
                    for(int i=0;i<12;i++)
                    {
                        float yy=6+i*4.5f,offset=MathF.Sin(i*.7f+strand)*10;
                        g.DrawLine(gold,center-offset,yy,center+offset,yy);
                        g.FillRectangle(bone,center-offset-2,yy-2,4,4);
                        g.FillRectangle(tone,center+offset-2,yy-2,4,4);
                    }
                }
                g.FillPolygon(tone,[new PointF(32,1),new(36,7),new(32,13),new(28,7)]);
                break;
            case RewardEmblem.Food:
                using (var outline = new Pen(Ink, 12)) g.DrawLine(outline, 32, 34, 49, 51);
                using (var shaft = new Pen(Bone, 8)) g.DrawLine(shaft, 32, 34, 49, 51);
                g.FillEllipse(bone, 45, 48, 10, 10); g.FillEllipse(bone, 50, 43, 10, 10);
                using (var meat = new SolidBrush(Color.FromArgb(183, 112, 62)))
                {
                    g.FillEllipse(meat, 8, 8, 35, 36);
                    g.DrawEllipse(edge, 8, 8, 35, 36);
                }
                g.DrawArc(gold, 15, 14, 21, 24, 190, 100);
                g.FillEllipse(bone, 17, 17, 5, 4);
                break;
            case RewardEmblem.Evolution:
            case RewardEmblem.Tissue:
                for (int i = 0; i < 10; i++)
                {
                    float y = 7 + i * 5.5f;
                    float offset = MathF.Sin(i * .65f) * 15;
                    g.DrawLine(gold, 32 - offset, y, 32 + offset, y);
                }
                PointF[] a = new PointF[40], b = new PointF[40];
                for (int i = 0; i < a.Length; i++)
                {
                    float y = 6 + i * 52f / 39;
                    float offset = MathF.Sin(i * 6.5f / 39) * 16;
                    a[i] = new(32 + offset, y); b[i] = new(32 - offset, y);
                }
                g.DrawLines(line, a); g.DrawLines(gold, b);
                g.FillEllipse(bone, 29, 3, 6, 6); g.FillEllipse(tone, 33, 55, 6, 6);
                break;
            case RewardEmblem.BattleArt:
            case RewardEmblem.Attack:
            case RewardEmblem.Claws:
                if (kind != RewardEmblem.Claws)
                    Poly(tone, new(32, 4), new(37, 23), new(57, 16), new(43, 32),
                        new(59, 44), new(39, 41), new(31, 60), new(26, 42), new(6, 48),
                        new(20, 32), new(7, 20), new(25, 23));
                for (int i = 0; i < 3; i++)
                {
                    float x = 10 + i * 15;
                    Poly(bone, new(x + 11, 8), new(x + 8, 30), new(x - 2, 53),
                        new(x + 3, 28));
                }
                break;
            case RewardEmblem.Shield:
            case RewardEmblem.Scales:
                Poly(tone, new(32, 5), new(54, 14), new(49, 40), new(32, 59),
                    new(15, 40), new(10, 14));
                Poly(dark, new(32, 13), new(45, 19), new(42, 36), new(32, 49), new(22, 36), new(19, 19));
                if (kind == RewardEmblem.Shield)
                { g.DrawLine(line, 32, 18, 32, 42); g.DrawLine(line, 23, 28, 41, 28); }
                else for (int i = 0; i < 3; i++)
                    g.DrawLines(line, [new(23, 20 + i * 8), new(32, 26 + i * 8), new(41, 20 + i * 8)]);
                break;
            case RewardEmblem.Spore:
                g.DrawBezier(gold, 31, 55, 39, 37, 18, 27, 31, 10);
                g.FillEllipse(tone, 8, 23, 20, 12); g.FillEllipse(bone, 34, 34, 20, 12);
                g.FillEllipse(bone, 22, 7, 19, 15);
                for (int i = 0; i < 4; i++) g.FillEllipse(tone, 9 + i * 12, 8 + i % 2 * 43, 4, 4);
                break;
            case RewardEmblem.Amber:
                Poly(tone, new(24, 4), new(47, 12), new(56, 35), new(37, 59), new(12, 47), new(7, 24));
                g.DrawEllipse(edge, 19, 18, 27, 28);
                g.DrawLine(line, 32, 23, 32, 34); g.DrawLine(line, 32, 34, 40, 38);
                g.DrawLine(line, 16, 20, 23, 12);
                break;
            case RewardEmblem.Storm:
                for (int i = 0; i < 8; i++)
                {
                    var turn = g.Save(); g.TranslateTransform(32, 32); g.RotateTransform(i * 45);
                    Poly(bone, new(-4, -12), new(0, -30), new(4, -12)); g.Restore(turn);
                }
                g.FillEllipse(tone, 24, 24, 16, 16);
                break;
            case RewardEmblem.Quake:
            case RewardEmblem.Stomp:
                g.DrawArc(gold, 5, 35, 54, 21, 5, 170);
                g.DrawArc(line, 15, 31, 34, 14, 5, 170);
                Poly(bone, new(26, 6), new(39, 6), new(39, 24), new(48, 29), new(46, 36), new(19, 36), new(22, 25));
                g.DrawLines(gold, [new(30, 42), new(25, 48), new(35, 50), new(29, 59)]);
                break;
            case RewardEmblem.Tail:
                using (var tail = new Pen(accent, 11) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawBezier(tail, 13, 15, 60, 15, 60, 53, 10, 49);
                g.DrawBezier(line, 14, 11, 57, 12, 58, 46, 15, 49);
                Poly(bone, new(17, 41), new(3, 51), new(20, 55));
                break;
            case RewardEmblem.Hunter:
                g.DrawEllipse(gold, 12, 12, 40, 40);
                g.DrawLine(line, 32, 3, 32, 16); g.DrawLine(line, 32, 48, 32, 61);
                g.DrawLine(line, 3, 32, 16, 32); g.DrawLine(line, 48, 32, 61, 32);
                Poly(bone, new(21, 32), new(32, 23), new(43, 32), new(32, 41));
                g.FillEllipse(dark, 29, 28, 6, 8);
                break;
            case RewardEmblem.Speed:
            case RewardEmblem.Wings:
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 3; i++)
                        Poly(i == 0 ? bone : tone, new(32, 39 + i * 3),
                            new(32 + side * (27 - i * 5), 9 + i * 11), new(32 + side * (17 - i * 3), 42 + i * 6));
                g.DrawLine(line, 32, 24, 32, 56);
                break;
            default:
                int count = kind == RewardEmblem.TwinSpikes ? 2 : 3;
                for (int i = 0; i < count; i++)
                {
                    float x = 12 + i * 15;
                    Poly(i % 2 == 0 ? bone : tone, new(x, 50), new(x + 7, 8 + i % 2 * 8),
                        new(x + 14, 50), new(x + 7, 57));
                    g.DrawLine(edge, x + 7, 27, x + 7, 51);
                }
                break;
        }
        g.Restore(saved);
    }
}
