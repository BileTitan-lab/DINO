namespace DinoDash.Desktop;

internal static class ActBuildTuning
{
    public const double BossRareChance=.35, FinalBossRareChance=.65;
    public const float EmpowerKnockback=1.8f, PredatorHealPerKill=.5f;
    public const float CounterInvincibility=.35f, TailLaunchSpeed=310f;
    public const float RainHeight=220f, SpearLife=1.6f, SeekingLife=2f;
    public const float AfterimageDelay=.4f, AfterimageLife=1.1f, AfterimageDamage=2f, AfterimageRadius=105f;
    public const float EchoDelay=.12f, EchoLife=.4f, EchoRadius=72f;
    public const float GroundWaveSpeed=310f, FragmentSpeed=360f, FragmentLife=.5f;
}

internal static partial class ActPowerCatalog
{
    private static PowerModifier M(PowerStat stat,float value)=>new(stat,PowerOperation.Multiply,value);
    private static PowerModifier A(PowerStat stat,float value)=>new(stat,PowerOperation.Add,value);
    private static BattleArtDefinition[] ExpandedArts()=>
    [
        new(BattleArtId.HunterRush,"猎杀突进","冲","高速突进，沿途撞击敌人；期间无敌。",3,.58f,95,500,Speed:740),
        new(BattleArtId.TyrantSmash,"霸王重击","爪","下一次爪击命中造成 250% 伤害并强力击退。",2.5f),
        new(BattleArtId.TailResonance,"尾骨震荡","尾","立即大范围尾扫，将附近敌人击飞。",3,Radius:210,Knockback:560),
        new(BattleArtId.SkySlam,"天降猛击","踏","跃起后快速下砸，落地形成大范围震波。",4,Radius:220,Knockback:480,Speed:560,Delay:.3f),
        new(BattleArtId.BoneRain,"骨雨","骨","短暂预警后，屏幕上方落下 9 枚骨刺。",1.5f,Projectiles:9,Speed:680,Delay:.55f),
        new(BattleArtId.PiercingSpear,"贯穿骨矛","矛","向前发射大型骨矛，贯穿多个敌人。",3,Speed:720,Pierce:20),
        new(BattleArtId.SeekingSpikes,"追猎骨刺","刺","发射 5 枚低伤害追踪骨刺。",.65f,Projectiles:5,Speed:420),
        new(BattleArtId.SpikeOverload,"骨刺过载","能","5 秒内骨刺不耗能；结束时能量归零。",Duration:5),
        new(BattleArtId.CounterInstinct,"反击本能","盾","4 秒内免疫首次攻击，并反击周围敌人。",3,4,175,340),
        new(BattleArtId.BloodRage,"血怒","血","失去 1 HP（最低 1）；8 秒内近战与攻速提高。",1.6f,8,SecondaryStrength:1.35f,Cost:1),
        new(BattleArtId.PredationRecovery,"捕食恢复","生","10 秒内击杀恢复少量生命，最多回复 2 HP。",2,10,SecondaryStrength:ActBuildTuning.PredatorHealPerKill),
        new(BattleArtId.TerrorRoar,"恐惧咆哮","震","普通敌人恐惧退避 2.5 秒；不控制 Boss。",Duration:2.5f,Radius:300,Speed:85),
        new(BattleArtId.AfterimageHunt,"残影狩猎","影","6 秒内冲刺结束留下残影，延迟发动攻击。",ActBuildTuning.AfterimageDamage,6,ActBuildTuning.AfterimageRadius,Delay:ActBuildTuning.AfterimageDelay),
        new(BattleArtId.PrimalBarrier,"原始屏障","盾","5 秒内最多抵挡两次伤害。",2,5),
        new(BattleArtId.LifeBurn,"生命燃烧","血","失去 2 HP（最低 1），补满骨刺并强化伤害 10 秒。",1.35f,10,Cost:2),
        new(BattleArtId.GeneRampage,"基因暴走","基","随机强化一种主要攻击能力，持续 12 秒。",1.7f,12),
        new(BattleArtId.EmergencyMolt,"紧急蜕变","形","刷新形态冷却并切换下一形态，强化伤害 5 秒。",1.25f,5)
    ];
    private static EvolutionDefinition[] ExpandedEvolutions()=>
    [
        new(EvolutionId.BoneRendingClaw,"裂骨爪","爪","第三段爪击伤害提高 30%。",M(PowerStat.FinisherDamage,1.3f)),
        new(EvolutionId.HuntingRhythm,"猎食节奏","猎","每三次近战命中，下一次爪击伤害提高。",A(PowerStat.RhythmHits,3),M(PowerStat.RhythmBonus,1.5f)),
        new(EvolutionId.ChargingClaw,"冲锋利爪","冲","冲刺结束后的首次爪击伤害提高 35%。",M(PowerStat.DashClawBonus,1.35f)),
        new(EvolutionId.SpinningTail,"回旋尾","尾","尾扫命中至少两敌时，减少后摇。",M(PowerStat.TailRecoveryRatio,.45f)),
        new(EvolutionId.ResonantTail,"震荡尾骨","震","尾扫命中时产生小型二次震波。",A(PowerStat.TailEchoDamage,.75f)),
        new(EvolutionId.GravityFeet,"重力足骨","踏","下坠越高，冲击越强；加成有上限。",A(PowerStat.SlamHeightBonus,.006f),A(PowerStat.SlamMaximumBonus,1.2f)),
        new(EvolutionId.GroundRupture,"破地冲击","震","下砸落地后向两侧释放短程地面震波。",A(PowerStat.SlamWaveDamage,1),A(PowerStat.SlamWaveRange,150)),
        new(EvolutionId.PiercingSpikes,"贯穿骨刺","刺","骨刺额外贯穿一个敌人。",A(PowerStat.ProjectilePierce,1)),
        new(EvolutionId.SplinteringSpikes,"碎裂骨刺","骨","骨刺命中后向周围射出低伤害碎片。",A(PowerStat.ProjectileFragments,3),M(PowerStat.FragmentDamageRatio,.35f)),
        new(EvolutionId.EnergyPredation,"能量猎食","能","近战击杀额外恢复 1 点骨刺能量。",A(PowerStat.MeleeKillEnergy,1)),
        new(EvolutionId.RapidRegeneration,"快速再生","生","进入新战斗房时，低于半血则回复 1 HP。",A(PowerStat.LowRoomHealing,1)),
        new(EvolutionId.DangerInstinct,"危险本能","猎","只剩 1 HP 时，移动和伤害提高。",M(PowerStat.CriticalDamage,1.2f),M(PowerStat.CriticalSpeed,1.12f)),
        new(EvolutionId.EvasiveHunter,"闪避猎手","闪","冲刺成功避伤后，短时间强化下一次命中。",M(PowerStat.DodgeBonus,1.4f),A(PowerStat.DodgeBonusDuration,3)),
        new(EvolutionId.AdaptiveGene,"适应性基因","基","切换形态后，5 秒内所有伤害提高 15%。",M(PowerStat.FormAdaptBonus,1.15f),A(PowerStat.FormAdaptDuration,5)),
        new(EvolutionId.InfiniteMarrow,"无限骨髓","骨","骨刺不再耗能，但骨刺伤害降低 30%。",M(PowerStat.EnergyCost,0),M(PowerStat.ProjectileDamage,.7f))
            {Rarity=EvolutionRarity.Rare,SourceRestriction=EvolutionRestriction.BossOnly},
        new(EvolutionId.TyrantBloodline,"暴君血脉","爪","近战范围扩大；重击产生震荡，骨刺伤害降低。",M(PowerStat.ClawRange,1.15f),M(PowerStat.TailRange,1.1f),M(PowerStat.StompRange,1.1f),A(PowerStat.HeavyEchoDamage,1),M(PowerStat.ProjectileDamage,.7f))
            {Rarity=EvolutionRarity.Rare,SourceRestriction=EvolutionRestriction.BossOnly},
        new(EvolutionId.PredatoryRegeneration,"捕食再生","生","每累计击杀 5 个敌人，自动恢复 1 HP。",A(PowerStat.KillHealThreshold,5))
            {Rarity=EvolutionRarity.Rare,SourceRestriction=EvolutionRestriction.BossOnly},
        new(EvolutionId.RapidAdaptation,"极速适应","形","形态冷却大幅降低，短暂继承上一形态的优势。",M(PowerStat.FormSwitchCooldown,.35f),A(PowerStat.InheritanceDuration,4),A(PowerStat.InheritanceStrength,.5f))
            {Rarity=EvolutionRarity.Rare,SourceRestriction=EvolutionRestriction.BossOnly},
        new(EvolutionId.PrimalOverdrive,"原始暴走","血","HP≤2 时速度与伤害提高，但受伤无敌缩短。",A(PowerStat.LowHpThreshold,2),M(PowerStat.LowHpAttackSpeed,1.3f),M(PowerStat.LowHpMoveSpeed,1.18f),M(PowerStat.LowHpDamage,1.35f),M(PowerStat.LowHpInvincibility,.6f))
            {Rarity=EvolutionRarity.Rare,SourceRestriction=EvolutionRestriction.BossOnly},
        new(EvolutionId.SkeletalProliferation,"骨骼增殖","刺","发射骨刺时有 35% 概率额外生成两枚小骨刺。",A(PowerStat.ExtraSpikeChance,.35f),A(PowerStat.ExtraSpikeCount,2))
            {Rarity=EvolutionRarity.Rare,SourceRestriction=EvolutionRestriction.BossOnly}
    ];
}
