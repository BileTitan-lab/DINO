using System.Reflection;
using System.Drawing;
using DinoDash.Desktop;

internal static class ExpandedBuildChecks
{
    private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    private static void Need(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Near(float a,float b,string message)=>Need(Math.Abs(a-b)<.005f,$"{message}: {a} != {b}");
    private static object? Call(ActEngine game,string method,params object?[] args)=>typeof(ActEngine).GetMethod(method,Flags)!.Invoke(game,args);
    private static T Field<T>(ActEngine game,string field)=>(T)typeof(ActEngine).GetField(field,Flags)!.GetValue(game)!;
    private static void Set(ActEngine game,string field,object value)=>typeof(ActEngine).GetField(field,Flags)!.SetValue(game,value);
    private static ActEngine Arena(int seed=42)
    {
        var game=new ActEngine(seed);Field<List<ActEnemy>>(game,"enemies").Clear();Field<List<ActSupply>>(game,"supplies").Clear();
        Field<List<RectangleF>>(game,"solids").Clear();Field<List<RectangleF>>(game,"solids").Add(new(0,300,36000,100));
        game.Layout.Hazards.Clear();game.Layout.Mechanisms.Clear();game.Layout.Rewards.Clear();
        game.PlacePlayerForTest(2000,300-game.PlayerHeight);return game;
    }
    private static ActEnemy Enemy(ActEngine game,float x,int hp=100)
    {var e=game.AddEnemyForTest(x);e.Health=e.MaxHealth=hp;return e;}
    private static void Art(ActEngine game,BattleArtId id)
    {int count=game.Run.BattleArtCount;Need(game.Run.AddArt(id),"No art slot");int slot=game.Run.BattleArts.ToList().FindIndex(a=>a==id);Need(game.UseBattleArt(slot)&&game.Run.BattleArtCount==count,"Art did not consume");}
    private static void Damage(ActEngine game,ActEnemy enemy,float amount,BuildDamageKind kind,bool melee=false)
        =>Call(game,"DamageEnemyTyped",enemy,amount,0f,melee,kind==BuildDamageKind.Claw,kind);
    private static void Tick(ActEngine game,float seconds)
    {for(int i=0;i<seconds/ActTuning.Step;i++)game.Update(ActTuning.Step);}
    private static void Evolution(ActRunState run,EvolutionId id)
        =>Need(run.AddEvolution(id,ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare?EvolutionSource.BossReward:EvolutionSource.NormalReward),"Could not grant evolution");

    public static void Run(Action<string,Action> check)
    {
        check("BUILD catalogue retains originals and contains 17 new arts, 14 normal and 6 rare evolutions",()=>
        {
            Need(ActPowerCatalog.Arts.Count==25 && ActPowerCatalog.Evolutions.Count==28,"Wrong content counts");
            Need(ActPowerCatalog.Evolutions.Count(e=>e.Rarity==EvolutionRarity.Rare)==6,"Wrong rare count");
            Need(ActPowerCatalog.Arts.Select(a=>a.Id).Distinct().Count()==25,"Duplicate art IDs");
            Need(ActPowerCatalog.Evolutions.Select(e=>e.Id).Distinct().Count()==28,"Duplicate evolution IDs");
            foreach(var art in ActPowerCatalog.Arts)Need(ActPowerCatalog.Art(art.Id)==art,"Art enum/catalog mismatch");
            foreach(var e in ActPowerCatalog.Evolutions)Need(ActPowerCatalog.Evolution(e.Id)==e,"Evolution enum/catalog mismatch");
        });
        check("BUILD hard reward exits statistically offer one hard route per four normal routes in every chapter",()=>
        {
            for(int chapter=1;chapter<=4;chapter++)
            {
                int hard=0,total=0;var signatures=new HashSet<string>();
                for(int seed=1;seed<=600;seed++)
                {
                    var run=new ChapterRoomRun(seed,chapter);ActChapterRooms.SelectRoom(run);
                    var exits=new List<RoomExit>();ActChapterRooms.Build(run,[],exits);
                    hard+=exits.Count(e=>e.Tier==BattleRoomTier.Hard);total+=exits.Count;
                    Need(exits.Count>=2 && exits.Select(e=>e.Reward).Distinct().Count()==exits.Count,"Duplicate or missing route choices");
                    Need(exits.Any(e=>e.Tier==BattleRoomTier.Normal),"No ordinary route");
                    signatures.Add(string.Join('/',exits.Select(e=>e.Reward)));
                }
                Need(Math.Abs((float)hard/total-.2f)<.035f,$"Hard route ratio in chapter {chapter}: {hard}/{total}");
                Need(signatures.Count>5,"Exit options lack diversity");
            }
        });
        check("BUILD rare source restrictions reject direct grants and all non-boss generation pools",()=>
        {
            var run=new ActRunState();
            foreach(var source in Enum.GetValues<EvolutionSource>().Where(s=>s!=EvolutionSource.BossReward))
            {
                Need(ActPowerCatalog.EligibleEvolutions(run,source).All(e=>e.Rarity==EvolutionRarity.Normal),"Rare leaked into "+source);
                foreach(var rare in ActPowerCatalog.Evolutions.Where(e=>e.Rarity==EvolutionRarity.Rare))
                    Need(!run.AddEvolution(rare.Id,source),"Direct acquisition bypassed source guard");
            }
            foreach(var rare in ActPowerCatalog.Evolutions.Where(e=>e.Rarity==EvolutionRarity.Rare))
                Need(run.AddEvolution(rare.Id,EvolutionSource.BossReward),"Boss source rejected");
        });
        check("BUILD merchants never stock rares, and mutation grants remain normal",()=>
        {
            for(int seed=0;seed<250;seed++)
            {
                var stock=ActRestStop.CreateAntechamber(seed,new ActRunState()).Stock;
                Need(stock.Where(s=>s.Evolution!=null).All(s=>ActPowerCatalog.Evolution(s.Evolution!.Value).Rarity==EvolutionRarity.Normal),"Rare merchant item");
                var game=Arena(seed);game.EnterRestStop(seed);game.Run.AddArt(BattleArtId.BoneArmor);
                Need(game.OfferMutations(),"Egg offer missing");
                var offers=Field<List<MutationId>>(game,"mutationOffers");offers.Clear();offers.Add(MutationId.OverEvolution);
                Need(game.ChooseMutation(0) && game.Run.Evolutions.All(id=>ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Normal),"Rare mutation outcome");
            }
        });
        check("BUILD Boss rewards mix at least two normals with at most one rare, exclude owned and reproduce seed",()=>
        {
            int earlyRare=0,finalRare=0;
            for(int seed=1;seed<=240;seed++)
            {
                foreach(int chapter in new[]{1,4})
                {
                    var g=Arena(seed);if(chapter==4)g.LoadMapForDebug(4,seed,false);
                    Evolution(g.Run,EvolutionId.SharpClaws);
                    Need(g.OfferEvolutions(EvolutionSource.BossReward),"Boss offer missing");
                    Need(g.EvolutionOffers.Count==3&&!g.EvolutionOffers.Contains(EvolutionId.SharpClaws),"Owned or missing offer");
                    int rare=g.EvolutionOffers.Count(id=>ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare);
                    Need(rare<=1 && g.EvolutionOffers.Count-rare>=2,"Invalid Boss mix");
                    if(chapter==1)earlyRare+=rare;else finalRare+=rare;
                    int index=rare==1?g.EvolutionOffers.ToList().FindIndex(id=>ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare):0;
                    Need(g.ChooseEvolution(index),"Boss evolution choice failed");
                    Need(g.PowerCueRare==(rare==1),"Rare cue mismatch");
                }
            }
            Need(earlyRare>50 && earlyRare<125 && finalRare>125 && finalRare<195,"Boss rarity rates outside tolerance");
            var a=Arena(774);var b=Arena(774);a.OfferEvolutions(EvolutionSource.BossReward);b.OfferEvolutions(EvolutionSource.BossReward);
            Need(a.EvolutionOffers.SequenceEqual(b.EvolutionOffers),"Boss rewards are not seed deterministic");
        });
        check("BUILD exhausted normal sources cannot award rare traits or softlock choices",()=>
        {
            var g=Arena();foreach(var normal in ActPowerCatalog.Evolutions.Where(e=>e.Rarity==EvolutionRarity.Normal))Evolution(g.Run,normal.Id);
            Need(!g.OfferEvolutions() && g.PendingPowerReward==ActRewardChoice.None,"Exhausted normal source leaked rare or paused");
            Need(g.OfferEvolutions(EvolutionSource.BossReward) && g.EvolutionOffers.All(id=>ActPowerCatalog.Evolution(id).Rarity==EvolutionRarity.Rare),"Exhausted Boss fallback missing");
            Need(g.ChooseEvolution(0),"Fallback choice invalid");
        });
        check("BUILD hunter rush moves farther, damages each enemy once and grants invulnerability",()=>
        {
            var g=Arena();var e=Enemy(g,2090);float start=g.PlayerX;int hp=g.Health;
            Art(g,BattleArtId.HunterRush);Call(g,"HurtPlayerCore",e,false);Need(g.Health==hp,"Rush lacks invulnerability");
            Call(g,"UpdateBuildCombat",.01f);int damaged=e.Health;
            Call(g,"UpdateBuildCombat",.01f);Need(e.Health==damaged && damaged<100,"Rush hit repeats or never hits");
            Tick(g,.8f);Need(g.PlayerX-start>350,"Rush distance too short");
        });
        check("BUILD next claw empowerment is consumed only on a real hit and multiplies knockback",()=>
        {
            var g=Arena();Art(g,BattleArtId.TyrantSmash);g.AttackRequested=true;Tick(g,.45f);
            Need(g.Run.ArmedClawMultiplier==2.5f,"Miss consumed empowerment");
            var e=Enemy(g,g.PlayerX+75);g.AttackRequested=true;
            for(int i=0;i<30 && e.Health==100;i++)g.Update(ActTuning.Step);
            Need(e.Health<=98 && g.Run.ArmedClawMultiplier==1 && Math.Abs(e.KnockbackSpeed)>ActCombatTuning.ClawKnockback,"Empowered claw missing damage or knockback");
        });
        check("BUILD enhanced tail launches enemies and sky slam automatically rises then lands",()=>
        {
            var g=Arena();var e=Enemy(g,2160);Art(g,BattleArtId.TailResonance);
            Need(e.Health<100 && e.VerticalSpeed<0 && e.Y<258,"Tail art did not launch or reach target");
            var sky=Arena();var target=Enemy(sky,2110);Art(sky,BattleArtId.SkySlam);
            Tick(sky,.2f);Need(sky.PlayerY<300-sky.PlayerHeight-35,"Sky slam did not jump");
            Tick(sky,1.4f);Need(sky.Grounded && target.Health<100,"Sky slam did not land or damage");
        });
        check("BUILD bone rain delays, spear pierces and seeking volley homes",()=>
        {
            var rain=Arena();Art(rain,BattleArtId.BoneRain);Need(rain.Projectiles.Count==0 && rain.BoneRainWarning>0,"Rain missing delay");
            Call(rain,"UpdateBuildCombat",.6f);Need(rain.Projectiles.Count==9 && rain.Projectiles.All(p=>p.Vy>0),"Rain volley missing");
            Art(rain,BattleArtId.BoneRain);Art(rain,BattleArtId.BoneRain);Call(rain,"UpdateBuildCombat",.6f);
            Need(rain.Projectiles.Count==27,"Duplicate bone rain arts overwrote their scheduled volleys");
            var spear=Arena();var one=Enemy(spear,2100);var two=Enemy(spear,2170);Art(spear,BattleArtId.PiercingSpear);
            for(int i=0;i<30;i++)Call(spear,"UpdateProjectiles",ActTuning.Step);
            Need(one.Health<100 && two.Health<100,"Spear did not pierce two targets");
            var seek=Arena();Enemy(seek,2200).Y=180;Art(seek,BattleArtId.SeekingSpikes);
            float initial=seek.Projectiles[2].Vy;Call(seek,"UpdateProjectiles",.05f);
            Need(seek.Projectiles.Count==5 && seek.Projectiles.All(p=>p.Homing) && seek.Projectiles[2].Vy<initial,"Seeking spike did not steer");
        });
        check("BUILD overload free shots expire to zero energy and infinite marrow remains free",()=>
        {
            var g=Arena();Art(g,BattleArtId.SpikeOverload);
            for(int i=0;i<12;i++)Call(g,"FireBoneSpike");Need(g.RangedEnergy==5,"Overload consumed energy");
            Call(g,"UpdatePowerTimers",5.1f);Need(g.RangedEnergy==0,"Overload did not drain on expiry");
            Evolution(g.Run,EvolutionId.InfiniteMarrow);g.RangedRequested=true;g.Update(ActTuning.Step);
            Call(g,"UpdateCombatTimers",.4f);g.RangedRequested=true;g.Update(ActTuning.Step);
            Need(g.RangedEnergy==0 && g.Projectiles.Any(p=>Math.Abs(p.Damage-.7f)<.005f),"Infinite marrow cannot shoot at empty energy");
        });
        check("BUILD counter blocks exactly one hit, barrier blocks two and then expires",()=>
        {
            var counter=Arena();var e=Enemy(counter,2110);Art(counter,BattleArtId.CounterInstinct);int hp=counter.Health;
            Call(counter,"HurtPlayerCore",e,false);Need(counter.Health==hp && e.Health<100 && counter.Run.Remaining(BuildBuff.Counter)==0,"Counter missing");
            Call(counter,"UpdatePowerTimers",1.1f);typeof(ActEngine).GetProperty("InvulnerabilityRemaining")!.SetValue(counter,0f);
            Call(counter,"HurtPlayerCore",e,false);Need(counter.Health<hp,"Counter blocked twice");
            var barrier=Arena();Art(barrier,BattleArtId.PrimalBarrier);
            Need(barrier.Run.AbsorbHit() && barrier.Run.AbsorbHit() && !barrier.Run.AbsorbHit(),"Barrier charge count incorrect");
            Art(barrier,BattleArtId.PrimalBarrier);barrier.Run.Tick(5.1f);Need(!barrier.Run.AbsorbHit(),"Expired barrier still protects");
        });
        check("BUILD blood rage and life burn have real nonfatal costs and reversible bonuses",()=>
        {
            var g=Arena();g.Run.SetHealth(2);Art(g,BattleArtId.BloodRage);
            Need(g.Health==1,"Blood rage health price missing");Near(g.Run.Resolve(PowerStat.MeleeDamage,1),1.6f,"Blood rage damage");
            Near(g.Run.Resolve(PowerStat.AttackSpeed,1),1.35f,"Blood rage speed");g.Run.Tick(8.1f);Near(g.Run.Resolve(PowerStat.MeleeDamage,1),1,"Blood rage did not expire");
            Call(g,"FireBoneSpike");Art(g,BattleArtId.LifeBurn);Need(g.Health==1 && g.RangedEnergy==g.MaximumEnergy,"Life burn killed player or failed energy refill");
            Near(g.Run.Resolve(PowerStat.AllDamage,1),1.35f,"Life burn damage");g.Run.Tick(10.1f);Near(g.Run.Resolve(PowerStat.AllDamage,1),1,"Life burn permanent bonus");
        });
        check("BUILD predation has a two-HP cap, kill counters ignore repeated and ineligible summons",()=>
        {
            var g=Arena();g.Run.SetHealth(1);Art(g,BattleArtId.PredationRecovery);
            for(int i=0;i<8;i++)Damage(g,Enemy(g,2300+i*40,1),4,BuildDamageKind.Projectile);
            Near(g.Run.ExactHealth,3,"Predation exceeded heal budget");
            var rare=Arena();rare.Run.SetHealth(1);Evolution(rare.Run,EvolutionId.PredatoryRegeneration);
            for(int i=0;i<4;i++)Damage(rare,Enemy(rare,2300+i*40,1),4,BuildDamageKind.Projectile);
            Need(rare.Health==1,"Deterministic heal fired early");
            var summon=Enemy(rare,2600,1);summon.BuildRewardEligible=false;Damage(rare,summon,4,BuildDamageKind.Projectile);
            Need(rare.Health==1,"Ineligible summon farmed heal");
            var fifth=Enemy(rare,2700,1);Damage(rare,fifth,4,BuildDamageKind.Projectile);Need(rare.Health==2,"Fifth kill did not heal");
            fifth.State=ActEnemyState.Patrol;fifth.Health=1;Damage(rare,fifth,4,BuildDamageKind.Projectile);
            Need(rare.Run.PredationKills==0,"Repeated unit farmed kill value");
        });
        check("BUILD fear disables normal enemies only and afterimages perform delayed attacks",()=>
        {
            var fear=Arena();var e=Enemy(fear,2070);var boss=ActLevel.Make(ActEnemyKind.Boss,2100);Field<List<ActEnemy>>(fear,"enemies").Add(boss);
            Art(fear,BattleArtId.TerrorRoar);Need(e.FearRemaining>0 && boss.FearRemaining==0,"Fear controlled Boss or missed normal enemy");
            float x=e.X;Call(fear,"UpdateEnemy",.1f);Need(e.X>x && e.State==ActEnemyState.Recover,"Fear did not retreat");
            var shadow=Arena();Art(shadow,BattleArtId.AfterimageHunt);shadow.DashRequested=true;Tick(shadow,.27f);
            Need(shadow.BuildPulses.Count==1,"Dash omitted afterimage");var pulse=shadow.BuildPulses[0];var target=Enemy(shadow,pulse.X-10);
            Call(shadow,"UpdateBuildCombat",.1f);Need(target.Health==100,"Afterimage hit without delay");
            Call(shadow,"UpdateBuildCombat",.4f);Need(target.Health<100,"Afterimage never struck");
        });
        check("BUILD gene rampage selects one ability and emergency molt switches without losing inventory",()=>
        {
            var g=Arena();Art(g,BattleArtId.GeneRampage);
            PowerStat[] stats=[PowerStat.MeleeDamage,PowerStat.ProjectileDamage,PowerStat.TailDamage,PowerStat.StompDamage];
            Need(stats.Count(s=>g.Run.Resolve(s,1)>1)==1,"Gene rampage changed wrong number of abilities");
            g.Run.Tick(12.1f);Need(stats.All(s=>g.Run.Resolve(s,1)==1),"Gene rampage permanent");
            g.Run.AddForm(PrimalForm.Raptor);g.Run.SwitchForm(PrimalForm.Raptor);g.Run.AddArt(BattleArtId.BoneArmor,1);
            Art(g,BattleArtId.EmergencyMolt);Need(g.Run.CurrentForm==PrimalForm.Base && g.Run.BattleArts[1]==BattleArtId.BoneArmor,"Emergency molt failed switch or lost other skill");
            Near(g.Run.Resolve(PowerStat.AllDamage,1),1.25f,"Emergency molt buff");
        });
        check("BUILD claw rhythm, dash claw, finisher and two-target tail recovery alter real combat",()=>
        {
            var g=Arena();Evolution(g.Run,EvolutionId.HuntingRhythm);Evolution(g.Run,EvolutionId.ChargingClaw);Evolution(g.Run,EvolutionId.BoneRendingClaw);
            var target=Enemy(g,2080);
            for(int i=0;i<3;i++)Damage(g,target,1,BuildDamageKind.Claw,true);
            Need(g.Run.RhythmReady,"Third hit did not arm rhythm");g.Run.ArmDashClaw();Near(g.Run.ConsumeClawBoost(),1.5f*1.35f,"Claw bonuses did not compose");
            Need(!g.Run.RhythmReady && !g.Run.DashClawReady,"Claw bonuses not consumed");Near(g.Run.Resolve(PowerStat.FinisherDamage,2),2.6f,"Finisher modifier");
            var tail=Arena();Evolution(tail.Run,EvolutionId.SpinningTail);Enemy(tail,1950);Enemy(tail,2080);
            tail.TailRequested=true;Call(tail,"HandleCombatInput");Call(tail,"UpdateCombatHits",.18f);
            Need(tail.TailElapsed==0 && Field<float>(tail,"attackRecovery")<.1f,"Two-target tail recovery unchanged");
        });
        check("BUILD secondary tail and heavy echoes do not recursively spawn, slam waves move along ground",()=>
        {
            var g=Arena();Evolution(g.Run,EvolutionId.ResonantTail);Evolution(g.Run,EvolutionId.TyrantBloodline);var e=Enemy(g,2080);
            Damage(g,e,2,BuildDamageKind.Tail,true);Need(g.BuildPulses.Count==2,"Tail and bloodline echoes did not stack");
            Call(g,"UpdateBuildCombat",.2f);Need(g.BuildPulses.Count==2,"Echo recursively generated echoes");Call(g,"UpdateBuildCombat",.3f);Need(g.BuildPulses.Count==0,"Echo did not expire");
            Evolution(g.Run,EvolutionId.GroundRupture);Call(g,"GroundSlamWaves");Need(g.BuildPulses.Count==2 && g.BuildPulses.All(p=>p.GroundWave),"Ground waves missing");
            float left=g.BuildPulses[0].X,right=g.BuildPulses[1].X;Call(g,"UpdateBuildCombat",.1f);
            Need(g.BuildPulses[0].X<left && g.BuildPulses[1].X>right,"Ground waves did not travel both directions");
        });
        check("BUILD gravity stomp has a capped height bonus and energy predation distinguishes ranged kills",()=>
        {
            int Slam(float height)
            {
                var g=Arena();Evolution(g.Run,EvolutionId.GravityFeet);var e=Enemy(g,2030);
                Set(g,"stompStartFeet",300-height);Call(g,"ImpactStomp");return 100-e.Health;
            }
            Need(Slam(180)>Slam(10) && Slam(10000)==Slam(400),"Gravity damage missing height scaling or cap");
            var energy=Arena();Evolution(energy.Run,EvolutionId.EnergyPredation);Call(energy,"FireBoneSpike");
            Damage(energy,Enemy(energy,2200,1),3,BuildDamageKind.Projectile);Need(energy.RangedEnergy==4,"Ranged kill restored melee energy");
            Damage(energy,Enemy(energy,2250,1),3,BuildDamageKind.Claw,true);Need(energy.RangedEnergy==5,"Melee kill failed energy refill");
        });
        check("BUILD ranged build composes twin, piercing, fragments and proliferation without recursive explosions",()=>
        {
            var g=Arena();foreach(var id in new[]{EvolutionId.TwinSpikes,EvolutionId.PiercingSpikes,EvolutionId.SplinteringSpikes,EvolutionId.SkeletalProliferation})Evolution(g.Run,id);
            int extras=0;
            for(int i=0;i<80;i++)
            {
                Field<List<BoneSpike>>(g,"projectiles").Clear();Call(g,"FireBoneSpike");
                Need(g.Projectiles.Count is 2 or 4 && g.Projectiles.All(p=>p.PierceRemaining==1),"Ranged synergy lost twin or pierce");
                if(g.Projectiles.Count==4)extras++;
            }
            Need(extras>12 && extras<50,"Proliferation probability incorrect");
            Field<List<BoneSpike>>(g,"projectiles").Clear();Call(g,"FireBoneSpike");var shot=g.Projectiles[0];
            var one=Enemy(g,2100);var two=Enemy(g,2160);
            for(int i=0;i<50;i++)Call(g,"UpdateProjectiles",ActTuning.Step);
            Need(one.Health<100 && two.Health<100,"Ordinary pierce missed second enemy");
            Need(g.Projectiles.Count<30,"Fragment recursively exploded");
            Call(g,"FragmentSpike",shot);var fragment=g.Projectiles.Last();int count=g.Projectiles.Count;
            Call(g,"FragmentSpike",fragment);Need(g.Projectiles.Count==count,"Fragment spawned fragments");
        });
        check("BUILD conditional health, entry healing and dodge boost respect triggers",()=>
        {
            var run=new ActRunState();Evolution(run,EvolutionId.RapidRegeneration);run.SetHealth(2);
            run.EnterRoom("a");Need(run.Health==3,"Room healing missing");run.SetHealth(1);run.EnterRoom("a");Need(run.Health==1,"Room healed twice");
            run.EnterRoom("b");Need(run.Health==2,"New room did not heal");Evolution(run,EvolutionId.DangerInstinct);
            Near(run.Resolve(PowerStat.AllDamage,1),1,"Danger active above one HP");run.SetHealth(1);Near(run.Resolve(PowerStat.AllDamage,1),1.2f,"Danger bonus");
            Evolution(run,EvolutionId.PrimalOverdrive);Near(run.Resolve(PowerStat.AllDamage,1),1.2f*1.35f,"Low HP damage did not compose");
            Need(run.Resolve(PowerStat.HurtInvincibility,1)<1 && run.Resolve(PowerStat.AttackSpeed,1)>1,"Overdrive risk or speed missing");
            var g=Arena();Evolution(g.Run,EvolutionId.EvasiveHunter);g.DashRequested=true;g.Update(ActTuning.Step);Call(g,"HurtPlayerCore",null,false);
            Need(g.Run.Remaining(BuildBuff.Dodge)>0,"True dash avoidance did not trigger boost");
            Damage(g,Enemy(g,2250),1,BuildDamageKind.Projectile);Need(g.Run.Remaining(BuildBuff.Dodge)==0,"Dodge boost not consumed on hit");
        });
        check("BUILD adaptive and inherited form modifiers expire without stacking, state clones and new runs reset",()=>
        {
            var g=Arena();Evolution(g.Run,EvolutionId.AdaptiveGene);Evolution(g.Run,EvolutionId.RapidAdaptation);
            g.Run.AddForm(PrimalForm.Tyrannosaur);g.Run.AddForm(PrimalForm.Pterosaur);
            Need(g.Run.SwitchForm(PrimalForm.Tyrannosaur),"Initial form switch");Near(g.Run.FormCooldown,1.75f,"Rare switch cooldown");
            g.Run.Tick(1.8f);Need(g.Run.SwitchForm(PrimalForm.Pterosaur),"Second form switch");
            Near(g.Run.Resolve(PowerStat.MeleeDamage,1),.8f*1.5f,"Previous tyrannosaur damage not inherited");
            Near(g.Run.Resolve(PowerStat.AllDamage,1),1.15f,"Adaptive multiplier stacked");
            var snapshot=g.ExportRunState();g.Run.Tick(6);Near(g.Run.Resolve(PowerStat.MeleeDamage,1),.8f,"Inheritance did not expire");
            Need(snapshot.Remaining(BuildBuff.Inheritance)>0 && snapshot.Evolutions.Count==2,"Clone lost runtime build");
            g.Run.AddArt(BattleArtId.PrimalBarrier);g.EnterNextMap(44);Need(g.Run.CurrentForm==PrimalForm.Pterosaur&&g.Run.BattleArtCount==1&&g.Run.Evolutions.Count==2,"Map switch lost build");
            g.Reset(45);Need(g.Run.Evolutions.Count==0&&g.Run.Forms.Count==1&&g.Run.BuildEffects.Count==0&&g.Run.BattleArtCount==0,"New run retained build");
        });
        check("BUILD full mixed builds remain finite through four chapter combat simulations",()=>
        {
            for(int chapter=1;chapter<=4;chapter++)
            {
                var g=new ActEngine(381,true);g.LoadMapForDebug(chapter,381,false,roomAdventure:true);
                foreach(var e in ActPowerCatalog.Evolutions)Evolution(g.Run,e.Id);
                g.Run.AddForm(PrimalForm.Tyrannosaur);g.Run.AddForm(PrimalForm.Pterosaur);
                var random=new Random(chapter*991);
                for(int frame=0;frame<1800;frame++)
                {
                    if(g.IsDead)g.Run.SetHealth(g.MaximumHealth);
                    if(g.PendingPowerReward==ActRewardChoice.BattleArt)g.DeclineBattleArt();
                    else if(g.PendingPowerReward==ActRewardChoice.Evolution)g.ChooseEvolution(0);
                    else if(g.PendingPowerReward==ActRewardChoice.Form)g.ChooseForm(0);
                    g.RightHeld=true;g.JumpHeld=true;g.JumpRequested=frame%53==0;
                    g.AttackRequested=frame%19==0;g.TailRequested=frame%37==0;g.StompRequested=frame%71==0;
                    g.RangedRequested=frame%23==0;g.DashRequested=frame%89==0;
                    if(frame%90==0){g.Run.AddArt(ActPowerCatalog.Arts[random.Next(25)].Id,0);g.UseBattleArt(0);}
                    if(frame%127==0){g.Run.ResetFormCooldown();g.Run.SwitchForm(g.Run.CurrentForm==PrimalForm.Tyrannosaur?PrimalForm.Pterosaur:PrimalForm.Tyrannosaur);}
                    g.Update(ActTuning.Step);
                    Need(float.IsFinite(g.PlayerX)&&float.IsFinite(g.PlayerY)&&float.IsFinite(g.VelocityY),"Nonfinite player physics");
                    Need(g.Health>=0&&g.Health<=g.MaximumHealth&&g.RangedEnergy>=0&&g.RangedEnergy<=g.MaximumEnergy,"Invalid shared resources");
                    Need(g.Projectiles.Count<300,"Unbounded projectile growth");
                }
            }
        });
    }
}
