using DinoDash.Desktop;

internal static class RestStopChecks
{
    public static void Run(Action<string,Action> check)
    {
        check("ACT rest facilities always include fire, unique extras and probabilistic merchant",()=>
        {
            int traders=0;var kinds=new HashSet<RestFacilityKind>();
            for(int seed=0;seed<400;seed++)
            {
                var run=new ActRunState();run.AddEvolution(EvolutionId.HeavyScales);
                var stop=new ActRestStop(seed,run);
                Require(stop.Facilities.Count is >=2 and <=3);
                Require(stop.Facilities.Count(f=>f.Kind==RestFacilityKind.Fire)==1);
                Require(stop.Facilities.Select(f=>f.Kind).Distinct().Count()==stop.Facilities.Count);
                foreach(var f in stop.Facilities) kinds.Add(f.Kind);
                if(stop.Facilities.Any(f=>f.Kind==RestFacilityKind.Merchant))
                {traders++;Require(stop.Stock.Count==3);}
                else Require(stop.Stock.Count==0);
                Require(stop.Stock.All(s=>s.Evolution!=EvolutionId.HeavyScales));
            }
            Require(traders>150 && traders<250 && kinds.Count==6);
        });
        check("ACT rest is safe, supports movement, binds interaction and does not consume combat resources",()=>
        {
            var game=Rest();game.Run.SetHealth(2);game.Run.AddArt(BattleArtId.BoneStorm);
            Require(game.Enemies.Count==0 && game.Hazards.Count==0 && game.Layout.Mechanisms.Count==0);
            float start=game.PlayerX;
            game.RightHeld=true;game.AttackRequested=game.StompRequested=game.RangedRequested=game.BattleArt1Requested=true;
            Tick(game,.4f);game.RightHeld=false;
            Require(game.PlayerX>start && game.Health==2 && game.Run.BattleArtCount==1 && game.Projectiles.Count==0);
            game.PlacePlayerForTest(523,248);
            var input=new ActInputRouter();var bindings=new ActBindings{Interact=82};
            input.Handle(game,bindings,82,true);game.Update(ActTuning.Step);
            Require(game.Health==5 && game.RestStop!.Facilities[0].Used);
            game.Run.SetHealth(1);input.Handle(game,bindings,82,false);input.Handle(game,bindings,82,true);game.Update(ActTuning.Step);
            Require(game.Health==1);
        });
        check("ACT rest and chapter travel retain run HP, bones and build; only new run clears them",()=>
        {
            var game=new ActEngine(11);game.Run.SetHealth(2);game.Run.AddBones(157);
            game.Run.AddArt(BattleArtId.BoneArmor);game.Run.AddEvolution(EvolutionId.SharpClaws);
            for(int chapter=1;chapter<=3;chapter++)
            {
                game.EnterRestStop(22+chapter);
                Require(game.IsResting && game.Health==2 && game.Run.BoneShards==157 && game.Run.Chapter==chapter);
                game.PlacePlayerForTest(game.ExitX-27,248);
                Tick(game,2);Require(!game.IsComplete && game.ExitCountdown>1.9f);
                game.PlacePlayerForTest(game.ExitX-200,248);Tick(game,.02f);Require(game.ExitCountdown==0);
                game.PlacePlayerForTest(game.ExitX-27,248);Tick(game,3.05f);Require(game.IsComplete);
                game.TravelFromExit();
                Require(!game.IsResting && game.MapIndex==chapter+1 && game.Run.Chapter==chapter+1);
                Require(game.Health==2 && game.Run.BoneShards==157 && game.Run.BattleArtCount==1 && game.Run.Has(EvolutionId.SharpClaws));
            }
            var copy=game.ExportRunState();Require(copy.BoneShards==157 && copy.Chapter==4);
            game.Reset(44);Require(!game.IsResting && game.Run.BoneShards==0 && game.Run.Evolutions.Count==0 && game.Health==5);
        });
        check("ACT rest reward facilities are single use and reuse full-slot reward choices",()=>
        {
            foreach(var kind in new[]{RestFacilityKind.BattleArt,RestFacilityKind.Evolution,RestFacilityKind.Bones,RestFacilityKind.Egg})
            {
                var game=Rest();game.RestStop!.Facilities.Clear();game.RestStop.Facilities.Add(new(kind,500));
                game.Run.AddArt(BattleArtId.BoneArmor);game.Run.AddArt(BattleArtId.BoneStorm);
                game.PlacePlayerForTest(473,248);Require(game.InteractWithRest());
                if(game.PendingPowerReward==ActRewardChoice.BattleArt)
                {Require(!game.ChooseBattleArt(0));Require(game.ChooseBattleArt(0,1));}
                if(game.PendingPowerReward==ActRewardChoice.Evolution)Require(game.ChooseEvolution(0));
                int bones=game.Run.BoneShards;Require(!game.InteractWithRest() && game.Run.BoneShards==bones);
                if(kind==RestFacilityKind.Bones)Require(bones>=20 && bones<=40);
            }
        });
        check("ACT merchant purchases reject insufficient funds, sold items and duplicate evolutions",()=>
        {
            var game=Rest();var stop=game.RestStop!;stop.Facilities.Add(new(RestFacilityKind.Merchant,800));
            stop.Stock.Clear();stop.Stock.Add(new(ShopKind.Art,50,BattleArtId.BoneArmor));
            stop.Stock.Add(new(ShopKind.Evolution,100,evolution:EvolutionId.HeavyScales));
            stop.Stock.Add(new(ShopKind.Healing,35));
            game.PlacePlayerForTest(773,248);Require(game.InteractWithRest() && game.ShopRequested);
            Require(!game.Purchase(0) && !stop.Stock[0].Sold);
            game.Run.AddBones(200);game.Run.AddArt(BattleArtId.BoneStorm);game.Run.AddArt(BattleArtId.EnergyReturn);
            Require(!game.Purchase(0) && game.Run.BoneShards==200);
            Require(!game.Purchase(0,9) && game.Run.BoneShards==200);
            Require(game.Purchase(0,1) && game.Run.BoneShards==150 && game.Run.BattleArts[1]==BattleArtId.BoneArmor);
            Require(!game.Purchase(0,0) && game.Run.BoneShards==150);
            Require(game.Purchase(1) && game.MaximumHealth==6 && game.Run.BoneShards==50);
            Require(!game.Purchase(2) && game.Run.BoneShards==50);
            game.Run.SetHealth(1);Require(game.Purchase(2) && game.Health==3 && game.Run.BoneShards==15);
            game.CloseShop();Require(!game.Purchase(0));
        });
        check("ACT boss grants collectible bones and exit waits for evolution before its countdown",()=>
        {
            var game=new ActEngine(77);
            foreach(var e in game.Enemies.Where(e=>e!=game.Boss))e.State=ActEnemyState.Dead;
            game.Boss.Health=1;game.Boss.Dormant=false;
            game.PlacePlayerForTest(game.Boss.X-80,248);
            game.Run.AddArt(BattleArtId.EarthquakeRoar);Require(game.UseBattleArt(0));
            Require(game.BoneDrops.Any(d=>d.Amount>=ActRestTuning.BossBonesMin));
            game.PlacePlayerForTest(game.ExitX-27,248);Tick(game,3.1f);
            Require(!game.IsComplete && game.ExitCountdown==0);
            var reward=game.BossEvolutionReward!;
            game.PlacePlayerForTest(reward.X-27,248);Tick(game,.2f);
            Require(game.Run.BoneShards>=ActRestTuning.BossBonesMin && game.ChooseEvolution(0));
            game.PlacePlayerForTest(game.ExitX-27,248);Tick(game,3.1f);Require(game.IsComplete);
            game.TravelFromExit();Require(game.IsResting && game.MapIndex==1 && game.Run.Evolutions.Count==1);
        });
    }
    private static ActEngine Rest(){var g=new ActEngine(2);g.EnterRestStop(13);return g;}
    private static void Tick(ActEngine g,float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/ActTuning.Step);i++)g.Update(ActTuning.Step);}
    private static void Require(bool condition){if(!condition)throw new InvalidOperationException("Rest stop regression");}
}
