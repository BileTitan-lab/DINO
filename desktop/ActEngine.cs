using System.Drawing;

namespace DinoDash.Desktop;

internal static class ActTuning
{
    public const float ViewWidth = 960f;
    public const float ViewHeight = 360f;
    public const float LevelWidth = 36000f;
    public const float GroundY = 300f;
    public const float Step = 1f / 120f;
    public const float MoveSpeed = 255f;
    public const float Acceleration = 1900f;
    public const float Braking = 2400f;
    public const float JumpSpeed = 640f;
    public const float StompJumpLock = .5f;
    public const float Gravity = 1800f;
    public const float DashSpeed = 650f;
    public const float DashDuration = .19f;
    public const float DashCooldown = 1.2f;
    public const float DamageInvulnerability = 1f;
    public const int MaxRangedEnergy = 5;
    public const int MeleeHitsPerEnergy = 2;
    public const int MaxHealth = 5;
    public const float LightHitStop = .035f;
    public const float HeavyHitStop = .075f;
    public const float PlayerHitStop = .09f;
    public const float LightShake = 2f;
    public const float HeavyShake = 6f;
}

internal enum ActEnemyState { Patrol, Chase, Windup, Attack, Recover, Hurt, Dazed, Dead }
internal enum ActEnemyKind { Melee, Ranged, Flying, Heavy, Boss }
internal enum BossMove { Charge, Tail, Quake, Roll, Club, Rockfall, Pounce, Feathers, RootBurst, FlameFan, Fissure, MeteorLeap }

internal sealed class ActEnemy
{
    public ActEnemyKind Kind { get; init; } = ActEnemyKind.Melee;
    public float X { get; set; } = 1560f;
    public float Y { get; set; } = ActTuning.GroundY - 42f;
    public float HomeX { get; init; } = 1560f;
    public float HomeY { get; init; } = ActTuning.GroundY - 42f;
    public float Width => Kind switch { ActEnemyKind.Heavy => 68f, ActEnemyKind.Boss => 126f, ActEnemyKind.Flying => 44f, _ => 36f };
    public float Height => Kind switch { ActEnemyKind.Heavy => 54f, ActEnemyKind.Boss => 100f, ActEnemyKind.Flying => 28f, _ => 42f };
    public int Health { get; set; } = 3;
    public int MaxHealth { get; set; } = 3;
    public ActBossId BossId { get; set; }
    public float AttackStartX, AttackStartY, BossAimY;
    public bool BossAirborne;
    public int VolleyCount;
    public int Facing { get; set; } = -1;
    public ActEnemyState State { get; set; } = ActEnemyState.Patrol;
    public float StateTime { get; set; }
    public float KnockbackSpeed { get; set; }
    public float FlashRemaining { get; set; }
    public bool AttackApplied { get; set; }
    public BossMove BossMove { get; set; }
    public int AttackCount { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public bool Dormant { get; set; }
    public float BonusDamageRemainder { get; set; }
    public float VerticalSpeed { get; set; }
    public float FearRemaining, FearSpeed;
    public bool BuildRewardEligible=true, BuildKillClaimed;
    public RectangleF Bounds => new(X, Y, Width, Height);
}

internal sealed partial class ActEngine
{
    private readonly List<RectangleF> solids = [];
    private readonly List<ActEnemy> enemies = [];
    private ActLevelLayout layout = null!;
    private float jumpBuffer;
    private float stompJumpLockRemaining;
    private float coyoteTime;
    private bool jumpCut;
    private float cameraY;
    private float safeX, safeFeetY;

    private bool roomAdventureEnabled;
    private bool UsesTerrain=>MapIndex>=2 || IsRoomJourney || IsResting;
    public ActEngine(int? seed = null,bool roomAdventure=false)
    {roomAdventureEnabled=roomAdventure;Reset(seed);}
    public int Seed => layout.Seed;
    public ActLevelLayout Layout => layout;
    public IReadOnlyList<RectangleF> Solids => solids;
    public ActEnemy Enemy => enemies[0];
    public IReadOnlyList<ActEnemy> Enemies => enemies;
    private readonly ActEnemy safeBoss = new() { Kind = ActEnemyKind.Boss, State = ActEnemyState.Dead, Health = 0 };
    public ActEnemy Boss => enemies.LastOrDefault(e=>e.Kind==ActEnemyKind.Boss) ?? safeBoss;
    public int MapIndex { get; private set; } = 1;
    public float WorldWidth => layout.Width;
    public float CameraY => cameraY;
    public float? TerrainYAt(float x) => UsesTerrain
        ? ActSecondMap.GroundAt(layout, x) : ActTuning.GroundY;
    public int DefeatedEnemies { get; private set; }
    public float RunTime { get; private set; }
    public IReadOnlyList<ActSupply> Supplies => supplies;
    public IReadOnlyList<ActHazard> Hazards => layout.Hazards;
    public IReadOnlyList<ActReward> Rewards => layout.Rewards;
    private readonly List<ActSupply> supplies = [];
    public float PlayerX { get; private set; }
    public float PlayerY { get; private set; }
    public float VelocityX { get; private set; }
    public float VelocityY { get; private set; }
    public float AnimationClock { get; private set; }
    public int Facing { get; private set; } = 1;
    public int Health { get => Run.Health; private set => Run.SetHealth(value); }
    public bool Grounded { get; private set; }
    public bool LeftHeld { get; set; }
    public bool RightHeld { get; set; }
    public bool JumpHeld { get; set; }
    public bool JumpRequested { get; set; }
    public float StompJumpLockRemaining => stompJumpLockRemaining;
    public bool DashRequested { get; set; }
    public bool AttackRequested { get; set; }
    public bool TailRequested { get; set; }
    public bool StompRequested { get; set; }
    public bool RangedRequested { get; set; }
    public bool BattleArt1Requested { get; set; }
    public bool BattleArt2Requested { get; set; }
    public float DashRemaining { get; private set; }
    public float DashCooldownRemaining { get; private set; }
    public float InvulnerabilityRemaining { get; private set; }
    public float HurtPoseRemaining { get; private set; }
    public bool IsDead => Health <= 0;
    public bool IsComplete { get; private set; }
    public float PlayerWidth => 54f;
    public float PlayerHeight => 52f;
    public float ExitX => layout.Exit;
    public RectangleF PlayerBounds => new(PlayerX + 5f, PlayerY + 3f, PlayerWidth - 10f, PlayerHeight - 5f);
    public float CameraX => Math.Clamp(PlayerX - ActTuning.ViewWidth * .38f, 0f,
        WorldWidth - ActTuning.ViewWidth);

    public void Reset(int? seed = null)
    {
        MapIndex = 1;
        RestStop = null;
        Run = new ActRunState();
        if(roomAdventureEnabled)BeginChapterRooms(seed ?? ActLevelRandomizer.NewSeed());
        ResetMap(seed);
    }

    private void ResetMap(int? seed)
    {
        combatAreas.Clear();roomExits.Clear();hoveredRoomExit=-1;
        layout = IsRoomJourney ? ActChapterRooms.Build(Run.RoomJourney!,combatAreas,roomExits)
            : IsResting ? ActRestStop.CreateLayout(RestStop!.Seed) : MapIndex == 1
            ? ActLevelRandomizer.Generate(seed ?? ActLevelRandomizer.NewSeed())
            : MapIndex == 2 ? ActSecondMap.Generate(seed ?? ActLevelRandomizer.NewSeed())
            : ActAdvancedMaps.Generate(MapIndex, seed ?? ActLevelRandomizer.NewSeed());
        ResetRoomServices();
        ResetPowers(layout.Seed);
        PlayerX = layout.SpawnX;
        PlayerY = layout.SpawnFeetY - PlayerHeight;
        VelocityX = VelocityY = 0f;
        AnimationClock = 0f;
        Facing = 1;
        Grounded = true;
        solids.Clear();
        if (!IsResting && !IsRoomJourney && MapIndex == 1) solids.AddRange(ActLevel.CreateSolids());
        solids.AddRange(layout.Solids);
        solids.AddRange(layout.MovingPlatforms.Select(p => p.Bounds));
        enemies.Clear();
        if (!IsResting && !IsRoomJourney && MapIndex == 1)
        {
            enemies.AddRange(ActLevel.CreateEnemies());
            enemies.InsertRange(enemies.Count - 1, layout.Enemies);
        }
        else enemies.AddRange(layout.Enemies);
        foreach (ActEnemy enemy in enemies.Where(e => e.Kind == ActEnemyKind.Boss))
            ActBossCatalog.Configure(enemy, MapIndex);
        supplies.Clear();
        supplies.AddRange(!IsResting && !IsRoomJourney && MapIndex == 1 ? ActLevel.CreateSupplies() : layout.Supplies);
        ApplyChapterPressure();
        // Keep the teaching-map camps clear of the old decorative platform islands.
        if (MapIndex == 1) solids.RemoveAll(solid => solid.Top < ActTuning.GroundY &&
            supplies.Any(supply => solid.Right > supply.Left && solid.Left < supply.Right));
        DefeatedEnemies = 0;
        RunTime = 0f;
        DashRemaining = DashCooldownRemaining = InvulnerabilityRemaining = 0f;
        HurtPoseRemaining=0;
        jumpBuffer = coyoteTime = stompJumpLockRemaining = 0f;
        jumpCut = false;
        cameraY = MapIndex == 1 ? 0f : layout.SpawnFeetY - ActTuning.GroundY;
        safeX = PlayerX;
        safeFeetY = layout.SpawnFeetY;
        ResetCombat();
        ResetEnemies();
        ResetFeedback();
        ResetRewards();
        IsComplete = false;
        Run.Chapter = MapIndex;
        if(!IsResting && !IsRoomAntechamber)
            Run.EnterRoom($"{MapIndex}:{Run.RoomJourney?.Depth}:{Run.RoomJourney?.Stage}:{Run.RoomJourney?.CurrentRoomId}:{Seed}");
        ResetRestTravel();
        ClearInput();
    }

    public void ClearInput()
    {
        LeftHeld = RightHeld = JumpHeld = false;
        JumpRequested = DashRequested = AttackRequested = false;
        TailRequested = StompRequested = RangedRequested = false;
        BattleArt1Requested = BattleArt2Requested = false;
        InteractRequested = false;
        FormStepRequested=0;FormMenuRequested=false;
        jumpBuffer = 0f;
        attackBuffer = 0f;
    }

    public void Update(float dt)
    {
        if (dt <= 0f || IsComplete || PendingPowerReward != ActRewardChoice.None) return;
        dt = Math.Min(dt, .05f);
        if (IsDead)
        {
            AnimationClock += dt;
            UpdateFeedback(dt);
            return;
        }
        UpdateFeedback(dt);
        if (HitStopRemaining > 0f) return;
        RunTime += dt;
        AnimationClock += dt;
        InvulnerabilityRemaining = Math.Max(0f, InvulnerabilityRemaining - dt);
        HurtPoseRemaining=Math.Max(0,HurtPoseRemaining-dt);
        DashCooldownRemaining = Math.Max(0f, DashCooldownRemaining - dt);
        stompJumpLockRemaining = Math.Max(0f, stompJumpLockRemaining - dt);
        UpdateRewardTimers(dt);
        UpdatePowerTimers(dt);
        HandleFormInput();
        if (IsResting || IsRoomAntechamber) AttackRequested = TailRequested = StompRequested = RangedRequested =
            BattleArt1Requested = BattleArt2Requested = false;
        if (BattleArt1Requested) UseBattleArt(0);
        if (BattleArt2Requested) UseBattleArt(1);
        BattleArt1Requested = BattleArt2Requested = false;
        UpdateCombatTimers(dt);
        UpdateEnvironmentPlatforms(dt);
        if (stompJumpLockRemaining > 0f) jumpBuffer = 0f;
        else if (JumpRequested) jumpBuffer = .11f;
        else jumpBuffer = Math.Max(0f, jumpBuffer - dt);
        if (Grounded) coyoteTime = .08f;
        else coyoteTime = Math.Max(0f, coyoteTime - dt);

        int direction = (RightHeld ? 1 : 0) - (LeftHeld ? 1 : 0);
        if (DashRequested && DashCooldownRemaining <= 0f)
        {
            DashRemaining = ActTuning.DashDuration * Run.Resolve(PowerStat.DashDistance,1);
            DashCooldownRemaining = ActTuning.DashCooldown * Run.Resolve(PowerStat.DashCooldown,1);
            if (direction != 0) Facing = direction;
            CancelCombat();
            BeginBuildDash();
        }
        HandleCombatInput();
        JumpRequested = DashRequested = AttackRequested = false;
        TailRequested = StompRequested = RangedRequested = false;

        if (DashRemaining > 0f)
        {
            VelocityX = Facing * BuildDashSpeed;
            DashRemaining = Math.Max(0f, DashRemaining - dt);
        }
        else
        {
            float hunt = Run.HuntRemaining > 0f
                ? ActPowerCatalog.Art(BattleArtId.HuntingInstinct).Strength : 1f;
            float target = direction * Run.Resolve(PowerStat.MoveSpeed,ActTuning.MoveSpeed) * MoveSpeedMultiplier * hunt;
            float rate = direction == 0 ? ActTuning.Braking : ActTuning.Acceleration;
            if(!Grounded)rate*=Run.Resolve(PowerStat.AirControl,1);
            if (direction != 0 && Math.Sign(VelocityX) != direction) rate *= 1.35f;
            VelocityX = Approach(VelocityX, target, rate * dt);
            if (direction != 0) Facing = direction;
        }
        MoveHorizontal(VelocityX * dt);
        UpdateRoomProgress();

        if (jumpBuffer > 0f && coyoteTime > 0f && DashRemaining <= 0f &&
            stompJumpLockRemaining <= 0f && !Stomping)
        {
            VelocityY = -ActTuning.JumpSpeed *
                MathF.Sqrt(Run.Resolve(PowerStat.JumpHeight, 1f));
            Grounded = false;
            coyoteTime = jumpBuffer = 0f;
            jumpCut = false;
        }
        if (!JumpHeld && VelocityY < 0f && !jumpCut)
        {
            VelocityY *= .48f;
            jumpCut = true;
        }
        if (Stomping) VelocityY = Math.Max(VelocityY, 850f);
        if (DashRemaining <= 0f) VelocityY += ActTuning.Gravity * dt;
        MoveVertical(VelocityY * dt);
        if (UsesTerrain)
        {
            if (PlayerY > (MapIndex >= 3 ? 720f : 475f)) RescueFromPit();
            if (Grounded) RememberSafeFooting();
            UpdateCameraHeight(dt);
        }
        if (IsResting || IsRoomAntechamber)
        {
            UpdateRest(dt);
            UpdatePose(dt);
            UpdateExitCountdown(dt);
            return;
        }
        UpdateBoneDrops(dt);
        InteractRequested = false;
        ActivateAmbushRooms();
        if (Stomping && (Grounded ||
            enemies.Any(enemy => enemy.State != ActEnemyState.Dead && !enemy.Dormant &&
                PlayerBounds.IntersectsWith(enemy.Bounds))))
            ImpactStomp();
        UpdateBuildCombat(dt);
        UpdateCombatHits(dt);
        UpdateProjectiles(dt);
        UpdateEnemy(dt);
        UpdateEnemyProjectiles(dt);
        UpdateBossHazards(dt);
        UpdateHazards();
        UpdateMechanisms(dt);
        if (!IsDead) foreach (ActSupply supply in supplies)
        {
            if (supply.Collected || (Health >= MaximumHealth &&
                RangedEnergy >= MaximumEnergy) ||
                !PlayerBounds.IntersectsWith(supply.Bounds)) continue;
            supply.Collected = true;
            Run.Heal(2);
            RangedEnergy = MaximumEnergy;
            AddImpact(supply.X, supply.Y, false);
        }
        if (!IsDead) UpdateRewards();
        if (enemies.Any(e=>e.Kind==ActEnemyKind.Boss) && Boss.State == ActEnemyState.Dead) SpawnBossEvolutionReward();
        UpdateRoomProgress();
        UpdatePose(dt);
        UpdateExitCountdown(dt);
    }

    private static float Approach(float current, float target, float amount) =>
        current < target ? Math.Min(current + amount, target) : Math.Max(current - amount, target);

    private void MoveHorizontal(float delta)
    {
        bool wasGrounded = Grounded;
        PlayerX = Math.Clamp(PlayerX + delta, 0f, WorldWidth - PlayerWidth);
        foreach (RectangleF solid in solids)
        {
            if (UsesTerrain && solid.Height <= 16f) continue;
            if (!PlayerBounds.IntersectsWith(solid)) continue;
            if (delta > 0f) PlayerX = solid.Left - PlayerWidth + 5f;
            else if (delta < 0f) PlayerX = solid.Right - 5f;
            VelocityX = 0f;
        }
        if (UsesTerrain && wasGrounded)
        {
            float? next = TerrainYAt(PlayerX + PlayerWidth * .5f);
            if (next is float y && Math.Abs(y - PlayerY - PlayerHeight) <= 8f)
            {
                PlayerY = y - PlayerHeight;
                VelocityY = 0f;
            }
            else if (!solids.Any(s => Math.Abs(s.Top - PlayerY - PlayerHeight) < 3f &&
                PlayerX + PlayerWidth / 2f >= s.Left && PlayerX + PlayerWidth / 2f < s.Right))
                Grounded = false;
        }
    }

    private void MoveVertical(float delta)
    {
        float oldTop = PlayerY;
        float oldBottom = PlayerY + PlayerHeight;
        PlayerY += delta;
        Grounded = false;
        foreach (RectangleF solid in solids)
        {
            if (PlayerX + PlayerWidth - 5f <= solid.Left || PlayerX + 5f >= solid.Right) continue;
            if (delta >= 0f && oldBottom <= solid.Top + 1f &&
                PlayerY + PlayerHeight >= solid.Top)
            {
                PlayerY = solid.Top - PlayerHeight;
                VelocityY = 0f;
                Grounded = true;
                break;
            }
            if (delta < 0f && oldTop >= solid.Bottom - 1f && PlayerY <= solid.Bottom)
            {
                if (UsesTerrain && solid.Height <= 16f) continue;
                PlayerY = solid.Bottom;
                VelocityY = 0f;
                break;
            }
        }
        if (UsesTerrain && !Grounded && delta >= 0f)
        {
            float? ground = TerrainYAt(PlayerX + PlayerWidth * .5f);
            if (ground is float y && oldBottom <= y + 3f && PlayerY + PlayerHeight >= y)
            {
                PlayerY = y - PlayerHeight;
                VelocityY = 0f;
                Grounded = true;
            }
        }
        if (!UsesTerrain && PlayerY > ActTuning.ViewHeight + 50f) HurtPlayer();
    }

    private void HurtPlayer(ActEnemy? source = null) => HurtPlayerCore(source, true);

    private void HurtPlayerCore(ActEnemy? source, bool clashable)
    {
        if (IsResting || IsRoomAntechamber || InvulnerabilityRemaining > 0f) return;
        if(DashRemaining>0){Run.NoteDodge();return;}
        if(clashable && source is not null && TryClash(source))return;
        if(TryBuildCounter())return;
        if (Run.AbsorbHit())
        {
            InvulnerabilityRemaining = .35f;
            AddImpact(PlayerX + PlayerWidth / 2f, PlayerY + 24f, true);
            return;
        }
        Run.TakeDamage(Run.Resolve(PowerStat.DamageTaken,1));
        HurtPoseRemaining=.21f;
        InvulnerabilityRemaining = Run.Resolve(PowerStat.HurtInvincibility,ActTuning.DamageInvulnerability);
        DashRemaining = 0f;
        CancelCombat();
        VelocityX = (source?.Facing ?? Facing) * 145f;
        VelocityY = -190f;
        AddImpact(PlayerX + PlayerWidth / 2f, PlayerY + 24f, true, Health == 0);
        UpdatePose(0f);
    }

    internal void PlacePlayerForTest(float x, float y)
    {
        PlayerX = x;
        PlayerY = y;
        VelocityX = VelocityY = 0f;
        Grounded = !UsesTerrain ? y + PlayerHeight >= ActTuning.GroundY :
            TerrainYAt(x + PlayerWidth * .5f) is float surface &&
            Math.Abs(y + PlayerHeight - surface) < 3f;
        if (UsesTerrain) cameraY = Math.Clamp(y + PlayerHeight - (MapIndex >= 3 ? 240f : ActTuning.GroundY),
            MapIndex >= 3 ? -270f : -150f, MapIndex >= 3 ? 240f : 170f);
    }

    internal ActEnemy AddEnemyForTest(float x)
    {
        var enemy = new ActEnemy { X = x, HomeX = x };
        enemies.Add(enemy);
        return enemy;
    }
}
