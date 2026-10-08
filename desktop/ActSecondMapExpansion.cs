namespace DinoDash.Desktop;

// The second half of the canyon has fixed sockets and authored alternatives.
// Every variant enters and exits at y=300; the two respite clearings rejoin
// the route regardless of the three independent seed choices.
internal static partial class ActSecondMap
{
    private static void AddExtendedTrail(ActLevelLayout map, Random random)
    {
        float x = WorldX(6950f, true) + ClearingLength; // 9650
        map.Zones.Add(new ActZone(x, "赤岩脊"));
        if (random.Next(2) == 0) RedMesa(map, x);
        else TwinRavines(map, x);

        x += 2700f;
        AddCamp(map, x, "赤岩营地");
        x += ClearingLength;
        map.Zones.Add(new ActZone(x, "风穴层道"));
        if (random.Next(2) == 0) HighSaddle(map, x);
        else LowerCauseway(map, x);

        x += 2700f;
        AddCamp(map, x, "古兽前哨");
        x += ClearingLength;
        map.Zones.Add(new ActZone(x, "巨兽断脊"));
        if (random.Next(2) == 0) LongDescent(map, x);
        else TerracedApproach(map, x);
        // Final point is 20550, where the existing boss approach begins.
    }

    private static void RedMesa(ActLevelLayout map, float x)
    {
        Land(map, "D1赤岩上坡", x, 300, x + 350, 255);
        Flat(map, "D1顶脊", x + 350, x + 730, 255);
        Land(map, "D1断脊落点", x + 890, 270, x + 1170, 350);
        Flat(map, "D1风蚀谷", x + 1170, x + 1630, 350);
        Land(map, "D1长回坡", x + 1630, 350, x + 2030, 230);
        Flat(map, "D1高台", x + 2030, x + 2330, 230);
        Land(map, "D1出口", x + 2330, 230, x + 2700, 300);
        map.Hazards.Add(new ActHazard(ActHazardKind.Vent, x + 1460, .4f, 350));
        Room(map, 4, "D1 赤岩脊", x, x + 2700, RoomTag.Challenge, ActRoute.Both,
            ActRewardKind.BattleArt,
            (ActEnemyKind.Heavy, x + 530, 255),
            (ActEnemyKind.Ranged, x + 1350, 350),
            (ActEnemyKind.Flying, x + 2200, 230));
    }

    private static void TwinRavines(ActLevelLayout map, float x)
    {
        Flat(map, "D2裂谷入口", x, x + 300, 300);
        Land(map, "D2下坡", x + 300, 300, x + 600, 370);
        Flat(map, "D2下层", x + 600, x + 1000, 370);
        Land(map, "D2背坡", x + 1000, 370, x + 1400, 250);
        Flat(map, "D2顶地", x + 1400, x + 1830, 250);
        Land(map, "D2第二落点", x + 2010, 305, x + 2330, 350);
        Land(map, "D2出口", x + 2330, 350, x + 2700, 300);
        Platform(map, x + 750, 290, 160);
        Platform(map, x + 1030, 270, 145);
        map.Shortcuts.Add(new ActShortcut("D2 悬空石阶", x + 750, x + 1175));
        Room(map, 4, "D2 双裂谷", x, x + 2700, RoomTag.Platform, ActRoute.Both,
            ActRewardKind.BattleArt,
            (ActEnemyKind.Melee, x + 660, 370),
            (ActEnemyKind.Ranged, x + 1580, 250),
            (ActEnemyKind.Flying, x + 2300, 345));
    }

    private static void HighSaddle(ActLevelLayout map, float x)
    {
        Land(map, "E1爬升", x, 300, x + 400, 215);
        Flat(map, "E1鞍背", x + 400, x + 780, 215);
        Land(map, "E1急降", x + 780, 215, x + 1250, 355);
        Flat(map, "E1洼地", x + 1250, x + 1550, 355);
        Land(map, "E1跃后回升", x + 1720, 355, x + 2100, 300);
        Flat(map, "E1出口", x + 2100, x + 2700, 300);
        map.Hazards.Add(new ActHazard(ActHazardKind.Rock, x + 1100, .35f,
            GroundAt(map, x + 1100)!.Value));
        Room(map, 5, "E1 风口高鞍", x, x + 2700, RoomTag.Challenge, ActRoute.Both,
            ActRewardKind.Heal,
            (ActEnemyKind.Ranged, x + 550, 215),
            (ActEnemyKind.Heavy, x + 1390, 355),
            (ActEnemyKind.Melee, x + 2190, 300));
    }

    private static void LowerCauseway(ActLevelLayout map, float x)
    {
        Land(map, "E2沉降", x, 300, x + 350, 370);
        Flat(map, "E2地下道", x + 350, x + 830, 370);
        Land(map, "E2回升", x + 830, 370, x + 1150, 250);
        Flat(map, "E2上层", x + 1150, x + 1630, 250);
        Land(map, "E2落点", x + 1800, 300, x + 2250, 345);
        Land(map, "E2出口", x + 2250, 345, x + 2700, 300);
        Platform(map, x + 430, 295, 180);
        Platform(map, x + 710, 280, 150);
        map.Shortcuts.Add(new ActShortcut("E2 上方断桥", x + 430, x + 860));
        map.Hazards.Add(new ActHazard(ActHazardKind.Vent, x + 690, .5f, 370));
        Room(map, 5, "E2 沉岩栈道", x, x + 2700, RoomTag.Platform, ActRoute.Both,
            ActRewardKind.Heal,
            (ActEnemyKind.Melee, x + 500, 370),
            (ActEnemyKind.Flying, x + 1360, 250),
            (ActEnemyKind.Ranged, x + 2330, 337));
    }

    private static void LongDescent(ActLevelLayout map, float x)
    {
        Flat(map, "F1断脊入口", x, x + 350, 300);
        Land(map, "F1长下坡", x + 350, 300, x + 850, 375);
        Flat(map, "F1沉谷", x + 850, x + 1300, 375);
        Land(map, "F1大上坡", x + 1300, 375, x + 1750, 235);
        Flat(map, "F1高地", x + 1750, x + 2150, 235);
        Platform(map, x + 2155, 235, 125);
        Platform(map, x + 2295, 218, 130);
        Land(map, "F1桥后落点", x + 2350, 300, x + 2850, 350);
        Flat(map, "F1下层战场", x + 2850, x + 3250, 350);
        Land(map, "F1出口", x + 3250, 350, x + 3700, 300);
        map.Hazards.Add(new ActHazard(ActHazardKind.Vent, x + 1090, .5f, 375));
        Room(map, 6, "F1 长谷断桥", x, x + 3700, RoomTag.Challenge, ActRoute.Both,
            ActRewardKind.FullRangedEnergy,
            (ActEnemyKind.Melee, x + 950, 375),
            (ActEnemyKind.Ranged, x + 1900, 235),
            (ActEnemyKind.Flying, x + 2650, 330),
            (ActEnemyKind.Heavy, x + 3080, 350));
    }

    private static void TerracedApproach(ActLevelLayout map, float x)
    {
        Land(map, "F2长上坡", x, 300, x + 400, 220);
        Flat(map, "F2第一台地", x + 400, x + 750, 220);
        Land(map, "F2回落", x + 750, 220, x + 1250, 350);
        Flat(map, "F2下层", x + 1250, x + 1650, 350);
        Land(map, "F2断口落点", x + 1810, 350, x + 2250, 300);
        Flat(map, "F2中层", x + 2250, x + 2650, 300);
        Land(map, "F2第二爬升", x + 2650, 300, x + 3050, 245);
        Flat(map, "F2上层", x + 3050, x + 3300, 245);
        Land(map, "F2出口", x + 3300, 245, x + 3700, 300);
        map.Hazards.Add(new ActHazard(ActHazardKind.Rock, x + 1180, .8f,
            GroundAt(map, x + 1180)!.Value));
        Room(map, 6, "F2 梯田突进", x, x + 3700, RoomTag.Challenge, ActRoute.Both,
            ActRewardKind.FullRangedEnergy,
            (ActEnemyKind.Heavy, x + 530, 220),
            (ActEnemyKind.Melee, x + 1350, 350),
            (ActEnemyKind.Ranged, x + 2450, 300),
            (ActEnemyKind.Flying, x + 3150, 245));
    }
}
