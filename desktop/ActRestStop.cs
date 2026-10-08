using System.Drawing;

namespace DinoDash.Desktop;

// Editable first-pass balance. Prices and weights belong here, not in UI code.
internal static class ActRestTuning
{
    public const float FireHealingRatio = .5f;
    public const int MinimumFacilities = 1, MaximumFacilities = 2;
    public const double MerchantChance = .5, CommonBoneChance = .3;
    public const int CommonBonesMin = 3, CommonBonesMax = 7;
    public const int HeavyBonesMin = 12, HeavyBonesMax = 20;
    public const int BossBonesMin = 65, BossBonesMax = 85;
    public const int SupplyBonesMin = 20, SupplyBonesMax = 40;
    public const float ExitRadius = 70, ExitSeconds = 3, InteractionRadius = 85;
    public static readonly (RestFacilityKind Kind, int Weight)[] FacilityWeights =
    [(RestFacilityKind.BattleArt, 30), (RestFacilityKind.Evolution, 15),
     (RestFacilityKind.Bones, 30), (RestFacilityKind.Egg, 25)];
    public static readonly (ShopKind Kind, int Weight, int Min, int Max)[] Goods =
    [(ShopKind.Art, 40, 40, 60), (ShopKind.Evolution, 12, 90, 120),
     (ShopKind.Healing, 30, 30, 40), (ShopKind.Energy, 25, 20, 30)];
    public const int ShopHealing = 2;
    internal static int Pick(Random rng, IEnumerable<int> weights)
    {
        int[] pool = weights.ToArray();
        int roll = rng.Next(pool.Sum());
        for (int i = 0; i < pool.Length; i++) { roll -= pool[i]; if (roll < 0) return i; }
        return pool.Length - 1;
    }
}

internal enum RestFacilityKind { Fire, Merchant, BattleArt, Evolution, Bones, Egg }
internal enum ShopKind { Art, Evolution, Healing, Energy }
internal sealed class RestFacility(RestFacilityKind kind, float x)
{
    public RestFacilityKind Kind { get; } = kind;
    public float X { get; } = x;
    public bool Used { get; set; }
    public bool DrainedByMutation { get; set; }
    public string Name => Kind switch { RestFacilityKind.Fire => "守夜火堆",
        RestFacilityKind.Merchant => "游商", RestFacilityKind.BattleArt => "战技遗刻",
        RestFacilityKind.Evolution => "进化之芽", RestFacilityKind.Bones => "骨片储藏",
        _ => "异变龙蛋" };
}
internal sealed class RestShopItem(ShopKind kind, int price, BattleArtId? art = null, EvolutionId? evolution = null)
{
    public ShopKind Kind { get; } = kind;
    public int Price { get; } = price;
    public BattleArtId? Art { get; } = art;
    public EvolutionId? Evolution { get; } = evolution;
    public bool Sold { get; set; }
    public string Name => Art is { } a ? ActPowerCatalog.Art(a).Name :
        Evolution is { } e ? ActPowerCatalog.Evolution(e).Name : Kind == ShopKind.Healing ? "生命补给" : "骨刺补给";
    public string Description => Art is { } a ? ActPowerCatalog.Art(a).Description :
        Evolution is { } e ? ActPowerCatalog.Evolution(e).Description : Kind == ShopKind.Healing ?
        $"恢复 {ActRestTuning.ShopHealing} 点生命" : "补满骨刺能量";
}
internal sealed class ActRestStop
{
    public int Seed { get; }
    public List<RestFacility> Facilities { get; } = [new(RestFacilityKind.Fire, 550)];
    public List<RestShopItem> Stock { get; } = [];
    internal Random Random { get; }
    public ActRestStop(int seed, ActRunState run):this(seed,run,false) { }
    public static ActRestStop CreateAntechamber(int seed,ActRunState run)=>new(seed,run,true);
    private ActRestStop(int seed,ActRunState run,bool antechamber)
    {
        Seed = seed; Random = new Random(seed);
        if(antechamber)
        {
            Facilities.Clear();
            Facilities.Add(new(RestFacilityKind.Merchant,ChapterRoomTuning.AntechamberMerchantX));
            Facilities.Add(new(RestFacilityKind.Bones,ChapterRoomTuning.AntechamberBonesX));
            GenerateStock(run);return;
        }
        int count = Random.Next(ActRestTuning.MinimumFacilities, ActRestTuning.MaximumFacilities + 1);
        var chosen = new List<RestFacilityKind>();
        if (count > 0 && Random.NextDouble() < ActRestTuning.MerchantChance) chosen.Add(RestFacilityKind.Merchant);
        var pool = ActRestTuning.FacilityWeights.ToList();
        while (chosen.Count < count && pool.Count > 0)
        {
            int index = ActRestTuning.Pick(Random, pool.Select(p => p.Weight));
            chosen.Add(pool[index].Kind); pool.RemoveAt(index);
        }
        for (int i = 0; i < chosen.Count; i++) Facilities.Add(new(chosen[i], 940 + i * 280));
        if(chosen.Contains(RestFacilityKind.Merchant))GenerateStock(run);
    }
    private void GenerateStock(ActRunState run)
    {
        for (int i = 0; i < 3; i++)
        {
            var traits = ActPowerCatalog.EligibleEvolutions(run,EvolutionSource.Merchant).Where(t =>
                !Stock.Any(s => s.Evolution == t.Id)).ToArray();
            var goods = ActRestTuning.Goods.Where(g => g.Kind != ShopKind.Evolution || traits.Length > 0).ToArray();
            var pick = goods[ActRestTuning.Pick(Random, goods.Select(g => g.Weight))];
            Stock.Add(new(pick.Kind, Random.Next(pick.Min, pick.Max + 1),
                pick.Kind == ShopKind.Art ? ActPowerCatalog.Arts[Random.Next(ActPowerCatalog.Arts.Count)].Id : null,
                pick.Kind == ShopKind.Evolution ? traits[Random.Next(traits.Length)].Id : null));
        }
    }
    public static ActLevelLayout CreateLayout(int seed)
    {
        var map = new ActLevelLayout(seed) { Width = 1800, Exit = 1660 };
        map.Terrain.Add(new("守夜营地", 0, 1800, 300, 300));
        map.Solids.Add(new RectangleF(0, 300, 1800, 120));
        map.Zones.Add(new(0, "守夜营地"));
        return map;
    }
}

internal sealed class ActBoneDrop(float x, float y, int amount)
{
    public float X { get; } = x;
    public float Y { get; } = y;
    public int Amount { get; } = amount;
    public bool Collected { get; set; }
}

internal sealed partial class ActEngine
{
    public ActRestStop? RestStop { get; private set; }
    private ActRestStop? roomServices;
    private ActRestStop? FacilityStop=>RestStop??roomServices;
    public IReadOnlyList<RestShopItem> ShopStock=>FacilityStop?.Stock??[];
    public IReadOnlyList<RestFacility> RoomFacilities=>roomServices?.Facilities??[];
    public bool IsResting => RestStop != null;
    public bool InteractRequested { get; set; }
    public bool ShopRequested { get; private set; }
    public float ExitCountdown { get; private set; }
    public bool ExitUnlocked => IsResting || Boss.State == ActEnemyState.Dead &&
        BossEvolutionReward is { Collected: true };
    public string RestNotice { get; private set; } = "";
    private float noticeRemaining;
    private Random boneRandom = new();
    private readonly List<ActBoneDrop> boneDrops = [];
    public IReadOnlyList<ActBoneDrop> BoneDrops => boneDrops;
    public RestFacility? NearbyFacility => FacilityStop?.Facilities.Where(f =>
        Math.Abs(f.X - PlayerX - PlayerWidth / 2) < ActRestTuning.InteractionRadius &&
        Math.Abs(PlayerY + PlayerHeight - 300) < 65).OrderBy(f => Math.Abs(f.X-PlayerX)).FirstOrDefault();

    private void ResetRoomServices()=>roomServices=IsRoomAntechamber?
        ActRestStop.CreateAntechamber(layout.Seed^0x213ab,Run):null;

    public void EnterRestStop(int? seed = null)
    {
        if (IsResting) return;
        int energy = RangedEnergy;
        Run.EnterRest();
        RestStop = new ActRestStop(seed ?? (Seed ^ (MapIndex * 19793)), Run);
        if(MapIndex==ActChapters.Count)
            foreach(var egg in RestStop.Facilities.Where(f=>f.Kind==RestFacilityKind.Egg).ToArray())
            {
                RestStop.Facilities.Remove(egg);
                var substitute=new[]{RestFacilityKind.Bones,RestFacilityKind.BattleArt,RestFacilityKind.Evolution}
                    .First(kind=>RestStop.Facilities.All(f=>f.Kind!=kind));
                RestStop.Facilities.Add(new(substitute,egg.X));
            }
        ResetMap(RestStop.Seed);
        RangedEnergy = energy;
    }
    public void TravelFromExit()
    {
        if (!IsComplete) return;
        if(IsRoomJourney && Run.RoomJourney!.Stage!=RoomJourneyStage.Boss)
        {TravelToNextRoom();return;}
        if (IsResting) EnterNextMap();
        else EnterRestStop();
    }
    private void ResetRestTravel()
    {
        ExitCountdown = 0; ShopRequested = false; RestNotice = ""; noticeRemaining = 0;
        boneRandom = new Random(Seed ^ 0x142fea);
        boneDrops.Clear();
        if (!IsResting) foreach (var branch in layout.Shortcuts)
        {
            float x = (branch.StartX + branch.EndX) / 2;
            float y = solids.Where(s => s.Left <= x && s.Right >= x && s.Top < 300)
                .Select(s => s.Top).DefaultIfEmpty(TerrainYAt(x) ?? 300).Min();
            boneDrops.Add(new(x, y - 23, boneRandom.Next(20,36)));
        }
    }
    private void UpdateExitCountdown(float dt)
    {
        if(IsRoomJourney && Run.RoomJourney!.Stage!=RoomJourneyStage.Boss)
        {UpdateRoomTravel(dt);return;}
        bool within = ExitUnlocked && PendingPowerReward == ActRewardChoice.None &&
            Math.Abs(PlayerX + PlayerWidth / 2 - ExitX) <= ActRestTuning.ExitRadius &&
            Math.Abs(PlayerY + PlayerHeight - (TerrainYAt(ExitX) ?? 300)) < 100;
        ExitCountdown = within ? ExitCountdown + dt : 0;
        if (ExitCountdown + .0001f >= ActRestTuning.ExitSeconds) IsComplete = true;
    }
    private void DropBones(ActEnemy enemy)
    {
        if (IsResting) return;
        int min, max;
        if (enemy.Kind == ActEnemyKind.Boss) { min = ActRestTuning.BossBonesMin; max = ActRestTuning.BossBonesMax; }
        else if (enemy.Kind == ActEnemyKind.Heavy) { min = ActRestTuning.HeavyBonesMin; max = ActRestTuning.HeavyBonesMax; }
        else
        {
            if (boneRandom.NextDouble() >= ActRestTuning.CommonBoneChance) return;
            min = ActRestTuning.CommonBonesMin; max = ActRestTuning.CommonBonesMax;
        }
        float x = enemy.X + enemy.Width / 2;
        boneDrops.Add(new(x, (enemy.Kind == ActEnemyKind.Flying ? TerrainYAt(x) ?? 300 : enemy.Y + enemy.Height) - 20,
            boneRandom.Next(min,max+1)));
    }
    private void UpdateBoneDrops(float dt)
    {
        noticeRemaining = Math.Max(0, noticeRemaining-dt);
        if (noticeRemaining == 0) RestNotice = "";
        foreach (var drop in boneDrops)
            if (!drop.Collected && Math.Abs(PlayerX+PlayerWidth/2-drop.X)<55 &&
                Math.Abs(PlayerY+PlayerHeight/2-drop.Y)<65)
            { drop.Collected = true; Run.AddBones(drop.Amount); Notice($"骨片 +{drop.Amount}"); }
    }
    private void Notice(string text) { RestNotice = text; noticeRemaining = 3; }
    private void UpdateRest(float dt)
    {
        UpdateBoneDrops(dt);
        if (InteractRequested) InteractWithRest();
        InteractRequested = false;
    }
    public bool InteractWithRest()
    {
        if (IsComplete || PendingPowerReward != ActRewardChoice.None || NearbyFacility is not { } facility || facility.Used) return false;
        var rest = FacilityStop!;
        if (facility.Kind == RestFacilityKind.Merchant) { ShopRequested = true; return true; }
        facility.Used = true;
        switch (facility.Kind)
        {
            case RestFacilityKind.Fire:
                Run.Heal((int)Math.Ceiling(MaximumHealth * ActRestTuning.FireHealingRatio));
                Notice("已休息 · 火光仍在守候"); break;
            case RestFacilityKind.Bones:
                int amount = IsRoomAntechamber?ChapterRoomTuning.AntechamberBones:
                    rest.Random.Next(ActRestTuning.SupplyBonesMin, ActRestTuning.SupplyBonesMax+1);
                Run.AddBones(amount); Notice($"获得 {amount} 骨片"); break;
            case RestFacilityKind.BattleArt: OfferBattleArts(); break;
            case RestFacilityKind.Evolution:
                if (!OfferEvolutions(EvolutionSource.RestStop)) Notice("已获得全部普通进化特质"); break;
            case RestFacilityKind.Egg:
                if(!OfferMutations())facility.Used=false;
                break;
        }
        AddImpact(facility.X, 270, false);
        return true;
    }
    public void CloseShop() => ShopRequested = false;
    public string PurchaseProblem(int index)
    {
        var store=FacilityStop;
        if (store==null || !ShopRequested || store.Facilities.All(f => f.Kind != RestFacilityKind.Merchant)) return "商店未开放";
        if (index < 0 || index >= store.Stock.Count) return "商品不存在";
        var item = store.Stock[index];
        if (item.Sold) return "已售罄";
        if (item.Evolution is { } e && Run.Has(e)) return "已拥有此进化";
        if (item.Evolution is { } restricted && !ActPowerCatalog.Evolution(restricted).Allows(EvolutionSource.Merchant)) return "此进化仅能由 Boss 奖励获得";
        if (Run.BoneShards < item.Price) return "骨片不足";
        if (item.Kind == ShopKind.Healing && Health == MaximumHealth) return "生命已满";
        if (item.Kind == ShopKind.Energy && RangedEnergy == MaximumEnergy) return "骨刺能量已满";
        return "";
    }
    public bool Purchase(int index, int? replaceSlot = null)
    {
        if (PurchaseProblem(index).Length != 0) return false;
        var item = FacilityStop!.Stock[index];
        if (item.Art is { } art && !Run.AddArt(art, replaceSlot)) return false;
        if (item.Evolution is { } evo && !Run.AddEvolution(evo,EvolutionSource.Merchant)) return false;
        if (item.Kind == ShopKind.Healing) Run.Heal(ActRestTuning.ShopHealing);
        if (item.Kind == ShopKind.Energy) RangedEnergy = MaximumEnergy;
        Run.SpendBones(item.Price); item.Sold = true;
        Notice($"已购买：{item.Name}");
        return true;
    }
}
