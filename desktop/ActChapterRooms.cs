using System.Drawing;

namespace DinoDash.Desktop;

internal enum ChapterRoomType { Combat, Terrain, Heights, Elite, Hazard, Branch }
internal enum RouteReward { Evolution, BattleArt, Bones, Heal, Form, Unknown }
internal enum RoomJourneyStage { Combat, Antechamber, Boss }
internal sealed record ChapterRoomDefinition(string Id, string Name, ChapterRoomType Type, int Difficulty, int Shape,
    RoomModuleKind Module=RoomModuleKind.Battle,bool Enabled=true,int Chapter=0);
internal sealed record RoomExit(int Index, float X, float FeetY, RouteReward Reward, BattleRoomTier Tier,
    RoomModuleKind Module=RoomModuleKind.Battle)
{
    public bool Dangerous=>Tier==BattleRoomTier.Hard;
    public RectangleF Bounds => new(X - 36, FeetY - 90, 72, 90);
}
internal sealed class RoomCombatArea(int index, float left, float right)
{
    public int Index { get; } = index;
    public float Left { get; } = left;
    public float Right { get; } = right;
    public bool Cleared { get; set; }
    public List<ActEnemy> Enemies { get; } = [];
    public string GroupId { get; set; } = "";
}
internal static class ChapterRoomTuning
{
    public const int MinimumRooms = 4, MaximumRooms = 5;
    public const int BoneReward = 40, Recovery = 2;
    public const int AntechamberBones = 40;
    public const float AntechamberMerchantX = 430f, AntechamberBonesX = 780f;
    public const float ThirdExitChance = .25f;
    public const float HardExitShare = .2f;
    public const float FirstAreaEnd = 1350f, TransitionLength = 360f;
    public const float SecondAreaStart = FirstAreaEnd + TransitionLength;
    public const float SecondAreaEnd = SecondAreaStart + 1050f;
    public const float ThirdAreaStart = SecondAreaEnd + 150f, ThirdAreaEnd = ThirdAreaStart + 690f;
    public const float TransitionRecoveryChance = .65f, TransitionHealChance = .7f;
    public const float ExitHallLength = 1500f;
    public const float GroundExitOffset = 180f, HighExitOffset = 760f, LowerExitOffset = 1280f;
    public const float HighExitFeet = 100f, LowerExitFeet = 380f;
}

// Owned by the run, rather than a level. Same seed and exit decisions reproduce
// rooms, authored enemy groups and reward directions without a global RNG.
internal sealed class ChapterRoomRun(int seed,int chapter=2)
{
    public int Seed { get; } = seed;
    public int Chapter { get; } = chapter;
    public int TotalRooms { get; } = chapter==1?4:new Random(seed).Next(ChapterRoomTuning.MinimumRooms, ChapterRoomTuning.MaximumRooms + 1);
    public int Depth { get; set; } = 1;
    public RoomJourneyStage Stage { get; set; }
    public string CurrentRoomId { get; set; } = "";
    public string TransitionId { get; set; } = "";
    public RouteReward Reward { get; set; } = RouteReward.Bones;
    public RoomModuleKind Module { get; set; } = RoomModuleKind.Battle;
    public BattleRoomTier Tier { get; set; }
    public bool EventCompleted { get; set; }
    public bool RewardGranted { get; set; }
    public int SelectedExit { get; set; } = -1;
    public List<string> Visited { get; } = [];
    public List<string> Groups { get; } = [];
    public int LastShape { get; set; } = -1;
    public int LastDifficulty { get; set; }
    public ChapterRoomRun Clone()
    {
        var copy = new ChapterRoomRun(Seed,Chapter) { Depth=Depth, Stage=Stage, CurrentRoomId=CurrentRoomId, TransitionId=TransitionId,
            Reward=Reward, Module=Module, Tier=Tier, EventCompleted=EventCompleted, RewardGranted=RewardGranted, SelectedExit=SelectedExit,
            LastShape=LastShape, LastDifficulty=LastDifficulty };
        copy.Visited.AddRange(Visited); copy.Groups.AddRange(Groups); return copy;
    }
    public Random RandomFor(int salt) => new(unchecked(Seed ^ Depth * 7919 ^ (int)Stage * 104729 ^ salt ^ (Chapter-2)*297121));
}

internal static partial class ActChapterRooms
{
    public static readonly ChapterRoomDefinition[] CanyonPool =
    [
        new("canyon-crossing", "风蚀石道", ChapterRoomType.Combat, 1, 0),
        new("canyon-ridge", "裂脊坡顶", ChapterRoomType.Terrain, 1, 1),
        new("canyon-basin", "回声低谷", ChapterRoomType.Terrain, 1, 2),
        new("canyon-gap", "断岩桥隙", ChapterRoomType.Terrain, 2, 3),
        new("canyon-perches", "鹰落高台", ChapterRoomType.Heights, 2, 4),
        new("canyon-fork", "双层岩廊", ChapterRoomType.Branch, 2, 5),
        new("canyon-cave", "沉岩洞道", ChapterRoomType.Branch, 1, 6),
        new("canyon-lift", "浮石阶梯", ChapterRoomType.Heights, 2, 7),
        new("canyon-hunters", "巨兽裂谷", ChapterRoomType.Elite, 3, 8),
        new("canyon-storm", "崩岩回廊", ChapterRoomType.Hazard, 3, 9)
    ];
    public static readonly ChapterRoomDefinition[] ReservedRooms =
        Enumerable.Range(1,ActChapters.Count).Select(chapter=>new ChapterRoomDefinition(
            $"chapter-{chapter}-event-placeholder","事件房占位",ChapterRoomType.Combat,0,-1,RoomModuleKind.Event,false,chapter)).ToArray();
    private static readonly EnemyGroup[] LightGroups =
    [new("scouts", [ActEnemyKind.Melee,ActEnemyKind.Melee]),
     new("wing-scout", [ActEnemyKind.Melee,ActEnemyKind.Flying]),
     new("lookout", [ActEnemyKind.Ranged,ActEnemyKind.Melee])];
    private static readonly EnemyGroup[] MixedGroups =
    [new("heavy-guard", [ActEnemyKind.Heavy,ActEnemyKind.Ranged]),
     new("wing-pack", [ActEnemyKind.Melee,ActEnemyKind.Melee,ActEnemyKind.Flying]),
     new("heavy-pack", [ActEnemyKind.Heavy,ActEnemyKind.Melee,ActEnemyKind.Melee])];
    private static readonly EnemyGroup[] HeightGroups =
    [new("high-watch", [ActEnemyKind.Ranged,ActEnemyKind.Flying]),
     new("high-pack", [ActEnemyKind.Flying,ActEnemyKind.Melee,ActEnemyKind.Ranged])];

    public static void SelectRoom(ChapterRoomRun run)
    {
        var random=run.RandomFor(0x15412);
        var definitions=PoolFor(run.Chapter);
        var pool=definitions.Concat(ReservedRooms.Where(r=>r.Chapter==run.Chapter)).Where(r=>r.Enabled && r.Module==run.Module &&
            ActRoomModules.Get(r.Module).Enabled && !run.Visited.Contains(r.Id) &&
            (run.Tier!=BattleRoomTier.Normal || r.Difficulty<3) &&
            (run.Depth>2 || r.Difficulty<3) &&
            (run.Depth<run.TotalRooms || r.Difficulty<3) &&
            (run.LastDifficulty<3 || r.Difficulty<3)).ToArray();
        var varied=pool.Where(r=>r.Type!=definitions.FirstOrDefault(p=>p.Shape==run.LastShape)?.Type).ToArray();
        if(varied.Length>0)pool=varied;
        if(run.Tier==BattleRoomTier.Hard)
        {
            var harder=pool.Where(r=>r.Difficulty>=2).ToArray();
            if(harder.Length>0)pool=harder;
        }
        if(pool.Length==0)throw new InvalidOperationException("No eligible authored rooms remain for this chapter.");
        // The first teaching encounter has a stable, readable ground route.
        var chosen=run.Chapter==1 && run.Depth==1?definitions[0]:pool[random.Next(pool.Length)];
        run.CurrentRoomId=chosen.Id;run.Visited.Add(chosen.Id);
        run.LastShape=chosen.Shape;run.LastDifficulty=chosen.Difficulty;
        ActRoomTransitions.Select(run);
        run.RewardGranted=false;run.EventCompleted=false;run.SelectedExit=-1;
    }

    public static ActLevelLayout Build(ChapterRoomRun run, List<RoomCombatArea> areas, List<RoomExit> exits)
    {
        areas.Clear();exits.Clear();
        int seed=run.RandomFor(0x1793).Next(1,int.MaxValue);
        if(run.Stage!=RoomJourneyStage.Combat)return BuildFinal(run,seed);
        var module=ActRoomModules.Get(run.Module);
        if(!module.Enabled)throw new NotSupportedException("This room module is not enabled.");
        return module.Build(run,seed,areas,exits);
    }

    internal static ActLevelLayout BuildBattle(ChapterRoomRun run,int seed,List<RoomCombatArea> areas,List<RoomExit> exits)
    {
        if(!RoomRewardRules.Allowed(run.Tier).Contains(run.Reward))
            throw new InvalidOperationException("Room tier and reward are incompatible.");
        var room=PoolFor(run.Chapter).First(r=>r.Id==run.CurrentRoomId);
        float hall=(run.Tier==BattleRoomTier.Hard?ChapterRoomTuning.ThirdAreaEnd:ChapterRoomTuning.SecondAreaEnd)+110;
        var map=new ActLevelLayout(seed) { Width=hall+ChapterRoomTuning.ExitHallLength, Exit=hall+1380,
            SpawnX=90,SpawnFeetY=300,BossArenaLeft=10000,BossArenaRight=11000 };
        map.Zones.Add(new(0,$"{room.Name} · {run.Depth}/{run.TotalRooms}"));
        if(run.Chapter==2)Terrain(map,room.Shape,run.TransitionId);
        else BuildThemedTerrain(map,run,room);
        areas.Add(new(0,360,ChapterRoomTuning.FirstAreaEnd));
        areas.Add(new(1,ChapterRoomTuning.SecondAreaStart,ChapterRoomTuning.SecondAreaEnd));
        if(run.Tier==BattleRoomTier.Hard)areas.Add(new(2,ChapterRoomTuning.ThirdAreaStart,ChapterRoomTuning.ThirdAreaEnd));
        var rng=run.RandomFor(0x55711);
        GenerateExits(run,exits,hall,rng);
        BuildExitHall(map,hall,exits);
        if(run.Chapter!=2)ActPlatformLayout.Finalize(map);
        foreach(var area in areas)
        {
            EnemyGroup[] pool=run.Chapter!=2?GroupsFor(run,room,area.Index):area.Index==0?LightGroups:
                room.Type==ChapterRoomType.Heights?HeightGroups:MixedGroups;
            var options=pool.Where(g=>run.Groups.LastOrDefault()!=g.Id).ToArray();
            var group=options[rng.Next(options.Length)];
            area.GroupId=group.Id;run.Groups.Add(group.Id);
            for(int i=0;i<group.Kinds.Length;i++)
            {
                float x=run.Chapter==2?area.Left+240+i*205:
                    area.Left+(area.Right-area.Left)*(i+1)/(group.Kinds.Length+1);
                var enemy=ActLevel.Make(group.Kinds[i],x);
                if(run.Chapter!=2)x=SupportedSpawn(map,x,enemy.Width);
                float foot=ActSecondMap.GroundAt(map,x+enemy.Width/2)??300;
                if(run.Chapter==2 && room.Type==ChapterRoomType.Heights && area.Index==1 && enemy.Kind==ActEnemyKind.Ranged)
                { map.Solids.Add(new(x-35,foot-95,150,14));foot-=95; }
                else if(run.Chapter>=3 && enemy.Kind==ActEnemyKind.Ranged && room.Type is ChapterRoomType.Heights or ChapterRoomType.Branch)
                {
                    var perch=map.Solids.Where(p=>p.Height<=16 && p.Left>=area.Left && p.Right<=area.Right &&
                        p.Top<foot-65).OrderBy(p=>Math.Abs(p.Left-x)).FirstOrDefault();
                    if(perch.Width>0){x=perch.Left+perch.Width/2-enemy.Width/2;foot=perch.Top;}
                }
                float y=enemy.Kind==ActEnemyKind.Flying?foot-135:foot-enemy.Height;
                var placed=new ActEnemy{Kind=enemy.Kind,X=x,Y=y,HomeX=x,HomeY=y,
                    Health=enemy.Health,MaxHealth=enemy.MaxHealth};
                map.Enemies.Add(placed);area.Enemies.Add(placed);
            }
        }
        if(run.Chapter==2 && room.Type==ChapterRoomType.Hazard)
            map.Mechanisms.Add(new(ActMechanismKind.Rock,ChapterRoomTuning.SecondAreaStart+510,300,55));
        AddTransitionRecovery(map,areas,run.TransitionId,run.RandomFor(0x63251));
        return map;
    }

    private static void AddTransitionRecovery(ActLevelLayout map,IReadOnlyList<RoomCombatArea> areas,string transitionId,Random rng)
    {
        for(int i=0;i<areas.Count-1;i++)
        {
            if(rng.NextDouble()>=ChapterRoomTuning.TransitionRecoveryChance)continue;
            // Use each passage's authored, unobstructed recovery sockets.
            float[] sockets=i==0?ActRoomTransitions.RecoveryPositions(map,transitionId).ToArray():
                [(areas[i].Right+areas[i+1].Left)/2];
            if(sockets.Length==0)continue;
            float x=sockets[rng.Next(sockets.Length)];
            float? surface=ActSecondMap.GroundAt(map,x);
            if(surface is not float y)continue;
            var kind=rng.NextDouble()<ChapterRoomTuning.TransitionHealChance
                ?ActRewardKind.Heal:ActRewardKind.FullRangedEnergy;
            map.Rewards.Add(new(kind,x,y-26));
        }
    }

    private static void GenerateExits(ChapterRoomRun run,List<RoomExit> exits,float hall,Random rng)
    {
        if(run.Depth>=run.TotalRooms)
        {exits.Add(new(0,hall+650,300,RouteReward.Bones,BattleRoomTier.Normal));return;}
        int count=rng.NextDouble()<ChapterRoomTuning.ThirdExitChance?3:2;
        var rewards=new List<RouteReward>();
        bool hasHard=rng.NextDouble()<count*ChapterRoomTuning.HardExitShare;
        int normalCount=count-(hasHard?1:0);
        var normal=RoomRewardRules.Allowed(BattleRoomTier.Normal).OrderBy(_=>rng.Next())
            .OrderBy(r=>r==run.Reward?1:0).Take(normalCount).ToArray();
        rewards.AddRange(normal);
        if(hasHard)
        {
            var pool=RoomRewardRules.Allowed(BattleRoomTier.Hard).Where(r=>r!=run.Reward).ToArray();
            rewards.Add(pool[rng.Next(pool.Length)]);
            // Keep a hard route on an elevated door, without reserving one in every room.
            (rewards[1],rewards[^1])=(rewards[^1],rewards[1]);
        }
        for(int i=0;i<count;i++)
            exits.Add(new(i,hall+(i==1?ChapterRoomTuning.HighExitOffset:
                i==2?ChapterRoomTuning.LowerExitOffset:ChapterRoomTuning.GroundExitOffset),
                i==1?ChapterRoomTuning.HighExitFeet:i==2?ChapterRoomTuning.LowerExitFeet:300,rewards[i],
                RoomRewardRules.TierFor(rewards[i])));
    }

    private static void BuildExitHall(ActLevelLayout map,float hall,IReadOnlyList<RoomExit> exits)
    {
        void Land(string name,float left,float right,float y0,float y1)=>
            map.Terrain.Add(new(name,hall+left,hall+right,y0,y1));
        if(exits.Count==1)
        {Land("巢穴前庭通路",0,ChapterRoomTuning.ExitHallLength,300,300);return;}
        Land("出口下层石坪",0,340,300,300);
        Land("出口登高长坡",340,620,300,ChapterRoomTuning.HighExitFeet);
        Land("出口高台",620,930,ChapterRoomTuning.HighExitFeet,ChapterRoomTuning.HighExitFeet);
        float lower=exits.Count==3?ChapterRoomTuning.LowerExitFeet:300;
        Land("出口下降石道",930,1230,ChapterRoomTuning.HighExitFeet,lower);
        Land("出口低谷",1230,ChapterRoomTuning.ExitHallLength,lower,lower);
        // Optional jump shortcut. The main slope reaches the high door by walking;
        // both one-way platforms remain at least 60 units above the rising floor.
        map.Solids.Add(new(hall+290,180,100,14));
        map.Solids.Add(new(hall+435,78,110,14));
    }

    private static ActLevelLayout BuildFinal(ChapterRoomRun run,int seed)
    {
        bool boss=run.Stage==RoomJourneyStage.Boss;
        var map=new ActLevelLayout(seed){Width=boss?1900:1250,SpawnX=90,SpawnFeetY=300,
            Exit=boss?1760:1100,BossArenaLeft=300,BossArenaRight=1630};
        map.Terrain.Add(new("巢穴石坪",0,map.Width,300,300));
        map.Zones.Add(new(0,boss?BossRoomName(run.Chapter):ActChapters.Name(run.Chapter)+" · 巢穴前庭"));
        if(boss)map.Enemies.Add(ActLevel.Make(ActEnemyKind.Boss,1100));
        if(boss && run.Chapter==3)
        {
            map.Solids.AddRange([new(460,215,170,14),new(740,140,180,14),new(1190,205,180,14)]);
            map.MovingPlatforms.Add(new(new RectangleF(330,210,95,14),ActPlatformMotion.Vertical,25));
        }
        if(boss && run.Chapter==4)map.Mechanisms.Add(new(ActMechanismKind.Eruption,520,300,70));
        if(boss && run.Chapter>=3)ActPlatformLayout.Finalize(map);
        return map;
    }

    // These profiles are authored coordinates, never random point generation.
    private static void Terrain(ActLevelLayout m,int shape,string transitionId)
    {
        void Line(string id,float x0,float y0,float x1,float y1)=>m.Terrain.Add(new(id,x0,x1,y0,y1));
        Line("入口石坪",0,300,360,300);
        switch(shape)
        {
            case 1: Line("迎风坡",360,300,680,225);Line("坡顶战场",680,225,970,225);Line("碎石下坡",970,225,1350,300);break;
            case 2: Line("谷口",360,300,640,365);Line("谷底战场",640,365,1050,365);Line("回升坡",1050,365,1350,300);break;
            default:Line("前庭战场",360,300,1350,300);break;
        }
        ActRoomTransitions.Build(m,transitionId);
        float back=ChapterRoomTuning.SecondAreaStart,end=ChapterRoomTuning.SecondAreaEnd;
        if(shape is 1 or 4 or 8)
        {
            Line("后庭上坡",back,300,back+280,230);
            Line("后庭高地",back+280,230,back+680,230);
            Line("后庭下坡",back+680,230,end,300);
        }
        else Line("后庭战场",back,300,end,300);
        Line("出口前石坪",end,300,m.Width-ChapterRoomTuning.ExitHallLength,300);
    }
}
