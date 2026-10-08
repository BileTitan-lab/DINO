using System.Drawing;

namespace DinoDash.Desktop;

internal enum ActBossId { Tyrant, Stoneback, CanopyReaper, CinderKing }
internal enum BossHazardKind { FallingRock, Roots, Flame }

internal sealed record ActBossDefinition(string Name, string Title, int Health,
    float Windup, float Recovery, float ThinkTime, float PhaseTwoTempo, BossMove[] Moves);

// Chapter progression lives here: damage remains one HP; pressure comes from patterns
// and shorter (but still readable) recoveries. Positions always use authored arenas.
internal static class ActBossCatalog
{
    public static readonly ActBossDefinition[] Definitions =
    [
        new("暴君", "苇海霸主", 24, .9f, 1.05f, .48f, .8f,
            [BossMove.Charge, BossMove.Tail, BossMove.Quake]),
        new("岩脊", "裂谷甲兽", 28, .95f, 1.1f, .42f, .9f,
            [BossMove.Roll, BossMove.Club, BossMove.Rockfall]),
        new("羽镰", "树冠猎主", 32, .82f, .95f, .36f, .88f,
            [BossMove.Pounce, BossMove.Feathers, BossMove.RootBurst]),
        new("烬冠", "熔炉之王", 36, .8f, .85f, .3f, .86f,
            [BossMove.FlameFan, BossMove.Fissure, BossMove.MeteorLeap])
    ];
    public const float RollSpeed = 410, RollDuration = .75f, ClubReach = 175;
    public const float LeapDuration = .86f, CanopyLeapHeight = 160, MagmaLeapHeight = 130;
    public const float FeatherSpeed = 285, FlameSpeed = 260;
    public const float RockWarning = 1.15f, RootWarning = .9f, FlameWarning = .95f;
    public static ActBossDefinition Get(ActEnemy enemy) => Definitions[(int)enemy.BossId];
    public static float Tempo(ActEnemy enemy) => enemy.Health * 2 <= enemy.MaxHealth ? Get(enemy).PhaseTwoTempo : 1f;
    public static bool IsLeap(BossMove move) => move is BossMove.Pounce or BossMove.MeteorLeap;
    public static void Configure(ActEnemy enemy, int chapter)
    {
        enemy.BossId = (ActBossId)Math.Clamp(chapter - 1, 0, 3);
        enemy.Health = enemy.MaxHealth = Get(enemy).Health;
    }
}

internal sealed class BossHazard
{
    public required ActEnemy Source;
    public BossHazardKind Kind;
    public RectangleF Bounds;
    public float Time, Warning, Duration;
    public bool Active => Time >= Warning;
    public float Progress => Math.Clamp(Time / Warning, 0, 1);
    public RectangleF DamageBounds
    {
        get
        {
            float p = Math.Clamp((Time - Warning) / Duration, 0, 1);
            if (Kind == BossHazardKind.FallingRock)
                return new(Bounds.X, Bounds.Top + Math.Min(1, p * 1.6f) * (Bounds.Height - 26), Bounds.Width, 26);
            float height = Bounds.Height * Math.Min(1, p * 6);
            return new(Bounds.X, Bounds.Bottom - height, Bounds.Width, height);
        }
    }
}

internal sealed partial class ActEngine
{
    private readonly List<BossHazard> bossHazards = [];
    public IReadOnlyList<BossHazard> BossHazards => bossHazards;

    private void UpdateChapterBoss(ActEnemy boss, float dt, float dx)
    {
        var def = ActBossCatalog.Get(boss);
        float tempo = ActBossCatalog.Tempo(boss);
        switch (boss.State)
        {
            case ActEnemyState.Patrol:
                if (Math.Abs(dx) < 730) ChangeEnemyState(boss, ActEnemyState.Chase);
                break;
            case ActEnemyState.Chase:
                FacePlayer(boss, dx);
                if (boss.StateTime < def.ThinkTime * tempo) break;
                boss.BossMove = def.Moves[boss.AttackCount % def.Moves.Length];
                boss.AttackCount++;
                boss.VolleyCount = 0;
                boss.AttackStartX = boss.X;
                boss.AttackStartY = boss.Y;
                boss.TargetX = Math.Clamp(PlayerX + PlayerWidth / 2 - boss.Width / 2,
                    layout.BossArenaLeft + 15, layout.BossArenaRight - boss.Width - 15);
                boss.TargetY = (TerrainYAt(boss.TargetX + boss.Width / 2) ?? ActTuning.GroundY) - boss.Height;
                boss.BossAimY = PlayerY + PlayerHeight / 2;
                if (ActBossCatalog.IsLeap(boss.BossMove))
                {
                    // Like the player, the hunter can rise through one-way branches
                    // but lands on their top. Keep the entire body supported.
                    var landing = solids.Where(s => s.Height <= 16 && s.Width >= boss.Width &&
                        s.Right > boss.TargetX && s.Left < boss.TargetX + boss.Width &&
                        s.Top <= boss.TargetY + boss.Height).OrderBy(s => s.Top).FirstOrDefault();
                    if (landing.Width > 0)
                    {
                        boss.TargetX = Math.Clamp(boss.TargetX, landing.Left, landing.Right - boss.Width);
                        boss.TargetY = landing.Top - boss.Height;
                    }
                }
                ChangeEnemyState(boss, ActEnemyState.Windup);
                break;
            case ActEnemyState.Windup:
                if (boss.StateTime >= def.Windup * tempo)
                {
                    ChangeEnemyState(boss, ActEnemyState.Attack);
                    boss.BossAirborne = ActBossCatalog.IsLeap(boss.BossMove);
                }
                break;
            case ActEnemyState.Attack:
                UpdateChapterBossAttack(boss, dt, tempo);
                break;
            case ActEnemyState.Recover:
                if (boss.StateTime >= def.Recovery * tempo) ChangeEnemyState(boss, ActEnemyState.Chase);
                break;
        }
    }

    private void UpdateChapterBossAttack(ActEnemy boss, float dt, float tempo)
    {
        bool phase2 = boss.Health * 2 <= boss.MaxHealth;
        float t = boss.StateTime;
        switch (boss.BossMove)
        {
            case BossMove.Roll:
                boss.X = Math.Clamp(boss.X + boss.Facing * ActBossCatalog.RollSpeed * dt,
                    layout.BossArenaLeft, layout.BossArenaRight - boss.Width);
                if (boss.Bounds.IntersectsWith(PlayerBounds)) HurtPlayer(boss);
                if (boss.State is ActEnemyState.Dazed or ActEnemyState.Dead) break;
                if (t >= ActBossCatalog.RollDuration || boss.X <= layout.BossArenaLeft || boss.X >= layout.BossArenaRight - boss.Width)
                    ChangeEnemyState(boss, ActEnemyState.Recover);
                break;
            case BossMove.Club:
                if (!boss.AttackApplied && t >= .24f)
                {
                    boss.AttackApplied = true;
                    if (ClubBounds(boss).IntersectsWith(PlayerBounds)) HurtPlayer(boss);
                    if (boss.State is ActEnemyState.Dazed or ActEnemyState.Dead) break;
                    AddImpact(boss.X + boss.Width / 2 + boss.Facing * 120, boss.Y + boss.Height - 12, true);
                }
                if (t >= .58f) ChangeEnemyState(boss, ActEnemyState.Recover);
                break;
            case BossMove.Rockfall:
                if (!boss.AttackApplied)
                {
                    boss.AttackApplied = true;
                    int count = phase2 ? 4 : 3;
                    for (int i = 0; i < count; i++)
                        AddBossHazard(boss, BossHazardKind.FallingRock,
                            boss.TargetX + boss.Width / 2 + (i - (count - 1) / 2f) * 125,
                            48, 155, ActBossCatalog.RockWarning + i * .22f, .23f);
                }
                if (t >= .65f) ChangeEnemyState(boss, ActEnemyState.Recover);
                break;
            case BossMove.Pounce:
            case BossMove.MeteorLeap:
                float p = Math.Clamp(t / ActBossCatalog.LeapDuration, 0, 1);
                float h = boss.BossMove == BossMove.Pounce ? ActBossCatalog.CanopyLeapHeight : ActBossCatalog.MagmaLeapHeight;
                float oldFeet = boss.Y + boss.Height;
                boss.X = boss.AttackStartX + (boss.TargetX - boss.AttackStartX) * p;
                boss.Y = boss.AttackStartY + (boss.TargetY - boss.AttackStartY) * p - MathF.Sin(p * MathF.PI) * h;
                bool landed = p >= 1;
                if (boss.Y + boss.Height > oldFeet)
                {
                    var support = solids.Where(s => s.Height <= 16 && s.Width >= boss.Width &&
                        s.Right > boss.X && s.Left < boss.X + boss.Width &&
                        oldFeet <= s.Top + .1f && boss.Y + boss.Height >= s.Top)
                        .OrderBy(s => s.Top).FirstOrDefault();
                    if (support.Width > 0)
                    {
                        boss.X = Math.Clamp(boss.X, support.Left, support.Right - boss.Width);
                        boss.Y = support.Top - boss.Height;
                        landed = true;
                    }
                }
                if (p > .45f && boss.Bounds.IntersectsWith(PlayerBounds)) HurtPlayer(boss);
                if (boss.State is ActEnemyState.Dazed or ActEnemyState.Dead) break;
                if (landed)
                {
                    boss.BossAirborne = false;
                    boss.VerticalSpeed = 0;
                    boss.AttackApplied = true;
                    AddImpact(boss.X + boss.Width / 2, boss.Y + boss.Height, true);
                    // Follow-up eruptions have their own full warning. The leap never
                    // creates untelegraphed damage away from its visible body.
                    if (boss.BossMove == BossMove.MeteorLeap)
                        foreach (int side in new[] { -1, 1 })
                            AddBossHazard(boss, BossHazardKind.Flame, boss.X + boss.Width / 2 + side * 115,
                                65, 90, .75f, .38f);
                    ChangeEnemyState(boss, ActEnemyState.Recover);
                }
                break;
            case BossMove.Feathers:
            case BossMove.FlameFan:
                bool feathers = boss.BossMove == BossMove.Feathers;
                int volleys = feathers ? (phase2 ? 2 : 1) : (phase2 ? 3 : 2);
                if (boss.VolleyCount < volleys && t >= .16f + boss.VolleyCount * .5f)
                {
                    FireBossFan(boss, feathers ? 5 : 3, feathers ? .24f : .2f,
                        feathers ? ActBossCatalog.FeatherSpeed : ActBossCatalog.FlameSpeed);
                    boss.VolleyCount++;
                    boss.AttackApplied = true;
                }
                if (t >= .4f + (volleys - 1) * .5f) ChangeEnemyState(boss, ActEnemyState.Recover);
                break;
            case BossMove.RootBurst:
            case BossMove.Fissure:
                if (!boss.AttackApplied)
                {
                    boss.AttackApplied = true;
                    bool roots = boss.BossMove == BossMove.RootBurst;
                    int count = roots ? (phase2 ? 4 : 3) : (phase2 ? 5 : 4);
                    for (int i = 0; i < count; i++)
                    {
                        float x = roots ? boss.TargetX + boss.Width / 2 + (i - 1) * 135
                            : boss.X + boss.Width / 2 + boss.Facing * (130 + i * 120);
                        AddBossHazard(boss, roots ? BossHazardKind.Roots : BossHazardKind.Flame,
                            x, roots ? 46 : 60, roots ? 75 : 105,
                            (roots ? ActBossCatalog.RootWarning : ActBossCatalog.FlameWarning) + i * .18f, .4f);
                    }
                }
                if (t >= .7f) ChangeEnemyState(boss, ActEnemyState.Recover);
                break;
        }
    }

    internal static RectangleF ClubBounds(ActEnemy boss) => new(
        boss.Facing > 0 ? boss.X + boss.Width / 2 : boss.X + boss.Width / 2 - ActBossCatalog.ClubReach,
        boss.Y + boss.Height - 44, ActBossCatalog.ClubReach, 44);

    private void FireBossFan(ActEnemy boss, int count, float spread, float speed)
    {
        float x = boss.X + boss.Width / 2 + boss.Facing * 52;
        float y = boss.Y + 32;
        float angle = MathF.Atan2(boss.BossAimY - y, boss.TargetX + boss.Width / 2 - x);
        for (int i = 0; i < count; i++)
        {
            float a = angle + (i - (count - 1) / 2f) * spread;
            enemyShots.Add(new EnemyShot { X = x, Y = y, Source = boss,
                Vx = MathF.Cos(a) * speed, Vy = MathF.Sin(a) * speed, Life = 3f });
        }
    }

    private void AddBossHazard(ActEnemy boss, BossHazardKind kind, float x,
        float width, float height, float warning, float duration)
    {
        // Skip strikes outside the arena rather than clamping several onto one point.
        if (x - width / 2 < layout.BossArenaLeft || x + width / 2 > layout.BossArenaRight) return;
        float floor = TerrainYAt(x) ?? ActTuning.GroundY;
        bossHazards.Add(new BossHazard { Source = boss, Kind = kind,
            Bounds = new(x - width / 2, floor - height, width, height), Warning = warning, Duration = duration });
    }

    private void UpdateBossHazards(float dt)
    {
        for (int i = bossHazards.Count - 1; i >= 0; i--)
        {
            var hazard = bossHazards[i];
            var previous = hazard.DamageBounds;
            hazard.Time += dt * EnemySpeedRatio;
            if (hazard.Source.State == ActEnemyState.Dead || hazard.Time > hazard.Warning + hazard.Duration)
            { bossHazards.RemoveAt(i); continue; }
            if (hazard.Active && RectangleF.Union(previous, hazard.DamageBounds).IntersectsWith(PlayerBounds)) HurtPlayerCore(hazard.Source, clashable: false);
        }
    }
}
