namespace DinoDash.Desktop;

internal enum RoomModuleKind { Battle, Event }
internal enum BattleRoomTier { Normal, Hard }

// Room purpose is independent of authored terrain and of its route reward.
// Future event rooms provide their own layout and completion condition here.
internal interface IChapterRoomModule
{
    RoomModuleKind Kind { get; }
    bool Enabled { get; }
    ActLevelLayout Build(ChapterRoomRun run,int seed,List<RoomCombatArea> areas,List<RoomExit> exits);
    bool IsComplete(ChapterRoomRun run,IReadOnlyList<RoomCombatArea> areas);
}

internal static class ActRoomModules
{
    public static readonly IReadOnlyList<IChapterRoomModule> Registered =
        [new BattleRoomModule(),new EventRoomPlaceholder()];
    public static IChapterRoomModule Get(RoomModuleKind kind)=>Registered.Single(m=>m.Kind==kind);

    private sealed class BattleRoomModule:IChapterRoomModule
    {
        public RoomModuleKind Kind=>RoomModuleKind.Battle;
        public bool Enabled=>true;
        public ActLevelLayout Build(ChapterRoomRun run,int seed,List<RoomCombatArea> areas,List<RoomExit> exits)=>
            ActChapterRooms.BuildBattle(run,seed,areas,exits);
        public bool IsComplete(ChapterRoomRun run,IReadOnlyList<RoomCombatArea> areas)=>
            areas.Count==RoomRewardRules.RequiredBattles(run.Tier) && areas.All(a=>a.Cleared);
    }

    private sealed class EventRoomPlaceholder:IChapterRoomModule
    {
        public RoomModuleKind Kind=>RoomModuleKind.Event;
        public bool Enabled=>false;
        public ActLevelLayout Build(ChapterRoomRun run,int seed,List<RoomCombatArea> areas,List<RoomExit> exits)=>
            throw new NotSupportedException("Event-room content is reserved and not enabled yet.");
        public bool IsComplete(ChapterRoomRun run,IReadOnlyList<RoomCombatArea> areas)=>run.EventCompleted;
    }
}

internal static class RoomRewardRules
{
    private static readonly RouteReward[] NormalRewards=[RouteReward.BattleArt,RouteReward.Bones,RouteReward.Heal];
    private static readonly RouteReward[] HardRewards=[RouteReward.Evolution,RouteReward.Form];
    public static IReadOnlyList<RouteReward> Allowed(BattleRoomTier tier)=>tier==BattleRoomTier.Hard?HardRewards:NormalRewards;
    public static BattleRoomTier TierFor(RouteReward reward)=>reward switch
    {
        RouteReward.Evolution or RouteReward.Form=>BattleRoomTier.Hard,
        RouteReward.BattleArt or RouteReward.Bones or RouteReward.Heal=>BattleRoomTier.Normal,
        _=>throw new ArgumentOutOfRangeException(nameof(reward),"No enabled room policy for this reward.")
    };
    public static int RequiredBattles(BattleRoomTier tier)=>tier==BattleRoomTier.Hard?3:2;
}
