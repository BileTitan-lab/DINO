using System.Drawing;

namespace DinoDash.Desktop;

internal static class ActEnemyTuning
{
    public const float Detection = 460f;
    public const float MeleeSpeed = 100f;
    public const float MeleeWindup = .42f;
    public const float MeleeRecover = .66f;
    public const float RangedPreferred = 245f;
    public const float RangedWindup = .65f;
    public const float RangedRecover = 1.25f;
    public const float RangedShotSpeed = 295f;
    public const float FlyingWindup = .52f;
    public const float FlyingDiveTime = .42f;
    public const float HeavyWindup = .8f;
    public const float HeavyChargeSpeed = 380f;
    public const float HeavyChargeTime = .52f;
    public const float BossWindup = .9f;
    public const float BossChargeSpeed = 460f;
    public const float BossChargeTime = .65f;
    public const float BossTailRange = 155f;
    public const float BossQuakeRange = 360f;
}

internal static class ActClashTuning
{
    public const float InputWindow=.19f;
    public const float TelegraphWindow=.22f;
    public const float Reach=102f;
    public const float PushSpeed=205f;
    public const float BossPushSpeed=135f;
    public const float PushDeceleration=900f;
    public const float StaggerSeconds=.72f;
    public const float BossStaggerSeconds=.55f;
    public const float Damage=1f;
}

internal sealed class EnemyShot
{
    public float X, Y, Vx, Vy, Life = 2.2f;
    public ActEnemy? Source;
    public RectangleF Bounds => new(X, Y, 13f, 8f);
}

internal sealed partial class ActEngine
{
    private readonly List<EnemyShot> enemyShots = [];
    public IReadOnlyList<EnemyShot> EnemyShots => enemyShots;

    private void ResetEnemies() { enemyShots.Clear(); bossHazards.Clear(); }

    private void UpdateEnemy(float dt)
    {
        float worldDt=dt;
        dt *= EnemySpeedRatio;
        foreach (ActEnemy enemy in enemies)
        {
            if (enemy.State == ActEnemyState.Dead || enemy.Dormant) continue;
            if(UpdateFear(enemy,worldDt))continue;
            float playerCenter = PlayerX + PlayerWidth / 2f;
            // Encounters stay on their side of a clearing. A retreat into camp
            // does not heal enemies or reset the player's cleared-room progress.
            if (supplies.Any(camp => camp.Contains(playerCenter) ||
                (enemy.HomeX < camp.Left && playerCenter > camp.Right) ||
                (enemy.HomeX > camp.Right && playerCenter < camp.Left)))
            {
                enemy.X = Approach(enemy.X, enemy.HomeX, 110f * dt);
                if (enemy.Kind == ActEnemyKind.Flying)
                    enemy.Y = Approach(enemy.Y, enemy.HomeY, 110f * dt);
                enemy.State = ActEnemyState.Patrol;
                enemy.StateTime = 0f;
                FollowTerrain(enemy, dt);
                continue;
            }
            enemy.StateTime += dt * (enemy.State is ActEnemyState.Chase or ActEnemyState.Recover
                ? ActChapterPressure.Aggression[MapIndex] : 1);
            enemy.FlashRemaining = Math.Max(0f, enemy.FlashRemaining - dt);
            if (enemy.State == ActEnemyState.Dazed)
            {
                float dazeOldX=enemy.X;
                float left=enemy.Kind==ActEnemyKind.Boss?layout.BossArenaLeft:0f;
                float right=enemy.Kind==ActEnemyKind.Boss?
                    layout.BossArenaRight-enemy.Width:WorldWidth-enemy.Width;
                enemy.X=Math.Clamp(enemy.X+enemy.KnockbackSpeed*dt,left,right);
                if(UsesTerrain)
                {
                    float center=enemy.X+enemy.Width*.5f,foot=enemy.Y+enemy.Height;
                    bool support=TerrainYAt(center) is float floor && floor>=foot-9f ||
                        solids.Any(s=>center>=s.Left && center<s.Right &&
                            Math.Abs(s.Top-foot)<=9f);
                    bool blocked=solids.Any(s=>s.Height>16f && enemy.Bounds.IntersectsWith(s)) ||
                        (enemy.Kind==ActEnemyKind.Flying && TerrainBlocks(enemy.Bounds));
                    if(blocked || (enemy.Kind!=ActEnemyKind.Flying && !support))
                        enemy.X=dazeOldX;
                }
                enemy.KnockbackSpeed=Approach(enemy.KnockbackSpeed,0f,
                    ActClashTuning.PushDeceleration*dt);
                if(enemy.StateTime >= (enemy.Kind==ActEnemyKind.Boss?
                    ActClashTuning.BossStaggerSeconds:ActClashTuning.StaggerSeconds))
                    ChangeEnemyState(enemy,ActEnemyState.Chase);
                FollowTerrain(enemy,dt);
                continue;
            }
            if (enemy.State == ActEnemyState.Hurt)
            {
                enemy.X = Math.Clamp(enemy.X + enemy.KnockbackSpeed * dt,
                    Math.Max(0f, enemy.HomeX - 350f),
                    Math.Min(WorldWidth - enemy.Width, enemy.HomeX + 350f));
                enemy.KnockbackSpeed = Approach(enemy.KnockbackSpeed, 0f, 1200f * dt);
                if (enemy.StateTime >= .23f) ChangeEnemyState(enemy, ActEnemyState.Chase);
                FollowTerrain(enemy, dt);
                continue;
            }
            float dx = PlayerX + PlayerWidth * .5f - enemy.X - enemy.Width * .5f;
            if (Math.Abs(dx) > 750f && enemy.Kind != ActEnemyKind.Boss) continue;
            if (MapIndex >= 3 && enemy.Kind != ActEnemyKind.Boss &&
                (!ClearSight(enemy) || (enemy.Kind is ActEnemyKind.Melee or ActEnemyKind.Heavy &&
                Math.Abs(PlayerY + PlayerHeight - enemy.Y - enemy.Height) > 100f)))
            {
                if (enemy.State != ActEnemyState.Recover) ChangeEnemyState(enemy, ActEnemyState.Patrol);
                FollowTerrain(enemy, dt);
                continue;
            }
            float oldX = enemy.X, oldY = enemy.Y;
            switch (enemy.Kind)
            {
                case ActEnemyKind.Melee: UpdateMelee(enemy, dt, dx); break;
                case ActEnemyKind.Ranged: UpdateRanged(enemy, dt, dx); break;
                case ActEnemyKind.Flying: UpdateFlying(enemy, dt, dx); break;
                case ActEnemyKind.Heavy: UpdateHeavy(enemy, dt, dx); break;
                case ActEnemyKind.Boss: UpdateBoss(enemy, dt, dx); break;
            }
            if ((MapIndex >= 3 || IsRoomJourney) && enemy.Kind is ActEnemyKind.Ranged or ActEnemyKind.Melee or ActEnemyKind.Heavy)
            {
                float nextCenter = enemy.X + enemy.Width / 2;
                float feet = oldY + enemy.Height;
                float? ground = TerrainYAt(nextCenter);
                bool leftLedge = solids.Any(s => oldX + enemy.Width / 2 >= s.Left &&
                    oldX + enemy.Width / 2 < s.Right && Math.Abs(s.Top - feet) < 3) &&
                    !solids.Any(s => nextCenter >= s.Left && nextCenter < s.Right && Math.Abs(s.Top - feet) < 8);
                if (ground is null || (ground is float floor && floor < feet - 10) ||
                    (enemy.Kind == ActEnemyKind.Ranged && leftLedge))
                    enemy.X = oldX;
            }
            if ((MapIndex >= 3 || IsRoomJourney) && (solids.Any(s => s.Height > 16 && enemy.Bounds.IntersectsWith(s)) ||
                (enemy.Kind == ActEnemyKind.Flying && (TerrainBlocks(enemy.Bounds) || solids.Any(s => s.IntersectsWith(enemy.Bounds))))))
            {
                enemy.X = oldX; enemy.Y = oldY;
                ChangeEnemyState(enemy, ActEnemyState.Recover);
            }
            FollowTerrain(enemy, dt);
        }
    }

    private static void ChangeEnemyState(ActEnemy enemy, ActEnemyState state)
    {
        // An attack may resolve in the same update that it is parried. Do not
        // let its normal recovery transition erase the clash stun.
        if (enemy.State == ActEnemyState.Dead ||
            (enemy.State == ActEnemyState.Dazed && state != ActEnemyState.Chase)) return;
        enemy.State = state;
        enemy.StateTime = 0f;
        enemy.AttackApplied = false;
    }

    private static void FacePlayer(ActEnemy enemy, float dx)
    {
        if (Math.Abs(dx) > 2f) enemy.Facing = Math.Sign(dx);
    }

    private void UpdateMelee(ActEnemy enemy, float dt, float dx)
    {
        float distance = Math.Abs(dx);
        switch (enemy.State)
        {
            case ActEnemyState.Patrol:
                enemy.X += enemy.Facing * 45f * dt;
                if (enemy.X < enemy.HomeX - 150f || enemy.X > enemy.HomeX + 90f)
                    enemy.Facing *= -1;
                if (distance < 300f) ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
            case ActEnemyState.Chase:
                if (distance > 355f) { ChangeEnemyState(enemy, ActEnemyState.Patrol); break; }
                FacePlayer(enemy, dx);
                if (distance > 66f) enemy.X += enemy.Facing * ActEnemyTuning.MeleeSpeed * dt;
                else ChangeEnemyState(enemy, ActEnemyState.Windup);
                break;
            case ActEnemyState.Windup:
                if (enemy.StateTime >= ActEnemyTuning.MeleeWindup)
                {
                    if (distance < 77f && Math.Abs(PlayerY + PlayerHeight / 2f -
                        enemy.Y - enemy.Height / 2f) < 45f) HurtPlayer(enemy);
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                }
                break;
            case ActEnemyState.Recover:
                if (enemy.StateTime >= ActEnemyTuning.MeleeRecover)
                    ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
        }
    }

    private void UpdateRanged(ActEnemy enemy, float dt, float dx)
    {
        float distance = Math.Abs(dx);
        switch (enemy.State)
        {
            case ActEnemyState.Patrol:
                if (distance < ActEnemyTuning.Detection)
                    ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
            case ActEnemyState.Chase:
                FacePlayer(enemy, dx);
                if (distance > 580f) { ChangeEnemyState(enemy, ActEnemyState.Patrol); break; }
                if (distance < 165f)
                    enemy.X -= enemy.Facing * 72f * dt;
                else if (distance > ActEnemyTuning.RangedPreferred + 35f)
                    enemy.X += enemy.Facing * 55f * dt;
                enemy.X = Math.Clamp(enemy.X, enemy.HomeX - 240f, enemy.HomeX + 240f);
                if (distance is > 125f and < 470f)
                    ChangeEnemyState(enemy, ActEnemyState.Windup);
                break;
            case ActEnemyState.Windup:
                if (enemy.StateTime >= ActEnemyTuning.RangedWindup)
                {
                    FireEnemyShot(enemy);
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                }
                break;
            case ActEnemyState.Recover:
                if (enemy.StateTime >= ActEnemyTuning.RangedRecover)
                    ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
        }
    }

    private void FireEnemyShot(ActEnemy enemy)
    {
        float x = enemy.X + enemy.Width / 2f;
        float y = enemy.Y + 15f;
        float targetY = PlayerY + PlayerHeight / 2f;
        float deltaX = PlayerX + PlayerWidth / 2f - x;
        float deltaY = targetY - y;
        float length = MathF.Max(1f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
        enemyShots.Add(new EnemyShot { X = x, Y = y,
            Source = enemy,
            Vx = deltaX / length * ActEnemyTuning.RangedShotSpeed,
            Vy = deltaY / length * ActEnemyTuning.RangedShotSpeed });
    }

    private void UpdateFlying(ActEnemy enemy, float dt, float dx)
    {
        float distance = Math.Abs(dx);
        switch (enemy.State)
        {
            case ActEnemyState.Patrol:
                enemy.X += enemy.Facing * 65f * dt;
                enemy.Y = enemy.HomeY + MathF.Sin(enemy.StateTime * 3f) * 9f;
                if (enemy.X < enemy.HomeX - 110f || enemy.X > enemy.HomeX + 110f)
                    enemy.Facing *= -1;
                if (distance < 350f) ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
            case ActEnemyState.Chase:
                FacePlayer(enemy, dx);
                enemy.X += enemy.Facing * 90f * dt;
                enemy.Y = Approach(enemy.Y, enemy.HomeY, 90f * dt);
                if (distance < 250f)
                {
                    enemy.TargetX = PlayerX + PlayerWidth / 2f;
                    enemy.TargetY = PlayerY + PlayerHeight / 2f;
                    ChangeEnemyState(enemy, ActEnemyState.Windup);
                }
                else if (distance > 570f) ChangeEnemyState(enemy, ActEnemyState.Patrol);
                break;
            case ActEnemyState.Windup:
                if (enemy.StateTime >= ActEnemyTuning.FlyingWindup)
                    ChangeEnemyState(enemy, ActEnemyState.Attack);
                break;
            case ActEnemyState.Attack:
                enemy.X = Approach(enemy.X, enemy.TargetX, 530f * dt);
                enemy.Y = Approach(enemy.Y, enemy.TargetY, 300f * dt);
                if (enemy.Bounds.IntersectsWith(PlayerBounds)) HurtPlayer(enemy);
                if (enemy.StateTime >= ActEnemyTuning.FlyingDiveTime)
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                break;
            case ActEnemyState.Recover:
                enemy.Y = Approach(enemy.Y, enemy.HomeY, 175f * dt);
                enemy.X = Approach(enemy.X, enemy.HomeX, 90f * dt);
                if (enemy.StateTime >= 1.1f && Math.Abs(enemy.Y - enemy.HomeY) < 4f)
                    ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
        }
    }

    private void UpdateHeavy(ActEnemy enemy, float dt, float dx)
    {
        float distance = Math.Abs(dx);
        switch (enemy.State)
        {
            case ActEnemyState.Patrol:
                if (distance < ActEnemyTuning.Detection)
                    ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
            case ActEnemyState.Chase:
                FacePlayer(enemy, dx);
                if (distance > 550f) { ChangeEnemyState(enemy, ActEnemyState.Patrol); break; }
                if (distance > 180f) enemy.X += enemy.Facing * 48f * dt;
                else ChangeEnemyState(enemy, ActEnemyState.Windup);
                break;
            case ActEnemyState.Windup:
                if (enemy.StateTime >= ActEnemyTuning.HeavyWindup)
                    ChangeEnemyState(enemy, ActEnemyState.Attack);
                break;
            case ActEnemyState.Attack:
                enemy.X += enemy.Facing * ActEnemyTuning.HeavyChargeSpeed * dt;
                if (enemy.Bounds.IntersectsWith(PlayerBounds)) HurtPlayer(enemy);
                if (enemy.StateTime >= ActEnemyTuning.HeavyChargeTime ||
                    Math.Abs(enemy.X - enemy.HomeX) > 420f)
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                break;
            case ActEnemyState.Recover:
                if (enemy.StateTime >= 1.15f) ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
        }
    }

    private void UpdateBoss(ActEnemy enemy, float dt, float dx)
    {
        if (enemy.BossId != ActBossId.Tyrant)
        {
            UpdateChapterBoss(enemy, dt, dx);
            return;
        }
        float distance = Math.Abs(dx);
        if (distance > 730f && enemy.State == ActEnemyState.Patrol) return;
        float haste = enemy.Health <= enemy.MaxHealth / 2 ? .8f : 1f;
        switch (enemy.State)
        {
            case ActEnemyState.Patrol:
                ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
            case ActEnemyState.Chase:
                FacePlayer(enemy, dx);
                if (enemy.StateTime < .48f * haste) break;
                enemy.BossMove = (enemy.AttackCount % 3) switch
                {
                    0 => BossMove.Charge, 1 => BossMove.Tail, _ => BossMove.Quake
                };
                // Close-range tail takes precedence over charge in a readable cycle.
                if (distance < 135f && enemy.AttackCount % 3 == 0)
                    enemy.BossMove = BossMove.Tail;
                enemy.AttackCount++;
                ChangeEnemyState(enemy, ActEnemyState.Windup);
                break;
            case ActEnemyState.Windup:
                if (enemy.StateTime >= ActEnemyTuning.BossWindup * haste)
                    ChangeEnemyState(enemy, ActEnemyState.Attack);
                break;
            case ActEnemyState.Attack:
                UpdateBossAttack(enemy, dt, haste);
                break;
            case ActEnemyState.Recover:
                if (enemy.StateTime >= 1.05f * haste)
                    ChangeEnemyState(enemy, ActEnemyState.Chase);
                break;
        }
    }

    private void UpdateBossAttack(ActEnemy enemy, float dt, float haste)
    {
        float distance = Math.Abs(PlayerX + PlayerWidth / 2f - enemy.X - enemy.Width / 2f);
        switch (enemy.BossMove)
        {
            case BossMove.Charge:
                enemy.X += enemy.Facing * ActEnemyTuning.BossChargeSpeed / haste * dt;
                enemy.X = Math.Clamp(enemy.X, layout.BossArenaLeft,
                    layout.BossArenaRight - enemy.Width);
                if (enemy.Bounds.IntersectsWith(PlayerBounds)) HurtPlayer(enemy);
                if (enemy.State is ActEnemyState.Dazed or ActEnemyState.Dead) break;
                if (enemy.StateTime >= ActEnemyTuning.BossChargeTime * haste ||
                    enemy.X <= layout.BossArenaLeft ||
                    enemy.X >= layout.BossArenaRight - enemy.Width)
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                break;
            case BossMove.Tail:
                if (!enemy.AttackApplied && enemy.StateTime >= .18f * haste)
                {
                    if (distance < ActEnemyTuning.BossTailRange &&
                        PlayerY + PlayerHeight > enemy.Y + 28f) HurtPlayer(enemy);
                    if (enemy.State is ActEnemyState.Dazed or ActEnemyState.Dead) break;
                    enemy.AttackApplied = true;
                }
                if (enemy.StateTime >= .38f * haste)
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                break;
            case BossMove.Quake:
                if (!enemy.AttackApplied && enemy.StateTime >= .24f * haste)
                {
                    if (distance < ActEnemyTuning.BossQuakeRange && Grounded &&
                        (MapIndex < 3 || Math.Abs(PlayerY + PlayerHeight - enemy.Y - enemy.Height) < 35f))
                        HurtPlayer(enemy);
                    enemy.AttackApplied = true;
                    AddImpact(enemy.X + enemy.Width / 2f,
                        ActTuning.GroundY - 4f, true);
                }
                if (enemy.StateTime >= .45f * haste)
                    ChangeEnemyState(enemy, ActEnemyState.Recover);
                break;
        }
    }

    private void UpdateEnemyProjectiles(float dt)
    {
        dt *= EnemySpeedRatio;
        for (int i = enemyShots.Count - 1; i >= 0; i--)
        {
            EnemyShot shot = enemyShots[i];
            if (shot.Source is { Kind: ActEnemyKind.Boss, State: ActEnemyState.Dead })
            { enemyShots.RemoveAt(i); continue; }
            shot.X += shot.Vx * dt;
            shot.Y += shot.Vy * dt;
            shot.Life -= dt;
            if (shot.Life <= 0f || shot.Y < 0f || shot.X < 0f ||
                shot.X > WorldWidth ||
                solids.Any(solid => shot.Bounds.IntersectsWith(solid)) ||
                TerrainBlocks(shot.Bounds) || supplies.Any(camp => camp.Contains(shot.X)))
            {
                enemyShots.RemoveAt(i);
                continue;
            }
            if (shot.Bounds.IntersectsWith(PlayerBounds))
            {
                HurtPlayerCore(shot.Source, clashable: false);
                enemyShots.RemoveAt(i);
            }
        }
    }
}
