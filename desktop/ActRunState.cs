namespace DinoDash.Desktop;

// In-memory state for one expedition. A map owns its enemies and pickups;
// this object owns everything that travels with the dinosaur to the next map.
internal sealed partial class ActRunState
{
    private readonly BattleArtId?[] arts = new BattleArtId?[ActPowerCatalog.BattleArtCapacity];
    private readonly HashSet<EvolutionId> evolutions = [];
    public IReadOnlyList<BattleArtId?> BattleArts => arts;
    public int BattleArtCount => arts.Count(id => id is not null);
    public IReadOnlyCollection<EvolutionId> Evolutions => evolutions;
    private float health = ActTuning.MaxHealth;
    public int Health => (int)MathF.Ceiling(health);
    public float ExactHealth => health;
    public int MaximumHealth => Math.Max(1, (int)Resolve(PowerStat.MaximumHealth, ActTuning.MaxHealth));
    public int ShieldCharges { get; private set; }
    public float RageRemaining { get; private set; }
    public float EnemySlowRemaining { get; private set; }
    public float HuntRemaining { get; private set; }
    public int BoneShards { get; private set; }
    public int Chapter { get; internal set; } = 1;
    public void AddBones(int amount) => BoneShards = (int)Math.Min(int.MaxValue, (long)BoneShards + Math.Max(0, amount));
    public bool SpendBones(int amount)
    {
        if (amount < 0 || BoneShards < amount) return false;
        BoneShards -= amount; return true;
    }

    public float Resolve(PowerStat stat, float baseValue)
    {
        float result = ApplyModifiers(ActPrimalCatalog.Form(CurrentForm).Modifiers, stat, baseValue);
        foreach (EvolutionId id in evolutions)
            foreach (PowerModifier modifier in ActPowerCatalog.Evolution(id).Modifiers)
                if (modifier.Stat == stat)
                    result = modifier.Operation == PowerOperation.Multiply
                        ? result * modifier.Amount : result + modifier.Amount;
        foreach (var id in mutations.Concat(activeMapMutations))
            result = ApplyModifiers(ActPrimalCatalog.Mutation(id).Modifiers, stat, result);
        return ResolveBuild(stat, result);
    }

    public bool Has(EvolutionId id) => evolutions.Contains(id);
    public bool AddEvolution(EvolutionId id, EvolutionSource source = EvolutionSource.NormalReward)
    {
        if (!Enum.IsDefined(id) || !ActPowerCatalog.Evolution(id).Allows(source)) return false;
        int previousMaximum = MaximumHealth;
        if (!evolutions.Add(id)) return false;
        Heal(MaximumHealth - previousMaximum);
        return true;
    }

    public void SetHealth(int value) => health = Math.Clamp(value, 0, MaximumHealth);
    public void Heal(int amount) => health = Math.Min(MaximumHealth, health + Math.Max(0, amount));
    public void HealFraction(float amount) => health = Math.Min(MaximumHealth, health + Math.Max(0, amount));
    public void TakeDamage(float amount) => health = Math.Max(0, health - Math.Max(0, amount));
    public bool AbsorbHit()
    {
        if (BarrierCharges > 0 && Remaining(BuildBuff.Barrier) > 0) { BarrierCharges--; return true; }
        if (ShieldCharges <= 0) return false;
        ShieldCharges--;
        return true;
    }
    public void AddShield(int charges) => ShieldCharges += Math.Max(0, charges);
    public void StartRage(float duration) => RageRemaining = Math.Max(RageRemaining, duration);
    public void StartEnemySlow(float duration) => EnemySlowRemaining = Math.Max(EnemySlowRemaining, duration);
    public void StartHunt(float duration) => HuntRemaining = Math.Max(HuntRemaining, duration);
    public void Tick(float dt)
    {
        TickBuild(dt);
        FormCooldown = Math.Max(0, FormCooldown - dt);
        RageRemaining = Math.Max(0f, RageRemaining - dt);
        EnemySlowRemaining = Math.Max(0f, EnemySlowRemaining - dt);
        HuntRemaining = Math.Max(0f, HuntRemaining - dt);
    }
    public void EnterNextMap()
    {
        activeMapMutations.Clear();
        activeMapMutations.UnionWith(pendingMapMutations);
        pendingMapMutations.Clear();
        Heal((int)Resolve(PowerStat.NextMapHealing, 0f));
    }

    public bool AddArt(BattleArtId id, int? replaceSlot = null)
    {
        if (replaceSlot is >= 0 and < ActPowerCatalog.BattleArtCapacity)
        { arts[replaceSlot.Value] = id; return true; }
        if (replaceSlot is not null) return false;
        int empty = Array.FindIndex(arts, item => item is null);
        if (empty < 0) return false;
        arts[empty] = id;
        return true;
    }
    public BattleArtId? ConsumeArt(int slot)
    {
        if (slot < 0 || slot >= arts.Length) return null;
        BattleArtId? id = arts[slot];
        arts[slot] = null;
        return id;
    }

    public ActRunState Clone()
    {
        var copy = new ActRunState();
        Array.Copy(arts, copy.arts, arts.Length);
        copy.evolutions.UnionWith(evolutions);
        copy.health = health;
        CopyPrimalState(copy);
        copy.ShieldCharges = ShieldCharges;
        copy.RageRemaining = RageRemaining;
        copy.EnemySlowRemaining = EnemySlowRemaining;
        copy.HuntRemaining = HuntRemaining;
        copy.BoneShards = BoneShards;
        copy.Chapter = Chapter;
        copy.RoomJourney = RoomJourney?.Clone();
        CopyBuild(copy);
        return copy;
    }
}
