namespace DinoDash.Desktop;

internal sealed partial class ActRunState
{
    public ChapterRoomRun? RoomJourney { get; set; }
}

internal sealed partial class ActEngine
{
    private readonly List<RoomCombatArea> combatAreas=[];
    private readonly List<RoomExit> roomExits=[];
    public bool IsRoomJourney => !IsResting && Run.RoomJourney is { } journey && journey.Chapter==MapIndex;
    public int ChapterSeed => Run.RoomJourney?.Seed ?? Seed;
    public IReadOnlyList<RoomCombatArea> CombatAreas=>combatAreas;
    public IReadOnlyList<RoomExit> RoomExits=>roomExits;
    public bool RoomCleared=>IsRoomJourney && Run.RoomJourney!.Stage==RoomJourneyStage.Combat &&
        ActRoomModules.Get(Run.RoomJourney.Module).IsComplete(Run.RoomJourney,combatAreas);
    public bool IsRoomAntechamber=>IsRoomJourney && Run.RoomJourney!.Stage==RoomJourneyStage.Antechamber;
    public bool RoomExitReady=>RoomCleared && Run.RoomJourney is {RewardGranted:true} &&
        PendingPowerReward==ActRewardChoice.None;
    private int hoveredRoomExit=-1;

    private void BeginChapterRooms(int seed,bool boss=false)
    {
        Run.RoomJourney=new ChapterRoomRun(seed,MapIndex);
        if(boss)Run.RoomJourney.Stage=RoomJourneyStage.Boss;
        else ActChapterRooms.SelectRoom(Run.RoomJourney);
    }

    private void UpdateRoomProgress()
    {
        if(!IsRoomJourney || Run.RoomJourney!.Stage!=RoomJourneyStage.Combat)return;
        foreach(var area in combatAreas)
        {
            if(area.Cleared)continue;
            if(area.Enemies.All(e=>e.State==ActEnemyState.Dead))
            {area.Cleared=true;Notice("战场已清理");}
        }
        var journey=Run.RoomJourney!;
        if(!RoomCleared || journey.RewardGranted)return;
        journey.RewardGranted=true;
        enemyShots.Clear();bossHazards.Clear();
        bool offered=journey.Reward switch
        {
            RouteReward.Evolution=>OfferEvolutions() || OfferForms(),
            RouteReward.BattleArt=>OfferBattleArts(),
            RouteReward.Form=>OfferForms(),
            _=>false
        };
        if(journey.Reward==RouteReward.Heal)
        {Run.Heal(ChapterRoomTuning.Recovery);RangedEnergy=MaximumEnergy;Notice("鸡腿补给 · 生命与骨刺恢复");}
        else if(journey.Reward==RouteReward.Bones || journey.Reward==RouteReward.BattleArt && !offered)
        {Run.AddBones(ChapterRoomTuning.BoneReward);Notice($"房间奖励 · 骨片 +{ChapterRoomTuning.BoneReward}");}
        else if(!offered && journey.Tier==BattleRoomTier.Hard)
            Notice("本局进化与形态奖励已收集完毕");
        ClearInput();
    }

    // A future event module calls this after its interaction, rather than
    // creating dummy enemies or pretending that an empty battle is cleared.
    public bool CompleteRoomEvent()
    {
        if(!IsRoomJourney || Run.RoomJourney!.Stage!=RoomJourneyStage.Combat ||
            Run.RoomJourney.Module!=RoomModuleKind.Event ||
            !ActRoomModules.Get(RoomModuleKind.Event).Enabled)return false;
        Run.RoomJourney.EventCompleted=true;
        UpdateRoomProgress();return true;
    }

    private void UpdateRoomTravel(float dt)
    {
        var journey=Run.RoomJourney!;
        if(journey.Stage==RoomJourneyStage.Boss)return;
        if(journey.Stage==RoomJourneyStage.Antechamber)
        {
            bool inside=Math.Abs(PlayerX+PlayerWidth/2-ExitX)<ActRestTuning.ExitRadius;
            ExitCountdown=inside?ExitCountdown+dt:0;
        }
        else
        {
            var door=RoomExitReady?roomExits.FirstOrDefault(e=>e.Bounds.IntersectsWith(PlayerBounds)):null;
            int id=door?.Index??-1;
            if(id!=hoveredRoomExit)ExitCountdown=0;
            hoveredRoomExit=id;
            ExitCountdown=id>=0?ExitCountdown+dt:0;
            if(ExitCountdown>=ActRestTuning.ExitSeconds)journey.SelectedExit=id;
        }
        if(ExitCountdown>=ActRestTuning.ExitSeconds)IsComplete=true;
    }

    private void TravelToNextRoom()
    {
        var journey=Run.RoomJourney!;
        if(journey.Stage==RoomJourneyStage.Antechamber)journey.Stage=RoomJourneyStage.Boss;
        else if(journey.Depth>=journey.TotalRooms)journey.Stage=RoomJourneyStage.Antechamber;
        else
        {
            var exit=roomExits.First(e=>e.Index==journey.SelectedExit);
            journey.Reward=exit.Reward;journey.Tier=exit.Tier;journey.Module=exit.Module;journey.Depth++;
            ActChapterRooms.SelectRoom(journey);
        }
        int energy=RangedEnergy;
        ResetMap(null);RangedEnergy=energy;
    }
}
