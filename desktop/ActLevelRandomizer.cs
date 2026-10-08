using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;

namespace DinoDash.Desktop;

internal enum RoomTag { Combat, Platform, Hazard, Rest, Ambush, Challenge }
internal enum ActRoute { Both, Upper, Lower }
internal enum ActHazardKind { None, Spikes, Vent, Rock }
internal enum ActRewardKind { Heal, FullRangedEnergy, DamageBuff, MoveBuff, BattleArt, Evolution, Form }

internal sealed record EnemyGroup(string Id, ActEnemyKind[] Kinds)
{
    public int DifficultyCost => Kinds.Sum(kind => kind switch
    {
        ActEnemyKind.Ranged => 2, ActEnemyKind.Heavy => 3, _ => 1
    });
}

internal sealed record RoomVariant(
    string Id, RoomTag Tag, ActRoute Route,
    RectangleF[] Platforms, string[] GroupIds,
    ActHazardKind[] HazardOptions);

internal sealed record RoomNode(
    int Index, float StartX, float EndX, int MinBudget, int MaxBudget,
    RoomVariant[] Variants)
{
    public PointF EntrancePoint => new(StartX + 60f, ActTuning.GroundY);
    public PointF ExitPoint => new(EndX - 60f, ActTuning.GroundY);
    public RectangleF CameraBounds => new(StartX, 0f, EndX - StartX,
        ActTuning.ViewHeight);
    public PointF EnemySpawnRoot => new(StartX + (EndX - StartX) * .60f,
        ActTuning.GroundY);
    public PointF RewardPoint => new(EndX - 150f, ActTuning.GroundY - 28f);
}

internal sealed record RoomSelection(RoomNode Node, RoomVariant Variant,
    EnemyGroup? Group, ActHazardKind Hazard, ActRewardKind Reward)
{
    public bool Activated { get; set; } = Variant.Tag != RoomTag.Ambush;
    public string Summary => $"{Node.Index}:{Variant.Id}/{Group?.Id ?? "None"}/{Hazard}/{Reward}/{Variant.Route}";
}

internal sealed class ActHazard(ActHazardKind kind, float x, float phase,
    float surfaceY = ActTuning.GroundY)
{
    public ActHazardKind Kind { get; } = kind;
    public float X { get; } = x;
    public float Phase { get; } = phase;
    public float SurfaceY { get; } = surfaceY;
    public bool IsActive(float time) => Kind switch
    {
        ActHazardKind.Spikes => true,
        ActHazardKind.Vent => (time + Phase) % 2.6f < .95f,
        ActHazardKind.Rock => (time + Phase) % 3.4f < 1.55f,
        _ => false
    };
    public RectangleF Bounds(float time)
    {
        float cycle = Kind == ActHazardKind.Rock ? (time + Phase) % 3.4f : 0f;
        return Kind switch
        {
            ActHazardKind.Spikes => new(X, SurfaceY - 17f, 38f, 17f),
            ActHazardKind.Vent => new(X, SurfaceY - 66f, 30f, 66f),
            ActHazardKind.Rock => new(X, SurfaceY - 280f + cycle / 1.55f * 250f, 27f, 27f),
            _ => RectangleF.Empty
        };
    }
}

internal sealed class ActReward(ActRewardKind kind, float x, float y,
    int roomIndex = -1)
{
    public ActRewardKind Kind { get; } = kind;
    public float X { get; } = x;
    public float Y { get; } = y;
    public int RoomIndex { get; } = roomIndex;
    public bool Collected { get; set; }
    public float AvailableAfter { get; init; }
    public RectangleF Bounds => new(X - 13f, Y - 13f, 26f, 26f);
}

internal sealed record ActShortcut(string Id, float StartX, float EndX);

internal sealed class ActLevelLayout(int seed)
{
    public int Seed { get; } = seed;
    public List<RoomSelection> Rooms { get; } = [];
    public List<RectangleF> Solids { get; } = [];
    public List<ActEnemy> Enemies { get; } = [];
    public List<ActHazard> Hazards { get; } = [];
    public List<ActReward> Rewards { get; } = [];
    public List<ActShortcut> Shortcuts { get; } = [];
    public List<ActTerrainSegment> Terrain { get; } = [];
    public List<ActSupply> Supplies { get; } = [];
    public List<ActZone> Zones { get; } = [];
    public List<ActDynamicPlatform> MovingPlatforms { get; } = [];
    public List<ActMechanism> Mechanisms { get; } = [];
    public float Width { get; set; } = ActTuning.LevelWidth;
    public float Exit { get; set; } = ActTuning.LevelWidth - 125f;
    public float SpawnX { get; set; } = 90f;
    public float SpawnFeetY { get; set; } = ActTuning.GroundY;
    public float BossArenaLeft { get; set; } = 31600f;
    public float BossArenaRight { get; set; } = 34000f;
    public string Signature => $"{Seed}|{string.Join("|", Rooms.Select(room => room.Summary))}|" +
        string.Join(",", Shortcuts.Select(shortcut => shortcut.Id)) +
        (Terrain.Count > 0 ? "|" + string.Join(",", Terrain.Select(t => t.Id)) : "");
}

internal static class ActLevelRandomizer
{
    private static RectangleF Platform(float x, float y, float width) =>
        new(x, y, width, 14f);

    public static readonly IReadOnlyList<EnemyGroup> Groups =
    [
        new("双爪", [ActEnemyKind.Melee, ActEnemyKind.Melee]),
        new("空中掩护", [ActEnemyKind.Melee, ActEnemyKind.Flying]),
        new("远程掩护", [ActEnemyKind.Melee, ActEnemyKind.Ranged]),
        new("重型空袭", [ActEnemyKind.Heavy, ActEnemyKind.Flying]),
        new("混合小队", [ActEnemyKind.Melee, ActEnemyKind.Melee,
            ActEnemyKind.Ranged, ActEnemyKind.Flying]),
        new("重型射手", [ActEnemyKind.Heavy, ActEnemyKind.Ranged]),
        new("重装前线", [ActEnemyKind.Heavy, ActEnemyKind.Melee,
            ActEnemyKind.Melee, ActEnemyKind.Flying]),
        new("精英混合", [ActEnemyKind.Heavy, ActEnemyKind.Ranged,
            ActEnemyKind.Flying, ActEnemyKind.Melee])
    ];

    // Relative platforms are authored by hand. Every version shares its
    // node's entrance, exit, ground, camera span and reward socket.
    public static readonly IReadOnlyList<RoomNode> Nodes =
    [
        new(1, 3200f, 4300f, 2, 3,
        [
            new("A1", RoomTag.Combat, ActRoute.Lower, [],
                ["双爪", "远程掩护"], [ActHazardKind.None]),
            new("A2", RoomTag.Platform, ActRoute.Both,
                [Platform(260f, 222f, 190f), Platform(520f, 188f, 170f)],
                ["空中掩护", "双爪"], [ActHazardKind.None]),
            new("A3", RoomTag.Hazard, ActRoute.Lower,
                [Platform(250f, 225f, 145f)], ["双爪"],
                [ActHazardKind.Spikes, ActHazardKind.Vent])
        ]),
        new(2, 6100f, 8300f, 2, 3,
        [
            new("B1", RoomTag.Platform, ActRoute.Both,
                [Platform(350f, 220f, 220f), Platform(660f, 190f, 210f),
                    Platform(980f, 220f, 210f)],
                ["空中掩护", "远程掩护"],
                [ActHazardKind.None, ActHazardKind.Rock]),
            new("B2", RoomTag.Combat, ActRoute.Upper,
                [Platform(330f, 225f, 190f), Platform(520f, 175f, 210f),
                    Platform(700f, 145f, 380f)],
                ["双爪", "空中掩护"], [ActHazardKind.None]),
            new("B3", RoomTag.Rest, ActRoute.Lower,
                [Platform(500f, 230f, 200f)], [],
                [ActHazardKind.None])
        ]),
        new(3, 17500f, 19000f, 4, 5,
        [
            new("C1", RoomTag.Combat, ActRoute.Lower, [],
                ["混合小队", "重型射手"], [ActHazardKind.None]),
            new("C2", RoomTag.Platform, ActRoute.Both,
                [Platform(330f, 225f, 190f), Platform(590f, 185f, 220f)],
                ["混合小队", "重型空袭"],
                [ActHazardKind.None, ActHazardKind.Rock]),
            new("C3", RoomTag.Ambush, ActRoute.Lower,
                [Platform(280f, 228f, 160f)],
                ["重型空袭", "重型射手"],
                [ActHazardKind.Spikes, ActHazardKind.Vent])
        ]),
        new(4, 24000f, 26000f, 6, 7,
        [
            new("D1", RoomTag.Challenge, ActRoute.Lower, [],
                ["重装前线", "精英混合"], [ActHazardKind.None]),
            new("D2", RoomTag.Platform, ActRoute.Both,
                [Platform(350f, 225f, 190f), Platform(620f, 185f, 220f),
                    Platform(970f, 220f, 170f)],
                ["重装前线", "精英混合"], [ActHazardKind.None]),
            new("D3", RoomTag.Hazard, ActRoute.Lower,
                [Platform(380f, 220f, 180f)],
                ["重装前线", "精英混合"],
                [ActHazardKind.Spikes, ActHazardKind.Vent, ActHazardKind.Rock])
        ])
    ];

    // Integer weights can be tuned without touching the selection logic.
    public static readonly IReadOnlyList<(ActRewardKind Kind, int Weight)> RewardWeights =
    [
        (ActRewardKind.Heal, 40),
        (ActRewardKind.FullRangedEnergy, 30),
        (ActRewardKind.DamageBuff, 20),
        (ActRewardKind.MoveBuff, 10)
    ];

    public static int NewSeed() => RandomNumberGenerator.GetInt32(1, int.MaxValue);

    public static ActLevelLayout Generate(int seed)
    {
        var random = new Random(seed);
        var layout = new ActLevelLayout(seed);
        int combatStreak = 0;
        int lastRest = -10;
        var usedVersionIds = new HashSet<string>();
        foreach (RoomNode node in Nodes)
        {
            RoomVariant[] choices = node.Variants.Where(variant =>
                !usedVersionIds.Contains(variant.Id) &&
                !(combatStreak >= 2 && IsCombat(variant.Tag)) &&
                !(variant.Tag == RoomTag.Rest && node.Index - lastRest < 2)).ToArray();
            if (choices.Length == 0) throw new InvalidOperationException(
                $"No valid room at node {node.Index}.");
            RoomVariant version = choices[random.Next(choices.Length)];
            usedVersionIds.Add(version.Id);
            combatStreak = IsCombat(version.Tag) ? combatStreak + 1 : 0;
            if (version.Tag == RoomTag.Rest) lastRest = node.Index;

            EnemyGroup? group = null;
            if (version.GroupIds.Length > 0)
            {
                EnemyGroup[] groupChoices = Groups.Where(candidate =>
                    version.GroupIds.Contains(candidate.Id) &&
                    candidate.DifficultyCost >= node.MinBudget &&
                    candidate.DifficultyCost <= node.MaxBudget).ToArray();
                if (groupChoices.Length == 0) throw new InvalidOperationException(
                    $"No enemy group fits {version.Id} budget.");
                group = groupChoices[random.Next(groupChoices.Length)];
            }
            ActHazardKind hazard = version.HazardOptions[
                random.Next(version.HazardOptions.Length)];
            ActRewardKind reward = version.Tag == RoomTag.Rest
                ? ActRewardKind.Heal : WeightedReward(random);
            if (node.Index == 1 ||
                (reward is ActRewardKind.DamageBuff or ActRewardKind.MoveBuff or
                    ActRewardKind.FullRangedEnergy && random.NextDouble() < .65))
                reward = ActRewardKind.BattleArt;
            var selection = new RoomSelection(node, version, group, hazard, reward);
            layout.Rooms.Add(selection);
            AddRoomContent(layout, selection, random);
        }

        // Both shortcuts are optional; the authored ground route remains intact.
        if (layout.Rooms[1].Variant.Route != ActRoute.Upper &&
            random.NextDouble() < .48)
            AddShortcut(layout, "石桥", 7320f, 8070f, random);
        if (random.NextDouble() < .38) AddShortcut(layout, "悬崖栈道", 14680f,
            15450f, random);
        Trace.WriteLine($"ACT MAP Seed={seed} {layout.Signature}");
        return layout;
    }

    private static bool IsCombat(RoomTag tag) =>
        tag is RoomTag.Combat or RoomTag.Ambush or RoomTag.Challenge;

    private static ActRewardKind WeightedReward(Random random)
    {
        int roll = random.Next(RewardWeights.Sum(entry => entry.Weight));
        foreach (var entry in RewardWeights)
        {
            roll -= entry.Weight;
            if (roll < 0) return entry.Kind;
        }
        throw new InvalidOperationException("Reward weights must be positive.");
    }

    private static void AddRoomContent(ActLevelLayout layout,
        RoomSelection selection, Random random)
    {
        RoomNode node = selection.Node;
        foreach (RectangleF relative in selection.Variant.Platforms)
            layout.Solids.Add(new RectangleF(node.StartX + relative.X,
                relative.Y, relative.Width, relative.Height));
        if (selection.Variant.Route == ActRoute.Upper)
            layout.Solids.Add(new RectangleF(node.StartX + 880f, 160f, 90f, 140f));
        if (selection.Variant.Route == ActRoute.Lower && node.Index == 2)
            layout.Solids.Add(new RectangleF(node.StartX + 780f, 245f, 55f, 55f));

        if (selection.Group is { } group)
        {
            for (int i = 0; i < group.Kinds.Length; i++)
            {
                float x = node.EnemySpawnRoot.X + i * 115f;
                ActEnemy enemy = ActLevel.Make(group.Kinds[i], x);
                enemy.Dormant = selection.Variant.Tag == RoomTag.Ambush;
                layout.Enemies.Add(enemy);
            }
        }
        if (selection.Hazard != ActHazardKind.None)
        {
            // Hazard sockets are away from the shared doorway and enemy roots.
            layout.Hazards.Add(new ActHazard(selection.Hazard,
                node.StartX + (node.EndX - node.StartX) * .31f,
                (float)random.NextDouble() * 2f));
            if (node.Index >= 3)
                layout.Hazards.Add(new ActHazard(selection.Hazard,
                    node.StartX + (node.EndX - node.StartX) * .43f,
                    (float)random.NextDouble() * 2f));
        }
        layout.Rewards.Add(new ActReward(selection.Reward,
            node.RewardPoint.X, node.RewardPoint.Y, node.Index));
    }

    private static void AddShortcut(ActLevelLayout layout, string id,
        float start, float end, Random random)
    {
        layout.Shortcuts.Add(new ActShortcut(id, start, end));
        layout.Solids.Add(Platform(start, 225f, 185f));
        layout.Solids.Add(Platform(start + 175f, 190f, end - start - 250f));
        layout.Solids.Add(Platform(end - 125f, 225f, 185f));
        // The higher path offers a bonus but exposes the player to falling rock.
        layout.Hazards.Add(new ActHazard(ActHazardKind.Rock,
            start + (end - start) * .55f, (float)random.NextDouble()));
        layout.Rewards.Add(new ActReward(ActRewardKind.BattleArt,
            end - 185f, 157f));
    }
}
