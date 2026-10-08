using System.Drawing;

namespace DinoDash.Desktop;

internal static class ActChapters
{
    public const int Count = 4;
    public static string Name(int index) => index switch
    { 1 => "晨雾湿地", 2 => "风蚀峡谷", 3 => "巨木秘境", 4 => "熔火遗迹", _ => "未知区域" };
}

internal static partial class ActAdvancedMaps
{
    private static void Segment(ActLevelLayout map, string id, float x, float a, float y,
        float b, float nextY) => map.Terrain.Add(new(id, x + a, x + b, y, nextY));
    private static void Shelf(ActLevelLayout map, float x, float y, float width = 180) =>
        map.Solids.Add(new RectangleF(x, y, width, 14));
    private static ActEnemy Spawn(ActEnemyKind kind, float x, float feet)
    {
        var sample = ActLevel.Make(kind, x);
        float y = kind == ActEnemyKind.Flying ? feet - 120 : feet - sample.Height;
        return new ActEnemy { Kind = kind, X = x, HomeX = x, Y = y, HomeY = y,
            Health = sample.Health, MaxHealth = sample.MaxHealth };
    }
    private static void Encounter(ActLevelLayout map, int index, float x, float length,
        string id, ActRoute route, ActRewardKind reward, params ActEnemy[] enemies)
    {
        map.Rooms.Add(new RoomSelection(new RoomNode(index, x, x + length, 0, 9, []),
            new RoomVariant(id, RoomTag.Platform, route, [], [], []),
            new EnemyGroup(id, enemies.Select(e => e.Kind).ToArray()), ActHazardKind.None, reward));
        map.Enemies.AddRange(enemies);
        map.Rewards.Add(new ActReward(reward, x + length - 130, 272, index));
        map.Zones.Add(new ActZone(x, id[(id.IndexOf(' ') + 1)..]));
    }
    private static void Camp(ActLevelLayout map, float x, int chapter, int style)
    {
        string name = (chapter, style) switch
        {
            (3, 0) => "树荫巢地", (3, 1) => "菌光花庭",
            (3, 2) => "盘根庇所", (3, _) => "静藤廊道",
            (4, 0) => "冷岩营地", (4, 1) => "黑曜避风所",
            (4, 2) => "余烬驿站", _ => "灰岩庇所"
        };
        Segment(map, name + "安全地", x, 0, 300, 900, 300);
        map.Supplies.Add(new ActSupply(x + 450, 272, name));
        map.Zones.Add(new ActZone(x, name));
    }

    public static ActLevelLayout Generate(int chapter, int seed)
    {
        if (chapter is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(chapter));
        var rng = new Random(seed);
        bool forest = chapter == 3;
        float width = forest ? 32700 : 43600;
        float entrance = forest ? 900 : 1100;
        var map = new ActLevelLayout(seed) { Width = width, Exit = width - 125,
            BossArenaLeft = width - 1050, BossArenaRight = width - 220 };
        Segment(map, "入口安全地", 0, 0, 300, entrance, 300);
        map.Zones.Add(new ActZone(0, ActChapters.Name(chapter)));
        float x = entrance;
        int count = forest ? 7 : 9;
        float length = forest ? 4000 : 4200;
        // The three original families share one ground profile. Keep only one
        // per run; the seven new authored profiles each appear at most once.
        int[] original = Enumerable.Range(0, 3).OrderBy(_ => rng.Next()).ToArray();
        int[] fresh = Enumerable.Range(3, 7).OrderBy(_ => rng.Next()).ToArray();
        var deck = (forest ? fresh.Take(6) : fresh).Concat(original.Take(1))
            .OrderBy(_ => rng.Next()).ToList();
        if (!forest) deck.Insert(4, -1); // The collapse node owns this socket.
        int[] campDeck = Enumerable.Range(0, 4).OrderBy(_ => rng.Next()).ToArray();
        for (int i = 0; i < count; i++)
        {
            int shape = deck[i];
            if (forest && shape < 3) ForestModule(map, i + 1, x, shape, rng.Next(3));
            else if (forest) ForestModuleExpanded(map, i + 1, x, shape, rng.Next(3));
            else if (shape == -1) CollapseModule(map, i + 1, x);
            else if (shape < 3) VolcanoModule(map, i + 1, x, shape, rng.Next(3));
            else VolcanoModuleExpanded(map, i + 1, x, shape, rng.Next(3));
            x += length;
            if (i % 2 == 1 && i < count - 1)
            { Camp(map, x, chapter, campDeck[i / 2]); x += 900; }
        }
        if (Math.Abs(x - (width - 1100)) > .1f) throw new InvalidOperationException("Chapter sockets disconnected.");
        Segment(map, "首领场地", x, 0, 300, 1100, 300);
        map.Zones.Add(new ActZone(x, forest ? "巨木之心" : "熔炉祭坛"));
        if (forest)
        {
            Shelf(map, x + 150, 225, 180);
            Shelf(map, x + 375, 150, 180);
            Shelf(map, x + 650, 220, 180);
            map.MovingPlatforms.Add(new(new RectangleF(x + 45, 220, 105, 14), ActPlatformMotion.Vertical, 35));
        }
        else map.Mechanisms.Add(new(ActMechanismKind.Eruption, x + 220, 300, 75));
        map.Enemies.Add(Spawn(ActEnemyKind.Boss, x + 580, 300));
        map.Terrain.Sort((a, b) => a.Left.CompareTo(b.Left));
        ActPlatformLayout.Finalize(map);
        return map;
    }

    private static void ForestModule(ActLevelLayout map, int index, float x, int shape, int route)
    {
        // Three authored route states: canopy + roots, canopy + shallow floor,
        // or an underground passage with a shorter middle branch.
        float low = route == 1 ? 330 : 430;
        string name = shape switch { 0 => "盘根深谷", 1 => "树冠回廊", _ => "垂藤竖井" };
        Segment(map, "林道入口", x, 0, 300, 250, 300);
        Segment(map, "入根下坡", x, 250, 300, 620, low);
        Segment(map, "根底通路", x, 620, low, 1650, low);
        Segment(map, "树根上坡", x, 1650, low, 2080, 300);
        Segment(map, "中层台地", x, 2080, 300, 2400, 260);
        Segment(map, "竖井下行", x, 2400, 260, 2800, 420);
        Segment(map, "竖井底", x, 2800, 420, 3100, 420);
        Segment(map, "竖井出口", x, 3100, 420, 3540, 300);
        Segment(map, "林道汇合", x, 3540, 300, 4000, 300);
        bool high = route != 2;
        if (high)
        {
            Shelf(map, x + 390, 245, 170);
            Shelf(map, x + 700, 180, 160);
            Shelf(map, x + 940, 110, 210);
            Shelf(map, x + 1470, 125, 185);
            Shelf(map, x + 1970, 225, 180);
            map.MovingPlatforms.Add(new(new RectangleF(x + 1170, 125, 190, 14),
                ActPlatformMotion.Horizontal, 80, 4.8f));
            map.MovingPlatforms.Add(new(new RectangleF(x + 1710, 175, 180, 14), ActPlatformMotion.Falling));
            map.Shortcuts.Add(new ActShortcut("树冠支路", x + 390, x + 2150));
        }
        else
        {
            Shelf(map, x + 470, 270, 160);
            Shelf(map, x + 890, 300, 240);
            Shelf(map, x + 1300, 300, 180);
            map.Solids.Add(new RectangleF(x + 930, 330, 520, 38));
            map.Shortcuts.Add(new ActShortcut("地下树根通道", x + 620, x + 1650));
        }
        map.MovingPlatforms.Add(new(new RectangleF(x + (high ? 575 : 650),
            high ? 265 : 345, 120, 14),
            ActPlatformMotion.Vertical, high ? 40 : 60, 5.5f));
        map.Mechanisms.Add(new(ActMechanismKind.Mushroom, x + 750, low, 75));
        map.Mechanisms.Add(new(ActMechanismKind.Mushroom, x + 2860, 420, 80));
        Shelf(map, x + 2790, 330, 150);
        Shelf(map, x + 3010, 245, 160);
        Shelf(map, x + 3290, 225, 150);
        if (shape == 1)
            map.MovingPlatforms.Add(new(new RectangleF(x + 2420, 195, 150, 14), ActPlatformMotion.Horizontal, 95, 5f));
        if (shape == 2)
            map.MovingPlatforms.Add(new(new RectangleF(x + 2660, 210, 150, 14), ActPlatformMotion.Falling));
        Encounter(map, index, x, 4000, $"T{index}-{route} {name}", high ? ActRoute.Both : ActRoute.Lower,
            index % 2 == 1 ? ActRewardKind.BattleArt : ActRewardKind.Heal,
            Spawn(ActEnemyKind.Melee, x + 1180, low),
            Spawn(high ? ActEnemyKind.Ranged : ActEnemyKind.Melee, x + (high ? 990 : 1460), high ? 110 : low),
            Spawn(shape == 2 ? ActEnemyKind.Heavy : ActEnemyKind.Melee, x + 2980, 420),
            Spawn(ActEnemyKind.Flying, x + 2470, 200));
        // Side reward belongs to the same encounter, so it cannot bypass the fight.
        if (high) map.Rewards.Add(new ActReward(ActRewardKind.FullRangedEnergy, x + 1030, 82, index));
    }

    private static void VolcanoModule(ActLevelLayout map, int index, float x, int shape, int state)
    {
        string name = shape switch { 0 => "熔湖石桥", 1 => "喷烟陡脊", _ => "灼热断层" };
        float crest = shape == 1 ? 200 : 250;
        Segment(map, "火道上坡", x, 0, 300, 400, crest);
        Segment(map, "火道高台", x, 400, crest, 700, crest);
        Segment(map, "岩浆前坡", x, 700, crest, 1100, 320);
        // Lava is crossed using bounded, authored stepping stones. Their worst
        // motion endpoints remain within a base jump; fallback ledges are lower.
        for (int p = 0; p < 4; p++)
        {
            float px = x + 1140 + p * 175;
            float py = p % 2 == 0 ? 300 : 275;
            if (state == 2 && p % 2 == 1)
                map.MovingPlatforms.Add(new(new RectangleF(px, py, 140, 14),
                    shape == 2 ? ActPlatformMotion.Vertical : ActPlatformMotion.Horizontal, 28, 4.5f));
            else Shelf(map, px, py, 140);
        }
        Shelf(map, x + 1280, 380, 200);
        Shelf(map, x + 1570, 380, 200);
        map.Mechanisms.Add(new(ActMechanismKind.Lava, x + 1100, 450, 750));
        Segment(map, "熔湖落点", x, 1850, 320, 2120, 350);
        Segment(map, "战斗岩台", x, 2120, 350, 2660, 350);
        Segment(map, "火道二坡", x, 2660, 350, 3000, 240);
        Segment(map, "裂缝前沿", x, 3000, 240, 3210, 240);
        map.Mechanisms.Add(new(ActMechanismKind.Lava, x + 3210, 390, 140));
        Segment(map, "裂缝后沿", x, 3350, 240, 3710, 300);
        Segment(map, "火道汇合", x, 3710, 300, 4200, 300);
        map.Mechanisms.Add(new(ActMechanismKind.Updraft, x + 2150, 350, 70));
        Shelf(map, x + 2250, 170, 220);
        map.Mechanisms.Add(new(state == 1 ? ActMechanismKind.Rock : ActMechanismKind.Eruption,
            x + 2440, 350, state == 1 ? 48 : 80));
        // A clear telegraph arena, with only one environmental attack active here.
        Encounter(map, index, x, 4200, $"V{index}-{state} {name}", ActRoute.Both,
            index % 3 == 0 ? ActRewardKind.Heal : ActRewardKind.BattleArt,
            Spawn(ActEnemyKind.Heavy, x + 520, crest),
            Spawn(state == 2 ? ActEnemyKind.Flying : ActEnemyKind.Melee, x + 2400, state == 2 ? 260 : 350),
            Spawn(ActEnemyKind.Ranged, x + 3850, 300));
    }

    private static void CollapseModule(ActLevelLayout map, int index, float x)
    {
        Segment(map, "崩塌入口", x, 0, 300, 400, 300);
        for (int i = 0; i < 15; i++)
            map.MovingPlatforms.Add(new(new RectangleF(x + 400 + i * 200, 300, 200, 14),
                ActPlatformMotion.Collapse, triggerX: x + 300, delay: 2.6f + i * .78f));
        map.Mechanisms.Add(new(ActMechanismKind.Lava, x + 400, 460, 3000));
        Segment(map, "崩塌出口", x, 3400, 300, 4200, 300);
        Encounter(map, index, x, 4200, $"V{index}-C 崩塌长桥", ActRoute.Upper,
            ActRewardKind.FullRangedEnergy);
    }
}
