using System.Reflection;
using DinoDash.Desktop;

internal static class AllChapterRoomChecks
{
    private static void Need(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);}
    private static void ResetMap(ActEngine game)=>typeof(ActEngine).GetMethod("ResetMap",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,[null]);
    private static void Tick(ActEngine game,float seconds)
    {for(int i=0;i<seconds/ActTuning.Step;i++)game.Update(ActTuning.Step);}
    private static void Clear(ActEngine game)
    {
        foreach(var enemy in game.Enemies){enemy.Dormant=false;enemy.State=ActEnemyState.Dead;}
        game.Update(ActTuning.Step);
        if(game.PendingPowerReward==ActRewardChoice.Evolution)game.ChooseEvolution(0);
        else if(game.PendingPowerReward==ActRewardChoice.Form)game.ChooseForm(0);
        else if(game.PendingPowerReward==ActRewardChoice.BattleArt)game.DeclineBattleArt();
    }
    private static void Travel(ActEngine game,float x,float feet)
    {
        game.PlacePlayerForTest(x-game.PlayerWidth/2,feet-game.PlayerHeight);Tick(game,3.1f);
        Need(game.IsComplete,$"Travel countdown failed: chapter {game.MapIndex}, {game.Run.RoomJourney?.Stage}");
        game.TravelFromExit();
    }
    public static void Run(Action<string,Action> check)
    {
        check("ROOM ALL chapters have distinct seeded pools, strict battle tiers and event placeholders",()=>
        {
            var allIds=new HashSet<string>();
            for(int chapter=1;chapter<=4;chapter++)
            {
                Need(ActChapterRooms.PoolFor(chapter).Count>=10,"Chapter lacks authored rooms");
                foreach(var room in ActChapterRooms.PoolFor(chapter))Need(allIds.Add(room.Id),"Chapter decks share a room ID");
                var signatures=new HashSet<string>();
                string Route(int seed)
                {
                    var run=new ChapterRoomRun(seed,chapter);var ids=new HashSet<string>();var signature=new List<string>();
                    for(int depth=1;depth<=run.TotalRooms;depth++)
                    {
                        run.Depth=depth;
                        if(depth>1){run.Reward=depth%2==0?RouteReward.Evolution:RouteReward.BattleArt;
                            run.Tier=RoomRewardRules.TierFor(run.Reward);}
                        ActChapterRooms.SelectRoom(run);
                        var areas=new List<RoomCombatArea>();var exits=new List<RoomExit>();
                        var layout=ActChapterRooms.Build(run,areas,exits);
                        Need(ids.Add(run.CurrentRoomId) && ActChapterRooms.PoolFor(chapter).Any(r=>r.Id==run.CurrentRoomId),
                            "Chapter selected repeated or foreign room");
                        Need(areas.Count==RoomRewardRules.RequiredBattles(run.Tier),"Wrong battlefield count");
                        Need(layout.Enemies.Count==areas.Sum(a=>a.Enemies.Count) && layout.Enemies.All(e=>!e.Dormant),"Hidden or unmatched enemy");
                        Need(layout.Mechanisms.All(m=>m.X+m.Width<layout.Width-ChapterRoomTuning.ExitHallLength),"Hazard in safe exit hall");
                        if(depth<run.TotalRooms)Need(exits.Count>=2 && exits.Count(e=>e.Tier==BattleRoomTier.Hard)<=1 &&
                            exits.Any(e=>e.Tier==BattleRoomTier.Normal) &&
                            exits.All(e=>e.Tier==RoomRewardRules.TierFor(e.Reward)),"Chapter lost reward routing rules");
                        if(chapter!=2)ActPlatformLayout.Validate(layout);
                        signature.Add(run.CurrentRoomId+run.TransitionId+string.Join('/',areas.Select(a=>a.GroupId))+
                            string.Join('/',exits.Select(e=>e.Reward)));
                    }
                    return string.Join('|',signature);
                }
                for(int seed=1;seed<=40;seed++)
                {string signature=Route(seed);Need(signature==Route(seed),"Chapter route not reproducible");signatures.Add(signature);}
                Need(signatures.Count>25,"Chapter routes lack variation");
            }
            Need(ActChapterRooms.ReservedRooms.Length==4 && ActChapterRooms.ReservedRooms.All(r=>!r.Enabled),"Event placeholders not reserved for four chapters");
        });
        check("ROOM ALL authored themes and transition profiles are traversable with base movement",()=>
        {
            for(int chapter=1;chapter<=4;chapter++)
            foreach(var tier in new[]{BattleRoomTier.Normal,BattleRoomTier.Hard})
            foreach(var room in ActChapterRooms.PoolFor(chapter))
            {
                var game=new ActEngine(47);game.LoadMapForDebug(chapter,47,false,roomAdventure:true);
                var run=game.Run.RoomJourney!;run.CurrentRoomId=room.Id;run.Tier=tier;
                run.Reward=tier==BattleRoomTier.Hard?RouteReward.Evolution:RouteReward.Bones;ResetMap(game);
                // Isolate traversal from combat. Environmental hazards stay enabled.
                foreach(var e in game.Enemies)e.State=ActEnemyState.Dead;
                game.Update(ActTuning.Step);game.Run.SetHealth(game.MaximumHealth);
                if(game.PendingPowerReward==ActRewardChoice.Evolution)game.ChooseEvolution(0);
                game.RightHeld=true;game.JumpHeld=true;
                float hall=game.WorldWidth-ChapterRoomTuning.ExitHallLength;
                for(int i=0;i<6500 && game.PlayerX<hall && !game.IsDead;i++)
                {
                    // Base hops also avoid the telegraphed vent/rock cycle during the test.
                    if(game.Grounded && (game.TerrainYAt(game.PlayerX+game.PlayerWidth+45)==null ||
                        game.Layout.Mechanisms.Any(m=>m.Kind is ActMechanismKind.Rock or ActMechanismKind.Eruption &&
                            m.Warning && Math.Abs(m.X-game.PlayerX)<110)))game.JumpRequested=true;
                    game.Update(ActTuning.Step);
                }
                Need(game.PlayerX>=hall && !game.IsDead,$"Base route blocked: chapter {chapter}, {tier}, {room.Id}, {game.PlayerX},{game.PlayerY}");
            }
            for(int chapter=1;chapter<=4;chapter++)
            foreach(var passage in ActRoomTransitions.PoolFor(chapter))
            {
                var game=new ActEngine(47);game.LoadMapForDebug(chapter,47,false,roomAdventure:true);
                game.Run.RoomJourney!.TransitionId=passage.Id;ResetMap(game);
                foreach(var enemy in game.Enemies)enemy.Dormant=true;
                game.PlacePlayerForTest(ChapterRoomTuning.FirstAreaEnd-90,248);
                game.RightHeld=true;game.JumpHeld=true;
                for(int i=0;i<1800 && game.PlayerX<ChapterRoomTuning.SecondAreaStart+40;i++)
                {
                    if(game.Grounded && game.TerrainYAt(game.PlayerX+game.PlayerWidth+40)==null)game.JumpRequested=true;
                    game.Update(ActTuning.Step);
                }
                Need(game.PlayerX>=ChapterRoomTuning.SecondAreaStart+40 && !game.IsDead,$"Passage blocked: chapter {chapter}, {passage.Id}");
                Need(ActRoomTransitions.RecoveryPositions(game.Layout,passage.Id).Any(),"Passage has no supported supply socket");
            }
        });
        check("ROOM ALL adventure runs complete four chapters via tiered rooms, merchant antechambers, bosses and camps",()=>
        {
            var game=new ActEngine(73,roomAdventure:true);
            game.Run.AddArt(BattleArtId.BoneArmor);game.Run.AddEvolution(EvolutionId.RegenerativeTissue);
            game.Run.AddForm(PrimalForm.Raptor);game.Run.AddBones(100);game.Run.SetHealth(3);
            for(int chapter=1;chapter<=4;chapter++)
            {
                var run=game.Run.RoomJourney!;
                Need(game.MapIndex==chapter && run.Chapter==chapter && run.Depth==1 && game.ChapterSeed==73,
                    "Chapter did not start its own seeded deck");
                int count=run.TotalRooms;
                for(int depth=1;depth<=count;depth++)
                {
                    Need(run.Depth==depth && !game.IsComplete,"Room local state not reset");Clear(game);
                    var exit=game.RoomExits[depth%game.RoomExits.Count];Travel(game,exit.X,exit.FeetY);
                }
                Need(game.IsRoomAntechamber && game.ShopStock.Count==3 && game.RoomFacilities.Count==2 &&
                    game.RoomFacilities.Any(f=>f.Kind==RestFacilityKind.Merchant),"Chapter missing fixed antechamber facilities");
                game.Run.SetHealth(2);int bones=game.Run.BoneShards;
                game.PlacePlayerForTest(ChapterRoomTuning.AntechamberBonesX-27,248);Need(game.InteractWithRest(),"Missing bones");
                Need(game.Run.BoneShards==bones+40,"Antechamber bones not guaranteed");
                Travel(game,game.ExitX,300);
                Need(run.Stage==RoomJourneyStage.Boss && game.Boss.BossId==(ActBossId)(chapter-1) &&
                    game.Health==2,"Chapter has wrong boss or automatic healing");
                game.Boss.Health=0;game.Boss.State=ActEnemyState.Dead;game.Update(ActTuning.Step);
                Need(game.BossEvolutionReward!=null && game.BossFormReward!=null,"Chapter boss reward missing");
                game.BossEvolutionReward!.Collected=true;game.Run.SetHealth(2);
                Travel(game,game.ExitX,300);
                Need(game.IsResting && game.Run.BattleArtCount==1 && game.Run.Has(EvolutionId.RegenerativeTissue) &&
                    game.Run.Forms.Contains(PrimalForm.Raptor) && game.Health==2,"Camp lost current build");
                if(chapter<4)
                {
                    Travel(game,game.ExitX,300);
                    Need(!game.IsResting && game.Health==3 && game.Run.RoomJourney!.Visited.Count==1,
                        "Cross chapter healing/history lifecycle is wrong");
                }
            }
            game.Reset(73);
            Need(game.Run.RoomJourney is {Chapter:1,Depth:1} && game.Run.BoneShards==0 &&
                game.Run.Forms.Count==1 && game.Run.Evolutions.Count==0 && game.Run.BattleArtCount==0,
                "New run retains chapter/build state");
        });
        check("ROOM ALL chapter identity and pending-map mutations survive room travel but expire in the camp",()=>
        {
            var game=new ActEngine(81,roomAdventure:true);game.EnterRestStop(10);
            game.Run.AddMutation(MutationId.Brutality);game.Run.SetHealth(1);
            game.EnterNextMap(81);
            Need(game.Run.Resolve(PowerStat.AllDamage,1)>1,"Next-map mutation did not activate");
            Clear(game);var exit=game.RoomExits[0];Travel(game,exit.X,exit.FeetY);
            Need(game.Run.Resolve(PowerStat.AllDamage,1)>1,"Ordinary room cleared map mutation");
            var copy=game.ExportRunState();
            Need(copy.RoomJourney!.Chapter==2 && copy.RoomJourney.Depth==2,"Run clone lost chapter identity");
            game.EnterRestStop(11);
            Need(Math.Abs(game.Run.Resolve(PowerStat.AllDamage,1)-1)<.01f,"Camp failed to clear map mutation");
        });
        check("ROOM ALL free encounters only lock exits and vertical routes keep the camera bounded",()=>
        {
            for(int chapter=1;chapter<=4;chapter++)
            {
                var game=new ActEngine(92);game.LoadMapForDebug(chapter,92,false,roomAdventure:true);
                foreach(var enemy in game.Enemies)enemy.Dormant=true;
                var b=game.CombatAreas[1];float start=b.Left+60;
                game.PlacePlayerForTest(start,(game.TerrainYAt(start+27)??300)-52);
                game.LeftHeld=true;Tick(game,.12f);game.ClearInput();
                Need(game.PlayerX<start,"Room imposes an internal progression wall");
                foreach(var enemy in b.Enemies){enemy.Dormant=false;enemy.State=ActEnemyState.Dead;}
                game.Update(ActTuning.Step);
                Need(b.Cleared && !game.RoomCleared && !game.RoomExitReady,"Clearing battle B bypassed battle A");
                var door=game.RoomExits[1];game.PlacePlayerForTest(door.X-27,door.FeetY-52);Tick(game,3.1f);
                Need(!game.IsComplete && game.ExitCountdown==0,"Locked door accepts early travel");
                game.PlacePlayerForTest(door.X-27,-30);game.JumpHeld=true;Tick(game,.2f);
                Need(float.IsFinite(game.CameraY) && game.CameraY>=-270 && game.CameraY<=240 && !game.IsDead,
                    "High route breaks camera bounds");
                var snapshot=game.ExportRunState();Need(snapshot.RoomJourney!.Chapter==chapter,"Clone lost chapter ID");
            }
        });
    }
}
