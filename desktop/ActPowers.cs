namespace DinoDash.Desktop;

internal enum ActRewardChoice { None, BattleArt, Evolution, Form, Mutation }

internal sealed partial class ActEngine
{
    private static readonly Dictionary<BattleArtId, Action<ActEngine, BattleArtDefinition>>
        ArtEffects = new Dictionary<BattleArtId, Action<ActEngine, BattleArtDefinition>>
        {
            [BattleArtId.RegenerativeSpores] = (game, art) =>
                game.Run.Heal((int)art.Strength),
            [BattleArtId.BoneArmor] = (game, art) =>
                game.Run.AddShield((int)art.Strength),
            [BattleArtId.PrimalRage] = (game, art) =>
                game.Run.StartRage(art.Duration),
            [BattleArtId.AmberLag] = (game, art) =>
                game.Run.StartEnemySlow(art.Duration),
            [BattleArtId.BoneStorm] = (game, art) => game.FireBoneStorm(art),
            [BattleArtId.EarthquakeRoar] = (game, art) => game.EarthquakeRoar(art),
            [BattleArtId.EnergyReturn] = (game, _) =>
                game.RangedEnergy = game.MaximumEnergy,
            [BattleArtId.HuntingInstinct] = (game, art) =>
            {
                game.DashCooldownRemaining = 0f;
                game.Run.StartHunt(art.Duration);
            }
        };
    static ActEngine() => RegisterExpandedArts();

    private Random powerRandom = new();
    private readonly List<BattleArtId> artOffers = [];
    private readonly List<EvolutionId> evolutionOffers = [];
    private bool bossEvolutionOffered;
    public ActReward? BossEvolutionReward { get; private set; }
    public ActRunState Run { get; private set; } = new();
    public int MaximumHealth => Run.MaximumHealth;
    public ActRewardChoice PendingPowerReward { get; private set; }
    public IReadOnlyList<BattleArtId> ArtOffers => artOffers;
    public IReadOnlyList<EvolutionId> EvolutionOffers => evolutionOffers;
    public BattleArtId? PowerCueArt { get; private set; }
    public bool PowerCueEvolution { get; private set; }
    public bool PowerCueRare { get; private set; }
    public EvolutionSource PendingEvolutionSource { get; private set; }
    public float PowerCueRemaining { get; private set; }
    public float EnemySpeedRatio => Run.EnemySlowRemaining > 0f
        ? ActPowerCatalog.Art(BattleArtId.AmberLag).Strength : 1f;

    public ActRunState ExportRunState() => Run.Clone();

    // The next map implementation can call this without persisting anything to disk.
    // It rebuilds only map-owned state and applies next-map passive healing.
    public void EnterNextMap(int? seed = null, bool? roomAdventure = null)
    {
        int energy = RangedEnergy;
        int routeSeed=Run.RoomJourney?.Seed ?? unchecked(Seed ^ 0x539271);
        RestStop = null;
        MapIndex = Math.Min(ActChapters.Count, MapIndex + 1);
        roomAdventureEnabled=roomAdventure ?? roomAdventureEnabled;
        Run.RoomJourney=null;
        if(roomAdventureEnabled)BeginChapterRooms(seed ?? routeSeed);
        ResetMap(seed);
        RangedEnergy = energy;
        Run.EnterNextMap();
    }

    // Debug-only direct selection. The caller chooses whether to carry the
    // current expedition; map-local enemies, pickups and timers always reset.
    internal void LoadMapForDebug(int mapIndex, int? seed, bool preserveRun, bool startAtBoss = false, bool roomAdventure = false)
    {
        if (mapIndex is < 1 or > ActChapters.Count) throw new ArgumentOutOfRangeException(nameof(mapIndex));
        if (!preserveRun) Run = new ActRunState();
        else if (Run.Health == 0) Run.SetHealth(Run.MaximumHealth);
        if(preserveRun){Run.EnterRest();Run.EnterNextMap();}
        MapIndex = mapIndex;
        roomAdventureEnabled=roomAdventure;
        RestStop = null;
        Run.RoomJourney=null;
        if(roomAdventure)BeginChapterRooms(seed ?? ActLevelRandomizer.NewSeed(),startAtBoss);
        ResetMap(seed);
        if (startAtBoss)
        {
            float x = Math.Max(layout.BossArenaLeft + 20, Boss.X - 280);
            PlacePlayerForTest(x, (TerrainYAt(x + PlayerWidth / 2) ?? ActTuning.GroundY) - PlayerHeight);
            safeX = x;
            safeFeetY = PlayerY + PlayerHeight;
        }
    }

    private void ResetPowers(int seed)
    {
        powerRandom = new Random(seed ^ 0x61c88647);
        artOffers.Clear();
        evolutionOffers.Clear();
        PendingPowerReward = ActRewardChoice.None;
        PowerCueRemaining = 0f;
        PowerCueArt = null;
        PowerCueEvolution = false;
        PowerCueRare = false;
        bossEvolutionOffered = false;
        BossEvolutionReward = null;
        BossFormReward = null;
        formOffers.Clear(); mutationOffers.Clear();
    }

    private void UpdatePowerTimers(float dt)
    {
        bool overloaded=Run.Remaining(BuildBuff.Overload)>0;
        Run.Tick(dt);
        if(overloaded && Run.Remaining(BuildBuff.Overload)<=0)RangedEnergy=0;
        PowerCueRemaining = Math.Max(0f, PowerCueRemaining - dt);
    }

    public bool OfferBattleArts()
    {
        if (PendingPowerReward != ActRewardChoice.None) return false;
        artOffers.Clear();
        artOffers.AddRange(ActPowerCatalog.Arts.OrderBy(_ => powerRandom.Next()).Take(3)
            .Select(definition => definition.Id));
        PendingPowerReward = ActRewardChoice.BattleArt;
        return true;
    }

    public bool OfferEvolutions(EvolutionSource source = EvolutionSource.NormalReward)
    {
        if (PendingPowerReward != ActRewardChoice.None) return false;
        evolutionOffers.Clear();
        var pool=ActPowerCatalog.EligibleEvolutions(Run,source).ToArray();
        var normals=pool.Where(d=>d.Rarity==EvolutionRarity.Normal).OrderBy(_=>powerRandom.Next()).ToArray();
        var rares=pool.Where(d=>d.Rarity==EvolutionRarity.Rare).OrderBy(_=>powerRandom.Next()).ToArray();
        bool rare=source==EvolutionSource.BossReward && rares.Length>0 &&
            powerRandom.NextDouble()<(MapIndex==ActChapters.Count?ActBuildTuning.FinalBossRareChance:ActBuildTuning.BossRareChance);
        evolutionOffers.AddRange(normals.Take(rare?2:3).Select(d=>d.Id));
        if(rare)evolutionOffers.Add(rares[0].Id);
        if(evolutionOffers.Count==0 && source==EvolutionSource.BossReward && rares.Length>0)evolutionOffers.Add(rares[0].Id);
        if (evolutionOffers.Count == 0) return false;
        PendingPowerReward = ActRewardChoice.Evolution;
        PendingEvolutionSource=source;
        return true;
    }

    public bool ChooseBattleArt(int offerIndex, int? replaceSlot = null)
    {
        if (PendingPowerReward != ActRewardChoice.BattleArt ||
            offerIndex < 0 || offerIndex >= artOffers.Count ||
            !Run.AddArt(artOffers[offerIndex], replaceSlot)) return false;
        artOffers.Clear();
        PendingPowerReward = ActRewardChoice.None;
        return true;
    }

    public bool DeclineBattleArt()
    {
        if (PendingPowerReward != ActRewardChoice.BattleArt) return false;
        artOffers.Clear();
        PendingPowerReward = ActRewardChoice.None;
        return true;
    }

    public bool ChooseEvolution(int offerIndex)
    {
        if (PendingPowerReward != ActRewardChoice.Evolution ||
            offerIndex < 0 || offerIndex >= evolutionOffers.Count ||
            !Run.AddEvolution(evolutionOffers[offerIndex],PendingEvolutionSource)) return false;
        PowerCueRare=ActPowerCatalog.Evolution(evolutionOffers[offerIndex]).Rarity==EvolutionRarity.Rare;
        evolutionOffers.Clear();
        PendingPowerReward = ActRewardChoice.None;
        PowerCueArt = null;
        PowerCueEvolution = true;
        PowerCueRemaining = 1.1f;
        return true;
    }

    public bool UseBattleArt(int slot)
    {
        if (IsDead || IsComplete || IsRoomAntechamber || PendingPowerReward != ActRewardChoice.None ||
            slot < 0 || slot >= Run.BattleArts.Count ||
            Run.BattleArts[slot] is not BattleArtId id) return false;
        BattleArtDefinition art = ActPowerCatalog.Art(id);
        ArtEffects[id](this, art);
        Run.ConsumeArt(slot);
        PowerCueArt = id;
        PowerCueEvolution = false;
        PowerCueRare = false;
        PowerCueRemaining = .8f;
        return true;
    }

    private void FireBoneStorm(BattleArtDefinition art)
    {
        float x = PlayerX + PlayerWidth / 2f;
        float y = PlayerY + PlayerHeight / 2f;
        for (int i = 0; i < art.Projectiles; i++)
        {
            float angle = i * 2f * MathF.PI / art.Projectiles;
            float vx = MathF.Cos(angle) * ActCombatTuning.BoneSpikeSpeed * Run.Resolve(PowerStat.ProjectileSpeed,1);
            float vy = MathF.Sin(angle) * ActCombatTuning.BoneSpikeSpeed * Run.Resolve(PowerStat.ProjectileSpeed,1);
            projectiles.Add(new BoneSpike { X = x, Y = y,
                Direction = vx < 0 ? -1 : 1, Vx = vx, Vy = vy,
                Life = art.Radius / ActCombatTuning.BoneSpikeSpeed * Run.Resolve(PowerStat.ProjectileLife,1),
                Damage = Run.Resolve(PowerStat.ProjectileDamage,art.Strength),
                PierceRemaining=(int)Run.Resolve(PowerStat.ProjectilePierce,0),
                Homing=Run.CurrentForm==PrimalForm.Pterosaur, Special = true });
        }
    }

    private void EarthquakeRoar(BattleArtDefinition art)
    {
        float center = PlayerX + PlayerWidth / 2f;
        foreach (ActEnemy enemy in enemies.Where(enemy =>
            enemy.State != ActEnemyState.Dead && !enemy.Dormant &&
            enemy.Kind != ActEnemyKind.Flying &&
            TerrainYAt(enemy.X + enemy.Width / 2f) is float ground &&
            Math.Abs(enemy.Y + enemy.Height - ground) < 42f &&
            Math.Abs(enemy.X + enemy.Width / 2f - center) <= art.Radius).ToArray())
            DamageEnemy(enemy, (int)art.Strength, art.Knockback, false);
        AddImpact(center, TerrainYAt(center) ?? ActTuning.GroundY, true);
    }

    private void NoteEnemyKill()
    {
        DashCooldownRemaining = Math.Max(0f, DashCooldownRemaining - Run.Resolve(PowerStat.KillDashReduction, 0f));
        if(powerRandom.NextDouble()<Run.Resolve(PowerStat.KillHealing,0))Run.Heal(1);
    }

    private void SpawnBossEvolutionReward()
    {
        if (bossEvolutionOffered) return;
        bossEvolutionOffered = true;
        BossEvolutionReward = new ActReward(ActRewardKind.Evolution,
            Boss.X + Boss.Width / 2f, Boss.Y + Boss.Height - 26f)
        {
            // Let the collapse finish before overlapping melee players collect it.
            AvailableAfter = RunTime + .9f
        };
        layout.Rewards.Add(BossEvolutionReward);
        BossFormReward = new ActReward(ActRewardKind.Form,
            Math.Min(layout.BossArenaRight-45,Boss.X+Boss.Width/2+95),
            (TerrainYAt(Boss.X+Boss.Width/2+95)??ActTuning.GroundY)-26)
            { AvailableAfter=RunTime+.9f };
        layout.Rewards.Add(BossFormReward);
    }
}
