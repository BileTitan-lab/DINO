using DinoDash.Desktop;
using System.Reflection;

internal static class PrimalChecks
{
    private static void Need(bool b,string message){if(!b)throw new InvalidOperationException(message);}
    private static void Near(float a,float b,string message)=>Need(MathF.Abs(a-b)<.005f,$"{message}: {a} != {b}");
    private static void Tick(ActEngine g,float time){for(int i=0;i<time/ActTuning.Step;i++)g.Update(ActTuning.Step);}
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Hit(ActEngine g,ActEnemy e,float amount=1,bool melee=true,bool claw=true)=>
        typeof(ActEngine).GetMethod("DamageEnemy",Flags)!.Invoke(g,[e,amount,0f,melee,claw]);
    private static ActEngine Rest()
    {var g=new ActEngine(127);g.EnterRestStop(381);return g;}
    private static void Choose(ActEngine g,MutationId id)
    {
        Need(g.OfferMutations(),"No mutation offer");
        var offers=(List<MutationId>)typeof(ActEngine).GetField("mutationOffers",Flags)!.GetValue(g)!;
        offers.Clear();offers.Add(id);Need(g.ChooseMutation(0),$"Cannot choose {id}");
    }
    public static void Run(Action<string,Action> check)
    {
        check("ACT mutation eggs offer unique affordable risks only in rest areas",()=>
        {
            var field=new ActEngine(2);Need(!field.OfferMutations(),"Egg outside rest");
            for(int seed=0;seed<50;seed++)
            {
                var g=new ActEngine(seed);g.EnterRestStop(seed);Need(g.OfferMutations() && g.MutationOffers.Count==3,"Three offers missing");
                Need(g.MutationOffers.Distinct().Count()==3,"Duplicate offer");
                Need(!g.MutationOffers.Contains(MutationId.OverEvolution),"Art cost without art");
            }
            var used=Rest();used.RestStop!.Facilities.First(f=>f.Kind==RestFacilityKind.Fire).Used=true;
            Need(!used.MutationEligible(MutationId.BloodTissue),"Used fire can be sacrificed");
            foreach(var evo in ActPowerCatalog.Evolutions)used.Run.AddEvolution(evo.Id);
            used.Run.AddArt(BattleArtId.BoneArmor);
            Need(!used.MutationEligible(MutationId.UnstableCells)&&!used.MutationEligible(MutationId.OverEvolution),"Unavailable evolution reward");
        });
        check("ACT permanent mutation prices and benefits apply exactly once",()=>
        {
            var blood=Rest();blood.Run.SetHealth(2);Choose(blood,MutationId.BloodGene);
            Need(blood.Health==1 && !blood.MutationEligible(MutationId.BloodGene),"Blood price or duplicate");Near(blood.Run.Resolve(PowerStat.MeleeDamage,1),1.25f,"Blood damage");
            var hollow=Rest();Choose(hollow,MutationId.HollowFrame);Need(hollow.MaximumHealth==4&&hollow.Health==4,"Hollow maximum");
            Near(hollow.Run.Resolve(PowerStat.MoveSpeed,1),1.1f,"Hollow movement");Near(hollow.Run.Resolve(PowerStat.JumpHeight,1),1.15f,"Hollow jump");
            Choose(hollow,MutationId.SpikeGrowth);Need(hollow.MaximumEnergy==4&&hollow.RangedEnergy==4,"Energy cap not clamped");Near(hollow.Run.Resolve(PowerStat.ProjectileDamage,1),1.4f,"Spike mutation");
            var over=Rest();over.Run.AddArt(BattleArtId.BoneArmor);Choose(over,MutationId.OverEvolution);
            Need(over.Run.BattleArtCount==0&&over.Run.Evolutions.Count==1,"Art sacrifice");
            var unstable=Rest();Choose(unstable,MutationId.UnstableCells);Need(unstable.Run.Evolutions.Count==2&&unstable.MaximumHealth>=4,"Unstable reward");
            var tissue=Rest();Choose(tissue,MutationId.BloodTissue);Need(tissue.RestStop!.Facilities.First(f=>f.Kind==RestFacilityKind.Fire).Used,"Fire not spent");
            tissue.Run.SetHealth(1);for(int i=0;i<50;i++)Hit(tissue,new ActEnemy{Health=1});Need(tissue.Health>1,"Kill healing never triggered");
            var brittle=Rest();Choose(brittle,MutationId.BrittleShell);Near(brittle.Run.Resolve(PowerStat.HurtInvincibility,1),.65f,"Invulnerability price");Near(brittle.Run.Resolve(PowerStat.AllDamage,1),1.2f,"Brittle reward");
        });
        check("ACT next-map mutation waits, activates once, survives cloning and expires at next rest",()=>
        {
            var g=Rest();Choose(g,MutationId.BloodGene);Choose(g,MutationId.Brutality);
            Need(g.Health==1&&g.Run.PendingMapMutations.Contains(MutationId.Brutality),"Pending effect missing");
            Near(g.Run.Resolve(PowerStat.AllDamage,1),1,"Effect active in rest");
            var pending=g.ExportRunState();Need(pending.PendingMapMutations.Count==1,"Clone lost pending effect");
            g.EnterNextMap(7);Need(g.Run.ActiveMapMutations.Count==1&&g.Run.PendingMapMutations.Count==0,"Activation failed");
            Near(g.Run.Resolve(PowerStat.AllDamage,1),1.5f,"Active multiplier");
            g.EnterRestStop(8);Near(g.Run.Resolve(PowerStat.AllDamage,1),1,"Map buff survived rest");
            Near(g.Run.Resolve(PowerStat.MeleeDamage,1),1.25f,"Permanent mutation lost");
            Need(g.Run.ActiveMapMutations.Count==0&&pending.PendingMapMutations.Count==1,"Clone collections shared");
            g.EnterNextMap(9);Near(g.Run.Resolve(PowerStat.AllDamage,1),1,"Old map buff reactivated");
        });
        check("ACT forms have one shared run, switch cooldown, stable stacking and new-run reset",()=>
        {
            var g=new ActEngine(31);Need(g.Run.Forms.Count==1&&g.Run.CurrentForm==PrimalForm.Base,"Starting form");
            g.Run.AddForm(PrimalForm.Ankylosaur);g.Run.AddForm(PrimalForm.Raptor);
            g.Run.AddEvolution(EvolutionId.SturdyTail);g.Run.AddArt(BattleArtId.BoneArmor);g.Run.AddBones(33);g.Run.SetHealth(3);
            Need(g.SwitchForm(PrimalForm.Ankylosaur)&&!g.SwitchForm(PrimalForm.Raptor),"Switch cooldown");
            Near(g.Run.Resolve(PowerStat.TailRange,1),1.68f,"Form plus evolution");
            for(int i=0;i<6;i++){g.Run.Tick(6);Need(g.SwitchForm(i%2==0?PrimalForm.Base:PrimalForm.Ankylosaur),"Repeat switch");}
            Near(g.Run.Resolve(PowerStat.TailRange,1),1.68f,"Modifiers accumulated");
            g.EnterRestStop(1);g.EnterNextMap(2);Need(g.Run.Forms.Count==3&&g.Run.BattleArtCount==1&&g.Run.BoneShards==33&&g.Health==3,"Run values lost");
            var clone=g.ExportRunState();Need(clone.Forms.Count==3&&clone.CurrentForm==g.Run.CurrentForm,"Form clone");
            g.Reset(1);Need(g.Run.Forms.Count==1&&g.Run.Mutations.Count==0&&g.Run.CurrentForm==PrimalForm.Base,"New run retained forms");
        });
        check("ACT ankylosaur takes half HP and healing preserves fractions",()=>
        {
            var g=new ActEngine(4);g.Run.AddForm(PrimalForm.Ankylosaur);Need(g.SwitchForm(PrimalForm.Ankylosaur),"Anky switch");
            var hurt=typeof(ActEngine).GetMethod("HurtPlayer",Flags)!;hurt.Invoke(g,[null]);
            Near(g.Run.ExactHealth,4.5f,"Half damage");Near(g.InvulnerabilityRemaining,1.25f,"Anky invulnerability");
            hurt.Invoke(g,[null]);Near(g.Run.ExactHealth,4.5f,"Invulnerability ignored");
            g.Run.Heal(1);Near(g.Run.ExactHealth,5,"Healing cap");
        });
        check("ACT form library caps at three including base and excess form rewards become evolution",()=>
        {
            var g=new ActEngine(8);
            Need(ActPrimalCatalog.FormCapacity==3 && g.Run.Forms.Count==1,"Form capacity or initial library incorrect");
            Need(g.Run.AddForm(PrimalForm.Raptor) && g.Run.AddForm(PrimalForm.Pterosaur),"Two additional forms not accepted");
            Need(!g.Run.AddForm(PrimalForm.Ankylosaur) && !g.Run.AddForm(PrimalForm.Tyrannosaur) &&
                !g.Run.AddForm(PrimalForm.Raptor) && !g.Run.AddForm((PrimalForm)99) &&
                g.Run.Forms.Count==3,"Extra or invalid form bypassed the limit");
            Need(g.OfferForms() && g.PendingPowerReward==ActRewardChoice.Evolution && g.FormOffers.Count==0,
                "A full library did not offer evolution instead");
            Need(!g.ChooseForm(0) && g.ChooseEvolution(0) && g.Run.Forms.Count==3,"Fallback reward changed library size");
            Need(!g.SwitchForm(PrimalForm.Ankylosaur) && g.SwitchForm(PrimalForm.Pterosaur),"Library cannot switch correctly after filling");
            var copy=g.ExportRunState();Need(copy.Forms.Count==3 && copy.Forms.Contains(PrimalForm.Base),"Clone lost limited forms");
            g.Reset(8);Need(g.Run.Forms.Count==1 && g.Run.AddForm(PrimalForm.Ankylosaur),"New run retained the full library");
        });
        check("ACT form, evolution, mutation and rage damage combine without rounding gains",()=>
        {
            var g=Rest();Choose(g,MutationId.BloodGene);Choose(g,MutationId.BrittleShell);
            g.Run.AddForm(PrimalForm.Tyrannosaur);g.SwitchForm(PrimalForm.Tyrannosaur);g.Run.AddEvolution(EvolutionId.SharpClaws);g.Run.StartRage(6);
            var e=new ActEnemy{Health=100,MaxHealth=100};for(int i=0;i<10;i++)Hit(g,e);
            int expected=(int)MathF.Floor(10*2*1.15f*1.25f*1.2f*1.4f+.001f);
            Need(100-e.Health==expected,$"Damage stack {100-e.Health} expected {expected}");
        });
        check("ACT pterosaur shots keep their homing and speed after form switches",()=>
        {
            var g=new ActEngine(8);g.Run.AddForm(PrimalForm.Pterosaur);g.SwitchForm(PrimalForm.Pterosaur);
            g.PlacePlayerForTest(g.Enemy.X-250,248);
            g.RangedRequested=true;Tick(g,.08f);var spike=g.Projectiles.Single();
            Need(spike.Homing&&spike.Vx>ActCombatTuning.BoneSpikeSpeed&&spike.Life>1.6f,"Ptero projectile");
            g.Run.Tick(6);Need(g.SwitchForm(PrimalForm.Base),"Return to base");Need(spike.Homing,"Old projectile changed form");
            var e=g.Enemy;e.X=spike.X+180;e.Y=spike.Y-80;Tick(g,.12f);Need(spike.Vy<0,"Homing did not steer");
        });
        check("ACT bosses drop both evolution and form; acquired forms leave the reward pool",()=>
        {
            var g=new ActEngine(3);g.Boss.Health=1;Hit(g,g.Boss);
            Need(g.BossEvolutionReward!=null&&g.BossFormReward?.Kind==ActRewardKind.Form,"Missing dual reward");
            Need(g.OfferForms()&&g.FormOffers.Count==3,"Form three-choice");var id=g.FormOffers[0];Need(g.ChooseForm(0)&&g.Run.Forms.Contains(id),"Acquire form");
            Need(g.OfferForms()&&!g.FormOffers.Contains(id),"Owned form offered");g.ChooseForm(0);
            foreach(var f in ActPrimalCatalog.Forms)g.Run.AddForm(f.Id);
            Need(g.OfferForms()&&g.PendingPowerReward==ActRewardChoice.Evolution,"Full library fallback");
        });
        check("ACT chapter enemy health, safe reinforcements and aggression increase",()=>
        {
            for(int chapter=2;chapter<=4;chapter++)
            {
                var g=new ActEngine(4);g.LoadMapForDebug(chapter,37,false);
                int original=(chapter==2?ActSecondMap.Generate(37):ActAdvancedMaps.Generate(chapter,37)).Enemies.Count;
                Need(g.Enemies.Count>original,$"No extra units chapter {chapter}");
                Need(g.Enemies.Where(e=>e.Kind==ActEnemyKind.Melee).All(e=>e.MaxHealth==(int)MathF.Ceiling(3*ActChapterPressure.Health[chapter])),"Normal HP scale");
                Need(g.Boss.MaxHealth==(int)MathF.Ceiling(ActBossCatalog.Get(g.Boss).Health*ActChapterPressure.Health[chapter]),"Boss HP scale");
                Need(ActChapterPressure.Aggression[chapter]>ActChapterPressure.Aggression[chapter-1],"Aggression progression");
            }
        });
    }
}
