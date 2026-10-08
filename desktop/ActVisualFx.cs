using System.Drawing;

namespace DinoDash.Desktop;

internal enum FxKind { Dust, Spark, Ember, Shard, Leaf, Ring, Streak, Bone }

// One visual-only particle. Distinct from ActParticle in ActFeedback, which the
// engine owns; these are never read by gameplay code.
internal sealed class FxParticle
{
    public float X, Y, Vx, Vy, Life, MaxLife, Size, Spin, Angle, Drag, Gravity;
    public FxKind Kind;
    public Color Tone;
    public float Fade => Math.Clamp(Life / MathF.Max(.0001f, MaxLife), 0f, 1f);
}

// A frozen copy of the dinosaur silhouette, used for dash trails.
internal sealed class FxAfterimage
{
    public float X, Y, Life, MaxLife;
    public int Facing, Frame;
    public bool Compact, Airborne;
    public float Fade => Math.Clamp(Life / MathF.Max(.0001f, MaxLife), 0f, 1f);
}

// An expanding impact ring (stomp, quake, heavy hit).
internal sealed class FxShockwave
{
    public float X, Y, Life, MaxLife, Radius, Growth, Thickness;
    public bool Flat;
    public Color Tone;
    public float Fade => Math.Clamp(Life / MathF.Max(.0001f, MaxLife), 0f, 1f);
}

internal sealed class FxClash
{
    public float X,Y,Life=.32f;
}

// A swept arc following an attack (claw, tail, enemy sweep).
internal sealed class FxTrail
{
    public float X, Y, Life, MaxLife, Inner, Outer, From, To, Width;
    public Color Tone;
    public float Fade => Math.Clamp(Life / MathF.Max(.0001f, MaxLife), 0f, 1f);
}

// A purely visual copy of an enemy that just died. The engine removes dead
// enemies from play immediately; this lets the body stagger, collapse and
// fade over ~0.8s without the engine knowing it exists.
internal sealed class FxCorpse
{
    public ActEnemyKind Kind;
    public float X, Y, Width, Height, Life, MaxLife, SlideSpeed;
    public int Facing, MaxHealth;
    public BossMove Move;
    public ActBossId BossId;
    public float Elapsed => MaxLife - Life;
}

// Per-enemy bookkeeping so transitions can be detected by comparison.
internal sealed class FxEnemyWatch
{
    public int Health;
    public ActEnemyState State = ActEnemyState.Patrol;
    public BossMove BossMove;
    public bool AttackApplied;
    public float ChargeDustCooldown;
    public float HurtPunch;
    public float DeathTime;
    public float WindupPulse;
    public float SquashTime;
}

// ============================================================================
// ActVisualFx -- the whole effect layer.
//
// This component OBSERVES ActEngine and plays effects. It reads public engine
// state, compares it against the previous frame, and spawns particles, trails,
// afterimages, shockwaves, flashes and camera shake from what changed.
//
// It never writes to the engine. It holds no authority over damage, timing,
// movement or state. Deleting this file would leave the game fully playable and
// numerically identical -- only flatter.
//
// Feedback weight is centralised in PlayHit so that every damage source in the
// game speaks the same visual language:
//   light   -> small spark fan, 1px shake, short flash
//   heavy   -> spark fan + shard burst + ring, 4px shake, longer flash
//   death   -> full burst + bone shards + double ring, 7px shake
// ============================================================================
internal sealed class ActVisualFx
{
    private readonly List<FxParticle> particles = [];
    private readonly List<FxAfterimage> afterimages = [];
    private readonly List<FxShockwave> shockwaves = [];
    private readonly List<FxClash> clashes=[];
    private readonly List<FxTrail> trails = [];
    private readonly List<FxCorpse> corpses = [];
    private readonly Dictionary<ActEnemy, FxEnemyWatch> watches = new(ReferenceEqualityComparer.Instance);

    private float clock;
    private float lastRunTime;
    private int lastHealth = ActTuning.MaxHealth;
    private int lastEnergy = ActTuning.MaxRangedEnergy;
    private int lastProjectiles;
    private bool lastGrounded = true;
    private bool lastDashing;
    private bool lastStomping;
    private float lastStompImpact;
    private ActPose lastPose = ActPose.Idle;
    private float lastVelocityY;
    private float afterimageCooldown;
    private float ambientCooldown;
    private float runDustCooldown;

    // Camera shake produced by the visual layer only. The engine's own
    // ShakeRemaining is read as a trigger; this decays smoothly instead.
    private float shakeMagnitude;
    private float shakeDecay = 1f;
    private float lastEngineShake;
    private int lastClashSerial;

    public IReadOnlyList<FxParticle> Particles => particles;
    public IReadOnlyList<FxAfterimage> Afterimages => afterimages;
    public IReadOnlyList<FxShockwave> Shockwaves => shockwaves;
    public IReadOnlyList<FxClash> Clashes=>clashes;
    public IReadOnlyList<FxTrail> Trails => trails;
    public IReadOnlyList<FxCorpse> Corpses => corpses;
    public float Clock => clock;

    // Seconds since the player's HP reached zero; drives the collapse frames.
    public float DeathElapsed { get; private set; }

    // 0..1 recoil for a specific enemy, used to knock its sprite back a few
    // pixels on hit. Pure offset; the enemy's real position is untouched.
    public float EnemyPunch(ActEnemy enemy) =>
        watches.TryGetValue(enemy, out FxEnemyWatch? watch) ? watch.HurtPunch : 0f;

    // Player body deformation, consumed by the sprite animator.
    public float LandSquash { get; private set; }
    public float JumpStretch { get; private set; }
    public float HurtPunch { get; private set; }
    public float ImpactFlash { get; private set; }
    public float HeavyFlash { get; private set; }

    public float ShakeX { get; private set; }
    public float ShakeY { get; private set; }

    public void Reset()
    {
        particles.Clear();
        afterimages.Clear();
        shockwaves.Clear();
        clashes.Clear();
        trails.Clear();
        corpses.Clear();
        watches.Clear();
        clock = lastRunTime = 0f;
        DeathElapsed = 0f;
        lastHealth = ActTuning.MaxHealth;
        lastEnergy = ActTuning.MaxRangedEnergy;
        lastProjectiles = 0;
        lastGrounded = true;
        lastDashing = lastStomping = false;
        lastStompImpact = 0f;
        lastPose = ActPose.Idle;
        lastVelocityY = 0f;
        afterimageCooldown = ambientCooldown = runDustCooldown = 0f;
        shakeMagnitude = 0f;
        shakeDecay = 1f;
        lastEngineShake = 0f;
        lastClashSerial=0;
        LandSquash = JumpStretch = HurtPunch = ImpactFlash = HeavyFlash = 0f;
        ShakeX = ShakeY = 0f;
    }

    public void Update(ActEngine engine, float dt)
    {
        if (dt <= 0f) return;
        dt = Math.Min(dt, .05f);
        // A fresh run rewinds RunTime; drop everything so effects never carry over.
        if (engine.RunTime < lastRunTime - .001f) Reset();
        lastRunTime = engine.RunTime;

        // Freezing effect motion during hit-stop is what gives impacts weight.
        float motion = engine.HitStopRemaining > 0f ? dt * .06f : dt;
        clock += dt;
        DeathElapsed = engine.IsDead ? DeathElapsed + dt : 0f;

        Observe(engine, dt);
        Simulate(motion);
        Decay(dt);
        UpdateShake(dt);
    }

    // ----------------------------------------------------------------- observe

    private void Observe(ActEngine engine, float dt)
    {
        ObservePlayer(engine, dt);
        ObserveEnemies(engine, dt);
        ObserveAmbient(engine, dt);
    }

    private void ObservePlayer(ActEngine engine, float dt)
    {
        if(engine.ClashSerial!=lastClashSerial)
        {
            lastClashSerial=engine.ClashSerial;
            clashes.Add(new FxClash{X=engine.ClashX,Y=engine.ClashY});
            for(int i=0;i<18;i++)
            {
                float angle=(i+.5f)*MathF.PI*2/18f,speed=115+i%3*35;
                particles.Add(new FxParticle{X=engine.ClashX,Y=engine.ClashY,
                    Vx=MathF.Cos(angle)*speed,Vy=MathF.Sin(angle)*speed,
                    Life=.25f,MaxLife=.25f,Size=2+i%2,Kind=FxKind.Spark,
                    Tone=i%3==0?ActUiTheme.Gold:Color.White,Drag=1.8f});
            }
            AddShake(8f,6f);
            HeavyFlash=Math.Max(HeavyFlash,.65f);
        }
        float cx = engine.PlayerX + engine.PlayerWidth * .5f;
        float feet = engine.PlayerY + engine.PlayerHeight;

        // --- landing -----------------------------------------------------
        if (engine.Grounded && !lastGrounded)
        {
            float force = Math.Clamp(MathF.Abs(lastVelocityY) / 700f, .25f, 1.9f);
            LandSquash = Math.Min(1f, force);
            SpawnLandingDust(cx, feet, force);
            if (force > .7f)
            {
                AddShake(force * 2.2f, 7f);
                shockwaves.Add(new FxShockwave
                {
                    X = cx, Y = feet, MaxLife = .26f, Life = .26f,
                    Radius = 6f, Growth = 150f * force, Thickness = 2f,
                    Flat = true, Tone = ActVisualTheme.Decor
                });
            }
        }
        // --- takeoff -----------------------------------------------------
        if (!engine.Grounded && lastGrounded && engine.VelocityY < -100f)
        {
            JumpStretch = 1f;
            for (int i = 0; i < 5; i++)
                particles.Add(new FxParticle
                {
                    X = cx + (i - 2) * 6f, Y = feet - 2f,
                    Vx = (i - 2) * 26f, Vy = 24f,
                    Life = .26f, MaxLife = .26f, Size = 3f,
                    Kind = FxKind.Dust, Tone = ActVisualTheme.Decor,
                    Gravity = 90f, Drag = 2.4f
                });
        }

        // --- dash --------------------------------------------------------
        bool dashing = engine.DashRemaining > 0f;
        if (dashing)
        {
            afterimageCooldown -= dt;
            if (afterimageCooldown <= 0f)
            {
                afterimageCooldown = .028f;
                afterimages.Add(new FxAfterimage
                {
                    X = engine.PlayerX, Y = engine.PlayerY,
                    Facing = engine.Facing, Frame = (int)(engine.PoseElapsed * 16f) % 2,
                    Compact = true, Airborne = !engine.Grounded,
                    Life = .2f, MaxLife = .2f
                });
            }
            if (engine.Grounded)
                particles.Add(new FxParticle
                {
                    X = cx - engine.Facing * 16f, Y = feet - 3f,
                    Vx = -engine.Facing * 130f, Vy = -22f,
                    Life = .3f, MaxLife = .3f, Size = 4f,
                    Kind = FxKind.Dust, Tone = ActVisualTheme.Decor,
                    Gravity = 190f, Drag = 3.1f
                });
        }
        if (dashing && !lastDashing)
        {
            AddShake(1.6f, 11f);
            for (int i = 0; i < 9; i++)
            {
                float spread = (i / 8f - .5f) * 1.5f;
                particles.Add(new FxParticle
                {
                    X = cx - engine.Facing * 12f, Y = engine.PlayerY + 26f,
                    Vx = -engine.Facing * (150f + i * 14f),
                    Vy = MathF.Sin(spread) * 80f,
                    Life = .26f, MaxLife = .26f, Size = 3f,
                    Kind = FxKind.Streak, Tone = ActVisualTheme.DecorDeep,
                    Angle = engine.Facing > 0 ? 0f : MathF.PI, Drag = 2.6f
                });
            }
        }

        // --- attacks -----------------------------------------------------
        if (engine.Pose != lastPose)
        {
            switch (engine.Pose)
            {
                case ActPose.Attack1:
                case ActPose.Attack2:
                case ActPose.Attack3:
                    SpawnClawTrail(engine);
                    break;
                case ActPose.Tail:
                    SpawnTailTrail(engine, engine.PlayerX, engine.PlayerY, engine.Facing,
                        ActVisualTheme.Ink, 110f);
                    break;
            }
        }

        // --- stomp -------------------------------------------------------
        if (engine.Stomping)
        {
            // Downward smear while diving.
            if (ambientCooldown <= 0f)
                particles.Add(new FxParticle
                {
                    X = cx + (ActVisualTheme.Hash01((int)(clock * 90f), 3) - .5f) * 18f,
                    Y = engine.PlayerY + 8f,
                    Vx = 0f, Vy = -190f,
                    Life = .22f, MaxLife = .22f, Size = 3f,
                    Kind = FxKind.Streak, Tone = ActVisualTheme.DecorDeep,
                    Angle = MathF.PI * .5f, Drag = 1.4f
                });
        }
        if (engine.StompImpactRemaining > lastStompImpact && engine.StompImpactRemaining > 0f)
        {
            // Ground-slam contact: twin rings, wide dust skirt, hard shake.
            shockwaves.Add(new FxShockwave
            {
                X = cx, Y = feet, MaxLife = .34f, Life = .34f,
                Radius = 8f, Growth = 330f, Thickness = 3f,
                Flat = true, Tone = ActVisualTheme.Ink
            });
            shockwaves.Add(new FxShockwave
            {
                X = cx, Y = feet, MaxLife = .46f, Life = .46f,
                Radius = 4f, Growth = 210f, Thickness = 2f,
                Flat = true, Tone = ActVisualTheme.Decor
            });
            SpawnLandingDust(cx, feet, 2.1f);
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                particles.Add(new FxParticle
                {
                    X = cx, Y = feet - 4f,
                    Vx = side * (110f + i * 17f), Vy = -70f - i * 9f,
                    Life = .42f, MaxLife = .42f, Size = 3f + i % 3,
                    Kind = FxKind.Shard, Tone = ActVisualTheme.DecorDeep,
                    Gravity = 620f, Drag = .7f, Spin = side * 9f
                });
            }
            HeavyFlash = 1f;
            AddShake(7f, 9f);
        }

        // --- bone spike fired -------------------------------------------
        if (engine.Projectiles.Count > lastProjectiles)
        {
            float mx = engine.Facing > 0 ? engine.PlayerX + engine.PlayerWidth : engine.PlayerX;
            for (int i = 0; i < 6; i++)
                particles.Add(new FxParticle
                {
                    X = mx, Y = engine.PlayerY + 27f,
                    Vx = engine.Facing * (60f + i * 22f),
                    Vy = (i - 3f) * 34f,
                    Life = .2f, MaxLife = .2f, Size = 2f,
                    Kind = FxKind.Spark, Tone = ActVisualTheme.Ink, Drag = 3.4f
                });
            AddShake(1.1f, 13f);
        }

        // --- resource gain ------------------------------------------------
        if (engine.RangedEnergy > lastEnergy)
            for (int i = 0; i < 7; i++)
                particles.Add(new FxParticle
                {
                    X = cx, Y = engine.PlayerY + 14f,
                    Vx = (ActVisualTheme.Hash01(i, (int)(clock * 60f)) - .5f) * 90f,
                    Vy = -50f - i * 11f,
                    Life = .5f, MaxLife = .5f, Size = 3f,
                    Kind = FxKind.Ember, Tone = Color.FromArgb(236, 232, 214),
                    Gravity = -22f, Drag = 1.7f
                });

        // --- player damage ------------------------------------------------
        if (engine.Health < lastHealth)
        {
            HurtPunch = 1f;
            ImpactFlash = 1f;
            AddShake(engine.Health <= 0 ? 8.5f : 5f, engine.Health <= 0 ? 5.5f : 8f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * MathF.Tau;
                particles.Add(new FxParticle
                {
                    X = cx, Y = engine.PlayerY + 24f,
                    Vx = MathF.Cos(angle) * (120f + i % 4 * 40f),
                    Vy = MathF.Sin(angle) * (110f + i % 3 * 35f) - 50f,
                    Life = .48f, MaxLife = .48f, Size = 3f + i % 2,
                    Kind = FxKind.Shard, Tone = ActVisualTheme.Ink,
                    Gravity = 480f, Drag = 1.1f, Spin = (i % 2 == 0 ? 8f : -8f)
                });
            }
            if (engine.Health <= 0)
            {
                shockwaves.Add(new FxShockwave
                {
                    X = cx, Y = engine.PlayerY + 24f, MaxLife = .6f, Life = .6f,
                    Radius = 10f, Growth = 260f, Thickness = 3f,
                    Tone = ActVisualTheme.Ink
                });
                for (int i = 0; i < 10; i++)
                    particles.Add(new FxParticle
                    {
                        X = cx, Y = engine.PlayerY + 20f,
                        Vx = (i - 5f) * 44f, Vy = -190f - i * 13f,
                        Life = .95f, MaxLife = .95f, Size = 4f,
                        Kind = FxKind.Bone, Tone = Color.FromArgb(214, 210, 198),
                        Gravity = 540f, Drag = .5f, Spin = i % 2 == 0 ? 6f : -7f
                    });
            }
        }

        // --- running dust -------------------------------------------------
        runDustCooldown -= dt;
        if (engine.Grounded && engine.Pose == ActPose.Run && runDustCooldown <= 0f)
        {
            runDustCooldown = .085f;
            particles.Add(new FxParticle
            {
                X = cx - engine.Facing * 14f, Y = feet - 2f,
                Vx = -engine.Facing * 40f, Vy = -28f,
                Life = .32f, MaxLife = .32f, Size = 3f,
                Kind = FxKind.Dust, Tone = ActVisualTheme.Decor,
                Gravity = 60f, Drag = 2.2f
            });
        }

        lastGrounded = engine.Grounded;
        lastDashing = dashing;
        lastStomping = engine.Stomping;
        lastStompImpact = engine.StompImpactRemaining;
        lastPose = engine.Pose;
        lastHealth = engine.Health;
        lastEnergy = engine.RangedEnergy;
        lastProjectiles = engine.Projectiles.Count;
        if (engine.HitStopRemaining <= 0f) lastVelocityY = engine.VelocityY;
    }

    private void SpawnLandingDust(float cx, float feet, float force)
    {
        int count = (int)(6 + force * 7f);
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            float lane = i / 2f / MathF.Max(1f, count / 2f);
            particles.Add(new FxParticle
            {
                X = cx + side * (4f + lane * 12f), Y = feet - 2f,
                Vx = side * (55f + lane * 135f) * force,
                Vy = -20f - lane * 46f,
                Life = .3f + lane * .22f, MaxLife = .3f + lane * .22f,
                Size = 3f + i % 3, Kind = FxKind.Dust,
                Tone = ActVisualTheme.Decor, Gravity = 130f, Drag = 2.7f
            });
        }
    }

    private void SpawnClawTrail(ActEngine engine)
    {
        int stage = engine.ComboStep;
        float reach = stage == 3 ? 92f : stage == 2 ? 80f : 72f;
        float lift = stage == 2 ? -8f : stage == 3 ? -2f : 4f;
        float originX = engine.Facing > 0
            ? engine.PlayerX + 44f : engine.PlayerX + engine.PlayerWidth - 44f;
        float from = engine.Facing > 0 ? -1.15f : MathF.PI + 1.15f;
        float to = engine.Facing > 0 ? 1.0f : MathF.PI - 1.0f;
        if (stage == 2) (from, to) = (to, from);
        trails.Add(new FxTrail
        {
            X = originX, Y = engine.PlayerY + 27f + lift,
            Inner = reach * .42f, Outer = reach,
            From = from, To = to,
            Width = stage == 3 ? 15f : stage == 2 ? 11f : 9f,
            Life = stage == 3 ? .26f : .19f,
            MaxLife = stage == 3 ? .26f : .19f,
            Tone = ActVisualTheme.Ink
        });
        // Finisher throws a few sparks ahead of the swing.
        if (stage != 3) return;
        for (int i = 0; i < 8; i++)
            particles.Add(new FxParticle
            {
                X = originX + engine.Facing * reach * .8f,
                Y = engine.PlayerY + 20f + i * 3f,
                Vx = engine.Facing * (70f + i * 26f), Vy = (i - 4f) * 42f,
                Life = .24f, MaxLife = .24f, Size = 2f + i % 2,
                Kind = FxKind.Spark, Tone = ActVisualTheme.Ink, Drag = 3.2f
            });
    }

    private void SpawnTailTrail(ActEngine engine, float x, float y, int facing,
        Color tone, float reach)
    {
        // Tail sweep hits both sides, so two mirrored arcs are drawn.
        for (int side = -1; side <= 1; side += 2)
            trails.Add(new FxTrail
            {
                X = x + engine.PlayerWidth * .5f, Y = y + 30f,
                Inner = reach * .3f, Outer = reach,
                From = side > 0 ? -.85f : MathF.PI + .85f,
                To = side > 0 ? .85f : MathF.PI - .85f,
                Width = 13f, Life = .28f, MaxLife = .28f, Tone = tone
            });
        for (int i = 0; i < 10; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            particles.Add(new FxParticle
            {
                X = x + engine.PlayerWidth * .5f + side * 40f, Y = y + 34f,
                Vx = side * (90f + i * 15f), Vy = -35f - i * 7f,
                Life = .3f, MaxLife = .3f, Size = 3f,
                Kind = FxKind.Dust, Tone = ActVisualTheme.Decor,
                Gravity = 250f, Drag = 2f
            });
        }
    }

    // ----------------------------------------------------------------- enemies

    private void ObserveEnemies(ActEngine engine, float dt)
    {
        foreach (ActEnemy enemy in engine.Enemies)
        {
            if (!watches.TryGetValue(enemy, out FxEnemyWatch? watch))
                watches[enemy] = watch = new FxEnemyWatch
                {
                    Health = enemy.Health, State = enemy.State
                };
            watch.ChargeDustCooldown -= dt;
            watch.HurtPunch = Math.Max(0f, watch.HurtPunch - dt * 5.5f);
            watch.SquashTime = Math.Max(0f, watch.SquashTime - dt * 4.5f);
            if (watch.DeathTime > 0f) watch.DeathTime += dt;

            float cx = enemy.X + enemy.Width * .5f;
            float cy = enemy.Y + enemy.Height * .5f;
            float feet = enemy.Y + enemy.Height;

            // --- took damage ---------------------------------------------
            if (enemy.Health < watch.Health)
            {
                int lost = watch.Health - enemy.Health;
                watch.HurtPunch = 1f;
                watch.SquashTime = 1f;
                PlayHit(cx, cy, lost >= 2, enemy.Health <= 0,
                    enemy.Kind is ActEnemyKind.Boss or ActEnemyKind.Heavy);
            }
            // --- died ------------------------------------------------------
            if (enemy.State == ActEnemyState.Dead && watch.State != ActEnemyState.Dead)
            {
                watch.DeathTime = .0001f;
                PlayDeath(cx, cy, enemy.Kind);
                bool big = enemy.Kind is ActEnemyKind.Boss or ActEnemyKind.Heavy;
                corpses.Add(new FxCorpse
                {
                    Kind = enemy.Kind, X = enemy.X, Y = enemy.Y,
                    Width = enemy.Width, Height = enemy.Height,
                    Facing = enemy.Facing, MaxHealth = enemy.MaxHealth,
                    Move = enemy.BossMove, BossId = enemy.BossId,
                    SlideSpeed = enemy.KnockbackSpeed * .35f,
                    Life = big ? 1.3f : .8f, MaxLife = big ? 1.3f : .8f
                });
            }

            // --- windup telegraph ----------------------------------------
            if (enemy.State == ActEnemyState.Windup)
            {
                watch.WindupPulse = Math.Min(1f, watch.WindupPulse + dt * 4f);
                // Anticipation grit gathering at the feet of a grounded charger.
                if (enemy.Kind is ActEnemyKind.Heavy or ActEnemyKind.Boss &&
                    watch.ChargeDustCooldown <= 0f)
                {
                    watch.ChargeDustCooldown = .09f;
                    particles.Add(new FxParticle
                    {
                        X = cx - enemy.Facing * enemy.Width * .3f, Y = feet - 2f,
                        Vx = -enemy.Facing * 34f, Vy = -60f,
                        Life = .34f, MaxLife = .34f, Size = 3f,
                        Kind = FxKind.Dust, Tone = ActVisualTheme.Decor,
                        Gravity = 150f, Drag = 1.8f
                    });
                }
            }
            else watch.WindupPulse = Math.Max(0f, watch.WindupPulse - dt * 5f);

            // --- charging -------------------------------------------------
            bool charging = enemy.State == ActEnemyState.Attack &&
                enemy.Kind is ActEnemyKind.Heavy or ActEnemyKind.Boss &&
                !(enemy.Kind == ActEnemyKind.Boss && enemy.BossMove != BossMove.Charge);
            if (charging && watch.ChargeDustCooldown <= 0f)
            {
                watch.ChargeDustCooldown = .04f;
                for (int i = 0; i < 2; i++)
                    particles.Add(new FxParticle
                    {
                        X = cx - enemy.Facing * enemy.Width * .4f,
                        Y = feet - 3f - i * 5f,
                        Vx = -enemy.Facing * (120f + i * 60f), Vy = -45f - i * 30f,
                        Life = .4f, MaxLife = .4f, Size = 4f - i,
                        Kind = FxKind.Dust, Tone = ActVisualTheme.DecorDeep,
                        Gravity = 210f, Drag = 1.9f
                    });
            }
            // Flying dive leaves a thin air streak.
            if (enemy.Kind == ActEnemyKind.Flying &&
                enemy.State == ActEnemyState.Attack && watch.ChargeDustCooldown <= 0f)
            {
                watch.ChargeDustCooldown = .05f;
                particles.Add(new FxParticle
                {
                    X = cx, Y = cy,
                    Vx = -enemy.Facing * 90f, Vy = -40f,
                    Life = .2f, MaxLife = .2f, Size = 2f,
                    Kind = FxKind.Streak, Tone = ActVisualTheme.Decor,
                    Angle = enemy.Facing > 0 ? MathF.PI : 0f, Drag = 2.5f
                });
            }

            // --- boss moves ------------------------------------------------
            if (enemy.Kind == ActEnemyKind.Boss)
                ObserveBoss(enemy, watch, cx, feet);

            // --- new attack this frame ------------------------------------
            if (enemy.State == ActEnemyState.Windup && watch.State != ActEnemyState.Windup)
                watch.SquashTime = .6f;
            if (enemy.Kind == ActEnemyKind.Melee &&
                enemy.State == ActEnemyState.Recover && watch.State == ActEnemyState.Windup)
            {
                // Claw swipe arc on the frame the strike resolves.
                trails.Add(new FxTrail
                {
                    X = cx + enemy.Facing * 10f, Y = cy,
                    Inner = 14f, Outer = 52f,
                    From = enemy.Facing > 0 ? -1f : MathF.PI + 1f,
                    To = enemy.Facing > 0 ? .8f : MathF.PI - .8f,
                    Width = 8f, Life = .16f, MaxLife = .16f,
                    Tone = ActVisualTheme.Ink
                });
            }

            watch.Health = enemy.Health;
            watch.State = enemy.State;
            watch.BossMove = enemy.BossMove;
            watch.AttackApplied = enemy.AttackApplied;
        }
    }

    private void ObserveBoss(ActEnemy boss, FxEnemyWatch watch, float cx, float feet)
    {
        if (boss.State != ActEnemyState.Attack) return;
        bool justApplied = boss.AttackApplied && !watch.AttackApplied;
        switch (boss.BossMove)
        {
            case BossMove.Quake when justApplied:
                // Ground-quake: three rings racing outward plus a debris fan.
                for (int ring = 0; ring < 3; ring++)
                    shockwaves.Add(new FxShockwave
                    {
                        X = cx, Y = ActTuning.GroundY - 4f,
                        MaxLife = .5f + ring * .12f, Life = .5f + ring * .12f,
                        Radius = 12f + ring * 26f,
                        Growth = ActEnemyTuning.BossQuakeRange / .45f * (1f - ring * .13f),
                        Thickness = 4f - ring,
                        Flat = true,
                        Tone = ring == 0 ? ActVisualTheme.Ink : ActVisualTheme.DecorDeep
                    });
                for (int i = 0; i < 26; i++)
                {
                    float side = i % 2 == 0 ? 1f : -1f;
                    float lane = i / 26f;
                    particles.Add(new FxParticle
                    {
                        X = cx + side * lane * 210f,
                        Y = ActTuning.GroundY - 4f,
                        Vx = side * (60f + lane * 190f),
                        Vy = -150f - lane * 190f,
                        Life = .6f + lane * .3f, MaxLife = .6f + lane * .3f,
                        Size = 3f + i % 3, Kind = FxKind.Shard,
                        Tone = ActVisualTheme.DecorDeep,
                        Gravity = 700f, Drag = .5f, Spin = side * 7f
                    });
                }
                HeavyFlash = 1f;
                AddShake(9.5f, 4.5f);
                break;
            case BossMove.Tail when justApplied:
                SpawnBossTailTrail(boss, cx);
                AddShake(4.5f, 7f);
                break;
            case BossMove.Charge:
                if (watch.ChargeDustCooldown > 0f) break;
                watch.ChargeDustCooldown = .035f;
                for (int i = 0; i < 3; i++)
                    particles.Add(new FxParticle
                    {
                        X = cx - boss.Facing * boss.Width * .45f,
                        Y = feet - 3f - i * 7f,
                        Vx = -boss.Facing * (170f + i * 80f), Vy = -55f - i * 40f,
                        Life = .46f, MaxLife = .46f, Size = 5f - i,
                        Kind = FxKind.Dust, Tone = ActVisualTheme.DecorDeep,
                        Gravity = 240f, Drag = 1.7f
                    });
                break;
        }
    }

    private void SpawnBossTailTrail(ActEnemy boss, float cx)
    {
        for (int side = -1; side <= 1; side += 2)
            trails.Add(new FxTrail
            {
                X = cx, Y = boss.Y + boss.Height * .62f,
                Inner = ActEnemyTuning.BossTailRange * .28f,
                Outer = ActEnemyTuning.BossTailRange,
                From = side > 0 ? -.7f : MathF.PI + .7f,
                To = side > 0 ? .75f : MathF.PI - .75f,
                Width = 20f, Life = .32f, MaxLife = .32f,
                Tone = ActVisualTheme.Ink
            });
        for (int i = 0; i < 16; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            particles.Add(new FxParticle
            {
                X = cx + side * (40f + i * 8f), Y = ActTuning.GroundY - 8f,
                Vx = side * (130f + i * 14f), Vy = -80f - i * 10f,
                Life = .44f, MaxLife = .44f, Size = 3f + i % 3,
                Kind = FxKind.Shard, Tone = ActVisualTheme.DecorDeep,
                Gravity = 580f, Drag = .9f, Spin = side * 8f
            });
        }
    }

    // ------------------------------------------------------- unified hit feel

    // Every damage event in the game routes through here, which is what makes a
    // claw, a spike and a stomp feel like the same game.
    private void PlayHit(float x, float y, bool heavy, bool lethal, bool armoured)
    {
        if (lethal) return; // PlayDeath covers it, avoids double-spending shake.
        ImpactFlash = Math.Max(ImpactFlash, heavy ? 1f : .6f);
        if (heavy) HeavyFlash = Math.Max(HeavyFlash, .8f);
        AddShake(heavy ? 4f : 1.5f, heavy ? 8f : 13f);

        int sparks = heavy ? 14 : 8;
        for (int i = 0; i < sparks; i++)
        {
            float angle = (i / (float)sparks - .5f) * 2.4f;
            float speed = (heavy ? 190f : 130f) + i % 4 * 42f;
            particles.Add(new FxParticle
            {
                X = x, Y = y,
                Vx = MathF.Cos(angle) * speed * (i % 2 == 0 ? 1f : -1f),
                Vy = MathF.Sin(angle) * speed - 40f,
                Life = heavy ? .3f : .2f, MaxLife = heavy ? .3f : .2f,
                Size = heavy ? 3f : 2f,
                Kind = FxKind.Spark, Tone = ActVisualTheme.Ink, Drag = 3.6f
            });
        }
        if (heavy)
        {
            shockwaves.Add(new FxShockwave
            {
                X = x, Y = y, MaxLife = .24f, Life = .24f,
                Radius = 5f, Growth = 260f, Thickness = 3f,
                Tone = ActVisualTheme.Ink
            });
            for (int i = 0; i < 7; i++)
                particles.Add(new FxParticle
                {
                    X = x, Y = y,
                    Vx = (i - 3f) * 58f, Vy = -120f - i * 16f,
                    Life = .5f, MaxLife = .5f, Size = 3f,
                    Kind = FxKind.Shard, Tone = ActVisualTheme.DecorDeep,
                    Gravity = 560f, Drag = .8f, Spin = i % 2 == 0 ? 7f : -7f
                });
        }
        // Armoured targets throw chips instead of soft sparks.
        if (!armoured) return;
        for (int i = 0; i < 5; i++)
            particles.Add(new FxParticle
            {
                X = x, Y = y,
                Vx = (i - 2f) * 74f, Vy = -60f - i * 22f,
                Life = .4f, MaxLife = .4f, Size = 2f,
                Kind = FxKind.Spark, Tone = Color.FromArgb(232, 228, 214),
                Gravity = 420f, Drag = 1.4f
            });
    }

    private void PlayDeath(float x, float y, ActEnemyKind kind)
    {
        bool big = kind is ActEnemyKind.Boss or ActEnemyKind.Heavy;
        ImpactFlash = 1f;
        HeavyFlash = 1f;
        AddShake(big ? 9f : 5.5f, big ? 4f : 7f);
        shockwaves.Add(new FxShockwave
        {
            X = x, Y = y, MaxLife = big ? .7f : .44f, Life = big ? .7f : .44f,
            Radius = 8f, Growth = big ? 420f : 280f, Thickness = big ? 4f : 3f,
            Tone = ActVisualTheme.Ink
        });
        shockwaves.Add(new FxShockwave
        {
            X = x, Y = y, MaxLife = big ? .9f : .6f, Life = big ? .9f : .6f,
            Radius = 3f, Growth = big ? 250f : 170f, Thickness = 2f,
            Tone = ActVisualTheme.DecorDeep
        });
        int burst = big ? 30 : 18;
        for (int i = 0; i < burst; i++)
        {
            float angle = i / (float)burst * MathF.Tau;
            float speed = (big ? 210f : 150f) + i % 5 * 34f;
            particles.Add(new FxParticle
            {
                X = x, Y = y,
                Vx = MathF.Cos(angle) * speed,
                Vy = MathF.Sin(angle) * speed - 70f,
                Life = .6f + i % 4 * .14f, MaxLife = .6f + i % 4 * .14f,
                Size = 3f + i % 3, Kind = FxKind.Shard,
                Tone = ActVisualTheme.Ink,
                Gravity = 560f, Drag = .9f, Spin = i % 2 == 0 ? 9f : -9f
            });
        }
        // Bones scatter -- the one flourish that says "this one is finished".
        int bones = big ? 9 : 5;
        for (int i = 0; i < bones; i++)
            particles.Add(new FxParticle
            {
                X = x, Y = y,
                Vx = (i - bones * .5f) * 62f, Vy = -200f - i * 17f,
                Life = 1.1f, MaxLife = 1.1f, Size = 4f,
                Kind = FxKind.Bone, Tone = Color.FromArgb(216, 212, 200),
                Gravity = 620f, Drag = .4f, Spin = i % 2 == 0 ? 6f : -8f
            });
    }

    // ----------------------------------------------------------------- ambient

    // Drifting motes and leaves. Spawned near the camera so density stays even
    // regardless of how fast the player is travelling.
    private void ObserveAmbient(ActEngine engine, float dt)
    {
        ambientCooldown -= dt;
        if (ambientCooldown > 0f) return;
        ambientCooldown = .2f;
        float camera = engine.CameraX;
        int tick = (int)(clock * 5f);
        for (int i = 0; i < 2; i++)
        {
            float x = camera + ActVisualTheme.Hash01(tick, i * 7) * ActTuning.ViewWidth;
            float y = 40f + ActVisualTheme.Hash01(tick, i * 11 + 3) * 230f;
            bool leaf = ActVisualTheme.Hash01(tick, i * 13 + 5) > .62f;
            particles.Add(new FxParticle
            {
                X = x, Y = y,
                Vx = -14f - ActVisualTheme.Hash01(tick, i * 17) * 22f,
                Vy = leaf ? 16f : -5f,
                Life = leaf ? 3.4f : 2.6f, MaxLife = leaf ? 3.4f : 2.6f,
                Size = leaf ? 4f : 2f,
                Kind = leaf ? FxKind.Leaf : FxKind.Ember,
                Tone = leaf ? ActVisualTheme.Grove : Color.FromArgb(226, 220, 204),
                Spin = leaf ? 2.4f : 0f
            });
        }
    }

    // ---------------------------------------------------------------- simulate

    private void Simulate(float dt)
    {
        for(int i=clashes.Count-1;i>=0;i--)
        {clashes[i].Life-=dt;if(clashes[i].Life<=0)clashes.RemoveAt(i);}
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            FxParticle p = particles[i];
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Vy += p.Gravity * dt;
            if (p.Drag > 0f)
            {
                float damp = MathF.Max(0f, 1f - p.Drag * dt);
                p.Vx *= damp;
                p.Vy *= damp;
            }
            // Leaves flutter rather than fall straight.
            if (p.Kind == FxKind.Leaf)
                p.Vx += MathF.Sin(clock * 2.1f + p.Y * .05f) * 22f * dt;
            p.Angle += p.Spin * dt;
            p.Life -= dt;
            if (p.Life <= 0f) particles.RemoveAt(i);
        }
        for (int i = afterimages.Count - 1; i >= 0; i--)
        {
            afterimages[i].Life -= dt;
            if (afterimages[i].Life <= 0f) afterimages.RemoveAt(i);
        }
        for (int i = shockwaves.Count - 1; i >= 0; i--)
        {
            FxShockwave wave = shockwaves[i];
            wave.Radius += wave.Growth * dt;
            wave.Life -= dt;
            if (wave.Life <= 0f) shockwaves.RemoveAt(i);
        }
        for (int i = trails.Count - 1; i >= 0; i--)
        {
            trails[i].Life -= dt;
            if (trails[i].Life <= 0f) trails.RemoveAt(i);
        }
        for (int i = corpses.Count - 1; i >= 0; i--)
        {
            FxCorpse corpse = corpses[i];
            corpse.X += corpse.SlideSpeed * dt;
            corpse.SlideSpeed *= MathF.Max(0f, 1f - 6f * dt);
            // Flyers drop out of the sky onto the ground line.
            if (corpse.Kind == ActEnemyKind.Flying)
                corpse.Y = MathF.Min(ActTuning.GroundY - corpse.Height,
                    corpse.Y + 260f * corpse.Elapsed * dt * 4f);
            corpse.Life -= dt;
            if (corpse.Life <= 0f) corpses.RemoveAt(i);
        }
        // Hard ceiling so a long fight can never accumulate cost.
        if (particles.Count > 620) particles.RemoveRange(0, particles.Count - 620);
    }

    private void Decay(float dt)
    {
        LandSquash = Math.Max(0f, LandSquash - dt * 6.5f);
        JumpStretch = Math.Max(0f, JumpStretch - dt * 7f);
        HurtPunch = Math.Max(0f, HurtPunch - dt * 4.5f);
        ImpactFlash = Math.Max(0f, ImpactFlash - dt * 7.5f);
        HeavyFlash = Math.Max(0f, HeavyFlash - dt * 4.2f);
    }

    // Reads the engine's shake trigger, then runs its own smooth decay so the
    // camera no longer flips between two fixed offsets.
    private void UpdateShake(float dt)
    {
        shakeMagnitude = Math.Max(0f, shakeMagnitude - shakeDecay * 60f * dt * .18f);
        shakeMagnitude *= MathF.Max(0f, 1f - shakeDecay * dt);
        if (shakeMagnitude < .05f)
        {
            shakeMagnitude = 0f;
            ShakeX = ShakeY = 0f;
            return;
        }
        // Two out-of-phase sines read as a physical knock rather than jitter.
        float t = clock * 46f;
        ShakeX = MathF.Sin(t) * shakeMagnitude;
        ShakeY = MathF.Sin(t * 1.63f + 1.1f) * shakeMagnitude * .58f;
    }

    public void NoteEngineShake(float engineShake)
    {
        // A rising engine shake value means the engine registered an impact we
        // have not already covered; match it so nothing goes unfelt.
        if (engineShake > lastEngineShake + .001f)
        {
            float strength = engineShake > .25f ? 6f : engineShake > .15f ? 4f : 1.6f;
            AddShake(strength, engineShake > .25f ? 5f : 9f);
        }
        lastEngineShake = engineShake;
    }

    private void AddShake(float magnitude, float decay)
    {
        if (magnitude <= shakeMagnitude) return;
        shakeMagnitude = magnitude;
        shakeDecay = decay;
    }
}
