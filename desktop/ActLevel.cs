using System.Drawing;

namespace DinoDash.Desktop;

internal sealed record ActZone(float X, string Name);
internal sealed class ActSupply(float x, float y = ActTuning.GroundY - 28f,
    string name = "林间补给处")
{
    public float X { get; } = x;
    public float Y { get; } = y;
    public string Name { get; } = name;
    public float Left => X - 260f;
    public float Right => X + 260f;
    public bool Contains(float x) => x >= Left && x <= Right;
    public bool Collected { get; set; }
    public RectangleF Bounds => new(X - 12f, Y - 12f, 24f, 24f);
}

// Fixed landmarks and encounters remain the spine of every run. The four
// interchangeable interiors are assembled separately by ActLevelRandomizer.
internal static class ActLevel
{
    public static readonly IReadOnlyList<ActZone> Zones =
    [
        new(0f, "晨雾湿地"), new(3000f, "芦苇沼泽"), new(6000f, "沉木栈道"),
        new(9000f, "迷雾林地"), new(14000f, "风蚀高地"),
        new(17500f, "古木荒原"), new(20500f, "碎岩谷地"),
        new(24000f, "失落遗迹"), new(31000f, "巨兽巢穴"),
        new(34500f, "曙光隘口")
    ];

    public static List<ActSupply> CreateSupplies() =>
    [new(8650f, name: "苇间营地"), new(16950f, name: "林荫补给处"),
        new(23400f, name: "石溪营地"), new(30700f, name: "巢穴前哨")];

    public static List<RectangleF> CreateSolids()
    {
        List<RectangleF> solids =
        [
            new(0f, ActTuning.GroundY, ActTuning.LevelWidth, 60f),
            new(450f, 222f, 175f, 14f), new(730f, 190f, 180f, 14f),
            new(1010f, 225f, 155f, 14f), new(1860f, 214f, 190f, 14f),
            new(2130f, 190f, 160f, 14f)
        ];
        // Platform islands alternate with open arenas. They are deliberately
        // shorter than a full jump and leave a ground route below.
        foreach (float start in new[] { 14000f, 16300f,
            22700f, 27400f, 30500f })
        {
            solids.Add(new RectangleF(start, 232f, 170f, 14f));
            solids.Add(new RectangleF(start + 250f, 178f, 155f, 14f));
            solids.Add(new RectangleF(start + 490f, 218f, 175f, 14f));
        }
        return solids;
    }

    public static List<ActEnemy> CreateEnemies() =>
    [
        Make(ActEnemyKind.Melee, 1560f),
        Make(ActEnemyKind.Ranged, 9650f),
        Make(ActEnemyKind.Melee, 11900f), Make(ActEnemyKind.Ranged, 12200f),
        Make(ActEnemyKind.Flying, 14900f),
        Make(ActEnemyKind.Heavy, 20800f), Make(ActEnemyKind.Ranged, 21100f),
        Make(ActEnemyKind.Heavy, 28600f), Make(ActEnemyKind.Ranged, 28900f),
        Make(ActEnemyKind.Boss, 32700f)
    ];

    internal static ActEnemy Make(ActEnemyKind kind, float x)
    {
        float y = kind switch
        {
            ActEnemyKind.Flying => 155f,
            ActEnemyKind.Heavy => ActTuning.GroundY - 54f,
            ActEnemyKind.Boss => ActTuning.GroundY - 100f,
            _ => ActTuning.GroundY - 42f
        };
        int hp = kind switch
        {
            ActEnemyKind.Flying => 2, ActEnemyKind.Heavy => 7,
            ActEnemyKind.Boss => 24, _ => 3
        };
        return new ActEnemy { Kind = kind, X = x, Y = y, HomeX = x,
            HomeY = y, Health = hp, MaxHealth = hp };
    }
}
