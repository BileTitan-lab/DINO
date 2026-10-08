using System.Reflection;
using DinoDash.Desktop;

internal static class ChapterRoomChecks
{
    private static void Need(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);}
    private static ActEngine Start(int seed=47)
    {var game=new ActEngine(seed);game.LoadMapForDebug(2,seed,false,roomAdventure:true);return game;}
    private static void Tick(ActEngine game,float seconds)
    {for(int i=0;i<seconds/ActTuning.Step;i++)game.Update(ActTuning.Step);}
    private static void FinishReward(ActEngine g)
    {
        if(g.PendingPowerReward==ActRewardChoice.BattleArt)g.DeclineBattleArt();
        else if(g.PendingPowerReward==ActRewardChoice.Evolution)g.ChooseEvolution(0);
        else if(g.PendingPowerReward==ActRewardChoice.Form)g.ChooseForm(0);
    }
    private static void Clear(ActEngine g)
    {
        foreach(var a in g.CombatAreas)
        {foreach(var e in a.Enemies){e.Dormant=false;e.State=ActEnemyState.Dead;}}
        g.Update(ActTuning.Step);FinishReward(g);
    }
    private static void Door(ActEngine g,RoomExit e)
    {g.PlacePlayerForTest(e.X-g.PlayerWidth/2,e.FeetY-g.PlayerHeight);Tick(g,3.1f);Need(g.IsComplete,"Exit never completed");g.TravelFromExit();}
    public static void Run(Action<string,Action> check)
    {
        check("ROOM enemies exist on entry, encounters are freely reachable and only exits stay locked",()=>
        {
            var g=Start();Need(g.CombatAreas.Count==2 && g.RoomExits.Count>=2,"Missing encounters or doors");
            var inhabitants=g.Enemies.ToArray();
            Need(inhabitants.Length==g.CombatAreas.Sum(a=>a.Enemies.Count) &&
                inhabitants.All(e=>!e.Dormant && e.State!=ActEnemyState.Dead),"Room contains hidden enemy spawns");
            float end=g.CombatAreas[0].Right;
            g.PlacePlayerForTest(end-8,(g.TerrainYAt(end+19)??300)-g.PlayerHeight);
            g.DashRequested=true;g.RightHeld=true;Tick(g,.1f);g.ClearInput();
            Need(g.PlayerX>end && !g.CombatAreas[0].Cleared,"Uncleared combat A still blocks forward movement");
            Tick(g,.15f); // Finish the dash before checking movement in the opposite direction.
            float start=g.CombatAreas[1].Left+100;
            g.PlacePlayerForTest(start,(g.TerrainYAt(start+27)??300)-52);g.LeftHeld=true;Tick(g,.15f);g.ClearInput();
            Need(g.PlayerX<start && g.Enemies.SequenceEqual(inhabitants),"Backtracking blocked or new enemies appeared");
            foreach(var e in g.CombatAreas[1].Enemies)e.State=ActEnemyState.Dead;
            g.Update(ActTuning.Step);
            Need(g.CombatAreas[1].Cleared && !g.CombatAreas[0].Cleared &&
                !g.Run.RoomJourney!.RewardGranted,"Combat B cannot clear before A or grants an early reward");
            var door=g.RoomExits[1];
            g.PlacePlayerForTest(door.X-27,door.FeetY-52);Tick(g,3.1f);
            Need(!g.IsComplete && !g.RoomExitReady && g.ExitCountdown==0,
                "An uncleared room permits exit travel");
            foreach(var e in g.CombatAreas[0].Enemies)e.State=ActEnemyState.Dead;
            g.Update(ActTuning.Step);
            Need(g.RoomExitReady && g.Run.RoomJourney!.RewardGranted,"All battles cleared but exits remain locked");
        });
        check("ROOM clear reward is single use and route reward waits until the next room is cleared",()=>
        {
            var g=Start();int before=g.Run.BoneShards;
            Clear(g);Need(g.RoomExitReady && g.Run.BoneShards==before+40,"Initial room reward missing");
            int awarded=g.Run.BoneShards;Tick(g,.1f);Need(g.Run.BoneShards==awarded,"Reward repeated");
            var e=g.RoomExits[0];Door(g,e);
            Need(g.Run.RoomJourney!.Depth==2 && g.Run.RoomJourney.Reward==e.Reward &&
                g.Run.BoneShards==awarded && g.PendingPowerReward==ActRewardChoice.None,"Door granted reward early");
            Clear(g);Need(g.Run.RoomJourney.RewardGranted,"Next room did not grant its selected reward");
        });
        check("ROOM routes reach antechamber, boss, existing camp and preserve the build",()=>
        {
            var g=Start();g.Run.AddArt(BattleArtId.BoneArmor);g.Run.AddEvolution(EvolutionId.SharpClaws);
            g.Run.AddForm(PrimalForm.Raptor);g.Run.SetHealth(3);
            int total=g.Run.RoomJourney!.TotalRooms;
            for(int depth=1;depth<=total;depth++)
            {Need(g.Run.RoomJourney.Depth==depth,"Depth skipped");Clear(g);Door(g,g.RoomExits[0]);}
            Need(g.Run.RoomJourney.Stage==RoomJourneyStage.Antechamber && g.Enemies.Count==0,"Missing quiet antechamber");
            g.PlacePlayerForTest(g.ExitX-27,248);Tick(g,3.1f);g.TravelFromExit();
            Need(g.Run.RoomJourney.Stage==RoomJourneyStage.Boss && g.Boss.Kind==ActEnemyKind.Boss && g.Boss.Health>0,"Missing chapter boss");
            g.Boss.Health=0;g.Boss.State=ActEnemyState.Dead;g.Update(ActTuning.Step);
            Need(g.BossEvolutionReward!=null && g.BossFormReward!=null,"Boss rewards missing");
            g.BossEvolutionReward!.Collected=true;
            g.Run.SetHealth(3);
            g.PlacePlayerForTest(g.ExitX-27,248);Tick(g,3.1f);g.TravelFromExit();
            Need(g.IsResting && g.Run.BattleArtCount==1 && g.Run.Has(EvolutionId.SharpClaws) &&
                g.Run.Forms.Contains(PrimalForm.Raptor) && g.Health==3,$"Run lost values between rooms/camp: rest={g.IsResting}, arts={g.Run.BattleArtCount}, hp={g.Health}");
        });
        check("ROOM seeded authored pools have unique rooms, varied groups, distinct exits and pacing",()=>
        {
            var passages=new HashSet<string>();
            string Signature(int seed)
            {
                var r=new ChapterRoomRun(seed);var a=new List<RoomCombatArea>();var e=new List<RoomExit>();
                var ids=new HashSet<string>();var signature=new List<string>();
                for(int i=1;i<=r.TotalRooms;i++)
                {
                    string previous=r.TransitionId;
                    r.Depth=i;ActChapterRooms.SelectRoom(r);Need(ids.Add(r.CurrentRoomId),"Duplicate room");
                    Need(r.TransitionId!=previous,"Adjacent passages repeat");passages.Add(r.TransitionId);
                    var m=ActChapterRooms.Build(r,a,e);
                    Need(a.Count>=2 && a.All(c=>c.Enemies.Count>=2),"Missing required battles");
                    Need(i==r.TotalRooms || e.Count>=2 && e.Select(x=>x.Reward).Distinct().Count()==e.Count,"Duplicate reward exits");
                    Need(i>2 || r.LastDifficulty<3,"Early extreme room");
                    Need(i<r.TotalRooms || r.LastDifficulty<3,"Extreme boss predecessor");
                    signature.Add(m.Signature+string.Join('/',a.Select(c=>c.GroupId))+string.Join('/',e.Select(x=>$"{x.Reward}:{x.Dangerous}")));
                }
                return string.Join('|',signature);
            }
            var results=new HashSet<string>();
            for(int i=1;i<=80;i++){string s=Signature(i);Need(s==Signature(i),"Seed not reproducible");results.Add(s);}
            Need(results.Count>60 && ActChapterRooms.CanyonPool.Length==10,"Insufficient room variation");
            Need(passages.Count==ActRoomTransitions.Pool.Length,"Passage pool is not fully sampled");
        });
        check("ROOM high difficulty requires three battles and only grants its evolution reward after all clear",()=>
        {
            var g=Start();var journey=g.Run.RoomJourney!;
            journey.Tier=BattleRoomTier.Hard;journey.Reward=RouteReward.Evolution;
            typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(g,[null]);
            Need(g.CombatAreas.Count==3,"Hard room omitted the extra combat");
            foreach(var area in g.CombatAreas.Take(2))
                foreach(var enemy in area.Enemies)enemy.State=ActEnemyState.Dead;
            g.Update(ActTuning.Step);
            Need(!g.RoomCleared && !g.RoomExitReady && !journey.RewardGranted &&
                g.PendingPowerReward==ActRewardChoice.None,"Third battle is optional or rewarded too early");
            foreach(var enemy in g.CombatAreas[2].Enemies)enemy.State=ActEnemyState.Dead;
            g.Update(ActTuning.Step);
            Need(g.PendingPowerReward==ActRewardChoice.Evolution && !g.RoomExitReady &&
                g.Run.BoneShards==0,"Hard room has incorrect reward or prematurely unlocked exits");
            g.ChooseEvolution(0);
            Need(g.Run.Evolutions.Count==1 && g.RoomExitReady,"Evolution choice failed to open exits");
        });
        check("ROOM module registry reserves disabled event rooms and isolates battle completion",()=>
        {
            var g=Start();var run=g.Run.RoomJourney!;
            Need(ActRoomModules.Get(RoomModuleKind.Battle).Enabled &&
                !ActRoomModules.Get(RoomModuleKind.Event).Enabled &&
                ActChapterRooms.ReservedRooms.All(r=>!r.Enabled && r.Module==RoomModuleKind.Event),
                "Event placeholder is missing or enabled");
            Need(!g.CompleteRoomEvent() && !run.EventCompleted,"Battle incorrectly accepts an event completion");
            Need(!ActRoomModules.Get(RoomModuleKind.Battle).IsComplete(run,[]),
                "Empty battle room clears without necessary encounters");
            var eventState=new ChapterRoomRun(8){Module=RoomModuleKind.Event};
            Need(!ActRoomModules.Get(RoomModuleKind.Event).IsComplete(eventState,[]),"Empty event auto completes");
            eventState.EventCompleted=true;
            Need(ActRoomModules.Get(RoomModuleKind.Event).IsComplete(eventState,[]),"Event completion hook is missing");
            for(int seed=1;seed<=80;seed++)
            {
                var sample=Start(seed).Run.RoomJourney!;
                Need(sample.Module==RoomModuleKind.Battle &&
                    ActChapterRooms.CanyonPool.Any(r=>r.Id==sample.CurrentRoomId),"Placeholder entered live pool");
            }
            run.Tier=BattleRoomTier.Hard;run.Reward=RouteReward.Form;
            var copy=g.ExportRunState().RoomJourney!;
            Need(copy.Tier==run.Tier && copy.Module==run.Module && copy.Reward==run.Reward,
                "Run clone lost room module or difficulty");
        });
        check("ROOM exits consistently pair ordinary resources with normal rooms and growth with hard rooms",()=>
        {
            var rewards=new HashSet<RouteReward>();
            for(int seed=1;seed<=50;seed++)
            {
                var g=Start(seed);
                for(int depth=1;depth<=g.Run.RoomJourney!.TotalRooms;depth++)
                {
                    var run=g.Run.RoomJourney!;
                    Need(g.CombatAreas.Count==RoomRewardRules.RequiredBattles(run.Tier) &&
                        RoomRewardRules.Allowed(run.Tier).Contains(run.Reward),"Tier/reward/battle count diverged");
                    Clear(g);
                    if(depth==run.TotalRooms)break;
                    Need(g.RoomExits.Any(e=>e.Tier==BattleRoomTier.Normal) &&
                        g.RoomExits.Select(e=>e.Reward).Distinct().Count()==g.RoomExits.Count &&
                        g.RoomExits.Count(e=>e.Tier==BattleRoomTier.Hard)<=1,"Exits lack distinct rewards or violate hard-route weighting");
                    foreach(var e in g.RoomExits)
                    {
                        rewards.Add(e.Reward);
                        Need(e.Module==RoomModuleKind.Battle && e.Reward!=RouteReward.Unknown &&
                            e.Tier==RoomRewardRules.TierFor(e.Reward),"Exit reward points to wrong tier");
                    }
                    var selected=g.RoomExits[depth%g.RoomExits.Count];
                    Door(g,selected);
                    Need(g.Run.RoomJourney!.Tier==selected.Tier && g.Run.RoomJourney.Module==selected.Module &&
                        g.Run.RoomJourney.Reward==selected.Reward && !g.Run.RoomJourney.RewardGranted,
                        "Door does not preserve selected room policy");
                }
            }
            Need(rewards.SetEquals([RouteReward.BattleArt,RouteReward.Bones,RouteReward.Heal,
                RouteReward.Evolution,RouteReward.Form]),"Enabled reward pool is incomplete");
        });
        check("ROOM boss antechamber always has safe merchant and single-use bones with real purchases",()=>
        {
            for(int seed=1;seed<=30;seed++)
            {
                var sample=Start(seed);sample.Run.RoomJourney!.Stage=RoomJourneyStage.Antechamber;
                typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(sample,[null]);
                Need(sample.IsRoomAntechamber && !sample.IsResting && sample.Enemies.Count==0 &&
                    sample.RoomFacilities.Select(f=>f.Kind).Order().SequenceEqual(
                        new[]{RestFacilityKind.Merchant,RestFacilityKind.Bones}.Order()) &&
                    sample.ShopStock.Count==3,"Antechamber lacks guaranteed facilities or is not safe");
            }
            var g=Start();g.Run.RoomJourney!.Stage=RoomJourneyStage.Antechamber;
            typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(g,[null]);
            g.Run.SetHealth(3);g.Run.AddBones(300);
            g.Run.AddArt(BattleArtId.BoneArmor);g.Run.AddArt(BattleArtId.PrimalRage);
            var bones=g.RoomFacilities.Single(f=>f.Kind==RestFacilityKind.Bones);
            g.PlacePlayerForTest(bones.X-27,248);
            Need(g.InteractWithRest() && g.Run.BoneShards==340 && bones.Used && !g.InteractWithRest(),
                "Bone supply amount or single-use behavior is wrong");
            Need(!g.UseBattleArt(0) && g.Run.BattleArtCount==2,"Antechamber consumes combat skills");
            typeof(ActEngine).GetMethod("HurtPlayerCore",BindingFlags.Instance|BindingFlags.NonPublic)!
                .Invoke(g,[null,true]);
            Need(g.Health==3,"Safe antechamber allows damage");
            var store=(ActRestStop)typeof(ActEngine).GetField("roomServices",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(g)!;
            store.Stock.Clear();
            store.Stock.Add(new(ShopKind.Healing,35));
            store.Stock.Add(new(ShopKind.Art,45,BattleArtId.BoneStorm));
            store.Stock.Add(new(ShopKind.Evolution,100,evolution:EvolutionId.SharpClaws));
            Need(!g.Purchase(0),"Closed shop accepts purchase");
            g.PlacePlayerForTest(ChapterRoomTuning.AntechamberMerchantX-27,248);
            Need(g.InteractWithRest() && g.ShopRequested,"Merchant cannot open shop");
            Need(!g.Purchase(-1) && g.Purchase(0) && g.Health==5 && g.Run.BoneShards==305 &&
                !g.Purchase(0),"Healing purchase or sold flag failed");
            Need(!g.Purchase(1) && g.Run.BoneShards==305 && g.Purchase(1,1) &&
                g.Run.BattleArts[1]==BattleArtId.BoneStorm && g.Run.BoneShards==260,
                "Full skill slots do not support paid replacement");
            Need(g.Purchase(2) && g.Run.Has(EvolutionId.SharpClaws) && g.Run.BoneShards==160,
                "Trait purchase does not work in antechamber");
            g.CloseShop();g.Run.SetHealth(3);
            g.PlacePlayerForTest(g.ExitX-27,248);Tick(g,3.1f);g.TravelFromExit();
            Need(g.Run.RoomJourney!.Stage==RoomJourneyStage.Boss && !g.IsResting &&
                g.Health==3 && g.Run.BoneShards==160 && g.RoomFacilities.Count==0 &&
                g.ShopStock.Count==0 && !g.ShopRequested && g.Run.Has(EvolutionId.SharpClaws),
                "Antechamber travel lost run state or retained stale facilities");
        });
        check("ROOM every authored main route clears with base jump and movement",()=>
        {
            foreach(var template in ActChapterRooms.CanyonPool)
            {
                var g=Start();g.Run.RoomJourney!.CurrentRoomId=template.Id;
                typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(g,[null]);
                Clear(g);g.RightHeld=true;g.JumpHeld=true;
                float hall=g.WorldWidth-ChapterRoomTuning.ExitHallLength;
                for(int i=0;i<5000 && g.PlayerX<hall;i++)
                {
                    if(g.Grounded && g.TerrainYAt(g.PlayerX+g.PlayerWidth+40)==null)g.JumpRequested=true;
                    g.Update(ActTuning.Step);
                }
                Need(g.PlayerX>=hall && !g.IsDead,$"Untraversable main route {template.Id} at {g.PlayerX},{g.PlayerY}");
            }
        });
        check("ROOM shorter transitions have seeded optional recovery on supported safe sockets",()=>
        {
            var kinds=new HashSet<ActRewardKind>();int present=0,absent=0;
            for(int seed=1;seed<=80;seed++)
            {
                var g=Start(seed);var repeat=Start(seed);
                var recovery=g.Rewards;
                Need(g.CombatAreas[1].Left-g.CombatAreas[0].Right==ChapterRoomTuning.TransitionLength &&
                    ChapterRoomTuning.TransitionLength<=400,"Transition was not shortened");
                Need(recovery.Select(r=>(r.Kind,r.X,r.Y)).SequenceEqual(repeat.Rewards.Select(r=>(r.Kind,r.X,r.Y))),
                    "Recovery is not seed reproducible");
                if(recovery.Count==0)absent++;else present++;
                foreach(var reward in recovery)
                {
                    kinds.Add(reward.Kind);
                    Need(reward.X>g.CombatAreas[0].Right && reward.X<g.CombatAreas[1].Left &&
                        g.TerrainYAt(reward.X) is float floor && Math.Abs(floor-reward.Y-26)<.01f &&
                        g.IsRewardUnlocked(reward),"Recovery is not immediately available on transition ground");
                    Need(!g.Layout.Mechanisms.Any(m=>m.X<reward.X+30 && m.X+m.Width>reward.X-30),
                        "Recovery placed under an environmental hazard");
                }
            }
            Need(present>20 && absent>5 && kinds.SetEquals([ActRewardKind.Heal,ActRewardKind.FullRangedEnergy]),
                "Recovery lacks presence/type variation");
        });
        check("ROOM pursuing ground enemies stop at ledges instead of falling and teleporting home",()=>
        {
            var g=Start();g.Run.RoomJourney!.CurrentRoomId="canyon-gap";
            g.Run.RoomJourney.TransitionId="broken-bridge";
            typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(g,[null]);
            var enemy=g.CombatAreas[0].Enemies.First(e=>e.Kind==ActEnemyKind.Melee);
            foreach(var other in g.Enemies)other.Dormant=other!=enemy;
            enemy.X=ChapterRoomTuning.FirstAreaEnd+65-enemy.Width/2-.3f;
            enemy.Y=300-enemy.Height;enemy.State=ActEnemyState.Chase;
            float edge=enemy.X;
            g.PlacePlayerForTest(ChapterRoomTuning.FirstAreaEnd+180-27,248);
            Tick(g,1.5f);
            Need(Math.Abs(enemy.X-edge)<2 && Math.Abs(enemy.Y+enemy.Height-300)<2 &&
                enemy.State!=ActEnemyState.Dead,"Enemy stepped into the gap or teleported to its spawn");
        });
        check("ROOM all ten authored passage styles connect and pass with base movement",()=>
        {
            Need(ActRoomTransitions.Pool.Length>=10,"Too few passage styles");
            foreach(var profile in ActRoomTransitions.Pool)
            {
                var g=Start();g.Run.RoomJourney!.CurrentRoomId="canyon-crossing";
                g.Run.RoomJourney.TransitionId=profile.Id;
                typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(g,[null]);
                Clear(g);
                float root=ChapterRoomTuning.FirstAreaEnd;
                Need(g.TerrainYAt(root)==300 && g.TerrainYAt(ChapterRoomTuning.SecondAreaStart)==300,
                    $"Mismatched passage connection: {profile.Id}");
                Need(ActRoomTransitions.RecoveryPositions(g.Layout,profile.Id).Any(),
                    $"No unobstructed recovery socket: {profile.Id}");
                g.PlacePlayerForTest(root-90,248);g.RightHeld=true;g.JumpHeld=true;
                for(int i=0;i<1800 && g.PlayerX<ChapterRoomTuning.SecondAreaStart+80;i++)
                {
                    if(g.Grounded && g.TerrainYAt(g.PlayerX+g.PlayerWidth+40)==null)g.JumpRequested=true;
                    g.Update(ActTuning.Step);
                }
                Need(g.PlayerX>=ChapterRoomTuning.SecondAreaStart+80 && g.Health==g.MaximumHealth,
                    $"Passage needs upgraded movement or causes a fall: {profile.Id}");
            }
        });
        check("ROOM transition recovery works before battles clear, is not wasted and is single use",()=>
        {
            foreach(var kind in new[]{ActRewardKind.Heal,ActRewardKind.FullRangedEnergy})
            {
                ActEngine? g=null;ActReward? reward=null;
                for(int seed=1;seed<=80;seed++)
                {var candidate=Start(seed);var pickup=candidate.Rewards.FirstOrDefault(r=>r.Kind==kind);
                    if(pickup!=null){g=candidate;reward=pickup;break;}}
                Need(g!=null && reward!=null,"Could not find the recovery type");
                // Freeze opponents to isolate pickup behavior without clearing the room.
                foreach(var enemy in g!.Enemies)enemy.Dormant=true;
                g.PlacePlayerForTest(reward!.X-27,(g.TerrainYAt(reward.X)??300)-52);
                g.Update(ActTuning.Step);Need(!reward.Collected,"Full resources wasted the recovery");
                if(kind==ActRewardKind.Heal)g.Run.SetHealth(1);
                else
                {
                    g.PlacePlayerForTest(90,248);g.RangedRequested=true;g.Update(ActTuning.Step);
                    Need(g.RangedEnergy<g.MaximumEnergy,"Test did not spend ranged energy");
                    g.PlacePlayerForTest(reward.X-27,(g.TerrainYAt(reward.X)??300)-52);
                }
                g.Update(ActTuning.Step);
                Need(reward.Collected && !g.RoomCleared && !g.RoomExitReady &&
                    (kind==ActRewardKind.Heal?g.Health==3:g.RangedEnergy==g.MaximumEnergy),
                    "Recovery was blocked by uncleared battles or restored the wrong resource");
                if(kind==ActRewardKind.Heal)
                {g.Run.SetHealth(1);g.Update(ActTuning.Step);Need(g.Health==1,"Pickup repeated healing");}
                else
                {Tick(g,1);g.RangedRequested=true;g.Update(ActTuning.Step);
                    Need(g.RangedEnergy<g.MaximumEnergy,"Pickup repeated energy restoration");}
            }
        });
        check("ROOM transitions preserve next-map mutation and energy without repeated chapter healing",()=>
        {
            var g=Start();g.Run.SetHealth(2);g.Run.AddEvolution(EvolutionId.RegenerativeTissue);
            g.Run.AddArt(BattleArtId.PrimalRage);g.RangedRequested=true;g.Update(ActTuning.Step);
            int energy=g.RangedEnergy;Clear(g);Door(g,g.RoomExits[0]);
            Need(g.Health==2 && g.RangedEnergy==energy && g.Run.BattleArtCount==1,"Room changed chapter-owned resources");
            var clone=g.ExportRunState();clone.RoomJourney!.Visited.Clear();
            Need(g.Run.RoomJourney!.Visited.Count==2,"Run clone shares mutable room history");
            Need(clone.RoomJourney.TransitionId==g.Run.RoomJourney.TransitionId,"Run clone lost its passage");
            var previous=g.Run.RoomJourney;
            g.Reset();Need(g.MapIndex==1 && g.Run.RoomJourney is {Chapter:1,Depth:1} &&
                g.Run.RoomJourney!=previous && g.Run.BattleArtCount==0 && g.Run.Evolutions.Count==0,
                "New run did not reset into fresh teaching rooms");
        });
        check("ROOM spaced exits have distinct elevations and are reachable without movement upgrades",()=>
        {
            var g=Start();Clear(g);
            var high=g.RoomExits[1];
            Need(high.X-g.RoomExits[0].X>=500 && g.RoomExits[0].FeetY-high.FeetY>=190,
                "Exit spacing or elevation is too small");
            float start=g.CombatAreas[^1].Right+80;
            g.PlacePlayerForTest(start,(g.TerrainYAt(start+27)??300)-52);
            g.RightHeld=true;
            for(int i=0;i<1200 && g.PlayerX+27<high.X;i++)g.Update(ActTuning.Step);
            g.RightHeld=false;
            Need(g.PlayerX+27>=high.X && Math.Abs(g.PlayerY+52-high.FeetY)<3 && g.Health==g.MaximumHealth,
                "Base movement cannot reach the high exit");
            Tick(g,3.1f);Need(g.IsComplete && g.Run.RoomJourney!.SelectedExit==high.Index,
                "High exit cannot be selected from its terrain");

            ActEngine? three=null;
            for(int seed=1;seed<=40;seed++)
            {var candidate=Start(seed);if(candidate.RoomExits.Count==3){three=candidate;break;}}
            Need(three!=null,"No three-exit room found");
            var low=three!.RoomExits[2];Clear(three);
            float x=three.RoomExits[1].X+140;
            three.PlacePlayerForTest(x,(three.TerrainYAt(x+27)??300)-52);
            three.RightHeld=true;
            for(int i=0;i<700 && three.PlayerX+27<low.X;i++)three.Update(ActTuning.Step);
            three.RightHeld=false;
            Need(three.PlayerX+27>=low.X && Math.Abs(three.PlayerY+52-low.FeetY)<3 &&
                three.Health==three.MaximumHealth,"Lower exit slope is not safely traversable");
            Tick(three,3.1f);Need(three.IsComplete && three.Run.RoomJourney!.SelectedExit==low.Index,
                "Lower exit cannot be selected from its terrain");
        });
    }
}
