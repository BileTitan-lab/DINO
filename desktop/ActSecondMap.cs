using System.Diagnostics;
using System.Drawing;

namespace DinoDash.Desktop;

// A walkable, authored line segment. A missing segment is a real opening, not
// a decorative hole in an otherwise continuous ground collider.
internal sealed record ActTerrainSegment(string Id, float Left, float Right,
    float LeftY, float RightY)
{
    public float YAt(float x) => LeftY + (RightY - LeftY) *
        Math.Clamp((x - Left) / (Right - Left), 0f, 1f);
}

internal static partial class ActSecondMap
{
    private static void Land(ActLevelLayout map, string id, float x0, float y0,
        float x1, float y1) => map.Terrain.Add(new(id, x0, x1, y0, y1));
    private static void Flat(ActLevelLayout map, string id, float x0, float x1,
        float y) => Land(map, id, x0, y, x1, y);
    private static void Platform(ActLevelLayout map, float x, float y, float width) =>
        map.Solids.Add(new RectangleF(x, y, width, 14f));

    public static float? GroundAt(ActLevelLayout map, float x)
    {
        foreach (ActTerrainSegment piece in map.Terrain)
            if (x >= piece.Left && x < piece.Right) return piece.YAt(x);
        return map.Terrain.Count > 0 && x == map.Terrain[^1].Right
            ? map.Terrain[^1].RightY : null;
    }

    private static ActEnemy Enemy(ActEnemyKind kind, float x, float footY)
    {
        ActEnemy template = ActLevel.Make(kind, x);
        float y = kind == ActEnemyKind.Flying ? footY - 145f : footY - template.Height;
        return new ActEnemy { Kind = kind, X = x, HomeX = x, Y = y, HomeY = y,
            Health = template.Health, MaxHealth = template.MaxHealth };
    }

    private static void Room(ActLevelLayout map, int index, string id, float start,
        float end, RoomTag tag, ActRoute route, ActRewardKind reward,
        params (ActEnemyKind Kind, float X, float Feet)[] encounters)
    {
        var node = new RoomNode(index, start, end, 0, 9, []);
        var variant = new RoomVariant(id, tag, route, [], [], []);
        EnemyGroup group = new(id, encounters.Select(e => e.Kind).ToArray());
        map.Rooms.Add(new RoomSelection(node, variant, group, ActHazardKind.None, reward));
        foreach (var encounter in encounters)
            map.Enemies.Add(Enemy(encounter.Kind, encounter.X, encounter.Feet));
        float x = end - 100f;
        float y = GroundAt(map, x) ?? 300f;
        map.Rewards.Add(new ActReward(reward, x, y - 28f, index));
    }

    public static ActLevelLayout Generate(int seed)
    {
        var random = new Random(seed);
        var map = new ActLevelLayout(seed)
        {
            Width = 8200f, Exit = 8075f, SpawnX = 90f, SpawnFeetY = 300f,
            BossArenaLeft = 7030f, BossArenaRight = 7970f
        };
        map.Zones.AddRange([
            new(0f, "风蚀峡谷"), new(1120f, "裂脊高地"),
            new(2900f, "低谷回声"), new(3650f, "沉岩断层"),
            new(5600f, "回旋石道"), new(6950f, "古兽巢穴")
        ]);

        // Shared interfaces: each module begins and ends at y=300.
        Flat(map, "入口", 0, 300, 300);
        Land(map, "入口上坡", 300, 300, 660, 225);
        Flat(map, "坡顶", 660, 920, 225);
        Land(map, "入口下坡", 920, 225, 1120, 300);
        map.Enemies.Add(Enemy(ActEnemyKind.Ranged, 770, 225));
        map.Enemies.Add(Enemy(ActEnemyKind.Melee, 1000,
            GroundAt(map, 1018)!.Value));

        if (random.Next(2) == 0) Ridge(map);
        else SplitValley(map);

        Flat(map, "中段入口", 2900, 3100, 300);
        Land(map, "长下坡", 3100, 300, 3340, 365);
        Flat(map, "可进入低谷", 3340, 3510, 365);
        Land(map, "低谷回升", 3510, 365, 3650, 300);
        map.Enemies.Add(Enemy(ActEnemyKind.Melee, 3390, 365));

        if (random.Next(2) == 0) Cave(map);
        else BrokenRidge(map);

        if (random.Next(2) == 0) HighSwitchback(map);
        else LowQuarry(map);

        Flat(map, "首领前缓冲", 6950, 8200, 300);
        map.Enemies.Add(Enemy(ActEnemyKind.Boss, 7480, 300));
        InsertSupplyClearings(map);
        AddExtendedTrail(map, random);
        // ActEngine.Boss uses the final enemy slot, including the final gate
        // and evolution drop. Expansion encounters must stay before the boss.
        ActEnemy boss = map.Enemies.Single(enemy => enemy.Kind == ActEnemyKind.Boss);
        map.Enemies.Remove(boss);
        map.Enemies.Add(boss);
        map.Terrain.Sort((a, b) => a.Left.CompareTo(b.Left));
        map.Zones.Sort((a, b) => a.X.CompareTo(b.X));
        Trace.WriteLine($"ACT MAP 2 Seed={seed} {map.Signature}");
        return map;
    }

    // Insert authored interludes at shared module seams; each existing combat
    // module is translated intact, retaining jump distances and enemy sockets.
    private static readonly float[] CampSeams = [2900f, 5600f, 6950f];
    private const float ClearingLength = 900f;
    private const float ExpansionLength = 10900f;
    internal static float WorldX(float authoredX, bool endPoint = false) =>
        authoredX + CampSeams.Count(seam => endPoint ? seam < authoredX : seam <= authoredX)
        * ClearingLength + ((endPoint ? authoredX > 6950f : authoredX >= 6950f)
            ? ExpansionLength : 0f);

    private static void InsertSupplyClearings(ActLevelLayout map)
    {
        for (int i = 0; i < map.Terrain.Count; i++)
        {
            var t = map.Terrain[i];
            map.Terrain[i] = t with { Left = WorldX(t.Left), Right = WorldX(t.Right, true) };
        }
        for (int i = 0; i < map.Solids.Count; i++)
        {
            var s = map.Solids[i];
            map.Solids[i] = new RectangleF(WorldX(s.X), s.Y, s.Width, s.Height);
        }
        for (int i = 0; i < map.Enemies.Count; i++)
        {
            var e = map.Enemies[i];
            map.Enemies[i] = new ActEnemy { Kind = e.Kind, X = WorldX(e.X),
                HomeX = WorldX(e.HomeX), Y = e.Y, HomeY = e.HomeY,
                Health = e.Health, MaxHealth = e.MaxHealth };
        }
        for (int i = 0; i < map.Rooms.Count; i++)
        {
            var r = map.Rooms[i];
            map.Rooms[i] = r with { Node = r.Node with {
                StartX = WorldX(r.Node.StartX), EndX = WorldX(r.Node.EndX, true) } };
        }
        for (int i = 0; i < map.Rewards.Count; i++)
        {
            var r = map.Rewards[i];
            map.Rewards[i] = new ActReward(r.Kind, WorldX(r.X), r.Y, r.RoomIndex);
        }
        for (int i = 0; i < map.Hazards.Count; i++)
        {
            var h = map.Hazards[i];
            map.Hazards[i] = new ActHazard(h.Kind, WorldX(h.X), h.Phase, h.SurfaceY);
        }
        for (int i = 0; i < map.Shortcuts.Count; i++)
        {
            var s = map.Shortcuts[i];
            map.Shortcuts[i] = s with { StartX = WorldX(s.StartX), EndX = WorldX(s.EndX, true) };
        }
        for (int i = 0; i < map.Zones.Count; i++)
            map.Zones[i] = map.Zones[i] with { X = WorldX(map.Zones[i].X) };
        string[] names = ["避风营地", "岩荫补给处", "裂谷前哨"];
        for (int i = 0; i < CampSeams.Length; i++)
            AddCamp(map, WorldX(CampSeams[i], true), names[i]);
        map.Terrain.Sort((a, b) => a.Left.CompareTo(b.Left));
        map.Zones.Sort((a, b) => a.X.CompareTo(b.X));
        map.Width = WorldX(map.Width);
        map.Exit = WorldX(map.Exit);
        map.BossArenaLeft = WorldX(map.BossArenaLeft);
        map.BossArenaRight = WorldX(map.BossArenaRight);
    }

    private static void AddCamp(ActLevelLayout map, float x, string name)
    {
        Flat(map, name, x, x + 900, 300);
        map.Supplies.Add(new ActSupply(x + 450, 272, name));
        map.Zones.Add(new ActZone(x, name));
    }

    private static void Ridge(ActLevelLayout map)
    {
        Land(map, "A1山坡", 1120, 300, 1380, 245);
        Flat(map, "A1山顶", 1380, 1720, 245);
        // 160 px leap over a deep crevasse; a base jump clears it.
        Land(map, "A1断崖下降", 1880, 245, 2110, 320);
        Flat(map, "A1谷底", 2110, 2520, 320);
        Land(map, "A1回升", 2520, 320, 2900, 300);
        Room(map, 1, "A1 山顶攻防", 1120, 2900, RoomTag.Combat, ActRoute.Both,
            ActRewardKind.BattleArt,
            (ActEnemyKind.Heavy, 1470, 245), (ActEnemyKind.Ranged, 2230, 320),
            (ActEnemyKind.Flying, 2480, 320));
    }

    private static void SplitValley(ActLevelLayout map)
    {
        Land(map, "A2下坡", 1120, 300, 1440, 330);
        Flat(map, "A2上层边缘", 1440, 1570, 330);
        Flat(map, "A2下层战场", 1570, 2180, 385);
        Land(map, "A2坡道出口", 2180, 385, 2500, 300);
        Flat(map, "A2出口", 2500, 2900, 300);
        Platform(map, 1660, 275, 160);
        Platform(map, 1740, 275, 180);
        Platform(map, 1940, 301, 190);
        Platform(map, 2145, 315, 200);
        map.Shortcuts.Add(new ActShortcut("A2 上层栈道", 1660, 2345));
        Room(map, 1, "A2 双层裂谷", 1120, 2900, RoomTag.Platform, ActRoute.Both,
            ActRewardKind.BattleArt,
            (ActEnemyKind.Ranged, 1800, 275), (ActEnemyKind.Melee, 2000, 385),
            (ActEnemyKind.Flying, 2280, 365));
    }

    private static void Cave(ActLevelLayout map)
    {
        Land(map, "B1洞口上坡", 3650, 300, 3910, 260);
        Flat(map, "B1前厅", 3910, 4160, 260);
        Land(map, "B1裂隙落点", 4320, 260, 4500, 315);
        Flat(map, "B1洞穴", 4500, 4800, 315);
        Land(map, "B1出洞上坡", 4800, 315, 5100, 270);
        Flat(map, "B1高台", 5100, 5350, 270);
        Land(map, "B1出口", 5350, 270, 5600, 300);
        // The 89 px crawl space changes attacks without trapping the 52 px player.
        map.Solids.Add(new RectangleF(4480, 165, 350, 61));
        Room(map, 2, "B1 沉岩洞穴", 3650, 5600, RoomTag.Combat, ActRoute.Lower,
            ActRewardKind.Heal,
            (ActEnemyKind.Melee, 4570, 315), (ActEnemyKind.Melee, 4730, 315),
            (ActEnemyKind.Ranged, 5160, 270));
    }

    private static void BrokenRidge(ActLevelLayout map)
    {
        Land(map, "B2断脊上坡", 3650, 300, 3900, 245);
        Flat(map, "B2崖端", 3900, 4160, 245);
        // The 410 px crevasse is crossed by three authored stepping stones.
        Platform(map, 4155, 245, 135);
        Platform(map, 4305, 216, 135);
        Platform(map, 4460, 255, 120);
        Land(map, "B2落点", 4570, 300, 4800, 330);
        Flat(map, "B2碎石滩", 4800, 5200, 330);
        Land(map, "B2回程", 5200, 330, 5600, 300);
        Room(map, 2, "B2 断脊石桥", 3650, 5600, RoomTag.Platform, ActRoute.Upper,
            ActRewardKind.Heal,
            (ActEnemyKind.Flying, 4370, 245), (ActEnemyKind.Ranged, 4940, 330),
            (ActEnemyKind.Melee, 5240, 327));
    }

    private static void HighSwitchback(ActLevelLayout map)
    {
        Land(map, "C1斜坡", 5600, 300, 5830, 220);
        Flat(map, "C1脊背", 5830, 6140, 220);
        Land(map, "C1崖后下坡", 6300, 220, 6540, 310);
        Flat(map, "C1洼地", 6540, 6750, 310);
        Land(map, "C1出口", 6750, 310, 6950, 300);
        map.Hazards.Add(new ActHazard(ActHazardKind.Rock, 6030, .4f, 220));
        Room(map, 3, "C1 回旋高地", 5600, 6950, RoomTag.Challenge, ActRoute.Both,
            ActRewardKind.FullRangedEnergy,
            (ActEnemyKind.Heavy, 5910, 220), (ActEnemyKind.Flying, 6420, 270),
            (ActEnemyKind.Ranged, 6640, 310));
    }

    private static void LowQuarry(ActLevelLayout map)
    {
        Land(map, "C2深坡", 5600, 300, 5850, 375);
        Flat(map, "C2采石场", 5850, 6140, 375);
        Land(map, "C2跃出斜坡", 6300, 375, 6570, 285);
        Flat(map, "C2高地", 6570, 6780, 285);
        Land(map, "C2出口", 6780, 285, 6950, 300);
        map.Hazards.Add(new ActHazard(ActHazardKind.Vent, 6000, .25f, 375));
        Room(map, 3, "C2 下层采石场", 5600, 6950, RoomTag.Challenge, ActRoute.Lower,
            ActRewardKind.FullRangedEnergy,
            (ActEnemyKind.Melee, 5910, 375), (ActEnemyKind.Flying, 6470, 310),
            (ActEnemyKind.Ranged, 6630, 285));
    }
}

internal sealed partial class ActEngine
{
    private void RememberSafeFooting()
    {
        float center = PlayerX + PlayerWidth / 2f;
        float? here = TerrainYAt(center);
        if (here is not float y || Math.Abs(PlayerY + PlayerHeight - y) > 3f ||
            TerrainYAt(center - 75f) is null || TerrainYAt(center + 75f) is null)
            return;
        safeX = PlayerX;
        safeFeetY = y;
    }

    private void RescueFromPit()
    {
        HurtPlayer();
        PlayerX = safeX;
        PlayerY = safeFeetY - PlayerHeight;
        VelocityX = VelocityY = 0f;
        Grounded = true;
        jumpBuffer = coyoteTime = 0f;
    }

    private void UpdateCameraHeight(float dt)
    {
        float feet = PlayerY + PlayerHeight;
        float lead = PlayerX + PlayerWidth / 2f + Facing * 170f;
        float? coming = TerrainYAt(lead);
        if (coming is null) coming = TerrainYAt(lead + Facing * 70f);
        float preview = coming is float y ? Math.Clamp((y - feet) * .24f, -28f, 28f) : 18f;
        float target = feet - (MapIndex >= 3 ? 240f : ActTuning.GroundY) + preview;
        if (MapIndex >= 3)
        {
            // Expose the pool before takeoff, without moving the player out of
            // the combat view. Never jump the camera when entering a new span.
            foreach (var lava in layout.Mechanisms.Where(m => m.Kind == ActMechanismKind.Lava &&
                lead > m.X - 100 && lead < m.X + m.Width + 100))
                target = Math.Max(target, Math.Min(feet - 135, lava.Y - ActTuning.ViewHeight + 40));
        }
        target = Math.Clamp(target, MapIndex >= 3 ? -270f : -150f, MapIndex >= 3 ? 240f : 170f);
        cameraY = Approach(cameraY, target, 240f * dt);
    }

    private bool TerrainBlocks(RectangleF bounds) => UsesTerrain &&
        TerrainYAt(bounds.X + bounds.Width / 2f) is float surface &&
        bounds.Bottom >= surface;

    private void FollowTerrain(ActEnemy enemy, float dt)
    {
        if (!UsesTerrain || enemy.BossAirborne || enemy.Kind == ActEnemyKind.Flying ||
            enemy.State == ActEnemyState.Dead) return;
        float center = enemy.X + enemy.Width / 2f;
        float foot = enemy.Y + enemy.Height;
        float? floor = TerrainYAt(center);
        foreach (RectangleF solid in solids)
            if (center >= solid.Left && center < solid.Right &&
                solid.Top >= foot - 7f && (floor is null || solid.Top < floor))
                floor = solid.Top;
        if (floor is float height && Math.Abs(height - foot) <= 8f)
        {
            enemy.Y = height - enemy.Height;
            enemy.VerticalSpeed = 0f;
        }
        else
        {
            enemy.VerticalSpeed += ActTuning.Gravity * dt;
            float nextFoot = foot + enemy.VerticalSpeed * dt;
            if (floor is float landing && foot <= landing + 2f && nextFoot >= landing)
            {
                enemy.Y = landing - enemy.Height;
                enemy.VerticalSpeed = 0f;
            }
            else enemy.Y += enemy.VerticalSpeed * dt;
        }
        if (enemy.Y > (MapIndex >= 3 ? 720f : 500f))
        {
            enemy.X = enemy.HomeX;
            enemy.Y = enemy.HomeY;
            enemy.VerticalSpeed = 0f;
            enemy.State = ActEnemyState.Patrol;
            enemy.StateTime = 0f;
        }
    }
}
