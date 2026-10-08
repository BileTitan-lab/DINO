using System.Drawing;

namespace DinoDash.Desktop;

internal enum ActPlatformMotion { Horizontal, Vertical, Falling, Collapse }
internal sealed class ActDynamicPlatform(RectangleF origin, ActPlatformMotion motion,
    float amplitude = 100f, float period = 4f, float triggerX = -1f, float delay = 0f)
{
    public RectangleF Origin { get; private set; } = origin;
    public ActPlatformMotion Motion { get; } = motion;
    public float Amplitude { get; } = amplitude;
    public RectangleF Bounds { get; private set; } = origin;
    public RectangleF Previous { get; private set; } = origin;
    public bool Active { get; private set; } = true;
    public float Age { get; private set; } = -1f;
    public bool Warning => Age >= 0 && Age < (Motion == ActPlatformMotion.Collapse ? delay + .9f : .7f);
    private float clock;
    // Map assembly may lift a platform before gameplay starts. Keep its
    // initial collision and render bounds in sync with the authored origin.
    public void SetOrigin(RectangleF adjusted)
    {
        Origin = Bounds = Previous = adjusted;
    }
    public void Update(float dt, float playerX, bool occupied)
    {
        Previous = Bounds;
        clock += dt;
        if (Motion is ActPlatformMotion.Horizontal or ActPlatformMotion.Vertical)
        {
            float offset = MathF.Sin(clock * MathF.Tau / period) * Amplitude;
            Bounds = new RectangleF(Origin.X + (Motion == ActPlatformMotion.Horizontal ? offset : 0),
                Origin.Y + (Motion == ActPlatformMotion.Vertical ? offset : 0), Origin.Width, Origin.Height);
            return;
        }
        if (Age < 0 && (Motion == ActPlatformMotion.Falling ? occupied :
            playerX >= triggerX && playerX <= triggerX + 3300f)) Age = 0f;
        if (Age < 0) return;
        Age += dt;
        float fallAt = Motion == ActPlatformMotion.Collapse ? delay + .9f : .7f;
        float falling = Math.Max(0, Age - fallAt);
        Bounds = new RectangleF(Origin.X, Origin.Y + 420f * falling * falling, Origin.Width, Origin.Height);
        Active = falling < 1.1f;
        if (Age > (Motion == ActPlatformMotion.Collapse ? 24f : 5f))
        { Age = -1; Active = true; Bounds = Origin; }
    }
}

internal enum ActMechanismKind { Mushroom, Updraft, Lava, Eruption, Rock }
internal sealed class ActMechanism(ActMechanismKind kind, float x, float y, float width = 65f)
{
    public ActMechanismKind Kind { get; } = kind;
    public float X { get; } = x;
    public float Y { get; } = y;
    public float Width { get; } = width;
    public float Clock { get; private set; }
    public float Cooldown { get; set; }
    public HashSet<ActEnemy> HitEnemies { get; } = [];
    public bool PlayerHit { get; set; }
    public float Cycle => Clock % 6f;
    public bool Warning => Kind is ActMechanismKind.Eruption or ActMechanismKind.Rock or ActMechanismKind.Updraft && Cycle >= 2.5f && Cycle < 3.8f;
    public bool Active => Kind is ActMechanismKind.Lava or ActMechanismKind.Mushroom || Cycle >= 3.8f && Cycle < 4.9f;
    public RectangleF Bounds => Kind switch
    {
        ActMechanismKind.Lava => new(X, Y, Width, 450f),
        ActMechanismKind.Rock => new(X, Y - 390 + Math.Clamp((Cycle - 3.8f) / 1.1f, 0, 1) * 410, Width, 38),
        ActMechanismKind.Mushroom => new(X, Y - 17, Width, 25),
        _ => new(X, Y - 150, Width, 155)
    };
    public void Update(float dt, float playerX)
    {
        Cooldown = Math.Max(0f, Cooldown - dt);
        if (Math.Abs(playerX - X) > 1000f) return;
        int oldCycle = (int)(Clock / 6);
        Clock += dt;
        if ((int)(Clock / 6) != oldCycle) { HitEnemies.Clear(); PlayerHit = false; }
    }
}

internal sealed partial class ActEngine
{
    private void UpdateEnvironmentPlatforms(float dt)
    {
        if (MapIndex < 3 && !IsRoomJourney) return;
        foreach (ActDynamicPlatform platform in layout.MovingPlatforms)
        {
            RectangleF old = platform.Bounds;
            bool rider = platform.Active && Grounded && Math.Abs(PlayerY + PlayerHeight - old.Top) < 3f &&
                PlayerX + PlayerWidth - 5 > old.Left && PlayerX + 5 < old.Right;
            bool active = platform.Active;
            platform.Update(dt, PlayerX, rider);
            if (rider && platform.Active)
            { PlayerX += platform.Bounds.X - old.X; PlayerY += platform.Bounds.Y - old.Y; }
            if (rider && !platform.Active) Grounded = false;
            foreach (ActEnemy enemy in enemies)
                if (active && platform.Active && enemy.Kind != ActEnemyKind.Flying &&
                    Math.Abs(enemy.Y + enemy.Height - old.Top) < 3f &&
                    enemy.X + enemy.Width / 2 >= old.Left && enemy.X + enemy.Width / 2 < old.Right)
                { enemy.X += platform.Bounds.X - old.X; enemy.Y += platform.Bounds.Y - old.Y; }
        }
        solids.Clear();
        solids.AddRange(layout.Solids);
        solids.AddRange(layout.MovingPlatforms.Where(p => p.Active).Select(p => p.Bounds));
    }

    private void UpdateMechanisms(float dt)
    {
        if (MapIndex < 3 && !IsRoomJourney) return;
        foreach (ActMechanism mechanism in layout.Mechanisms)
        {
            mechanism.Update(dt, PlayerX);
            if (!mechanism.Active) continue;
            if (PlayerBounds.IntersectsWith(mechanism.Bounds))
            {
                if (mechanism.Kind is ActMechanismKind.Mushroom or ActMechanismKind.Updraft)
                {
                    if (VelocityY >= 0 && mechanism.Cooldown <= 0)
                    { VelocityY = -940f; jumpCut = true; Grounded = false; mechanism.Cooldown = .3f; }
                }
                else if (mechanism.Kind == ActMechanismKind.Lava) RescueFromPit();
                else if (!mechanism.PlayerHit && InvulnerabilityRemaining <= 0 && DashRemaining <= 0)
                { mechanism.PlayerHit = true; HurtPlayer(); }
            }
            if (mechanism.Kind == ActMechanismKind.Rock)
                foreach (ActEnemy enemy in enemies)
                    if (enemy.Kind != ActEnemyKind.Boss && enemy.State != ActEnemyState.Dead &&
                        enemy.Bounds.IntersectsWith(mechanism.Bounds) && mechanism.HitEnemies.Add(enemy))
                        DamageEnemy(enemy, 2, 100f, false);
        }
    }

    private bool ClearSight(ActEnemy enemy)
    {
        float fromX = enemy.X + enemy.Width / 2, fromY = enemy.Y + 14;
        float dx = PlayerX + PlayerWidth / 2 - fromX, dy = PlayerY + PlayerHeight / 2 - fromY;
        int steps = Math.Max(2, (int)(Math.Sqrt(dx * dx + dy * dy) / 18));
        for (int i = 1; i < steps; i++)
        {
            float x = fromX + dx * i / steps, y = fromY + dy * i / steps;
            if (TerrainYAt(x) is float ground && y > ground + 4 ||
                solids.Any(s => s.Contains(x, y))) return false;
        }
        return true;
    }
}
