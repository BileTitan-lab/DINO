using System.Drawing;

namespace DinoDash.Desktop;

internal enum ActPose
{
    Idle, Run, Jump, Fall, Dash, Attack1, Attack2, Attack3,
    Tail, Stomp, Ranged, Hurt, Dead
}

internal sealed class BoneSpike
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Direction { get; set; }
    public float Vx { get; set; }
    public float Vy { get; set; }
    public bool Special { get; set; }
    public float Damage { get; set; } = ActCombatTuning.BoneSpikeDamage;
    public bool Homing { get; set; }
    public float Life { get; set; } = 1.6f;
    public int PierceRemaining;
    public bool IsFragment;
    public float Scale=1;
    public readonly HashSet<ActEnemy> HitEnemies=[];
    public RectangleF Bounds
    {
        get {float speed=MathF.Sqrt(Vx*Vx+Vy*Vy);float ux=speed>0?Math.Abs(Vx)/speed:1,uy=speed>0?Math.Abs(Vy)/speed:0;
            return new(X,Y,(20*ux+8*uy)*Scale,(8*ux+20*uy)*Scale);}
    }
}

internal static class ActCombatTuning
{
    public const int ClawDamage = 1;
    public const int FinisherDamage = 2;
    public const int TailDamage = 2;
    public const int StompDamage = 2;
    public const int BoneSpikeDamage = 1;
    public const float ClawKnockback = 220f;
    public const float FinisherKnockback = 355f;
    public const float TailKnockback = 430f;
    public const float StompKnockback = 315f;
    public const float BoneSpikeSpeed = 540f;
    public const float BoneSpikeCooldown = .3f;
}

internal sealed partial class ActEngine
{
    private static readonly float[] ComboDurations = [.25f, .29f, .42f];
    private static readonly float[] ComboHitStarts = [.065f, .085f, .14f];
    private static readonly float[] ComboHitEnds = [.145f, .18f, .27f];
    private readonly List<BoneSpike> projectiles = [];
    private float attackElapsed;
    private float attackRecovery;
    private float attackBuffer;
    private float comboWindow;
    private float tailElapsed;
    private float rangedPoseRemaining;
    private float rangedCooldown;
    private bool attackHit;
    private bool tailHit;
    private int comboStep;
    private float meleeHitCounter;
    private float stompImpactRemaining;
    private bool stomping;

    public int RangedEnergy { get; private set; }
    public IReadOnlyList<BoneSpike> Projectiles => projectiles;
    public int ComboStep => comboStep;
    public float AttackElapsed => attackElapsed;
    public float TailElapsed => tailElapsed;
    public float StompImpactRemaining => stompImpactRemaining;
    public bool Stomping => stomping;
    public bool IsAttacking => attackElapsed > 0f || tailElapsed > 0f;
    public ActPose Pose { get; private set; } = ActPose.Idle;
    public float PoseElapsed { get; private set; }

    private void ResetCombat()
    {
        clashWindowRemaining=0f;
        attackElapsed = attackRecovery = attackBuffer = comboWindow = 0f;
        tailElapsed = rangedPoseRemaining = rangedCooldown = 0f;
        stompImpactRemaining = 0f;
        attackHit = tailHit = stomping = false;
        comboStep = 0; meleeHitCounter = 0;
        RangedEnergy = MaximumEnergy;
        projectiles.Clear();
        ResetBuildMap();
        Pose = ActPose.Idle;
        PoseElapsed = 0f;
    }

    private void CancelCombat()
    {
        clashWindowRemaining=0f;
        attackElapsed = tailElapsed = 0f;
        stomping = false;
    }

    private void UpdateCombatTimers(float dt)
    {
        clashWindowRemaining=Math.Max(0f,clashWindowRemaining-dt);
        attackRecovery = Math.Max(0f, attackRecovery - dt * Run.Resolve(PowerStat.AttackSpeed,1));
        attackBuffer = Math.Max(0f, attackBuffer - dt);
        comboWindow = Math.Max(0f, comboWindow - dt);
        rangedCooldown = Math.Max(0f, rangedCooldown - dt);
        rangedPoseRemaining = Math.Max(0f, rangedPoseRemaining - dt);
        stompImpactRemaining = Math.Max(0f, stompImpactRemaining - dt);
        if (comboWindow <= 0f && attackElapsed <= 0f) comboStep = 0;
    }

    private void HandleCombatInput()
    {
        if (AttackRequested) attackBuffer = .22f;
        if (StompRequested && !Grounded && DashRemaining <= 0f)
        {
            CancelCombat();
            attackBuffer = 0f;
            stomping = true;
            stompStartFeet=PlayerY+PlayerHeight;
            VelocityY = Math.Max(VelocityY, 800f);
            return;
        }
        bool ready = DashRemaining <= 0f && !stomping &&
            attackElapsed <= 0f && tailElapsed <= 0f && attackRecovery <= 0f;
        if (!ready) return;
        if (TailRequested)
        {
            tailElapsed = .0001f;
            tailHit = false;
            attackBuffer = 0f;
            comboStep = 0;
            return;
        }
        if (RangedRequested && (RangedEnergy > 0 || FreeBoneSpikes) && rangedCooldown <= 0f)
        {
            FireBoneSpike();
            return;
        }
        if (attackBuffer <= 0f) return;
        comboStep = comboWindow > 0f && comboStep is >= 1 and < 3
            ? comboStep + 1 : 1;
        comboWindow = attackBuffer = 0f;
        attackElapsed = .0001f;
        attackHit = false;
        // Only a fresh claw press opens the clash window; a queued combo does not.
        clashWindowRemaining=AttackRequested?ActClashTuning.InputWindow:0f;
        if(clashWindowRemaining>0f && TryClashNearby())return;
        if(comboStep==3)MoveHorizontal(Facing*Run.Resolve(PowerStat.FinisherLunge,0));
    }

    private void UpdateCombatHits(float dt)
    {
        if (attackElapsed > 0f)
        {
            attackElapsed += dt * Run.Resolve(PowerStat.AttackSpeed,1);
            int index = comboStep - 1;
            if (!attackHit && attackElapsed >= ComboHitStarts[index] &&
                attackElapsed <= ComboHitEnds[index])
            {
                float reach = (comboStep == 3 ? 76f : 61f)*Run.Resolve(PowerStat.ClawRange,1);
                RectangleF hitbox = Facing > 0
                    ? new RectangleF(PlayerX + 45f, PlayerY + 7f, reach, 42f)
                    : new RectangleF(PlayerX + PlayerWidth - 45f - reach, PlayerY + 7f,
                        reach, 42f);
                ActEnemy? target = enemies
                    .Where(enemy => enemy.State != ActEnemyState.Dead && !enemy.Dormant &&
                        hitbox.IntersectsWith(enemy.Bounds))
                    .OrderBy(enemy => Facing * enemy.X)
                    .FirstOrDefault();
                if (target is not null)
                {
                    attackHit = true;
                    bool empowered=Run.ArmedClawMultiplier>1;
                    float boost=Run.ConsumeClawBoost();
                    DamageEnemyTyped(target,
                        (comboStep == 3 ? Run.Resolve(PowerStat.FinisherDamage,ActCombatTuning.FinisherDamage) : ActCombatTuning.ClawDamage)*boost,
                        (comboStep == 3 ? Run.Resolve(PowerStat.FinisherKnockback,ActCombatTuning.FinisherKnockback) : ActCombatTuning.ClawKnockback)*(empowered?ActBuildTuning.EmpowerKnockback:1),
                        true,true,BuildDamageKind.Claw);
                }
            }
            if (attackElapsed >= ComboDurations[index])
            {
                attackElapsed = 0f;
                attackRecovery = comboStep == 3 ? .16f : .075f;
                comboWindow = .65f;
            }
        }
        if (tailElapsed <= 0f) return;
        tailElapsed += dt;
        if (!tailHit && tailElapsed is >= .17f and <= .36f)
        {
            float reach = 96f * Run.Resolve(PowerStat.TailRange, 1f);
            RectangleF sweep = new(PlayerX - reach, PlayerY + 8f,
                PlayerWidth + reach * 2f, 45f);
            var targets=enemies.Where(enemy =>
                enemy.State != ActEnemyState.Dead && !enemy.Dormant &&
                sweep.IntersectsWith(enemy.Bounds)).ToArray();
            foreach (ActEnemy enemy in targets)
            {
                tailHit = true;
                DamageEnemyTyped(enemy, Run.Resolve(PowerStat.TailDamage,ActCombatTuning.TailDamage),
                    ActCombatTuning.TailKnockback *
                        Run.Resolve(PowerStat.TailKnockback, 1f), true,false,BuildDamageKind.Tail);
            }
            if(targets.Length>=2){float ratio=Run.Resolve(PowerStat.TailRecoveryRatio,1);if(ratio<1){tailElapsed=.5f;attackRecovery=.15f*ratio;}}
        }
        if (tailElapsed >= .5f)
        {
            tailElapsed = 0f;
            if(attackRecovery<=0)attackRecovery = .15f;
        }
    }

    private void DamageEnemy(ActEnemy enemy, float amount, float knockback, bool melee,
        bool claw = false)
        => DamageEnemyTyped(enemy,amount,knockback,melee,claw,claw?BuildDamageKind.Claw:BuildDamageKind.Skill);

    private void DamageEnemyTyped(ActEnemy enemy,float amount,float knockback,bool melee,bool claw,BuildDamageKind kind)
    {
        if (enemy.State == ActEnemyState.Dead) return;
        float rage = melee && Run.RageRemaining > 0f
            ? ActPowerCatalog.Art(BattleArtId.PrimalRage).Strength : 1f;
        float clawMultiplier = claw ? Run.Resolve(PowerStat.ClawDamage, 1f) : 1f;
        float exact = amount * (melee?Run.Resolve(PowerStat.MeleeDamage,1):1) * clawMultiplier *
            Run.Resolve(PowerStat.AllDamage,1) * rage * DamageMultiplier + enemy.BonusDamageRemainder;
        int damage=(int)MathF.Floor(exact+.00001f);
        enemy.BonusDamageRemainder=Math.Max(0,exact-damage);
        enemy.Health = Math.Max(0, enemy.Health - damage);
        int push = Math.Sign(enemy.X + enemy.Width / 2f -
            (PlayerX + PlayerWidth / 2f));
        if (push == 0) push = Facing;
        if (enemy.Kind != ActEnemyKind.Boss || enemy.State is ActEnemyState.Patrol or ActEnemyState.Chase)
            enemy.Facing = -push;
        enemy.KnockbackSpeed = push * (enemy.Kind == ActEnemyKind.Boss ? knockback * .16f : knockback);
        enemy.FlashRemaining = .16f;
        if (enemy.Health == 0)
        {
            enemy.State = ActEnemyState.Dead;
            if (enemy.BossAirborne)
            {
                enemy.Y = (TerrainYAt(enemy.X + enemy.Width / 2) ?? ActTuning.GroundY) - enemy.Height;
                enemy.BossAirborne = false;
            }
        }
        else if (enemy.Kind != ActEnemyKind.Boss)
        {
            enemy.State = ActEnemyState.Hurt;
            enemy.StateTime = 0f;
        }
        if (enemy.Health == 0)
        {
            DefeatedEnemies++;
            NoteEnemyKill();
            DropBones(enemy);
            if (enemy.Kind == ActEnemyKind.Boss) SpawnBossEvolutionReward();
        }
        NoteBuildHit(enemy,kind,enemy.Health==0,melee);
        AddImpact(enemy.X + enemy.Width / 2f, enemy.Y + enemy.Height / 2f,
            amount >= 2, enemy.Health == 0);
        if (melee && RangedEnergy < MaximumEnergy)
        {
            meleeHitCounter++;
            if (meleeHitCounter >= Run.Resolve(PowerStat.RechargeHits,ActTuning.MeleeHitsPerEnergy))
            {
                RangedEnergy++;
                meleeHitCounter = 0;
            }
        }
        else if (RangedEnergy == MaximumEnergy) meleeHitCounter = 0;
    }

    private void ImpactStomp()
    {
        stomping = false;
        stompJumpLockRemaining = ActTuning.StompJumpLock;
        jumpBuffer = 0f;
        stompImpactRemaining = .27f;
        AddImpact(PlayerX + PlayerWidth / 2f,
            PlayerY + PlayerHeight, true);
        float scale = Run.Resolve(PowerStat.StompRange, 1f);
        float radius = (skyArt?.Radius??63f) * scale;
        RectangleF impact = new(PlayerX - radius,
            PlayerY + PlayerHeight - 49f * scale,
            PlayerWidth + radius * 2f, 69f * scale);
        foreach (ActEnemy enemy in enemies.Where(enemy =>
            enemy.State != ActEnemyState.Dead && !enemy.Dormant &&
                impact.IntersectsWith(enemy.Bounds)).ToArray())
            DamageEnemyTyped(enemy, Run.Resolve(PowerStat.StompDamage,skyArt?.Strength??ActCombatTuning.StompDamage)*
                (1+Math.Min(Run.Resolve(PowerStat.SlamMaximumBonus,0),Math.Max(0,PlayerY+PlayerHeight-stompStartFeet)*Run.Resolve(PowerStat.SlamHeightBonus,0))),
                (skyArt?.Knockback??ActCombatTuning.StompKnockback) * Run.Resolve(PowerStat.TailKnockback,1), true,false,BuildDamageKind.Slam);
        GroundSlamWaves();skyArt=null;
    }

    private void FireBoneSpike()
    {
        int firstNew=projectiles.Count;
        if(!FreeBoneSpikes)RangedEnergy=Math.Max(0,RangedEnergy-1);
        rangedCooldown = ActCombatTuning.BoneSpikeCooldown;
        rangedPoseRemaining = .24f;
        projectiles.Add(new BoneSpike
        {
            X = Facing > 0 ? PlayerX + PlayerWidth + 4f : PlayerX - 24f,
            Y = PlayerY + 24f,
            Direction = Facing, Vx = Facing * ActCombatTuning.BoneSpikeSpeed
        });
        if (Run.Resolve(PowerStat.RangedProjectiles, 1f) > 1f)
            projectiles.Add(new BoneSpike
            {
                X = Facing > 0 ? PlayerX + PlayerWidth + 4f : PlayerX - 24f,
                Y = PlayerY + 21f, Direction = Facing,
                Vx = Facing * ActCombatTuning.BoneSpikeSpeed * .99f,
                Vy = -ActCombatTuning.BoneSpikeSpeed *
                    Run.Resolve(PowerStat.RangedSpread, 0f), Special = true
            });
        if(powerRandom.NextDouble()<Run.Resolve(PowerStat.ExtraSpikeChance,0))
            for(int i=0;i<(int)Run.Resolve(PowerStat.ExtraSpikeCount,0);i++)projectiles.Add(new(){X=PlayerX+PlayerWidth/2,Y=PlayerY+24,
                Direction=Facing,Vx=Facing*ActCombatTuning.BoneSpikeSpeed,Vy=(i==0?-1:1)*90,Scale=.65f,Special=true});
        foreach(var spike in projectiles.Skip(firstNew))
        {
            spike.Vx*=Run.Resolve(PowerStat.ProjectileSpeed,1);spike.Vy*=Run.Resolve(PowerStat.ProjectileSpeed,1);
            spike.Life*=Run.Resolve(PowerStat.ProjectileLife,1);
            spike.Damage=Run.Resolve(PowerStat.ProjectileDamage,ActCombatTuning.BoneSpikeDamage)*(spike.Scale<1?.6f:1);
            spike.PierceRemaining=(int)Run.Resolve(PowerStat.ProjectilePierce,0);
            spike.Homing=Run.CurrentForm==PrimalForm.Pterosaur;
        }
    }

    private void UpdateProjectiles(float dt)
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            BoneSpike spike = projectiles[i];
            if(spike.Homing)
            {
                var target=enemies.Where(e=>e.State!=ActEnemyState.Dead && !e.Dormant && !spike.HitEnemies.Contains(e) &&
                    Math.Abs(e.X-spike.X)<ActPrimalCatalog.HomingRadius && Math.Abs(e.Y-spike.Y)<200)
                    .OrderBy(e=>MathF.Abs(e.X-spike.X)+MathF.Abs(e.Y-spike.Y)).FirstOrDefault();
                if(target!=null)
                {
                    float dx=target.X+target.Width/2-spike.X,dy=target.Y+target.Height/2-spike.Y;
                    float length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy)),speed=MathF.Sqrt(spike.Vx*spike.Vx+spike.Vy*spike.Vy);
                    float k=Math.Min(1,dt*ActPrimalCatalog.HomingTurnRate);
                    spike.Vx+=(dx/length*speed-spike.Vx)*k;spike.Vy+=(dy/length*speed-spike.Vy)*k;
                }
            }
            var oldBounds=spike.Bounds;
            spike.X += spike.Vx * dt;
            spike.Y += spike.Vy * dt;
            spike.Life -= dt;
            bool blocked = spike.Life <= 0f || spike.X < 0f ||
                spike.X > WorldWidth || spike.Y < -600f ||
                solids.Any(solid => spike.Bounds.IntersectsWith(solid)) ||
                TerrainBlocks(spike.Bounds);
            if (blocked) { projectiles.RemoveAt(i); continue; }
            var hits = enemies
                .Where(enemy => enemy.State != ActEnemyState.Dead && !enemy.Dormant && !spike.HitEnemies.Contains(enemy) &&
                    RectangleF.Union(oldBounds,spike.Bounds).IntersectsWith(enemy.Bounds))
                .OrderBy(enemy => spike.Direction * enemy.X)
                .ToArray();
            foreach(var firstHit in hits)
            {
                spike.HitEnemies.Add(firstHit);
                DamageEnemyTyped(firstHit, spike.Damage, 105f, false,false,BuildDamageKind.Projectile);
                FragmentSpike(spike);
                if(spike.PierceRemaining--<=0){projectiles.RemoveAt(i);break;}
            }
        }
    }

    private void UpdatePose(float dt)
    {
        ActPose next =
            IsDead ? ActPose.Dead :
            DashRemaining > 0f ? ActPose.Dash :
            stomping || stompImpactRemaining > 0f ? ActPose.Stomp :
            HurtPoseRemaining > 0 ? ActPose.Hurt :
            tailElapsed > 0f ? ActPose.Tail :
            attackElapsed > 0f ? comboStep switch
            {
                2 => ActPose.Attack2, 3 => ActPose.Attack3, _ => ActPose.Attack1
            } :
            rangedPoseRemaining > 0f ? ActPose.Ranged :
            !Grounded && VelocityY < 0f ? ActPose.Jump :
            !Grounded ? ActPose.Fall :
            Math.Abs(VelocityX) > 20f ? ActPose.Run : ActPose.Idle;
        if (next != Pose) { Pose = next; PoseElapsed = 0f; }
        else PoseElapsed += dt;
    }
}
