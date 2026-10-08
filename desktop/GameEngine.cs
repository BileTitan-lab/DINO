using System.Drawing;

namespace DinoDash.Desktop;

internal enum ObstacleKind { Ground, Flying }
internal enum RewardKind { None, Card, Relic }
internal enum CardType { HealingHerb, Shield, SlowTime, Ghost, DoublePoints, ClearObstacles }
internal enum RelicType { AmberHeart, LuckyEggshell, LightBones, GoldenFossil, BrokenCrown }

internal static class RewardText
{
    public static string Name(CardType card) => card switch
    {
        CardType.HealingHerb => "治疗草药",
        CardType.Shield => "护盾",
        CardType.SlowTime => "时间减速",
        CardType.Ghost => "幽灵化",
        CardType.DoublePoints => "双倍积分",
        _ => "清障"
    };

    public static string Description(CardType card) => card switch
    {
        CardType.HealingHerb => "立即回复 1 HP，不超过最大血量。",
        CardType.Shield => "抵挡下一次碰撞，触发后消失。",
        CardType.SlowTime => "接下来 4 秒世界速度降低 40%。",
        CardType.Ghost => "接下来 3 秒穿过障碍，不受伤害。",
        CardType.DoublePoints => "接下来 8 秒获得双倍积分。",
        _ => "立即清除当前屏幕中的所有障碍。"
    };

    public static string Name(RelicType relic) => relic switch
    {
        RelicType.AmberHeart => "琥珀心脏",
        RelicType.LuckyEggshell => "幸运蛋壳",
        RelicType.LightBones => "轻盈骨骼",
        RelicType.GoldenFossil => "黄金化石",
        _ => "残破王冠"
    };

    public static string Description(RelicType relic) => relic switch
    {
        RelicType.AmberHeart => "最大 HP +1，并立即回复 1 HP。",
        RelicType.LuckyEggshell => "累计受到 3 次伤害后，自动挡下下一次碰撞。",
        RelicType.LightBones => "跳跃高度提高约 15%。",
        RelicType.GoldenFossil => "所有获得的积分提高 15%。",
        _ => "当前仅剩 1 HP 时，获得的积分提高 40%。"
    };
}

internal sealed class Obstacle
{
    public required ObstacleKind Kind { get; init; }
    public required int Level { get; init; }
    public required float X { get; set; }
    public required float Y { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }
    public float FlapClock { get; set; }
}

internal sealed class GameEngine
{
    public const float WorldWidth = 960f;
    public const float WorldHeight = 360f;
    public const float GroundY = 285f;
    public const float FixedStep = 1f / 120f;
    public const float BaseSpeed = 350f;
    public const int SpeedStageMeters = 200;
    public const int FirstSpeedBoundaryMeters = 2000;
    public const int SecondSpeedBoundaryMeters = 5000;
    public const int ThirdSpeedBoundaryMeters = 8000;
    public const float SpeedIncreaseBefore2000 = 30f;
    public const float SpeedIncreaseTo5000 = 20f;
    public const float SpeedIncreaseTo8000 = 10f;
    public const float SpeedIncreaseAfter8000 = 5f;
    public const int MultiplierStageMeters = 500;
    public const int MaxHealth = 3;
    public const float InvulnerabilitySeconds = 1f;
    public const float Gravity = 1800f;
    public const float FastFallSpeed = 720f;
    public const float FastFallGravity = 3200f;
    public const int CardCapacity = 2;
    public const float RelicIntervalSeconds = 80f;

    private static readonly (float Width, float Height)[] GroundSizes =
    {
        (28f, 30f), (34f, 46f), (42f, 62f)
    };
    private static readonly (float Y, float Width, float Height)[] FlyingSizes =
    {
        (249f, 48f, 28f), (226f, 48f, 30f), (170f, 48f, 30f)
    };

    private readonly Random random;
    private readonly List<Obstacle> obstacles = new();
    private readonly List<CardType> cards = new();
    private readonly List<RelicType> relics = new();
    private CardType[] cardOffers = Array.Empty<CardType>();
    private RelicType[] relicOffers = Array.Empty<RelicType>();
    private float spawnDistanceRemaining;
    private int closeGapStreak;
    private double earnedPoints;
    private double runTime;
    private double nextCardTime;
    private double nextRelicTime;
    private int damageSinceEgg;

    public GameEngine(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Reset();
    }

    public float Distance { get; private set; }
    public float Speed { get; private set; } = BaseSpeed;
    public float GroundOffset { get; private set; }
    public float CloudOffset { get; private set; }
    public float RunClock { get; private set; }
    public float DinoX { get; } = 108f;
    public float DinoY { get; private set; }
    public float DinoWidth { get; private set; } = 44f;
    public float DinoHeight { get; private set; } = 52f;
    public float VerticalSpeed { get; private set; }
    public bool Grounded { get; private set; } = true;
    public bool Crouching { get; private set; }
    public bool CrouchHeld { get; set; }
    public bool JumpRequested { get; set; }
    public int Health { get; private set; } = MaxHealth;
    public int MaximumHealth { get; private set; } = MaxHealth;
    public float InvulnerabilityRemaining { get; private set; }
    public float SlowTimeRemaining { get; private set; }
    public float GhostRemaining { get; private set; }
    public float DoublePointsRemaining { get; private set; }
    public int ShieldCharges { get; private set; }
    public int EggProtectionCharges { get; private set; }
    public RewardKind PendingReward { get; private set; }
    public IReadOnlyList<CardType> Cards => cards;
    public IReadOnlyList<RelicType> Relics => relics;
    public IReadOnlyList<CardType> CardOffers => cardOffers;
    public IReadOnlyList<RelicType> RelicOffers => relicOffers;
    public double RunTime => runTime;
    public bool IsGameOver => Health == 0;
    public IReadOnlyList<Obstacle> Obstacles => obstacles;
    public int Score => (int)Math.Clamp(Math.Floor(earnedPoints + 1e-7), 0d, int.MaxValue);
    public int Stage => StageAtDistance(Distance);
    public float ScoreMultiplier => MultiplierAtDistance(Distance) * PassivePointsMultiplier *
        (DoublePointsRemaining > 0f ? 2f : 1f);
    public float WorldSpeed => Speed * (SlowTimeRemaining > 0f ? .6f : 1f);

    private float PassivePointsMultiplier =>
        (relics.Contains(RelicType.GoldenFossil) ? 1.15f : 1f) *
        (relics.Contains(RelicType.BrokenCrown) && Health == 1 ? 1.4f : 1f);

    public static int StageAtDistance(float value) =>
        Math.Max(1, (int)MathF.Floor(value / (SpeedStageMeters * 10f)) + 1);

    public static float MultiplierAtDistance(float value) =>
        1f + Math.Max(0, (int)MathF.Floor(value / (MultiplierStageMeters * 10f))) * .2f;

    public static float SpeedAtDistance(float value)
    {
        int completedStages = StageAtDistance(value) - 1;
        int firstBoundary = FirstSpeedBoundaryMeters / SpeedStageMeters - 1;
        int secondBoundary = SecondSpeedBoundaryMeters / SpeedStageMeters - 1;
        int thirdBoundary = ThirdSpeedBoundaryMeters / SpeedStageMeters - 1;

        return BaseSpeed
            + Math.Min(completedStages, firstBoundary) * SpeedIncreaseBefore2000
            + Math.Clamp(completedStages - firstBoundary, 0, secondBoundary - firstBoundary) * SpeedIncreaseTo5000
            + Math.Clamp(completedStages - secondBoundary, 0, thirdBoundary - secondBoundary) * SpeedIncreaseTo8000
            + Math.Max(0, completedStages - thirdBoundary) * SpeedIncreaseAfter8000;
    }

    public void Reset()
    {
        Distance = 0f;
        earnedPoints = 0d;
        runTime = 0d;
        nextCardTime = RandomCardInterval();
        nextRelicTime = RelicIntervalSeconds;
        Speed = BaseSpeed;
        GroundOffset = 0f;
        CloudOffset = 0f;
        RunClock = 0f;
        spawnDistanceRemaining = 430f;
        closeGapStreak = 0;
        obstacles.Clear();
        cards.Clear();
        relics.Clear();
        cardOffers = Array.Empty<CardType>();
        relicOffers = Array.Empty<RelicType>();
        PendingReward = RewardKind.None;
        CrouchHeld = false;
        JumpRequested = false;
        Health = MaxHealth;
        MaximumHealth = MaxHealth;
        InvulnerabilityRemaining = 0f;
        SlowTimeRemaining = 0f;
        GhostRemaining = 0f;
        DoublePointsRemaining = 0f;
        ShieldCharges = 0;
        EggProtectionCharges = 0;
        damageSinceEgg = 0;
        Grounded = true;
        Crouching = false;
        DinoWidth = 44f;
        DinoHeight = 52f;
        DinoY = GroundY - DinoHeight;
        VerticalSpeed = 0f;
    }

    public void ClearInput()
    {
        CrouchHeld = false;
        JumpRequested = false;
        if (Grounded) SetPose(false);
    }

    public void Update(float deltaTime)
    {
        if (IsGameOver || PendingReward != RewardKind.None || deltaTime <= 0f) return;
        int parts = Math.Max(1, (int)MathF.Ceiling(
            SpeedAtDistance(Distance + Speed * deltaTime) * deltaTime / 8f));
        float partTime = deltaTime / parts;
        for (int part = 0; part < parts && !IsGameOver && PendingReward == RewardKind.None; part++)
            Advance(partTime);
    }

    private void Advance(float deltaTime)
    {
        Speed = SpeedAtDistance(Distance);
        float movement = WorldSpeed * deltaTime;
        bool ghostActive = GhostRemaining > 0f;
        AddDistancePoints(movement);
        Distance += movement;
        Speed = SpeedAtDistance(Distance);
        GroundOffset = (GroundOffset + movement) % 48f;
        CloudOffset = (CloudOffset + movement * .08f) % WorldWidth;
        RunClock += movement / 70f;

        if (Grounded && CrouchHeld && !Crouching) SetPose(true);
        if (Grounded && !CrouchHeld && Crouching) SetPose(false);
        if (JumpRequested) Jump();
        JumpRequested = false;

        if (!Grounded)
        {
            if (CrouchHeld) VerticalSpeed = Math.Max(VerticalSpeed, FastFallSpeed);
            VerticalSpeed += (CrouchHeld ? FastFallGravity : Gravity) * deltaTime;
            DinoY += VerticalSpeed * deltaTime;
            if (DinoY + DinoHeight >= GroundY)
            {
                Grounded = true;
                VerticalSpeed = 0f;
                DinoY = GroundY - DinoHeight;
                SetPose(CrouchHeld);
                DinoY = GroundY - DinoHeight;
            }
        }

        spawnDistanceRemaining -= movement;
        if (spawnDistanceRemaining <= 0f) SpawnObstacle();
        foreach (Obstacle obstacle in obstacles)
        {
            obstacle.X -= movement;
            if (obstacle.Kind == ObstacleKind.Flying) obstacle.FlapClock += deltaTime * 10f;
        }
        InvulnerabilityRemaining = Math.Max(0f, InvulnerabilityRemaining - deltaTime);
        Obstacle? collision = !ghostActive && InvulnerabilityRemaining <= 0f ? DetectCollision() : null;
        if (collision is not null)
        {
            if (ShieldCharges > 0) ShieldCharges--;
            else if (EggProtectionCharges > 0) EggProtectionCharges--;
            else
            {
                Health--;
                if (relics.Contains(RelicType.LuckyEggshell) && !IsGameOver && ++damageSinceEgg == 3)
                {
                    EggProtectionCharges++;
                    damageSinceEgg = 0;
                }
            }
            if (!IsGameOver) InvulnerabilityRemaining = InvulnerabilitySeconds;
        }
        obstacles.RemoveAll(obstacle => obstacle.X + obstacle.Width < -20f);
        SlowTimeRemaining = Math.Max(0f, SlowTimeRemaining - deltaTime);
        GhostRemaining = Math.Max(0f, GhostRemaining - deltaTime);
        DoublePointsRemaining = Math.Max(0f, DoublePointsRemaining - deltaTime);
        if (IsGameOver) return;

        runTime += deltaTime;
        CheckRewardSchedule();
    }

    private void AddDistancePoints(float movement)
    {
        double position = Distance;
        double finish = position + movement;
        double stageLength = MultiplierStageMeters * 10d;
        while (position < finish)
        {
            int multiplierStage = (int)Math.Floor(position / stageLength);
            double nextBoundary = (multiplierStage + 1d) * stageLength;
            double segmentEnd = Math.Min(finish, nextBoundary);
            earnedPoints += (segmentEnd - position) / 10d * (1d + multiplierStage * .2d) *
                PassivePointsMultiplier * (DoublePointsRemaining > 0f ? 2d : 1d);
            position = segmentEnd;
        }
    }

    private void Jump()
    {
        if (!Grounded || Crouching || CrouchHeld) return;
        Grounded = false;
        VerticalSpeed = -660f * (relics.Contains(RelicType.LightBones) ? MathF.Sqrt(1.15f) : 1f);
    }

    private void SetPose(bool crouching)
    {
        float bottom = DinoY + DinoHeight;
        Crouching = crouching;
        DinoWidth = crouching ? 58f : 44f;
        DinoHeight = crouching ? 30f : 52f;
        DinoY = bottom - DinoHeight;
    }

    private void SpawnObstacle()
    {
        bool flying = random.NextDouble() < .48;
        int level = random.Next(3);
        Obstacle obstacle;
        if (flying)
        {
            var size = FlyingSizes[level];
            obstacle = new Obstacle
            {
                Kind = ObstacleKind.Flying, Level = level, X = WorldWidth + 30f,
                Y = size.Y, Width = size.Width, Height = size.Height,
                FlapClock = (float)random.NextDouble() * 10f
            };
        }
        else
        {
            var size = GroundSizes[level];
            obstacle = new Obstacle
            {
                Kind = ObstacleKind.Ground, Level = level, X = WorldWidth + 30f,
                Y = GroundY - size.Height, Width = size.Width, Height = size.Height
            };
        }
        obstacles.Add(obstacle);
        spawnDistanceRemaining += NextObstacleGap(obstacle);
    }

    internal float NextObstacleGap(Obstacle previous)
    {
        float encounterSpeed = Math.Max(Speed, SpeedAtDistance(
            Distance + WorldWidth + 30f - DinoX + Speed * 2f));
        bool needsJump = previous.Kind == ObstacleKind.Ground || previous.Level == 0;
        float minimum = needsJump ? .72f : .40f;
        int distanceTiers = Math.Max(0, (int)MathF.Floor(Distance / (MultiplierStageMeters * 10f)));
        float pressure = Math.Min(.55f, distanceTiers * .055f);
        double roll = random.NextDouble();
        float variation = (float)random.NextDouble();
        float closeChance = .05f + pressure * .30f;
        if (roll < closeChance && closeGapStreak < 2)
        {
            closeGapStreak++;
            // A small edge-to-edge gap lets two or three obstacles be cleared in one jump.
            return previous.Width + 8f + variation * 36f;
        }

        closeGapStreak = 0;
        float shortChance = closeChance + .48f - pressure * .08f;
        float mediumChance = .88f + pressure * .14f;
        float extra = roll < shortChance ? variation * .16f :
            roll < mediumChance ? .12f + variation * .35f : .60f + variation * .50f;
        return encounterSpeed * (minimum + extra * (1f - pressure * .5f));
    }

    private Obstacle? DetectCollision()
    {
        RectangleF[] dinoBoxes = Crouching
            ? new[]
            {
                new RectangleF(DinoX + 7f, DinoY + 10f, 32f, 17f),
                new RectangleF(DinoX + 37f, DinoY + 3f, 18f, 16f)
            }
            : new[]
            {
                new RectangleF(DinoX + 15f, DinoY + 23f, 19f, 26f),
                new RectangleF(DinoX + 27f, DinoY + 3f, 22f, 21f)
            };

        foreach (Obstacle obstacle in obstacles)
        {
            RectangleF obstacleBox = obstacle.Kind == ObstacleKind.Flying
                ? new RectangleF(obstacle.X + 2f, obstacle.Y + 8f, obstacle.Width - 12f, 15f)
                : new RectangleF(obstacle.X + 4f, obstacle.Y + 3f, obstacle.Width - 8f, obstacle.Height - 6f);
            if (dinoBoxes.Any(box => box.IntersectsWith(obstacleBox))) return obstacle;
        }
        return null;
    }

    private double RandomCardInterval() => 10d + random.NextDouble() * 15d;

    private T[] PickOffers<T>(T[] pool)
    {
        T[] shuffled = (T[])pool.Clone();
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled.Take(3).ToArray();
    }

    private void CheckRewardSchedule()
    {
        bool relicDue = runTime + 1e-6 >= nextRelicTime;
        bool cardDue = runTime + 1e-6 >= nextCardTime;
        if (relicDue)
        {
            nextRelicTime += RelicIntervalSeconds;
            RelicType[] available = Enum.GetValues<RelicType>()
                .Where(relic => !relics.Contains(relic)).ToArray();
            if (available.Length > 0)
            {
                if (cardDue) nextCardTime += RandomCardInterval();
                relicOffers = PickOffers(available);
                PendingReward = RewardKind.Relic;
                return;
            }
        }
        if (cardDue)
        {
            nextCardTime += RandomCardInterval();
            cardOffers = PickOffers(Enum.GetValues<CardType>());
            PendingReward = RewardKind.Card;
        }
    }

    public bool ChooseCard(int offerIndex, int replaceSlot = -1)
    {
        if (PendingReward != RewardKind.Card || offerIndex < 0 || offerIndex >= cardOffers.Length)
            return false;
        if (cards.Count == CardCapacity)
        {
            if (replaceSlot < 0 || replaceSlot >= CardCapacity) return false;
            cards[replaceSlot] = cardOffers[offerIndex];
        }
        else cards.Add(cardOffers[offerIndex]);
        cardOffers = Array.Empty<CardType>();
        PendingReward = RewardKind.None;
        return true;
    }

    public bool DeclineCard()
    {
        if (PendingReward != RewardKind.Card || cards.Count < CardCapacity) return false;
        cardOffers = Array.Empty<CardType>();
        PendingReward = RewardKind.None;
        return true;
    }

    public bool ChooseRelic(int offerIndex)
    {
        if (PendingReward != RewardKind.Relic || offerIndex < 0 || offerIndex >= relicOffers.Length)
            return false;
        RelicType chosen = relicOffers[offerIndex];
        GrantRelic(chosen);
        relicOffers = Array.Empty<RelicType>();
        PendingReward = RewardKind.None;
        return true;
    }

    private void GrantRelic(RelicType chosen)
    {
        if (relics.Contains(chosen)) return;
        relics.Add(chosen);
        if (chosen != RelicType.AmberHeart) return;
        MaximumHealth++;
        Health = Math.Min(MaximumHealth, Health + 1);
    }

    public bool UseCard(int slot)
    {
        if (IsGameOver || PendingReward != RewardKind.None || slot < 0 || slot >= cards.Count)
            return false;
        CardType card = cards[slot];
        cards.RemoveAt(slot);
        switch (card)
        {
            case CardType.HealingHerb:
                Health = Math.Min(MaximumHealth, Health + 1);
                break;
            case CardType.Shield:
                ShieldCharges++;
                break;
            case CardType.SlowTime:
                SlowTimeRemaining = Math.Max(SlowTimeRemaining, 4f);
                break;
            case CardType.Ghost:
                GhostRemaining = Math.Max(GhostRemaining, 3f);
                break;
            case CardType.DoublePoints:
                DoublePointsRemaining = Math.Max(DoublePointsRemaining, 8f);
                break;
            case CardType.ClearObstacles:
                obstacles.RemoveAll(obstacle => obstacle.X < WorldWidth &&
                    obstacle.X + obstacle.Width > 0f);
                break;
        }
        return true;
    }

    internal void SetTestObstacles(IEnumerable<Obstacle> items)
    {
        obstacles.Clear();
        obstacles.AddRange(items);
        spawnDistanceRemaining = float.PositiveInfinity;
    }

    internal void SetTestDistance(float value)
    {
        Distance = value;
        Speed = SpeedAtDistance(value);
    }

    internal void SetTestRewardTimes(double cardTime, double relicTime)
    {
        nextCardTime = cardTime;
        nextRelicTime = relicTime;
    }

    internal void SetTestCards(params CardType[] items)
    {
        if (items.Length > CardCapacity) throw new ArgumentException("Too many test cards", nameof(items));
        cards.Clear();
        cards.AddRange(items);
    }

    internal void GrantTestRelic(RelicType chosen) => GrantRelic(chosen);
}
