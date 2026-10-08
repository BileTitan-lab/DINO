namespace DinoDash.Desktop;

internal enum BattleArtId
{
    RegenerativeSpores, BoneArmor, PrimalRage, AmberLag,
    BoneStorm, EarthquakeRoar, EnergyReturn, HuntingInstinct,
    HunterRush, TyrantSmash, TailResonance, SkySlam, BoneRain, PiercingSpear,
    SeekingSpikes, SpikeOverload, CounterInstinct, BloodRage, PredationRecovery,
    TerrorRoar, AfterimageHunt, PrimalBarrier, LifeBurn, GeneRampage, EmergencyMolt
}

internal enum EvolutionId
{
    SharpClaws, SturdyTail, HollowBones, ThunderFeet,
    TwinSpikes, HunterInstinct, HeavyScales, RegenerativeTissue,
    BoneRendingClaw, HuntingRhythm, ChargingClaw, SpinningTail, ResonantTail,
    GravityFeet, GroundRupture, PiercingSpikes, SplinteringSpikes, EnergyPredation,
    RapidRegeneration, DangerInstinct, EvasiveHunter, AdaptiveGene,
    InfiniteMarrow, TyrantBloodline, PredatoryRegeneration, RapidAdaptation,
    PrimalOverdrive, SkeletalProliferation
}

internal enum PowerStat
{
    ClawDamage, TailRange, TailKnockback, JumpHeight,
    StompRange, RangedProjectiles, RangedSpread, MaximumHealth,
    KillDashReduction, NextMapHealing, MeleeDamage, AllDamage, MoveSpeed, AttackSpeed,
    TailDamage, StompDamage, FinisherDamage, FinisherKnockback, FinisherLunge,
    ProjectileDamage, ProjectileSpeed, ProjectileLife, RechargeHits, MaxEnergy,
    DashCooldown, DashDistance, HurtInvincibility, DamageTaken, AirControl, KillHealing,
    ClawRange, RhythmHits, RhythmBonus, DashClawBonus, TailRecoveryRatio, TailEchoDamage,
    SlamHeightBonus, SlamMaximumBonus, SlamWaveDamage, SlamWaveRange, ProjectilePierce,
    ProjectileFragments, FragmentDamageRatio, MeleeKillEnergy, LowRoomHealing,
    CriticalDamage, CriticalSpeed, DodgeBonus, DodgeBonusDuration,
    FormAdaptBonus, FormAdaptDuration, EnergyCost, HeavyEchoDamage, KillHealThreshold,
    FormSwitchCooldown, InheritanceDuration, InheritanceStrength, LowHpThreshold,
    LowHpAttackSpeed, LowHpMoveSpeed, LowHpDamage, LowHpInvincibility,
    ExtraSpikeChance, ExtraSpikeCount
}

internal enum PowerOperation { Multiply, Add }
internal readonly record struct PowerModifier(PowerStat Stat, PowerOperation Operation, float Amount);

internal sealed record BattleArtDefinition(
    BattleArtId Id, string Name, string Icon, string Description,
    float Strength = 0f, float Duration = 0f, float Radius = 0f,
    float Knockback = 0f, int Projectiles = 0,float Speed=0f,float Delay=0f,
    float SecondaryStrength=0f,float Cost=0f,int Pierce=0);

internal enum EvolutionRarity { Normal, Rare }
internal enum EvolutionSource { NormalReward, BossReward, Merchant, RestStop, Mutation, Event }
internal enum EvolutionRestriction { Unrestricted, BossOnly }

internal sealed record EvolutionDefinition(
    EvolutionId Id, string Name, string Icon, string Description,
    params PowerModifier[] Modifiers)
{
    public EvolutionRarity Rarity { get; init; }
    public EvolutionRestriction SourceRestriction { get; init; }
    public bool Allows(EvolutionSource source)=>source==EvolutionSource.BossReward ||
        Rarity!=EvolutionRarity.Rare && SourceRestriction!=EvolutionRestriction.BossOnly;
}

// All tunable art strengths, durations, radii and passive modifiers live here.
// Gameplay asks the RunState for resolved stats; it does not know trait names.
internal static partial class ActPowerCatalog
{
    public const int BattleArtCapacity = 2;

    public static readonly IReadOnlyList<BattleArtDefinition> Arts =
    [
        new(BattleArtId.RegenerativeSpores, "再生孢子", "✚", "立即恢复 2 HP。", Strength: 2f),
        new(BattleArtId.BoneArmor, "骨甲", "◆", "抵挡下一次受到的伤害。", Strength: 1f),
        new(BattleArtId.PrimalRage, "原始狂暴", "爪", "6 秒内近战伤害提高 40%。", Strength: 1.4f, Duration: 6f),
        new(BattleArtId.AmberLag, "时滞琥珀", "琥", "5 秒内敌人行动速度降低 50%。", Strength: .5f, Duration: 5f),
        new(BattleArtId.BoneStorm, "骨刺风暴", "骨", "向周围发射 8 枚骨刺。", Strength: 1f, Projectiles: 8, Radius: 260f),
        new(BattleArtId.EarthquakeRoar, "地震咆哮", "震", "震击并击退附近地面敌人。", Strength: 2f, Radius: 215f, Knockback: 480f),
        new(BattleArtId.EnergyReturn, "能量回流", "能", "立即补满骨刺能量。"),
        new(BattleArtId.HuntingInstinct, "狩猎本能", "猎", "刷新冲刺；5 秒内移速提高 25%。", Strength: 1.25f, Duration: 5f),
        ..ExpandedArts()
    ];

    public static readonly IReadOnlyList<EvolutionDefinition> Evolutions =
    [
        new(EvolutionId.SharpClaws, "锋利前爪", "爪", "普通爪击伤害提高 15%。",
            new PowerModifier(PowerStat.ClawDamage, PowerOperation.Multiply, 1.15f)),
        new(EvolutionId.SturdyTail, "强壮尾骨", "尾", "尾扫范围提高 20%，击退提高 12%。",
            new PowerModifier(PowerStat.TailRange, PowerOperation.Multiply, 1.2f),
            new PowerModifier(PowerStat.TailKnockback, PowerOperation.Multiply, 1.12f)),
        new(EvolutionId.HollowBones, "空心骨骼", "跃", "最大跳跃高度提高 10%。",
            new PowerModifier(PowerStat.JumpHeight, PowerOperation.Multiply, 1.1f)),
        new(EvolutionId.ThunderFeet, "雷鸣足骨", "踏", "踩踏范围提高 25%。",
            new PowerModifier(PowerStat.StompRange, PowerOperation.Multiply, 1.25f)),
        new(EvolutionId.TwinSpikes, "双生骨刺", "刺", "每次骨刺攻击额外发射一枚斜向骨刺。",
            new PowerModifier(PowerStat.RangedProjectiles, PowerOperation.Add, 1f),
            new PowerModifier(PowerStat.RangedSpread, PowerOperation.Add, .14f)),
        new(EvolutionId.HunterInstinct, "猎手本能", "猎", "击杀敌人缩短冲刺冷却 0.4 秒。",
            new PowerModifier(PowerStat.KillDashReduction, PowerOperation.Add, .4f)),
        new(EvolutionId.HeavyScales, "厚重鳞片", "鳞", "最大 HP +1，立即恢复 1 HP。",
            new PowerModifier(PowerStat.MaximumHealth, PowerOperation.Add, 1f)),
        new(EvolutionId.RegenerativeTissue, "再生组织", "生", "进入下一张地图时恢复 1 HP。",
            new PowerModifier(PowerStat.NextMapHealing, PowerOperation.Add, 1f)),
        ..ExpandedEvolutions()
    ];

    public static BattleArtDefinition Art(BattleArtId id) => Arts[(int)id];
    public static EvolutionDefinition Evolution(EvolutionId id) => Evolutions[(int)id];
    public static IEnumerable<EvolutionDefinition> EligibleEvolutions(ActRunState run,EvolutionSource source)=>
        Evolutions.Where(e=>e.Allows(source) && !run.Has(e.Id));
}
