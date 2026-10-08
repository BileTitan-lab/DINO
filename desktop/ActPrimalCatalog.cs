namespace DinoDash.Desktop;

internal enum PrimalForm { Base, Raptor, Tyrannosaur, Pterosaur, Ankylosaur }
internal enum MutationId { BloodGene, HollowFrame, Brutality, SpikeGrowth, OverEvolution, UnstableCells, BloodTissue, BrittleShell }
internal enum MutationDuration { Run, NextMap }
internal sealed record FormDefinition(PrimalForm Id, string Name, string Icon, string Description, params PowerModifier[] Modifiers);
internal sealed record MutationDefinition(MutationId Id, string Name, string Cost, string Benefit,
    MutationDuration Duration, int HpCost, int MaxHpCost, int ArtCost, int EvolutionCount, bool FireCost,
    params PowerModifier[] Modifiers);

internal static class ActPrimalCatalog
{
    public const int FormCapacity = 3; // Includes the starting base form.
    public const float SwitchCooldown = 5f;
    public const float HomingTurnRate = 4f, HomingRadius = 420f;
    private static PowerModifier M(PowerStat stat, float amount) => new(stat, PowerOperation.Multiply, amount);
    private static PowerModifier A(PowerStat stat, float amount) => new(stat, PowerOperation.Add, amount);
    public static readonly FormDefinition[] Forms =
    [
        new(PrimalForm.Base,"基础形态","原","原始战斗手感，无额外修正。"),
        new(PrimalForm.Raptor,"迅猛形态","迅","爪击速度 +30%，移动 +15%；第三击突进增强，尾扫范围 -15%，冲刺冷却减半。",
            M(PowerStat.AttackSpeed,1.3f),M(PowerStat.MoveSpeed,1.15f),M(PowerStat.TailRange,.85f),M(PowerStat.DashCooldown,.5f),A(PowerStat.FinisherLunge,32)),
        new(PrimalForm.Tyrannosaur,"暴龙形态","暴","近战伤害翻倍，攻速 -25%、移动 -15%；第三击、尾扫与踩踏强化，骨刺回复较慢。",
            M(PowerStat.MeleeDamage,2),M(PowerStat.AttackSpeed,.75f),M(PowerStat.MoveSpeed,.85f),M(PowerStat.FinisherDamage,1.3f),
            M(PowerStat.FinisherKnockback,1.5f),M(PowerStat.TailDamage,1.2f),M(PowerStat.TailKnockback,1.4f),M(PowerStat.StompRange,1.2f),M(PowerStat.StompDamage,1.2f),M(PowerStat.RechargeHits,1.5f)),
        new(PrimalForm.Pterosaur,"翼龙形态","翼","跳跃 +15%，空中操控增强；骨刺速度 +30%、射程增强并追踪，近战回复骨刺加快，近战伤害 -20%。",
            M(PowerStat.JumpHeight,1.15f),M(PowerStat.AirControl,1.5f),M(PowerStat.ProjectileSpeed,1.3f),M(PowerStat.ProjectileLife,1.4f),M(PowerStat.RechargeHits,.5f),M(PowerStat.MeleeDamage,.8f)),
        new(PrimalForm.Ankylosaur,"甲龙形态","甲","受到伤害减半，攻速 -15%；尾扫范围 +40%、击退 +50%，踩踏范围 +35%，无敌时间 +25%，冲刺距离 -15%。",
            M(PowerStat.DamageTaken,.5f),M(PowerStat.AttackSpeed,.85f),M(PowerStat.TailRange,1.4f),M(PowerStat.TailKnockback,1.5f),
            M(PowerStat.StompRange,1.35f),M(PowerStat.HurtInvincibility,1.25f),M(PowerStat.DashDistance,.85f))
    ];
    public static readonly MutationDefinition[] Mutations =
    [
        new(MutationId.BloodGene,"狂血基因","立即失去 2 HP，最低保留 1 HP","本局近战伤害 +25%",MutationDuration.Run,2,0,0,0,false,M(PowerStat.MeleeDamage,1.25f)),
        new(MutationId.HollowFrame,"空心骨骼","本局最大 HP -1","本局移动 +10%，跳跃高度 +15%",MutationDuration.Run,0,1,0,0,false,A(PowerStat.MaximumHealth,-1),M(PowerStat.MoveSpeed,1.1f),M(PowerStat.JumpHeight,1.15f)),
        new(MutationId.Brutality,"暴虐本能","当前 HP 立即变为 1","下一张地图所有伤害 +50%；进入再下一处休息处移除",MutationDuration.NextMap,-1,0,0,0,false,M(PowerStat.AllDamage,1.5f)),
        new(MutationId.SpikeGrowth,"骨刺增殖","本局骨刺最大能量 -1","本局骨刺伤害 +40%",MutationDuration.Run,0,0,0,0,false,A(PowerStat.MaxEnergy,-1),M(PowerStat.ProjectileDamage,1.4f)),
        new(MutationId.OverEvolution,"过度进化","随机失去一个持有的战技","随机获得一个尚未拥有的进化特质",MutationDuration.Run,0,0,1,1,false),
        new(MutationId.UnstableCells,"不稳定细胞","本局最大 HP -1","随机获得两个尚未拥有的进化特质",MutationDuration.Run,0,1,0,2,false,A(PowerStat.MaximumHealth,-1)),
        new(MutationId.BloodTissue,"嗜血组织","此处火堆立即失效","本局每次击杀有 25% 概率恢复 1 HP",MutationDuration.Run,0,0,0,0,true,A(PowerStat.KillHealing,.25f)),
        new(MutationId.BrittleShell,"脆化甲壳","本局受伤无敌时间缩短 35%","本局所有伤害 +20%",MutationDuration.Run,0,0,0,0,false,M(PowerStat.HurtInvincibility,.65f),M(PowerStat.AllDamage,1.2f))
    ];
    public static FormDefinition Form(PrimalForm id) => Forms[(int)id];
    public static MutationDefinition Mutation(MutationId id) => Mutations[(int)id];
}

internal sealed partial class ActRunState
{
    private readonly HashSet<PrimalForm> forms = [PrimalForm.Base];
    private readonly HashSet<MutationId> mutations = [];
    private readonly HashSet<MutationId> pendingMapMutations = [];
    private readonly HashSet<MutationId> activeMapMutations = [];
    public IReadOnlyCollection<PrimalForm> Forms => forms;
    public IReadOnlyCollection<MutationId> Mutations => mutations;
    public IReadOnlyCollection<MutationId> PendingMapMutations => pendingMapMutations;
    public IReadOnlyCollection<MutationId> ActiveMapMutations => activeMapMutations;
    public PrimalForm CurrentForm { get; private set; }
    public float FormCooldown { get; private set; }
    public int MaximumEnergy => Math.Max(1,(int)Resolve(PowerStat.MaxEnergy,ActTuning.MaxRangedEnergy));
    public bool AddForm(PrimalForm id) => Enum.IsDefined(id) && forms.Count<ActPrimalCatalog.FormCapacity && forms.Add(id);
    public bool SwitchForm(PrimalForm id)
    {
        if (!forms.Contains(id) || CurrentForm==id || FormCooldown>0) return false;
        var previous=CurrentForm;
        CurrentForm=id; FormCooldown=ActPrimalCatalog.SwitchCooldown*Resolve(PowerStat.FormSwitchCooldown,1);
        NoteFormChanged(previous); return true;
    }
    public void EnterRest() => activeMapMutations.Clear();
    public bool HasMutation(MutationId id) => mutations.Contains(id) || pendingMapMutations.Contains(id) || activeMapMutations.Contains(id);
    internal void AddMutation(MutationId id)
    {
        if(ActPrimalCatalog.Mutation(id).Duration==MutationDuration.NextMap) pendingMapMutations.Add(id);
        else mutations.Add(id);
        health=Math.Min(health,MaximumHealth);
    }
    private static float ApplyModifiers(IEnumerable<PowerModifier> mods, PowerStat stat,float value)
    {
        foreach(var m in mods.Where(m=>m.Stat==stat)) value=m.Operation==PowerOperation.Multiply?value*m.Amount:value+m.Amount;
        return value;
    }
    private void CopyPrimalState(ActRunState copy)
    {
        copy.forms.UnionWith(forms);copy.mutations.UnionWith(mutations);
        copy.pendingMapMutations.UnionWith(pendingMapMutations);copy.activeMapMutations.UnionWith(activeMapMutations);
        copy.CurrentForm=CurrentForm;copy.FormCooldown=FormCooldown;
    }
}
