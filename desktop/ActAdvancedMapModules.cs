using System.Drawing;

namespace DinoDash.Desktop;

internal static partial class ActAdvancedMaps
{
    // Each line below is a hand-authored profile. Only whole profiles and
    // explicitly placed branches/encounters are shuffled between runs.
    private static void Path(ActLevelLayout map, string id, float origin,
        params (float X, float Y)[] points)
    {
        for (int i = 1; i < points.Length; i++)
            Segment(map, id + i, origin, points[i - 1].X, points[i - 1].Y,
                points[i].X, points[i].Y);
    }

    private static float Foot(ActLevelLayout map, float x) =>
        ActSecondMap.GroundAt(map, x) ?? throw new InvalidOperationException($"Missing authored floor at {x}");

    private static void Shelves(ActLevelLayout map, float origin,
        params (float X, float Y, float Width)[] ledges)
    { foreach (var (x, y, width) in ledges) Shelf(map, origin + x, y, width); }

    private static void ForestModuleExpanded(ActLevelLayout map, int index, float x,
        int shape, int route)
    {
        string name = shape switch
        {
            3 => "巨根阶庭", 4 => "叶影断廊", 5 => "空心古树",
            6 => "双峰藤谷", 7 => "幽荧菌谷", 8 => "鸟巢断枝",
            _ => "沉根回声"
        };
        switch (shape)
        {
            case 3:
                Path(map, name, x, (0, 300), (300, 300), (650, 260), (900, 210),
                    (1350, 210), (1700, 320), (2000, 320), (2400, 440),
                    (2800, 440), (3200, 300), (3600, 260), (4000, 300));
                break;
            case 4:
                Path(map, name, x, (0, 300), (350, 300), (750, 430), (1250, 430),
                    (1550, 300), (1900, 260), (2150, 300), (2500, 300),
                    (2800, 400), (3250, 400), (3550, 300), (4000, 300));
                break;
            case 5:
                Path(map, name, x, (0, 300), (500, 270), (950, 270), (1250, 400),
                    (1850, 400), (2300, 300), (2600, 230), (3000, 230),
                    (3400, 360), (3800, 300), (4000, 300));
                // Under the hollow trunk is a walkable lower passage.
                map.Solids.Add(new RectangleF(x + 1310, 295, 430, 23));
                break;
            case 6:
                Path(map, name, x, (0, 300), (500, 200), (900, 200), (1300, 370),
                    (1650, 370), (2100, 180), (2500, 180), (2900, 420),
                    (3350, 420), (3700, 300), (4000, 300));
                break;
            case 7:
                Path(map, name, x, (0, 300), (500, 350), (900, 480),
                    (1500, 480), (1850, 330), (2350, 330), (2700, 450),
                    (3150, 450), (3500, 300), (4000, 300));
                break;
            case 8:
                Path(map, name, x, (0, 300), (400, 260), (850, 260),
                    (1250, 410), (1600, 410), (2000, 300), (2400, 240),
                    (2700, 310), (3100, 430), (3450, 430), (3800, 300),
                    (4000, 300));
                break;
            default:
                Path(map, name, x, (0, 300), (450, 330), (800, 450),
                    (1200, 450), (1650, 280), (2100, 280), (2500, 390),
                    (2900, 390), (3350, 230), (3650, 230), (4000, 300));
                break;
        }

        // Two readable upper branch plans, or a root-level side passage.
        // The uninterrupted lower path always remains available.
        if (route == 0)
        {
            (float X, float Y, float Width)[] crown = shape switch
            {
                3 => [(390, 210, 180), (700, 175, 170), (1000, 125, 190),
                    (1340, 145, 180), (1680, 220, 185)],
                4 => [(380, 240, 175), (710, 260, 180), (1010, 205, 190),
                    (1390, 220, 180), (1780, 180, 175)],
                5 => [(390, 200, 175), (700, 170, 170), (1010, 180, 190),
                    (1350, 210, 190), (1770, 235, 180)],
                6 => [(360, 175, 185), (700, 125, 180), (1040, 180, 190),
                    (1430, 215, 175), (1780, 150, 180)],
                7 => [(400, 265, 185), (740, 240, 180), (1060, 190, 185),
                    (1410, 230, 175), (1780, 220, 180)],
                8 => [(400, 195, 190), (730, 155, 170), (1050, 175, 190),
                    (1410, 215, 175), (1760, 200, 180)]
                , _ => [(400, 245, 185), (730, 210, 180), (1050, 165, 180),
                    (1420, 200, 175), (1800, 175, 180)]
            };
            Shelves(map, x, crown);
            map.MovingPlatforms.Add(new(new RectangleF(x + 2060, shape == 6 ? 100 : 180, 170, 14),
                ActPlatformMotion.Horizontal, 65, 5.5f));
            map.MovingPlatforms.Add(new(new RectangleF(x + 2510, 205, 170, 14),
                ActPlatformMotion.Falling));
            map.Shortcuts.Add(new ActShortcut(name + "树冠", x + 390, x + 2800));
        }
        else if (route == 1)
        {
            int[] anchors = (shape % 3) switch
            {
                0 => [500, 790, 1080, 1370, 1660, 1950, 2240, 2530],
                1 => [450, 735, 1020, 1305, 1590, 1875, 2160, 2445],
                _ => [535, 815, 1095, 1375, 1655, 1935, 2215, 2495]
            };
            foreach (int anchor in anchors)
            {
                float top = Foot(map, x + anchor + 85) - 70;
                if (shape == 5 && anchor >= 1300 && anchor < 1740)
                    top = 220; // Route passes over the hollow trunk.
                Shelf(map, x + anchor, top, 170);
            }
            float liftX = x + anchors[^1] + 185;
            map.MovingPlatforms.Add(new(new RectangleF(liftX,
                Foot(map, liftX + 65) - 120, 130, 14),
                ActPlatformMotion.Vertical, 45, 5f));
            map.Shortcuts.Add(new ActShortcut(name + "横枝", x + 500, x + 2650));
        }
        else
        {
            float floor1 = Foot(map, x + 730);
            float floor2 = Foot(map, x + 2220);
            Shelves(map, x, (650, floor1 - 60, 160),
                (2130, floor2 - 65, 175));
            map.Shortcuts.Add(new ActShortcut(name + "树根", x + 650, x + 2250));
        }

        float firstValley = shape switch { 3 => 2480, 4 => 970, 5 => 1480,
            6 => 1440, 7 => 1180, 8 => 1420, _ => 1030 };
        float secondValley = shape switch { 3 => 2670, 4 => 2980, 5 => 3500,
            6 => 3150, 7 => 2900, 8 => 3250, _ => 2740 };
        map.Mechanisms.Add(new(ActMechanismKind.Mushroom,
            x + firstValley, Foot(map, x + firstValley), 78));
        map.Mechanisms.Add(new(ActMechanismKind.Mushroom,
            x + secondValley, Foot(map, x + secondValley), 78));
        map.MovingPlatforms.Add(new(new RectangleF(x + 2860,
            Foot(map, x + 2920) - 105, 130, 14), ActPlatformMotion.Vertical, 45, 5.2f));

        float low1 = Foot(map, x + 1220 + 18);
        float low2 = Foot(map, x + 2750 + (shape == 6 ? 34 : 18));
        float low3 = Foot(map, x + 3370 + 34);
        bool highEnemy = route != 2;
        float highX = route == 0 ? x + 1070 : x + 1115;
        float highFoot = route == 0 ? shape switch
        { 3 => 125, 4 => 205, 5 => 180, 6 => 180, 7 => 190, 8 => 175, _ => 165 } :
            route == 2 ? 0 :
            map.Solids.First(s => highX + 18 >= s.Left && highX + 18 < s.Right &&
                s.Top < Foot(map, highX + 18)).Top;
        Encounter(map, index, x, 4000, $"T{index}-{shape}-{route} {name}",
            route == 2 ? ActRoute.Lower : ActRoute.Both,
            index % 2 == 1 ? ActRewardKind.BattleArt : ActRewardKind.Heal,
            Spawn(ActEnemyKind.Melee, x + 1220, low1),
            highEnemy ? Spawn(ActEnemyKind.Ranged, highX, highFoot) :
                Spawn(ActEnemyKind.Melee, x + 1880, Foot(map, x + 1898)),
            Spawn(shape == 6 ? ActEnemyKind.Heavy : ActEnemyKind.Melee,
                x + 2750, low2),
            Spawn(ActEnemyKind.Flying, x + 3370, low3));
        if (highEnemy) map.Rewards.Add(new ActReward(ActRewardKind.FullRangedEnergy,
            highX + 18, highFoot - 29, index));
    }

    private static void LavaBridge(ActLevelLayout map, float x, float from,
        float to, float surface, float platformY, int state, params float[] stones)
    {
        map.Mechanisms.Add(new(ActMechanismKind.Lava, x + from, surface, to - from));
        // A short crack needs a short stone. Keep its entire travel inside
        // the opening so the visible top never grows out of either bank.
        if (to - from <= 200)
        {
            RectangleF shortStone = new(x + from + 30, platformY, to - from - 60, 14);
            if (state == 2)
                map.MovingPlatforms.Add(new(shortStone, ActPlatformMotion.Horizontal, 20, 4.7f));
            else map.Solids.Add(shortStone);
            return;
        }
        for (int i = 0; i < stones.Length; i++)
        {
            RectangleF bounds = new(x + stones[i], platformY + (i % 2 == 0 ? 0 : -16), 145, 14);
            if (state == 2 && i == stones.Length / 2)
                map.MovingPlatforms.Add(new(bounds, ActPlatformMotion.Horizontal, 24, 4.7f));
            else map.Solids.Add(bounds);
        }
    }

    private static void VolcanoModuleExpanded(ActLevelLayout map, int index, float x,
        int shape, int state)
    {
        string name = shape switch
        {
            3 => "阶叠熔湖", 4 => "黑曜石台地", 5 => "脉动喷口",
            6 => "倾斜玄武岩", 7 => "灰烬竖井", 8 => "断桥回廊",
            _ => "余烬阶桥"
        };
        switch (shape)
        {
            case 3:
                Path(map, name, x, (0, 300), (300, 250), (650, 220), (900, 270));
                LavaBridge(map, x, 900, 1530, 440, 270, state, 930, 1120, 1310);
                Path(map, name, x, (1530, 300), (1900, 370), (2250, 370),
                    (2600, 300), (2850, 300));
                LavaBridge(map, x, 2850, 2990, 415, 295, state, 2870);
                Path(map, name, x, (2990, 300), (3400, 250), (3700, 300), (4200, 300));
                break;
            case 4:
                Path(map, name, x, (0, 300), (450, 340), (900, 340),
                    (1200, 220), (1500, 220));
                LavaBridge(map, x, 1500, 2100, 440, 240, state, 1530, 1725, 1920);
                Path(map, name, x, (2100, 300), (2450, 430), (2900, 430),
                    (3200, 300), (4200, 300));
                break;
            case 5:
                Path(map, name, x, (0, 300), (400, 190), (750, 190),
                    (1000, 330), (1200, 330));
                LavaBridge(map, x, 1200, 1370, 450, 310, state, 1225);
                Path(map, name, x, (1370, 330), (2000, 270), (2500, 270),
                    (3050, 230));
                LavaBridge(map, x, 3050, 3200, 375, 230, state, 3075);
                Path(map, name, x, (3200, 230), (3650, 330), (4200, 300));
                break;
            case 6:
                Path(map, name, x, (0, 300), (400, 400), (750, 400), (1000, 310));
                LavaBridge(map, x, 1000, 1600, 440, 305, state, 1035, 1230, 1425);
                Path(map, name, x, (1600, 300), (2050, 260), (2500, 180),
                    (2950, 180), (3400, 390), (3800, 300), (4200, 300));
                break;
            case 7:
                Path(map, name, x, (0, 300), (500, 250), (900, 250), (1200, 330));
                LavaBridge(map, x, 1200, 1800, 465, 325, state, 1235, 1430, 1625);
                Path(map, name, x, (1800, 360), (2250, 420), (2750, 420),
                    (3100, 300), (3550, 250), (4200, 300));
                break;
            case 8:
                Path(map, name, x, (0, 300), (450, 200), (800, 200),
                    (1200, 360), (1500, 360));
                LavaBridge(map, x, 1500, 2050, 475, 335, state, 1520, 1710, 1900);
                Path(map, name, x, (2050, 300), (2500, 230), (2900, 230),
                    (3400, 360), (3800, 300), (4200, 300));
                break;
            default:
                Path(map, name, x, (0, 300), (480, 210), (760, 210),
                    (1100, 295));
                LavaBridge(map, x, 1100, 1550, 435, 295, state, 1130, 1325);
                Path(map, name, x, (1550, 310), (1950, 350), (2350, 260),
                    (2650, 260));
                LavaBridge(map, x, 2650, 2840, 405, 260, state, 2675);
                Path(map, name, x, (2840, 260), (3270, 390), (3650, 300),
                    (4200, 300));
                break;
        }

        float middle = Foot(map, x + 2400);
        if (shape is 4 or 7)
        {
            map.Mechanisms.Add(new(ActMechanismKind.Updraft,
                x + 2290, Foot(map, x + 2290), 70));
            Shelf(map, x + 2410, middle - 135, 210);
        }
        else if (shape is 3 or 8)
            Shelf(map, x + 2160, Foot(map, x + 2270) - 120, 190);
        if (shape == 6) Shelf(map, x + 2750, 115, 190);

        float hazardX = shape switch { 3 => 2200, 4 => 2650, 5 => 2480,
            6 => 3050, 7 => 2520, 8 => 2700, _ => 2180 };
        float hazardY = Foot(map, x + hazardX + 24);
        map.Mechanisms.Add(new(state == 1 ? ActMechanismKind.Rock : ActMechanismKind.Eruption,
            x + hazardX, hazardY, state == 1 ? 48 : 75));

        float firstX = shape switch { 3 => 470, 4 => 890, 5 => 760,
            6 => 650, 7 => 810, 8 => 900, _ => 600 };
        float middleX = shape switch { 3 => 1950, 4 => 2460, 5 => 2300,
            6 => 2250, 7 => 2330, 8 => 2430, _ => 2250 };
        Encounter(map, index, x, 4200, $"V{index}-{shape}-{state} {name}",
            ActRoute.Both, index % 3 == 0 ? ActRewardKind.Heal : ActRewardKind.BattleArt,
            Spawn(ActEnemyKind.Heavy, x + firstX, Foot(map, x + firstX + 34)),
            Spawn(state == 2 ? ActEnemyKind.Flying : ActEnemyKind.Melee,
                x + middleX, Foot(map, x + middleX + 18)),
            Spawn(ActEnemyKind.Ranged, x + 3850, Foot(map, x + 3868)));
    }
}
