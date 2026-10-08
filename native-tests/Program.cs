using DinoDash.Desktop;

int passed = 0;
void Check(string name, Action action)
{
    try { action(); }
    catch (Exception error) { Console.Error.WriteLine($"FAIL {name}\n{error}"); Environment.Exit(1); }
    passed++;
    Console.WriteLine($"PASS {name}");
}
void Assert(bool condition, string message = "assertion failed")
{
    if (!condition) throw new InvalidOperationException(message);
}
void Run(GameEngine engine, float seconds)
{
    int steps = (int)MathF.Ceiling(seconds / GameEngine.FixedStep);
    for (int i = 0; i < steps && !engine.IsGameOver; i++) engine.Update(GameEngine.FixedStep);
}
void Hit(GameEngine engine)
{
    engine.SetTestObstacles(new[] { new Obstacle
    {
        Kind = ObstacleKind.Ground, Level = 0, X = engine.DinoX + 18f,
        Y = GameEngine.GroundY - 30f, Width = 28f, Height = 30f
    }});
    engine.Update(GameEngine.FixedStep);
}

Check("reset and configured stage speed", () =>
{
    var engine = new GameEngine(1);
    Assert(engine.Score == 0 && engine.Stage == 1 && engine.Speed == GameEngine.BaseSpeed);
    Assert(engine.Health == GameEngine.MaxHealth && !engine.IsGameOver);
    for (int index = 0; index < 100; index++)
    {
        float distance = index * GameEngine.SpeedStageMeters * 10f;
        Assert(GameEngine.StageAtDistance(distance) == index + 1);
    }
    (int Meters, float Speed)[] expectedSpeeds =
    {
        (0, 350f), (200, 380f), (1800, 620f), (2000, 640f),
        (4800, 920f), (5000, 930f), (7800, 1070f),
        (8000, 1075f), (8200, 1080f)
    };
    foreach (var (meters, speed) in expectedSpeeds)
    {
        float distance = meters * 10f;
        Assert(GameEngine.SpeedAtDistance(distance) == speed,
            $"speed at {meters}m should be {speed}");
        if (meters > 0)
            Assert(GameEngine.SpeedAtDistance(distance - 1f) < speed,
                $"speed changed before {meters}m");
    }
});

Check("score uses distance tiers without exposing distance as points", () =>
{
    var engine = new GameEngine(11);
    engine.SetTestObstacles(Array.Empty<Obstacle>());
    engine.SetTestRewardTimes(double.MaxValue, double.MaxValue);
    while (engine.Distance < 5_000f) engine.Update(GameEngine.FixedStep);
    Assert(engine.Score >= 500 && engine.Score < 502, $"first tier score {engine.Score}");
    Assert(Math.Abs(engine.ScoreMultiplier - 1.2f) < .001f);
    while (engine.Distance < 10_000f) engine.Update(GameEngine.FixedStep);
    Assert(engine.Score >= 1_100 && engine.Score < 1_102, $"second tier score {engine.Score}");
    Assert(Math.Abs(engine.ScoreMultiplier - 1.4f) < .001f);
});

Check("three hits end run while repeated contact is ignored for one second", () =>
{
    var engine = new GameEngine(12);
    Obstacle hit() => new()
    {
        Kind = ObstacleKind.Ground, Level = 0, X = engine.DinoX + 18f,
        Y = GameEngine.GroundY - 30f, Width = 28f, Height = 30f
    };
    engine.SetTestObstacles(new[] { hit() });
    engine.Update(GameEngine.FixedStep);
    Assert(engine.Health == 2 && engine.InvulnerabilityRemaining > .9f && !engine.IsGameOver);
    engine.SetTestObstacles(new[] { hit() });
    Run(engine, .5f);
    Assert(engine.Health == 2, "overlapping obstacle dealt damage during invulnerability");
    engine.SetTestObstacles(Array.Empty<Obstacle>());
    Run(engine, .49f);
    engine.SetTestObstacles(new[] { hit() });
    Run(engine, .03f);
    Assert(engine.Health == 1 && !engine.IsGameOver);
    engine.SetTestObstacles(Array.Empty<Obstacle>());
    Run(engine, .99f);
    engine.SetTestObstacles(new[] { hit() });
    Run(engine, .03f);
    Assert(engine.Health == 0 && engine.IsGameOver);
    int stoppedScore = engine.Score;
    Run(engine, 1f);
    Assert(engine.Score == stoppedScore, "score kept rising after game over");
    engine.Reset();
    Assert(engine.Health == 3 && engine.Score == 0 && engine.InvulnerabilityRemaining == 0f);
});

Check("jump has no double jump", () =>
{
    var engine = new GameEngine(2) { JumpRequested = true };
    engine.Update(GameEngine.FixedStep);
    Assert(!engine.Grounded && engine.VerticalSpeed < 0);
    float first = engine.VerticalSpeed;
    engine.JumpRequested = true;
    engine.Update(GameEngine.FixedStep);
    Assert(engine.VerticalSpeed > first, "second jump changed upward velocity");
});

Check("input router handles focused-control keyboard messages", () =>
{
    const int space = 32;
    const int down = 40;
    var engine = new GameEngine(20);
    var input = new GameInputRouter();

    Assert(input.Handle(engine, space, true, false, space, down));
    Assert(engine.JumpRequested, "jump keydown was not routed");
    engine.Update(GameEngine.FixedStep);
    Assert(!engine.Grounded, "routed jump did not leave the ground");

    engine.JumpRequested = false;
    Assert(input.Handle(engine, space, true, false, space, down));
    Assert(!engine.JumpRequested, "key repeat requested another jump");
    Assert(input.Handle(engine, space, false, false, space, down));

    Assert(input.Handle(engine, down, true, false, space, down));
    Assert(engine.CrouchHeld, "crouch keydown was not held");
    engine.Update(GameEngine.FixedStep);
    Assert(engine.VerticalSpeed >= GameEngine.FastFallSpeed || (engine.Grounded && engine.Crouching),
        $"routed crouch did not trigger fast fall: speed {engine.VerticalSpeed}, grounded {engine.Grounded}");
    Assert(input.Handle(engine, down, false, false, space, down));
    Assert(!engine.CrouchHeld, "crouch keyup was not routed");

    Assert(!input.Handle(engine, 65, true, false, space, down), "unbound key was captured");
});

Check("fast fall works during ascent and lands crouching", () =>
{
    var normal = new GameEngine(3) { JumpRequested = true };
    normal.Update(GameEngine.FixedStep);
    float normalTime = 0f;
    while (!normal.Grounded && normalTime < 3f) { normal.Update(GameEngine.FixedStep); normalTime += GameEngine.FixedStep; }

    var fast = new GameEngine(3) { JumpRequested = true };
    fast.Update(GameEngine.FixedStep);
    Run(fast, .1f);
    fast.CrouchHeld = true;
    fast.Update(GameEngine.FixedStep);
    Assert(fast.VerticalSpeed >= GameEngine.FastFallSpeed);
    float fastTime = .1f + GameEngine.FixedStep;
    while (!fast.Grounded && fastTime < 3f) { fast.Update(GameEngine.FixedStep); fastTime += GameEngine.FixedStep; }
    Assert(fast.Grounded && fast.Crouching);
    Assert(fastTime < normalTime * .6f, $"fast {fastTime}, normal {normalTime}");
    fast.CrouchHeld = false;
    fast.Update(GameEngine.FixedStep);
    Assert(!fast.Crouching);
});

Check("three flying heights match standing and crouching rules", () =>
{
    for (int level = 0; level < 3; level++)
    {
        float[] y = { 249f, 226f, 170f };
        var standing = new GameEngine(4);
        standing.SetTestObstacles(new[] { new Obstacle
        {
            Kind = ObstacleKind.Flying, Level = level, X = 108f, Y = y[level], Width = 48f, Height = level == 0 ? 28f : 30f
        }});
        standing.Update(GameEngine.FixedStep);
        Assert((standing.Health == 2) == (level < 2), $"standing level {level}");

        var crouching = new GameEngine(4) { CrouchHeld = true };
        crouching.SetTestObstacles(new[] { new Obstacle
        {
            Kind = ObstacleKind.Flying, Level = level, X = 108f, Y = y[level], Width = 48f, Height = level == 0 ? 28f : 30f
        }});
        crouching.Update(GameEngine.FixedStep);
        Assert((crouching.Health == 2) == (level == 0), $"crouching level {level}");
    }
});

Check("mixed obstacle gaps include close clusters and get denser with distance", () =>
{
    var engine = new GameEngine(967);
    var previous = new Obstacle { Kind = ObstacleKind.Flying, Level = 1, X = 0, Y = 226, Width = 48, Height = 30 };
    var gaps = Enumerable.Range(0, 12000).Select(_ => engine.NextObstacleGap(previous)).ToArray();
    double mean = gaps.Average();
    double deviation = Math.Sqrt(gaps.Average(gap => Math.Pow(gap - mean, 2)));
    int earlyCloseCount = gaps.Count(gap => gap < previous.Width + 45f);
    int closeStreak = 0;
    foreach (float gap in gaps)
    {
        closeStreak = gap < previous.Width + 45f ? closeStreak + 1 : 0;
        Assert(closeStreak <= 2, "cluster exceeded three obstacles");
        Assert(gap >= previous.Width + 8f, "obstacles overlapped at spawn");
    }
    Assert(earlyCloseCount is > 300 and < 900, $"early close gap count {earlyCloseCount}");
    Assert(mean / engine.Speed < 1f);
    Assert(deviation / mean > .25);
    Assert(gaps.Max() / gaps.Min() > 3f);
    engine.SetTestDistance(50_000f);
    var laterGaps = Enumerable.Range(0, 12000).Select(_ => engine.NextObstacleGap(previous)).ToArray();
    int laterCloseCount = laterGaps.Count(gap => gap < previous.Width + 45f);
    Assert(laterCloseCount > earlyCloseCount * 2.5, "late close clusters did not become more common");
    Assert(laterGaps.Average() / engine.Speed < mean / GameEngine.BaseSpeed * .9d,
        "distance did not increase obstacle frequency");
});

Check("extreme speed does not tunnel through obstacle", () =>
{
    var engine = new GameEngine(5);
    engine.SetTestDistance(100_000_000f);
    engine.SetTestObstacles(new[] { new Obstacle
    {
        Kind = ObstacleKind.Ground, Level = 0, X = 260f, Y = GameEngine.GroundY - 30f, Width = 28f, Height = 30f
    }});
    engine.Update(GameEngine.FixedStep);
    Assert(engine.Health == 2);
});

Check("reward timing, pause, slots, replacement, and relic priority", () =>
{
    var engine = new GameEngine(42);
    engine.SetTestObstacles(Array.Empty<Obstacle>());
    while (engine.PendingReward == RewardKind.None && engine.RunTime < 61d)
        engine.Update(GameEngine.FixedStep);
    Assert(engine.PendingReward == RewardKind.Card && engine.RunTime is >= 10d and <= 25.1d);
    Assert(engine.CardOffers.Count == 3 && engine.CardOffers.Distinct().Count() == 3);
    float frozenDistance = engine.Distance;
    engine.Update(5f);
    Assert(engine.Distance == frozenDistance, "reward did not pause physics");
    Assert(engine.ChooseCard(0) && engine.Cards.Count == 1);

    engine.SetTestRewardTimes(engine.RunTime + .02d, double.MaxValue);
    Run(engine, .03f);
    Assert(engine.PendingReward == RewardKind.Card);
    Assert(engine.ChooseCard(1) && engine.Cards.Count == 2);

    engine.SetTestRewardTimes(engine.RunTime + .02d, double.MaxValue);
    Run(engine, .03f);
    Assert(!engine.ChooseCard(0), "full slots accepted a card without replacement");
    Assert(engine.DeclineCard() && engine.Cards.Count == 2);

    engine.SetTestRewardTimes(engine.RunTime + .02d, double.MaxValue);
    Run(engine, .03f);
    CardType offered = engine.CardOffers[2];
    Assert(engine.ChooseCard(2, 0) && engine.Cards.Count == 2 && engine.Cards[0] == offered);

    var simultaneous = new GameEngine(43);
    simultaneous.SetTestObstacles(Array.Empty<Obstacle>());
    simultaneous.SetTestRewardTimes(.1d, .1d);
    Run(simultaneous, .2f);
    Assert(simultaneous.PendingReward == RewardKind.Relic && simultaneous.CardOffers.Count == 0);
    Assert(simultaneous.RelicOffers.Count == 3 && simultaneous.RelicOffers.Distinct().Count() == 3);
    Assert(simultaneous.ChooseRelic(0) && simultaneous.Relics.Count == 1);
});

Check("consumable card effects and durations", () =>
{
    var healing = new GameEngine(44);
    Hit(healing);
    healing.SetTestCards(CardType.HealingHerb);
    Assert(healing.UseCard(0) && healing.Health == 3 && healing.Cards.Count == 0);

    var shield = new GameEngine(45);
    shield.SetTestCards(CardType.Shield);
    Assert(shield.UseCard(0) && shield.ShieldCharges == 1);
    Hit(shield);
    Assert(shield.Health == 3 && shield.ShieldCharges == 0 && shield.InvulnerabilityRemaining > 0f);

    var slow = new GameEngine(46);
    slow.SetTestObstacles(Array.Empty<Obstacle>());
    slow.SetTestCards(CardType.SlowTime);
    Assert(slow.UseCard(0));
    Run(slow, 1f);
    Assert(slow.Distance is > 205f and < 220f, $"slow distance {slow.Distance}");
    Run(slow, 3.1f);
    Assert(slow.SlowTimeRemaining == 0f && Math.Abs(slow.WorldSpeed - slow.Speed) < .01f);

    var ghost = new GameEngine(47);
    ghost.SetTestCards(CardType.Ghost);
    Assert(ghost.UseCard(0));
    Hit(ghost);
    Assert(ghost.Health == 3);
    ghost.SetTestObstacles(Array.Empty<Obstacle>());
    Run(ghost, 3.1f);
    Hit(ghost);
    Assert(ghost.Health == 2);

    var baseline = new GameEngine(48);
    baseline.SetTestObstacles(Array.Empty<Obstacle>());
    Run(baseline, 1f);
    var doublePoints = new GameEngine(48);
    doublePoints.SetTestObstacles(Array.Empty<Obstacle>());
    doublePoints.SetTestCards(CardType.DoublePoints);
    Assert(doublePoints.UseCard(0));
    Run(doublePoints, 1f);
    Assert(doublePoints.Score >= baseline.Score * 2 - 1 && doublePoints.Score <= baseline.Score * 2 + 1);

    var clearing = new GameEngine(49);
    clearing.SetTestObstacles(new[]
    {
        new Obstacle { Kind = ObstacleKind.Ground, Level = 0, X = 100f, Y = 255f, Width = 28f, Height = 30f },
        new Obstacle { Kind = ObstacleKind.Flying, Level = 1, X = 900f, Y = 226f, Width = 48f, Height = 30f },
        new Obstacle { Kind = ObstacleKind.Ground, Level = 0, X = 990f, Y = 255f, Width = 28f, Height = 30f }
    });
    clearing.SetTestCards(CardType.ClearObstacles);
    Assert(clearing.UseCard(0) && clearing.Obstacles.Count == 1 && clearing.Obstacles[0].X == 990f);
});

Check("permanent relic abilities and reset", () =>
{
    var heart = new GameEngine(50);
    Hit(heart);
    heart.GrantTestRelic(RelicType.AmberHeart);
    Assert(heart.MaximumHealth == 4 && heart.Health == 3);

    var bones = new GameEngine(51);
    bones.GrantTestRelic(RelicType.LightBones);
    bones.JumpRequested = true;
    bones.Update(GameEngine.FixedStep);
    var normalJump = new GameEngine(51) { JumpRequested = true };
    normalJump.Update(GameEngine.FixedStep);
    Assert(bones.VerticalSpeed < normalJump.VerticalSpeed - 40f);

    var gold = new GameEngine(52);
    gold.SetTestObstacles(Array.Empty<Obstacle>());
    gold.GrantTestRelic(RelicType.GoldenFossil);
    Run(gold, 1f);
    var plain = new GameEngine(52);
    plain.SetTestObstacles(Array.Empty<Obstacle>());
    Run(plain, 1f);
    Assert(gold.Score >= plain.Score * 1.15f - 1f);

    var crown = new GameEngine(53);
    Hit(crown);
    crown.SetTestObstacles(Array.Empty<Obstacle>());
    Run(crown, 1.01f);
    Hit(crown);
    Assert(crown.Health == 1);
    crown.SetTestObstacles(Array.Empty<Obstacle>());
    crown.GrantTestRelic(RelicType.BrokenCrown);
    Assert(Math.Abs(crown.ScoreMultiplier - GameEngine.MultiplierAtDistance(crown.Distance) * 1.4f) < .001f);

    var egg = new GameEngine(54);
    egg.GrantTestRelic(RelicType.AmberHeart);
    egg.GrantTestRelic(RelicType.LuckyEggshell);
    for (int index = 0; index < 3; index++)
    {
        Hit(egg);
        Assert(egg.Health == 3 - index);
        egg.SetTestObstacles(Array.Empty<Obstacle>());
        Run(egg, 1.01f);
    }
    Assert(egg.EggProtectionCharges == 1);
    Hit(egg);
    Assert(egg.Health == 1 && egg.EggProtectionCharges == 0);
    egg.Reset();
    Assert(egg.Health == 3 && egg.MaximumHealth == 3 && egg.Relics.Count == 0 &&
        egg.Cards.Count == 0 && egg.EggProtectionCharges == 0);
});

void ActRun(ActEngine game, float seconds)
{
    int steps = (int)MathF.Ceiling(seconds / ActTuning.Step);
    for (int i = 0; i < steps && !game.IsDead && !game.IsComplete; i++)
        game.Update(ActTuning.Step);
}

Check("ACT starts still, accelerates under input, and camera follows", () =>
{
    var game = new ActEngine();
    float start = game.PlayerX;
    ActRun(game, 1f);
    Assert(game.PlayerX == start && game.Health == 5);
    game.RightHeld = true;
    ActRun(game, 3f);
    Assert(game.PlayerX > start + 600f && game.CameraX > 0f);
    game.RightHeld = false;
    float release = game.PlayerX;
    ActRun(game, .3f);
    Assert(game.PlayerX > release && game.VelocityX == 0f);
});

Check("ACT variable jump and platform landing", () =>
{
    var shortJump = new ActEngine { JumpHeld = true, JumpRequested = true };
    shortJump.Update(ActTuning.Step);
    shortJump.JumpHeld = false;
    float shortTop = shortJump.PlayerY;
    for (int i = 0; i < 100; i++)
    {
        shortJump.Update(ActTuning.Step);
        shortTop = Math.Min(shortTop, shortJump.PlayerY);
    }
    var longJump = new ActEngine { JumpHeld = true, JumpRequested = true };
    float longTop = longJump.PlayerY;
    for (int i = 0; i < 100; i++)
    {
        longJump.Update(ActTuning.Step);
        longTop = Math.Min(longTop, longJump.PlayerY);
    }
    Assert(longTop < shortTop - 50f, $"short {shortTop}, long {longTop}");
    var landing = new ActEngine();
    landing.PlacePlayerForTest(490f, 100f);
    ActRun(landing, 1f);
    Assert(landing.Grounded && Math.Abs(landing.PlayerY - 170f) < .1f,
        $"platform landing {landing.PlayerY}");
});

Check("ACT bindings allow Shift with movement and suppress repeat", () =>
{
    var game = new ActEngine();
    var bindings = new ActBindings();
    var router = new ActInputRouter();
    Assert(router.Handle(game, bindings, bindings.Right, true));
    Assert(router.Handle(game, bindings, bindings.Dash, true));
    Assert(game.RightHeld && game.DashRequested);
    game.Update(ActTuning.Step);
    Assert(game.DashRemaining > 0f && game.VelocityX > ActTuning.MoveSpeed);
    game.DashRequested = false;
    router.Handle(game, bindings, bindings.Dash, true);
    Assert(!game.DashRequested);
    router.Handle(game, bindings, bindings.Right, false);
    Assert(!game.RightHeld);
    Assert(router.Handle(game, bindings, bindings.Pause, true) && router.ConsumePause());
    Assert(!router.ConsumePause());
    router.Clear(game);
    Assert(!game.RightHeld && !game.JumpHeld);
    var duplicate = bindings.Clone();
    duplicate.Attack = duplicate.Jump;
    Assert(!duplicate.Valid());
});

Check("ACT melee lands once per swing and enemy telegraphs one damage", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(game.Enemy.X - 70f, ActTuning.GroundY - game.PlayerHeight);
    game.AttackRequested = true;
    ActRun(game, .2f);
    Assert(game.Enemy.Health == 2, $"enemy hp {game.Enemy.Health}");
    ActRun(game, .15f);
    Assert(game.Enemy.Health == 2, "one claw hit applied twice");
    var victim = new ActEngine();
    victim.PlacePlayerForTest(victim.Enemy.X - 48f, ActTuning.GroundY - victim.PlayerHeight);
    ActRun(victim, .04f);
    Assert(victim.Enemy.State == ActEnemyState.Windup && victim.Health == 5);
    ActRun(victim, .35f);
    Assert(victim.Health == 5, "enemy hit before windup ended");
    ActRun(victim, .08f);
    Assert(victim.Health == 4 && victim.InvulnerabilityRemaining > 0f);
    ActRun(victim, .7f);
    Assert(victim.Health == 4, "one enemy attack hurt more than once");
});

Check("ACT dash avoids the telegraphed hit and exit requires victory", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(game.Enemy.X - 48f, ActTuning.GroundY - game.PlayerHeight);
    game.Enemy.State = ActEnemyState.Windup;
    game.Enemy.StateTime = .415f;
    game.DashRequested = true;
    game.LeftHeld = true;
    game.Update(ActTuning.Step);
    Assert(game.Health == 5 && game.DashCooldownRemaining > 0f);
    var finish = new ActEngine();
    finish.PlacePlayerForTest(finish.ExitX, ActTuning.GroundY - finish.PlayerHeight);
    finish.Update(ActTuning.Step);
    Assert(!finish.IsComplete);
    finish.Boss.State = ActEnemyState.Dead;
    finish.Update(ActTuning.Step);
    Assert(finish.PendingPowerReward == ActRewardChoice.None && finish.BossEvolutionReward is not null);
    Assert(!finish.IsComplete);
    finish.BossEvolutionReward!.Collected = true;
    ActRun(finish, 3.1f);
    Assert(finish.IsComplete);
    finish.Reset();
    Assert(!finish.IsComplete && finish.Health == ActTuning.MaxHealth &&
        finish.Enemy.Health == 3);
});

Check("ACT enemy can be defeated through claw attacks and death resets", () =>
{
    var game = new ActEngine();
    for (int hit = 0; hit < 3; hit++)
    {
        game.PlacePlayerForTest(game.Enemy.X - 70f,
            ActTuning.GroundY - game.PlayerHeight);
        game.AttackRequested = true;
        ActRun(game, .43f);
    }
    Assert(game.Enemy.State == ActEnemyState.Dead && game.Enemy.Health == 0);

    var victim = new ActEngine();
    for (int hit = 0; hit < ActTuning.MaxHealth; hit++)
    {
        victim.PlacePlayerForTest(victim.Enemy.X - 48f,
            ActTuning.GroundY - victim.PlayerHeight);
        victim.Enemy.State = ActEnemyState.Windup;
        victim.Enemy.StateTime = .42f;
        victim.Enemy.Facing = -1;
        victim.Update(ActTuning.Step);
        Assert(victim.Health == ActTuning.MaxHealth - hit - 1);
        if (!victim.IsDead)
        {
            victim.PlacePlayerForTest(100f, ActTuning.GroundY - victim.PlayerHeight);
            ActRun(victim, 1.2f);
        }
    }
    Assert(victim.IsDead);
    victim.Reset();
    Assert(victim.Health == ActTuning.MaxHealth && !victim.IsDead);
});

Check("ACT queues a claw press during recovery", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(game.Enemy.X - 70f,
        ActTuning.GroundY - game.PlayerHeight);
    game.AttackRequested = true;
    ActRun(game, .32f);
    Assert(game.Enemy.Health == 2);
    game.AttackRequested = true;
    ActRun(game, .13f);
    Assert(game.IsAttacking, "queued claw was dropped before recovery ended");
});

Check("ACT three claw stages have distinct timing and heavy finisher", () =>
{
    var game = new ActEngine();
    game.Enemy.Health = 8;
    for (int stage = 1; stage <= 3; stage++)
    {
        game.PlacePlayerForTest(game.Enemy.X - 70f,
            ActTuning.GroundY - game.PlayerHeight);
        game.AttackRequested = true;
        game.Update(ActTuning.Step);
        Assert(game.ComboStep == stage, $"combo step {game.ComboStep}");
        Assert(game.Pose == (stage switch
        {
            1 => ActPose.Attack1, 2 => ActPose.Attack2, _ => ActPose.Attack3
        }));
        ActRun(game, .43f);
    }
    Assert(game.Enemy.Health == 4, $"combo damage left {game.Enemy.Health}");
    game.PlacePlayerForTest(100f, ActTuning.GroundY - game.PlayerHeight);
    ActRun(game, .8f);
    Assert(game.ComboStep == 0, "combo did not expire");
});

Check("ACT tail sweep hits behind and knocks the enemy back", () =>
{
    var game = new ActEngine();
    game.Enemy.Health = 5;
    game.PlacePlayerForTest(game.Enemy.X + 72f,
        ActTuning.GroundY - game.PlayerHeight);
    game.TailRequested = true;
    ActRun(game, .24f);
    Assert(game.Enemy.Health == 3 && game.Enemy.KnockbackSpeed < 0f);
    Assert(game.Pose == ActPose.Tail);
    ActRun(game, .15f);
    Assert(game.Enemy.Health == 3, "tail dealt repeated damage");
});

Check("ACT tail sweep reaches multiple nearby enemies", () =>
{
    var game = new ActEngine();
    ActEnemy second = game.AddEnemyForTest(game.Enemy.X + 30f);
    game.PlacePlayerForTest(game.Enemy.X + 80f,
        ActTuning.GroundY - game.PlayerHeight);
    game.TailRequested = true;
    ActRun(game, .26f);
    Assert(game.Enemy.Health == 1 && second.Health == 1,
        $"area hit: {game.Enemy.Health}, {second.Health}");
});

Check("ACT stomp fast-falls and causes a single landing impact", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(game.Enemy.X - 20f, 145f);
    game.StompRequested = true;
    game.Update(ActTuning.Step);
    Assert(game.Stomping && game.Pose == ActPose.Stomp);
    ActRun(game, .2f);
    Assert(!game.Stomping && game.StompImpactRemaining > 0f);
    Assert(game.Enemy.Health == 1, $"stomp damage left {game.Enemy.Health}");
    ActRun(game, .15f);
    Assert(game.Enemy.Health == 1, "stomp impact repeated");
});

Check("ACT stomp blocks and discards jump presses for half a second", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(300f, 145f);
    game.StompRequested = true;
    ActRun(game, .25f);
    Assert(game.Grounded && game.StompJumpLockRemaining > 0f,
        "landing did not start the stomp jump lock");
    game.JumpHeld = true;
    game.JumpRequested = true;
    game.Update(ActTuning.Step);
    Assert(game.Grounded && game.VelocityY >= 0f,
        "jump bypassed the stomp recovery");
    game.JumpHeld = false;
    ActRun(game, .55f);
    Assert(game.Grounded, "blocked jump was buffered until recovery ended");
    game.JumpHeld = true;
    game.JumpRequested = true;
    game.Update(ActTuning.Step);
    Assert(!game.Grounded && game.VelocityY < 0f,
        "a fresh jump was not accepted after recovery");
});

Check("ACT bone spike spends energy and melee hits recharge it", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(game.Enemy.X - 250f,
        ActTuning.GroundY - game.PlayerHeight);
    game.RangedRequested = true;
    game.Update(ActTuning.Step);
    Assert(game.RangedEnergy == 4 && game.Projectiles.Count == 1 &&
        game.Pose == ActPose.Ranged);
    ActRun(game, .6f);
    Assert(game.Enemy.Health == 2 && game.Projectiles.Count == 0);
    for (int hit = 0; hit < 2; hit++)
    {
        game.PlacePlayerForTest(game.Enemy.X - 70f,
            ActTuning.GroundY - game.PlayerHeight);
        game.AttackRequested = true;
        ActRun(game, .43f);
    }
    Assert(game.Enemy.State == ActEnemyState.Dead && game.RangedEnergy == 5);
});

Check("ACT bone spike stops at the first enemy", () =>
{
    var game = new ActEngine();
    ActEnemy second = game.AddEnemyForTest(game.Enemy.X + 80f);
    game.PlacePlayerForTest(game.Enemy.X - 260f,
        ActTuning.GroundY - game.PlayerHeight);
    game.RangedRequested = true;
    ActRun(game, .65f);
    Assert(game.Enemy.Health == 2 && second.Health == 3);
});

Check("ACT fresh claw parries a late melee attack and briefly dazes the enemy", () =>
{
    var game = new ActEngine();
    ActEnemy enemy = game.Enemy;
    game.PlacePlayerForTest(enemy.X - 66f, ActTuning.GroundY - game.PlayerHeight);
    float initialX=enemy.X;
    int health = game.Health, enemyHealth = enemy.Health;
    enemy.State = ActEnemyState.Windup;
    enemy.StateTime = ActEnemyTuning.MeleeWindup - .07f;
    game.AttackRequested = true;
    game.Update(ActTuning.Step);
    Assert(game.ClashSerial == 1 && game.Health == health,
        "claw did not cancel the incoming attack");
    Assert(enemy.Health == enemyHealth - 1 && enemy.State == ActEnemyState.Dazed,
        "clash did not deal one damage and apply daze");
    ActRun(game, .5f);
    Assert(enemy.State == ActEnemyState.Dazed && game.Health == health,
        "dazed enemy resumed its attack too soon");
    Assert(enemy.X > initialX + 8f,
        "clash did not push the enemy a short distance away");
    ActRun(game, .5f);
    Assert(enemy.State != ActEnemyState.Dazed, "daze never expired");
});

Check("ACT contact-time parry survives attack recovery transition", () =>
{
    var game = new ActEngine();
    ActEnemy enemy = game.Enemy;
    game.PlacePlayerForTest(enemy.X - 66f, ActTuning.GroundY - game.PlayerHeight);
    enemy.State = ActEnemyState.Recover;
    game.AttackRequested = true;
    game.Update(ActTuning.Step);
    Assert(game.ClashSerial == 0, "nonattacking enemy was parried");
    enemy.State = ActEnemyState.Windup;
    enemy.StateTime = ActEnemyTuning.MeleeWindup - ActTuning.Step / 2f;
    game.Update(ActTuning.Step);
    Assert(game.ClashSerial == 1 && enemy.State == ActEnemyState.Dazed &&
        game.Health == game.MaximumHealth, "contact attack bypassed the parry or cleared daze");
});

Check("ACT clash accepts a slightly earlier strike at the extended reach", () =>
{
    var game = new ActEngine();
    ActEnemy enemy = game.Enemy;
    float x=enemy.X-game.PlayerWidth-96f;
    game.PlacePlayerForTest(x,ActTuning.GroundY-game.PlayerHeight);
    enemy.State=ActEnemyState.Windup;
    enemy.StateTime=ActEnemyTuning.MeleeWindup-.2f;
    game.AttackRequested=true;
    game.Update(ActTuning.Step);
    Assert(game.ClashSerial==1 && enemy.State==ActEnemyState.Dazed,
        "extended timing and reach did not accept a valid clash");
});

Check("ACT clash requires timely forward claw and excludes boss quake", () =>
{
    var early = new ActEngine();
    early.PlacePlayerForTest(early.Enemy.X - 66f,
        ActTuning.GroundY - early.PlayerHeight);
    early.Enemy.State = ActEnemyState.Windup;
    early.Enemy.StateTime = 0f;
    early.AttackRequested = true;
    early.Update(ActTuning.Step);
    Assert(early.ClashSerial == 0, "early windup was treated as a clash");

    var behind = new ActEngine();
    behind.PlacePlayerForTest(behind.Enemy.X + behind.Enemy.Width + 16f,
        ActTuning.GroundY - behind.PlayerHeight);
    behind.Enemy.State = ActEnemyState.Windup;
    behind.Enemy.StateTime = ActEnemyTuning.MeleeWindup - .04f;
    behind.AttackRequested = true;
    behind.Update(ActTuning.Step);
    Assert(behind.ClashSerial == 0, "an attack behind the player was parried");

    var bossGame = new ActEngine();
    ActEnemy boss = bossGame.Boss;
    bossGame.PlacePlayerForTest(boss.X - 66f,
        ActTuning.GroundY - bossGame.PlayerHeight);
    boss.State = ActEnemyState.Windup;
    boss.BossMove = BossMove.Quake;
    boss.StateTime = ActEnemyTuning.BossWindup - .04f;
    bossGame.AttackRequested = true;
    bossGame.Update(ActTuning.Step);
    Assert(bossGame.ClashSerial == 0, "ground shockwave was parried as a direct strike");

    var direct = new ActEngine();
    ActEnemy directBoss = direct.Boss;
    direct.PlacePlayerForTest(directBoss.X - 66f,
        ActTuning.GroundY - direct.PlayerHeight);
    int bossHealth = directBoss.Health;
    directBoss.State = ActEnemyState.Windup;
    directBoss.BossMove = BossMove.Tail;
    directBoss.StateTime = ActEnemyTuning.BossWindup - .04f;
    direct.AttackRequested = true;
    direct.Update(ActTuning.Step);
    Assert(direct.ClashSerial == 1 && directBoss.Health == bossHealth - 1 &&
        directBoss.State == ActEnemyState.Dazed && direct.Health == direct.MaximumHealth,
        "boss direct strike did not enter brief clash recovery");
    bossGame.Reset();
    Assert(bossGame.ClashSerial == 0, "clash feedback leaked into a new run");
});

Check("ACT new actions use separate bindable inputs", () =>
{
    var bindings = new ActBindings();
    var game = new ActEngine();
    var router = new ActInputRouter();
    Assert(bindings.Valid() && bindings.Clone().Valid());
    Assert(router.Handle(game, bindings, bindings.Tail, true) && game.TailRequested);
    router.Handle(game, bindings, bindings.Tail, false);
    Assert(router.Handle(game, bindings, bindings.Stomp, true) && game.StompRequested);
    router.Handle(game, bindings, bindings.Stomp, false);
    Assert(router.Handle(game, bindings, bindings.Ranged, true) && game.RangedRequested);
    router.Clear(game);
    Assert(!game.TailRequested && !game.StompRequested && !game.RangedRequested);
});

Check("ACT bone spikes expire without returning energy", () =>
{
    var game = new ActEngine();
    game.RangedRequested = true;
    ActRun(game, 1.8f);
    Assert(game.Projectiles.Count == 0 && game.RangedEnergy == 4);
    ActRun(game, 2f);
    Assert(game.RangedEnergy == 4, "energy regenerated without melee");
});

Check("ACT bone spike disappears when it reaches a platform wall", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(650f, 166f);
    game.RangedRequested = true;
    ActRun(game, .2f);
    Assert(game.Projectiles.Count == 0 && game.RangedEnergy == 4);
    Assert(game.Enemy.Health == 3);
});

Check("ACT route keeps fixed boss, enemy types and supplies without encounter walls", () =>
{
    var game = new ActEngine();
    Assert(game.Enemies.Count is >= 14 and <= 26 &&
        game.Boss.Kind == ActEnemyKind.Boss && game.Boss.HomeX == 32700f);
    foreach (ActEnemyKind kind in Enum.GetValues<ActEnemyKind>())
        Assert(game.Enemies.Any(enemy => enemy.Kind == kind), $"missing {kind}");
    Assert(game.Supplies.Count == 4);
    const float formerGateX = 4400f;
    game.PlacePlayerForTest(formerGateX - game.PlayerWidth - 1f,
        ActTuning.GroundY - game.PlayerHeight);
    game.RightHeld = true;
    ActRun(game, .2f);
    Assert(game.PlayerX + game.PlayerWidth > formerGateX + 2f,
        "removed encounter wall still blocks the route");
});

Check("ACT ranged enemy telegraphs then fires a hittable shot", () =>
{
    var game = new ActEngine();
    ActEnemy ranged = game.Enemies.First(enemy => enemy.Kind == ActEnemyKind.Ranged);
    game.PlacePlayerForTest(ranged.X - 220f,
        ActTuning.GroundY - game.PlayerHeight);
    ranged.State = ActEnemyState.Windup;
    ranged.StateTime = ActEnemyTuning.RangedWindup - ActTuning.Step / 2f;
    game.Update(ActTuning.Step);
    Assert(game.EnemyShots.Count == 1 && ranged.State == ActEnemyState.Recover);
    ActRun(game, 1f);
    Assert(game.Health < ActTuning.MaxHealth && game.EnemyShots.Count == 0);
});

Check("ACT flying dive and heavy charge have windup and recovery", () =>
{
    var flyerGame = new ActEngine();
    ActEnemy flyer = flyerGame.Enemies.First(enemy => enemy.Kind == ActEnemyKind.Flying);
    flyerGame.PlacePlayerForTest(flyer.X - 170f,
        ActTuning.GroundY - flyerGame.PlayerHeight);
    flyer.State = ActEnemyState.Windup;
    flyer.StateTime = ActEnemyTuning.FlyingWindup - ActTuning.Step / 2f;
    flyer.TargetX = flyerGame.PlayerX;
    flyer.TargetY = flyerGame.PlayerY + 15f;
    flyerGame.Update(ActTuning.Step);
    Assert(flyer.State == ActEnemyState.Attack);
    ActRun(flyerGame, .52f);
    Assert(flyer.State == ActEnemyState.Recover);

    var heavyGame = new ActEngine();
    ActEnemy heavy = heavyGame.Enemies.First(enemy => enemy.Kind == ActEnemyKind.Heavy);
    heavyGame.PlacePlayerForTest(heavy.X - 160f,
        ActTuning.GroundY - heavyGame.PlayerHeight);
    heavy.State = ActEnemyState.Windup;
    heavy.StateTime = ActEnemyTuning.HeavyWindup - ActTuning.Step / 2f;
    heavyGame.Update(ActTuning.Step);
    Assert(heavy.State == ActEnemyState.Attack);
    ActRun(heavyGame, .6f);
    Assert(heavy.State == ActEnemyState.Recover);
});

Check("ACT boss cycles three readable attacks and unlocks the exit", () =>
{
    var game = new ActEngine();
    ActEnemy boss = game.Boss;
    game.PlacePlayerForTest(boss.X - 220f,
        ActTuning.GroundY - game.PlayerHeight);
    for (int i = 0; i < 3; i++)
    {
        boss.State = ActEnemyState.Chase;
        boss.StateTime = .48f;
        game.Update(ActTuning.Step);
        Assert(boss.State == ActEnemyState.Windup &&
            boss.BossMove == (BossMove)i, $"boss attack {i}");
        boss.StateTime = ActEnemyTuning.BossWindup;
        game.Update(ActTuning.Step);
        Assert(boss.State == ActEnemyState.Attack);
        boss.State = ActEnemyState.Recover;
    }
    boss.Health = 0;
    boss.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(game.ExitX,
        ActTuning.GroundY - game.PlayerHeight);
    game.Update(ActTuning.Step);
    Assert(game.PendingPowerReward == ActRewardChoice.None && game.BossEvolutionReward is not null);
    game.BossEvolutionReward!.Collected = true;
    Assert(game.ExitUnlocked, "boss reward did not unlock the exit");
    ActRun(game, 3.1f);
    Assert(game.IsComplete);
});

Check("ACT hit feedback pauses combat and a fixed supply heals only once", () =>
{
    var game = new ActEngine();
    game.PlacePlayerForTest(game.Enemy.X - 70f,
        ActTuning.GroundY - game.PlayerHeight);
    game.AttackRequested = true;
    ActRun(game, .1f);
    Assert(game.HitStopRemaining > 0f && game.Particles.Count > 0 &&
        game.ShakeRemaining > 0f);
    game.Enemy.State = ActEnemyState.Windup;
    game.Enemy.StateTime = ActEnemyTuning.MeleeWindup;
    game.PlacePlayerForTest(game.Enemy.X - 48f,
        ActTuning.GroundY - game.PlayerHeight);
    ActRun(game, .16f);
    Assert(game.Health == ActTuning.MaxHealth - 1);
    ActSupply supply = game.Supplies[0];
    game.Enemy.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(supply.X - 15f,
        ActTuning.GroundY - game.PlayerHeight);
    ActRun(game, .2f);
    Assert(supply.Collected && game.Health == ActTuning.MaxHealth);
    game.Reset();
    Assert(!game.Supplies[0].Collected && game.Particles.Count == 0);
});

Check("ACT boss quake hits grounded player but misses an airborne one", () =>
{
    var grounded = new ActEngine();
    grounded.PlacePlayerForTest(grounded.Boss.X - 220f,
        ActTuning.GroundY - grounded.PlayerHeight);
    grounded.Boss.State = ActEnemyState.Attack;
    grounded.Boss.BossMove = BossMove.Quake;
    grounded.Boss.StateTime = .24f;
    grounded.Update(ActTuning.Step);
    Assert(grounded.Health == ActTuning.MaxHealth - 1);

    var airborne = new ActEngine();
    airborne.PlacePlayerForTest(airborne.Boss.X - 220f, 140f);
    airborne.Boss.State = ActEnemyState.Attack;
    airborne.Boss.BossMove = BossMove.Quake;
    airborne.Boss.StateTime = .24f;
    airborne.Update(ActTuning.Step);
    Assert(airborne.Health == ActTuning.MaxHealth);
});

Check("ACT same seed reproduces room, hazard, reward and shortcut layout", () =>
{
    var first = new ActEngine(8675309);
    var second = new ActEngine(8675309);
    Assert(first.Seed == second.Seed &&
        first.Layout.Signature == second.Layout.Signature);
    Assert(first.Hazards.Count == second.Hazards.Count);
    for (int i = 0; i < first.Hazards.Count; i++)
        Assert(first.Hazards[i].Kind == second.Hazards[i].Kind &&
            first.Hazards[i].X == second.Hazards[i].X &&
            first.Hazards[i].Phase == second.Hazards[i].Phase);
    Assert(first.Rewards.Select(r => (r.Kind, r.X, r.Y)).SequenceEqual(
        second.Rewards.Select(r => (r.Kind, r.X, r.Y))));
    first.Reset(8675309);
    Assert(first.Layout.Signature == second.Layout.Signature);
});

Check("ACT authored modules obey sockets, budgets and pacing across seeds", () =>
{
    var seen = Enumerable.Range(0, 4).Select(_ => new HashSet<string>()).ToArray();
    var rewards = new HashSet<ActRewardKind>();
    bool shortcutPresent = false, shortcutAbsent = false, hazardPresent = false;
    for (int seed = 1; seed <= 240; seed++)
    {
        ActLevelLayout layout = ActLevelRandomizer.Generate(seed);
        Assert(layout.Rooms.Count == 4);
        int combatStreak = 0;
        int lastRest = -10;
        foreach (RoomSelection room in layout.Rooms)
        {
            RoomNode node = room.Node;
            seen[node.Index - 1].Add(room.Variant.Id);
            Assert(node.EntrancePoint.Y == ActTuning.GroundY &&
                node.ExitPoint.Y == ActTuning.GroundY &&
                node.CameraBounds.X == node.StartX &&
                node.CameraBounds.Right == node.EndX);
            Assert(room.Group is null ||
                room.Group.DifficultyCost >= node.MinBudget &&
                room.Group.DifficultyCost <= node.MaxBudget);
            bool combat = room.Variant.Tag is RoomTag.Combat or
                RoomTag.Ambush or RoomTag.Challenge;
            combatStreak = combat ? combatStreak + 1 : 0;
            Assert(combatStreak <= 2);
            if (room.Variant.Tag == RoomTag.Rest)
            {
                Assert(node.Index - lastRest >= 2);
                lastRest = node.Index;
            }
            rewards.Add(room.Reward);
        }
        shortcutPresent |= layout.Shortcuts.Count > 0;
        shortcutAbsent |= layout.Shortcuts.Count == 0;
        hazardPresent |= layout.Hazards.Count > 0;
        Assert(layout.Rooms[0].Reward == ActRewardKind.BattleArt,
            "every expedition needs an early battle-art reward point");
        Assert(layout.Enemies.All(enemy => enemy.Kind != ActEnemyKind.Boss));
        Assert(layout.Enemies.All(enemy =>
            ActLevelRandomizer.Nodes.Any(node => enemy.HomeX > node.StartX + 150f &&
                enemy.HomeX < node.EndX - 150f)));
        List<System.Drawing.RectangleF> solids = ActLevel.CreateSolids();
        solids.AddRange(layout.Solids);
        Assert(layout.Enemies.All(enemy =>
            solids.All(solid => !enemy.Bounds.IntersectsWith(solid))),
            $"seed {seed} spawned an enemy in solid terrain");
        Assert(layout.Rooms[1].Variant.Route != ActRoute.Upper ||
            layout.Shortcuts.All(shortcut => shortcut.Id != "石桥"));
    }
    Assert(seen.All(variants => variants.Count == 3));
    Assert(rewards.Count == 5 && rewards.Contains(ActRewardKind.BattleArt) &&
        shortcutPresent && shortcutAbsent && hazardPresent);
});

Check("ACT room reward unlocks after fight and temporary buff resets", () =>
{
    ActEngine? selected = null;
    ActReward? selectedReward = null;
    for (int seed = 1; seed <= 200 && selected is null; seed++)
    {
        var candidate = new ActEngine(seed);
        ActReward? reward = candidate.Rewards.FirstOrDefault(reward =>
            reward.Kind == ActRewardKind.DamageBuff && reward.RoomIndex >= 1 &&
            candidate.Layout.Rooms[reward.RoomIndex - 1].Group is not null);
        if (reward is null) continue;
        selected = candidate;
        selectedReward = reward;
    }
    Assert(selected is not null && selectedReward is not null);
    ActEngine game = selected!;
    ActReward prize = selectedReward!;
    RoomNode node = game.Layout.Rooms[prize.RoomIndex - 1].Node;
    game.PlacePlayerForTest(prize.X - 12f,
        ActTuning.GroundY - game.PlayerHeight);
    game.Update(ActTuning.Step);
    Assert(!prize.Collected && !game.IsRewardUnlocked(prize));
    foreach (ActEnemy enemy in game.Enemies.Where(enemy =>
        enemy.HomeX >= node.StartX && enemy.HomeX < node.EndX))
        enemy.State = ActEnemyState.Dead;
    game.Update(ActTuning.Step);
    Assert(prize.Collected && game.DamageBuffRemaining > 0f &&
        game.DamageMultiplier > 1f);
    game.Reset(game.Seed);
    Assert(game.DamageBuffRemaining == 0f &&
        game.Layout.Signature == ActLevelRandomizer.Generate(game.Seed).Signature);
});

Check("ACT random spike hurts player and ambush activates on entry", () =>
{
    ActEngine? spikeGame = null;
    for (int seed = 1; seed <= 300 && spikeGame is null; seed++)
    {
        var candidate = new ActEngine(seed);
        if (candidate.Hazards.Any(hazard => hazard.Kind == ActHazardKind.Spikes))
            spikeGame = candidate;
    }
    Assert(spikeGame is not null);
    ActHazard spike = spikeGame!.Hazards.First(hazard =>
        hazard.Kind == ActHazardKind.Spikes);
    spikeGame.PlacePlayerForTest(spike.X - 8f,
        ActTuning.GroundY - spikeGame.PlayerHeight);
    spikeGame.Update(ActTuning.Step);
    Assert(spikeGame.Health == ActTuning.MaxHealth - 1);

    ActEngine? ambushGame = null;
    for (int seed = 1; seed <= 300 && ambushGame is null; seed++)
    {
        var candidate = new ActEngine(seed);
        if (candidate.Layout.Rooms.Any(room => room.Variant.Tag == RoomTag.Ambush))
            ambushGame = candidate;
    }
    Assert(ambushGame is not null);
    RoomSelection ambush = ambushGame!.Layout.Rooms.First(room =>
        room.Variant.Tag == RoomTag.Ambush);
    Assert(!ambush.Activated && ambushGame.Enemies.Any(enemy =>
        enemy.Dormant && enemy.HomeX >= ambush.Node.StartX &&
        enemy.HomeX < ambush.Node.EndX));
    ambushGame.PlacePlayerForTest(ambush.Node.StartX + 220f,
        ActTuning.GroundY - ambushGame.PlayerHeight);
    ambushGame.Update(ActTuning.Step);
    Assert(ambush.Activated && !ambushGame.Enemies.Any(enemy =>
        enemy.Dormant && enemy.HomeX >= ambush.Node.StartX &&
        enemy.HomeX < ambush.Node.EndX));
});

Check("ACT upper-only authored route crosses its blocking wall with base jump", () =>
{
    ActEngine? game = null;
    for (int seed = 1; seed <= 100 && game is null; seed++)
    {
        var candidate = new ActEngine(seed);
        if (candidate.Layout.Rooms[1].Variant.Id == "B2") game = candidate;
    }
    Assert(game is not null);
    foreach (ActEnemy enemy in game!.Enemies) enemy.State = ActEnemyState.Dead;
    game.Update(ActTuning.Step);
    Assert(game.PendingPowerReward == ActRewardChoice.None);
    game.PlacePlayerForTest(6350f,
        ActTuning.GroundY - game.PlayerHeight);
    game.RightHeld = true;
    foreach (float top in new[] { 225f, 175f, 145f })
    {
        game.JumpRequested = true;
        game.JumpHeld = true;
        ActRun(game, .7f);
        Assert(game.Grounded &&
            Math.Abs(game.PlayerY + game.PlayerHeight - top) <= 2f,
            $"did not reach platform {top}, x={game.PlayerX}, y={game.PlayerY}");
    }
    game.JumpHeld = false;
    ActRun(game, 1.7f);
    Assert(game.PlayerX > 7100f && game.Health == ActTuning.MaxHealth,
        "upper path failed to cross the wall");
});

Check("ACT all four reward effects are collectible and temporary buffs expire", () =>
{
    foreach (ActRewardKind kind in new[] { ActRewardKind.Heal,
        ActRewardKind.FullRangedEnergy, ActRewardKind.DamageBuff,
        ActRewardKind.MoveBuff })
    {
        ActEngine? game = null;
        ActReward? prize = null;
        for (int seed = 1; seed <= 200 && game is null; seed++)
        {
            var candidate = new ActEngine(seed);
            ActReward? match = candidate.Rewards.FirstOrDefault(reward =>
                reward.Kind == kind && reward.RoomIndex >= 1);
            if (match is null) continue;
            game = candidate;
            prize = match;
        }
        Assert(game is not null && prize is not null);
        if (kind == ActRewardKind.Heal)
        {
            game!.PlacePlayerForTest(game.Enemy.X - 48f,
                ActTuning.GroundY - game.PlayerHeight);
            game.Enemy.State = ActEnemyState.Windup;
            game.Enemy.StateTime = ActEnemyTuning.MeleeWindup;
            game.Update(ActTuning.Step);
            Assert(game.Health == ActTuning.MaxHealth - 1);
        }
        if (kind == ActRewardKind.FullRangedEnergy)
        {
            game!.RangedRequested = true;
            game.Update(ActTuning.Step);
            Assert(game.RangedEnergy == ActTuning.MaxRangedEnergy - 1);
        }
        RoomNode node = game!.Layout.Rooms[prize!.RoomIndex - 1].Node;
        foreach (ActEnemy enemy in game.Enemies.Where(enemy =>
            enemy.HomeX >= node.StartX && enemy.HomeX < node.EndX))
            enemy.State = ActEnemyState.Dead;
        game.PlacePlayerForTest(prize.X - 12f,
            ActTuning.GroundY - game.PlayerHeight);
        ActRun(game, .2f);
        Assert(prize.Collected, $"{kind} was not collected");
        switch (kind)
        {
            case ActRewardKind.Heal:
                Assert(game.Health == ActTuning.MaxHealth); break;
            case ActRewardKind.FullRangedEnergy:
                Assert(game.RangedEnergy == ActTuning.MaxRangedEnergy); break;
            case ActRewardKind.DamageBuff:
                Assert(game.DamageMultiplier > 1f); break;
            case ActRewardKind.MoveBuff:
                Assert(game.MoveSpeedMultiplier > 1f);
                game.PlacePlayerForTest(90f,
                    ActTuning.GroundY - game.PlayerHeight);
                ActRun(game, ActRewardTuning.BuffDuration + .3f);
                Assert(game.MoveSpeedMultiplier == 1f);
                break;
        }
    }
});

Check("ACT battle art rewards, two fixed slots, replacement and pause", () =>
{
    var game = new ActEngine(42);
    Assert(ActPowerCatalog.Arts.Count == 25 && ActPowerCatalog.Evolutions.Count == 28);
    Assert(game.OfferBattleArts());
    Assert(game.ArtOffers.Count == 3 && game.ArtOffers.Distinct().Count() == 3);
    float pausedAt = game.RunTime;
    game.Update(ActTuning.Step);
    Assert(game.RunTime == pausedAt, "reward did not pause simulation");
    BattleArtId first = game.ArtOffers[0];
    Assert(game.ChooseBattleArt(0) && game.Run.BattleArts[0] == first);
    Assert(game.OfferBattleArts() && game.ChooseBattleArt(1));
    Assert(game.Run.BattleArtCount == 2);
    Assert(game.OfferBattleArts());
    Assert(!game.ChooseBattleArt(0), "full inventory accepted without replacement");
    BattleArtId replacement = game.ArtOffers[2];
    Assert(game.ChooseBattleArt(2, 1) && game.Run.BattleArts[1] == replacement &&
        game.Run.BattleArts[0] == first);
    Assert(game.OfferBattleArts() && game.DeclineBattleArt() &&
        game.Run.BattleArts[1] == replacement);
    ActReward prize = game.Rewards.First(reward => reward.Kind == ActRewardKind.BattleArt);
    if (prize.RoomIndex >= 0)
    {
        RoomNode room = game.Layout.Rooms[prize.RoomIndex - 1].Node;
        foreach (ActEnemy enemy in game.Enemies.Where(enemy => enemy.HomeX >= room.StartX &&
            enemy.HomeX < room.EndX)) enemy.State = ActEnemyState.Dead;
    }
    game.PlacePlayerForTest(prize.X - 12f, ActTuning.GroundY - game.PlayerHeight);
    game.Update(ActTuning.Step);
    Assert(prize.Collected && game.PendingPowerReward == ActRewardChoice.BattleArt);
});

Check("ACT all battle arts apply and consume", () =>
{
    foreach (BattleArtDefinition definition in ActPowerCatalog.Arts)
    {
        var game = new ActEngine(710 + (int)definition.Id);
        Assert(game.Run.AddArt(definition.Id));
        if (definition.Id == BattleArtId.RegenerativeSpores)
            game.Run.SetHealth(2);
        if (definition.Id == BattleArtId.EnergyReturn)
        {
            game.RangedRequested = true;
            game.Update(ActTuning.Step);
            Assert(game.RangedEnergy == ActTuning.MaxRangedEnergy - 1);
        }
        if (definition.Id == BattleArtId.HuntingInstinct)
        {
            game.DashRequested = true;
            game.Update(ActTuning.Step);
            Assert(game.DashCooldownRemaining > 0f);
        }
        if (definition.Id is BattleArtId.BoneStorm or BattleArtId.EarthquakeRoar)
            game.PlacePlayerForTest(game.Enemy.X - 100f,
                ActTuning.GroundY - game.PlayerHeight);
        int nearbyEnemyHealth = game.Enemy.Health;
        Assert(game.UseBattleArt(0) && game.Run.BattleArts[0] is null);
        switch (definition.Id)
        {
            case BattleArtId.RegenerativeSpores:
                Assert(game.Health == 4); break;
            case BattleArtId.BoneArmor:
                game.PlacePlayerForTest(game.Enemy.X - 48f,
                    ActTuning.GroundY - game.PlayerHeight);
                game.Enemy.State = ActEnemyState.Windup;
                game.Enemy.StateTime = ActEnemyTuning.MeleeWindup;
                game.Update(ActTuning.Step);
                Assert(game.Health == game.MaximumHealth && game.Run.ShieldCharges == 0); break;
            case BattleArtId.PrimalRage:
                Assert(game.Run.RageRemaining == definition.Duration);
                break;
            case BattleArtId.AmberLag:
                Assert(game.EnemySpeedRatio == definition.Strength);
                break;
            case BattleArtId.BoneStorm:
                Assert(game.Projectiles.Count == definition.Projectiles &&
                    game.Projectiles.Any(p => p.Vx < 0) &&
                    game.Projectiles.Any(p => p.Vx > 0) &&
                    game.Projectiles.Any(p => p.Vy < 0));
                for (int i = 0; i < 70 && game.Enemy.Health == nearbyEnemyHealth; i++)
                    game.Update(ActTuning.Step);
                Assert(game.Enemy.Health < nearbyEnemyHealth,
                    "bone storm shots never reached nearby enemy");
                break;
            case BattleArtId.EarthquakeRoar:
                Assert(game.Particles.Count > 0 && game.Enemy.Health < nearbyEnemyHealth &&
                    game.Enemy.KnockbackSpeed != 0f); break;
            case BattleArtId.EnergyReturn:
                Assert(game.RangedEnergy == ActTuning.MaxRangedEnergy); break;
            case BattleArtId.HuntingInstinct:
                Assert(game.DashCooldownRemaining == 0f &&
                    game.Run.HuntRemaining == definition.Duration); break;
        }
        Assert(!game.UseBattleArt(0), "consumed art fired twice");
    }
});

Check("ACT evolution choices, distinct passives and next-map RunState", () =>
{
    var game = new ActEngine(154);
    Assert(game.OfferEvolutions());
    Assert(game.EvolutionOffers.Count == 3 &&
        game.EvolutionOffers.Distinct().Count() == 3);
    EvolutionId chosen = game.EvolutionOffers[1];
    Assert(game.ChooseEvolution(1) && game.Run.Has(chosen));
    Assert(game.OfferEvolutions() && !game.EvolutionOffers.Contains(chosen));
    Assert(game.ChooseEvolution(0));
    foreach (EvolutionDefinition definition in ActPowerCatalog.Evolutions)
        game.Run.AddEvolution(definition.Id);
    Assert(game.Run.Evolutions.Count == 22 && !game.OfferEvolutions());
    Assert(game.MaximumHealth == ActTuning.MaxHealth + 1);
    Assert(Math.Abs(game.Run.Resolve(PowerStat.ClawDamage, 1f) - 1.15f) < .001f);
    Assert(Math.Abs(game.Run.Resolve(PowerStat.TailRange, 1f) - 1.2f) < .001f);
    Assert(Math.Abs(game.Run.Resolve(PowerStat.JumpHeight, 1f) - 1.1f) < .001f);
    Assert(Math.Abs(game.Run.Resolve(PowerStat.StompRange, 1f) - 1.25f) < .001f);
    Assert(game.Run.Resolve(PowerStat.RangedProjectiles, 1f) == 2f);
    game.Run.AddArt(BattleArtId.RegenerativeSpores);
    game.Run.SetHealth(2);
    ActRunState exported = game.ExportRunState();
    game.EnterNextMap(155, roomAdventure: false);
    Assert(game.Health == 4 && game.MaximumHealth == 6 &&
        game.Run.Evolutions.Count == 22 && game.Run.BattleArtCount == 1);
    game.Run.ConsumeArt(0);
    Assert(exported.BattleArtCount == 1 && exported.Health == 2,
        "RunState export was not a snapshot");
    game.Reset(155);
    Assert(game.Run.Evolutions.Count == 0 && game.Run.BattleArtCount == 0 &&
        game.Health == ActTuning.MaxHealth && game.MaximumHealth == ActTuning.MaxHealth);
});

Check("ACT evolved projectile, slowed enemy, boss evolution gate", () =>
{
    var twin = new ActEngine(99);
    twin.Run.AddEvolution(EvolutionId.TwinSpikes);
    twin.RangedRequested = true;
    twin.Update(ActTuning.Step);
    Assert(twin.Projectiles.Count == 2 && twin.RangedEnergy ==
        ActTuning.MaxRangedEnergy - 1 && twin.Projectiles.Any(p => p.Vy < 0f));
    var slow = new ActEngine(99);
    slow.Run.AddArt(BattleArtId.AmberLag);
    slow.UseBattleArt(0);
    slow.PlacePlayerForTest(slow.Enemy.X - 50f,
        ActTuning.GroundY - slow.PlayerHeight);
    slow.Enemy.State = ActEnemyState.Windup;
    slow.Enemy.StateTime = 0f;
    ActRun(slow, .5f);
    Assert(slow.Health == slow.MaximumHealth &&
        slow.Enemy.State == ActEnemyState.Windup,
        "slow did not delay enemy attack");
    ActRun(slow, .5f);
    Assert(slow.Health < slow.MaximumHealth, "enemy attack never resumed");
    var bossRun = new ActEngine(99);
    bossRun.Boss.Health = 1;
    bossRun.PlacePlayerForTest(bossRun.Boss.X - 100,
        ActTuning.GroundY - bossRun.PlayerHeight);
    bossRun.Run.AddArt(BattleArtId.EarthquakeRoar);
    Assert(bossRun.UseBattleArt(0) && bossRun.Boss.State == ActEnemyState.Dead);
    Assert(bossRun.PendingPowerReward == ActRewardChoice.None &&
        bossRun.BossEvolutionReward is not null, "boss death opened reward directly");
    var drop = bossRun.BossEvolutionReward!;
    Assert(Math.Abs(drop.X - (bossRun.Boss.X + bossRun.Boss.Width / 2)) < .01f);
    ActRun(bossRun, 1.3f);
    Assert(!drop.Collected && bossRun.PendingPowerReward == ActRewardChoice.None);
    bossRun.PlacePlayerForTest(drop.X - bossRun.PlayerWidth / 2,
        ActTuning.GroundY - bossRun.PlayerHeight);
    bossRun.Update(ActTuning.Step);
    Assert(drop.Collected && bossRun.PendingPowerReward == ActRewardChoice.Evolution &&
        !bossRun.IsComplete);
    Assert(bossRun.ChooseEvolution(0));
    ActRun(bossRun, .5f);
    Assert(bossRun.PendingPowerReward == ActRewardChoice.None &&
        bossRun.Rewards.Count(r => r.Kind == ActRewardKind.Evolution) == 1);
    bossRun.PlacePlayerForTest(bossRun.ExitX, ActTuning.GroundY - bossRun.PlayerHeight);
    ActRun(bossRun, 3.1f);
    Assert(bossRun.IsComplete);
    bossRun.Reset();
    Assert(bossRun.BossEvolutionReward is null &&
        bossRun.Rewards.All(r => r.Kind != ActRewardKind.Evolution));
});

Check("ACT boss corpse pickup waits for collapse and survives until contact", () =>
{
    var game = new ActEngine(817);
    game.Boss.State = ActEnemyState.Dead;
    game.Boss.Health = 0;
    game.PlacePlayerForTest(game.Boss.X + game.Boss.Width / 2 - game.PlayerWidth / 2,
        ActTuning.GroundY - game.PlayerHeight);
    game.Update(ActTuning.Step);
    ActReward drop = game.BossEvolutionReward!;
    ActRun(game, .45f);
    Assert(!drop.Collected && game.PendingPowerReward == ActRewardChoice.None,
        "overlapping melee player skipped the death presentation");
    ActRun(game, .6f);
    Assert(drop.Collected && game.PendingPowerReward == ActRewardChoice.Evolution);
    float time = game.RunTime;
    ActRun(game, .5f);
    Assert(game.RunTime == time, "pickup selection did not pause the simulation");
    Assert(game.ChooseEvolution(0));
    game.EnterNextMap(818, roomAdventure: false);
    Assert(game.BossEvolutionReward is null && game.Run.Evolutions.Count == 1);
});

Check("ACT canyon modules vary terrain, encounters and supply sockets by seed", () =>
{
    var combinations = new HashSet<string>();
    for (int seed = 1; seed <= 640; seed++)
    {
        var game = new ActEngine(seed);
        game.Run.AddEvolution(EvolutionId.HollowBones);
        game.Run.AddArt(BattleArtId.RegenerativeSpores);
        game.Run.SetHealth(3);
        game.EnterNextMap(seed, roomAdventure: false);
        Assert(game.MapIndex == 2 && game.Health == 3 && game.Run.Evolutions.Count == 1 &&
            game.Run.BattleArtCount == 1 && game.Layout.Rooms.Count == 6);
        Assert(game.Boss.Kind == ActEnemyKind.Boss &&
            game.Boss.X >= game.Layout.BossArenaLeft && game.Boss.X < game.ExitX,
            "extended encounters displaced the final boss");
        Assert(game.Layout.Terrain.Count > 40 && game.Layout.Supplies.Count == 5 &&
            game.Layout.Zones.Count == 14 && game.WorldWidth == 21800f);
        Assert(game.Layout.Rooms.Select(r => r.Variant.Id[0])
            .SequenceEqual(['A', 'B', 'C', 'D', 'E', 'F']));
        combinations.Add(string.Join('-', game.Layout.Rooms.Select(r => r.Variant.Id)));
        Assert(game.Layout.Terrain.All(t => t.Right > t.Left &&
            Math.Abs(t.RightY - t.LeftY) / (t.Right - t.Left) <= .5f));
        Assert(game.Layout.Terrain.Any(t => t.RightY < t.LeftY) &&
            game.Layout.Terrain.Any(t => t.RightY > t.LeftY));
        Assert(game.Layout.Enemies.All(enemy => enemy.Kind == ActEnemyKind.Flying ||
            Math.Abs(enemy.Y + enemy.Height -
                (game.Layout.Solids.Where(s => enemy.X + enemy.Width / 2 >= s.Left &&
                    enemy.X + enemy.Width / 2 < s.Right && s.Top >= enemy.Y + enemy.Height - 2)
                    .Select(s => (float?)s.Top).DefaultIfEmpty(null).Min() ??
                game.TerrainYAt(enemy.X + enemy.Width / 2) ?? -999f)) < 5f));
        Assert(game.Layout.Rewards.All(r => r.X < game.WorldWidth));
        game.Reset(seed);
        Assert(game.MapIndex == 1 && game.Run.Evolutions.Count == 0);
    }
    Assert(combinations.Count == 64,
        $"Only {combinations.Count} of 64 authored layouts appeared.");
});

Check("ACT canyon slopes support walking and dash without vertical jitter", () =>
{
    var game = new ActEngine(36);
    game.EnterNextMap(36, roomAdventure: false);
    foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(350, game.TerrainYAt(377)!.Value - game.PlayerHeight);
    game.RightHeld = true;
    float lastFeet = game.PlayerY + game.PlayerHeight;
    for (int i = 0; i < 115; i++)
    {
        game.Update(ActTuning.Step);
        float feet = game.PlayerY + game.PlayerHeight;
        Assert(Math.Abs(feet - lastFeet) < 4f, "walking slope jittered or snapped");
        Assert(game.Grounded && Math.Abs(feet - game.TerrainYAt(game.PlayerX + 27)!.Value) < 2f);
        lastFeet = feet;
    }
    Assert(game.PlayerX > 500 && game.CameraY < -20);
    game.PlacePlayerForTest(350, game.TerrainYAt(377)!.Value - game.PlayerHeight);
    game.DashRequested = true;
    for (int i = 0; i < 22; i++) game.Update(ActTuning.Step);
    Assert(game.PlayerX > 400 && game.Grounded &&
        Math.Abs(game.PlayerY + game.PlayerHeight - game.TerrainYAt(game.PlayerX + 27)!.Value) < 2f);
});

Check("ACT canyon pit rescues the player and traversable lower route rejoins", () =>
{
    var game = new ActEngine(7);
    game.EnterNextMap(7, roomAdventure: false);
    game.Layout.Hazards.Clear();
    foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
    float start = game.TerrainYAt(7827)!.Value;
    game.PlacePlayerForTest(7800, start - game.PlayerHeight);
    game.RightHeld = true;
    int hp = game.Health;
    for (int i = 0; i < 480 && game.Health == hp; i++) game.Update(ActTuning.Step);
    Assert(game.Health == hp - 1 && game.PlayerX < 7940 && game.PlayerY < 400,
        "pit failed to return player to stable ground");
    game.PlacePlayerForTest(4250, game.TerrainYAt(4277)!.Value - game.PlayerHeight);
    game.RightHeld = true;
    for (int i = 0; i < 155; i++) game.Update(ActTuning.Step);
    Assert(game.PlayerX > 4500 && game.Grounded && game.CameraY < 75,
        "lower route did not climb back to the shared interface");
});

Check("ACT canyon ordinary gaps clear with unmodified jump and reconverge", () =>
{
    foreach (var (variant, lip, landing) in new[] {
        ("A1 山顶攻防", 1720f, 1880f), ("B1 沉岩洞穴", 5060f, 5220f),
        ("C1 回旋高地", 7940f, 8100f), ("C2 下层采石场", 7940f, 8100f) })
    {
        int seed = Enumerable.Range(1, 100).First(value =>
            ActSecondMap.Generate(value).Rooms.Any(room => room.Variant.Id == variant));
        var game = new ActEngine(1);
        game.EnterNextMap(seed, roomAdventure: false);
        game.Layout.Hazards.Clear();
        foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
        float startX = lip - 240f;
        float floor = game.TerrainYAt(startX + 27)!.Value;
        game.PlacePlayerForTest(startX, floor - game.PlayerHeight);
        game.RightHeld = true;
        for (int i = 0; i < 200 && game.PlayerX < lip - 38f; i++) game.Update(ActTuning.Step);
        Assert(game.Grounded && game.PlayerX >= lip - 38f, variant + " approach failed");
        int hp = game.Health;
        game.JumpHeld = true; game.JumpRequested = true;
        for (int i = 0; i < 160 && game.PlayerX < landing + 60; i++)
            game.Update(ActTuning.Step);
        Assert(game.PlayerX > landing + 20 && game.Health == hp &&
            game.Grounded && game.PlayerY + game.PlayerHeight < 410f,
            variant + " pit could not be jumped with base movement");
    }
});

Check("ACT canyon stepping-stone chasm clears with base movement", () =>
{
    int seed = Enumerable.Range(1, 100).First(value =>
        ActSecondMap.Generate(value).Rooms[1].Variant.Id.StartsWith("B2"));
    var game = new ActEngine(1);
    game.EnterNextMap(seed, roomAdventure: false);
    game.Layout.Hazards.Clear();
    foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(4900f,
        game.TerrainYAt(4927f)!.Value - game.PlayerHeight);
    game.RightHeld = true;
    int hp = game.Health;
    // The first ledge is level with the lip. Jump early enough to land on the
    // higher middle ledge, then jump again to the far bank.
    while (game.PlayerX < 5060f) game.Update(ActTuning.Step);
    game.JumpHeld = game.JumpRequested = true;
    while (game.PlayerX < 5235f) game.Update(ActTuning.Step);
    game.RightHeld = false;
    game.JumpHeld = false;
    for (int i = 0; i < 125 && !game.Grounded; i++) game.Update(ActTuning.Step);
    Assert(game.Grounded && game.PlayerX >= 5200f &&
        Math.Abs(game.PlayerY + game.PlayerHeight - 216f) < 2f,
        $"middle stepping stone unreachable: {game.PlayerX}, {game.PlayerY}");
    game.RightHeld = true;
    game.JumpHeld = game.JumpRequested = true;
    while (game.PlayerX < 5490f) game.Update(ActTuning.Step);
    game.JumpHeld = false;
    for (int i = 0; i < 125 && !game.Grounded; i++) game.Update(ActTuning.Step);
    Assert(game.Grounded && game.PlayerX > 5470f && game.Health == hp &&
        Math.Abs(game.PlayerY + game.PlayerHeight -
            game.TerrainYAt(game.PlayerX + game.PlayerWidth / 2f)!.Value) < 2f,
        $"stepping-stone route did not reach far bank: x={game.PlayerX}, y={game.PlayerY}, hp={game.Health}, grounded={game.Grounded}");
});

Check("ACT canyon enemies track slopes and vertical camera stays smooth", () =>
{
    int seed = Enumerable.Range(1, 100).First(value =>
        ActSecondMap.Generate(value).Rooms[0].Variant.Id.StartsWith("A1"));
    var game = new ActEngine(1);
    game.EnterNextMap(seed, roomAdventure: false);
    ActEnemy heavy = game.Enemies.First(enemy => enemy.Kind == ActEnemyKind.Heavy);
    foreach (ActEnemy enemy in game.Enemies.Where(enemy => enemy != heavy))
        enemy.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(1170f,
        game.TerrainYAt(1197f)!.Value - game.PlayerHeight);
    bool reachedSlope = false;
    for (int i = 0; i < 420; i++)
    {
        game.Update(ActTuning.Step);
        if (heavy.X + heavy.Width / 2f >= 1380f) continue;
        reachedSlope = true;
        Assert(Math.Abs(heavy.Y + heavy.Height -
            game.TerrainYAt(heavy.X + heavy.Width / 2f)!.Value) < 4f,
            "heavy enemy detached from the slope");
        break;
    }
    Assert(reachedSlope, "heavy enemy did not pursue downhill");

    foreach (float x in new[] { 700f, 4290f })
    {
        float foot = game.TerrainYAt(x + 27f)!.Value;
        game.PlacePlayerForTest(x, foot - game.PlayerHeight);
        float previous = game.CameraY;
        for (int i = 0; i < 130; i++)
        {
            game.Update(ActTuning.Step);
            Assert(Math.Abs(game.CameraY - previous) <= 2.1f,
                "vertical camera snapped during height transition");
            previous = game.CameraY;
        }
        Assert(x < 1000f ? game.CameraY < -35f : game.CameraY > 35f,
            "camera failed to frame the new terrain height");
    }
});

Check("ACT extended canyon camps connect terrain and exclude combat spawns", () =>
{
    for (int seed = 1; seed <= 64; seed++)
    {
        var game = new ActEngine(seed);
        foreach (ActSupply camp in game.Supplies)
        {
            Assert(!game.Enemies.Any(e => e.X + e.Width > camp.Left && e.X < camp.Right),
                "teaching-map enemy spawned in camp");
            Assert(!game.Hazards.Any(h => h.X >= camp.Left && h.X <= camp.Right));
            Assert(!game.Solids.Any(s => s.Top < ActTuning.GroundY &&
                s.Right > camp.Left && s.Left < camp.Right), "camp blocked by old platforms");
        }
        game.EnterNextMap(seed, roomAdventure: false);
        Assert(game.WorldWidth == 21800f && game.ExitX > game.Boss.X);
        foreach (ActSupply camp in game.Supplies)
        {
            Assert(!game.Enemies.Any(e => e.X + e.Width > camp.Left && e.X < camp.Right));
            Assert(!game.Hazards.Any(h => h.X >= camp.Left && h.X <= camp.Right));
            for (float x = camp.X - 449; x <= camp.X + 449; x += 2)
                Assert(game.TerrainYAt(x) is float ground && ground >= 300 && ground <= 330,
                    "camp approach disconnected from adjacent module");
            Assert(Math.Abs(game.TerrainYAt(camp.X)!.Value - camp.Y - 28) < .01f);
        }
    }
});

Check("ACT supplies restore health and energy once on both maps", () =>
{
    foreach (int map in new[] { 1, 2 })
    {
        var game = new ActEngine(12);
        if (map == 2) game.EnterNextMap(12, roomAdventure: false);
        foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
        ActSupply camp = game.Supplies[0];
        game.PlacePlayerForTest(camp.X - 27, camp.Y + 28 - game.PlayerHeight);
        game.Update(ActTuning.Step);
        Assert(!camp.Collected, "full health and energy wasted the supply");
        game.Run.SetHealth(2);
        game.RangedRequested = true;
        game.Update(ActTuning.Step);
        Assert(camp.Collected && game.Health == 4 && game.RangedEnergy == ActTuning.MaxRangedEnergy);
        game.Run.SetHealth(2);
        ActRun(game, .4f);
        game.RangedRequested = true;
        game.Update(ActTuning.Step);
        Assert(game.Health == 2 && game.RangedEnergy == ActTuning.MaxRangedEnergy - 1,
            "used camp replenished a second time");
        ActSupply next = game.Supplies[1];
        game.Run.SetHealth(game.MaximumHealth);
        game.PlacePlayerForTest(next.X - 27, next.Y + 28 - game.PlayerHeight);
        game.Update(ActTuning.Step);
        Assert(next.Collected && game.Health == game.MaximumHealth &&
            game.RangedEnergy == ActTuning.MaxRangedEnergy, "energy-only resupply failed");
    }
});

Check("ACT camp respite ends pursuit without restoring enemy health", () =>
{
    var game = new ActEngine(12);
    ActSupply camp = game.Supplies[0];
    ActEnemy pursuer = game.AddEnemyForTest(camp.Left - 90);
    pursuer.Health = 1;
    pursuer.State = ActEnemyState.Chase;
    pursuer.X = camp.Left - 15;
    game.PlacePlayerForTest(camp.Left + 25, ActTuning.GroundY - game.PlayerHeight);
    ActRun(game, 2f);
    Assert(game.Health == game.MaximumHealth && pursuer.Health == 1 &&
        pursuer.X + pursuer.Width < camp.Left && pursuer.State == ActEnemyState.Patrol);
});

Check("ACT canyon raised platform permits movement below and jumping through", () =>
{
    int seed = Enumerable.Range(1, 120).First(value =>
        ActSecondMap.Generate(value).Rooms[0].Variant.Id.StartsWith("A2"));
    var game = new ActEngine(1);
    game.EnterNextMap(seed, roomAdventure: false);
    foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(1400f, game.TerrainYAt(1427f)!.Value - game.PlayerHeight);
    game.RightHeld = true;
    for (int i = 0; i < 250 && game.PlayerX < 1730f; i++) game.Update(ActTuning.Step);
    Assert(game.PlayerX >= 1730f && game.Health == game.MaximumHealth,
        $"thin platform blocked horizontal passage at {game.PlayerX}");
    game.RightHeld = false;
    game.PlacePlayerForTest(1620f, 385f - game.PlayerHeight);
    game.JumpHeld = game.JumpRequested = true;
    ActRun(game, .45f);
    game.JumpHeld = false;
    for (int i = 0; i < 150 && !game.Grounded; i++) game.Update(ActTuning.Step);
    Assert(game.Grounded && Math.Abs(game.PlayerY + game.PlayerHeight - 275f) < 2f,
        "thin platform stopped the ascent or could not be landed on from above");
});

Check("ACT second-half authored gaps clear with base jump", () =>
{
    foreach (var (prefix, lip, bank) in new[] {
        ("D1", 10380f, 10540f), ("D2", 11480f, 11660f),
        ("E1", 14800f, 14970f), ("E2", 14880f, 15050f),
        ("F1", 19000f, 19200f), ("F2", 18500f, 18660f) })
    {
        int seed = Enumerable.Range(1, 140).First(value =>
            ActSecondMap.Generate(value).Rooms.Any(room =>
                room.Variant.Id.StartsWith(prefix)));
        var game = new ActEngine(1);
        game.EnterNextMap(seed, roomAdventure: false);
        game.Layout.Hazards.Clear();
        foreach (ActEnemy enemy in game.Enemies) enemy.State = ActEnemyState.Dead;
        float startX = lip - 225f;
        game.PlacePlayerForTest(startX,
            game.TerrainYAt(startX + 27f)!.Value - game.PlayerHeight);
        game.RightHeld = true;
        for (int i = 0; i < 200 && game.PlayerX < lip - 38f; i++)
            game.Update(ActTuning.Step);
        Assert(game.Grounded && game.PlayerX >= lip - 38f, prefix + " approach failed");
        int hp = game.Health;
        game.JumpHeld = game.JumpRequested = true;
        for (int i = 0; i < 200 && game.PlayerX < bank + 65f; i++)
            game.Update(ActTuning.Step);
        Assert(game.PlayerX > bank + 20f && game.Health == hp && game.Grounded,
            $"{prefix} gap failed: x={game.PlayerX} y={game.PlayerY} hp={game.Health}");
    }
});

Check("ACT extended canyon retains the real boss reward and exit", () =>
{
    var game = new ActEngine(77);
    game.EnterNextMap(77, roomAdventure: false);
    Assert(game.Boss.Kind == ActEnemyKind.Boss && game.Boss.X > 20550f);
    foreach (ActEnemy enemy in game.Enemies.Where(enemy => enemy != game.Boss))
        enemy.State = ActEnemyState.Dead;
    game.Boss.Health = 0;
    game.Boss.State = ActEnemyState.Dead;
    game.PlacePlayerForTest(game.Boss.X + game.Boss.Width / 2f - game.PlayerWidth / 2f,
        300f - game.PlayerHeight);
    ActRun(game, 1.2f);
    Assert(game.BossEvolutionReward?.Collected == true &&
        game.PendingPowerReward == ActRewardChoice.Evolution);
    Assert(game.ChooseEvolution(0));
    game.PlacePlayerForTest(game.ExitX, 300f - game.PlayerHeight);
    ActRun(game, 3.1f);
    Assert(game.IsComplete && game.MapIndex == 2);
});

Check("ACT debug map selection can travel both ways with chosen run state", () =>
{
    var game = new ActEngine(310);
    game.Run.AddArt(BattleArtId.BoneArmor);
    game.Run.AddEvolution(EvolutionId.HollowBones);
    game.Run.SetHealth(3);
    game.RightHeld = true;
    game.LoadMapForDebug(2, 777, true);
    Assert(game.MapIndex == 2 && game.Seed == 777 && game.Health == 3 &&
        game.Run.BattleArtCount == 1 && game.Run.Evolutions.Count == 1 &&
        !game.RightHeld && game.RunTime == 0f &&
        game.Layout.Signature == ActSecondMap.Generate(777).Signature);
    game.LoadMapForDebug(1, 310, true);
    Assert(game.MapIndex == 1 && game.Seed == 310 && game.Health == 3 &&
        game.Run.BattleArtCount == 1 && game.Run.Evolutions.Count == 1);
    game.LoadMapForDebug(2, 778, false);
    Assert(game.MapIndex == 2 && game.Seed == 778 && game.Health == ActTuning.MaxHealth &&
        game.Run.BattleArtCount == 0 && game.Run.Evolutions.Count == 0);
    game.LoadMapForDebug(1, null, false);
    Assert(game.MapIndex == 1 && game.Seed > 0);
    bool rejected = false;
    try { game.LoadMapForDebug(5, 1, false); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Assert(rejected && game.MapIndex == 1);
});

AdvancedMapChecks.Run(Check);
RestStopChecks.Run(Check);
BossChecks.Run(Check);
PrimalChecks.Run(Check);
ChapterRoomChecks.Run(Check);
AllChapterRoomChecks.Run(Check);
ExpandedBuildChecks.Run(Check);
Console.WriteLine($"\n{passed} native test groups passed.");
