namespace DinoDash.Desktop;

internal enum BuildBuff { BloodRage, Predation, Counter, Overload, Barrier, LifeBurn, GeneRampage, Emergency, Adaptive, Inheritance, Dodge, Afterimages }
internal sealed record BuildEffect(float Remaining, string Name, PowerModifier[] Modifiers);

internal sealed partial class ActRunState
{
    private readonly Dictionary<BuildBuff, BuildEffect> buildEffects=[];
    private readonly HashSet<string> healedRooms=[];
    public IReadOnlyDictionary<BuildBuff, BuildEffect> BuildEffects=>buildEffects;
    public int BarrierCharges { get; private set; }
    public int RhythmHits { get; private set; }
    public bool RhythmReady { get; private set; }
    public bool DashClawReady { get; private set; }
    public int PredationKills { get; private set; }
    public float PredatorBudget { get; private set; }
    public float PredatorHealPerKill { get; private set; }=ActBuildTuning.PredatorHealPerKill;
    public float ArmedClawMultiplier { get; private set; }=1;
    private static PowerModifier Multiply(PowerStat stat,float value)=>new(stat,PowerOperation.Multiply,value);
    public float Remaining(BuildBuff id)=>buildEffects.TryGetValue(id,out var effect)?effect.Remaining:0;
    public void StartBuild(BuildBuff id,float duration,string name,params PowerModifier[] mods)
        =>buildEffects[id]=new(Math.Max(0,duration),name,mods);
    public void StopBuild(BuildBuff id)=>buildEffects.Remove(id);
    public void StartBarrier(int charges,float duration) { BarrierCharges=charges; StartBuild(BuildBuff.Barrier,duration,"原始屏障"); }
    public void StartPredation(float budget,float duration,float healing) { PredatorBudget=budget;PredatorHealPerKill=Math.Max(0,healing);StartBuild(BuildBuff.Predation,duration,"捕食恢复"); }
    public void ArmClaw(float multiplier)=>ArmedClawMultiplier=Math.Max(ArmedClawMultiplier,multiplier);
    public void ArmDashClaw()=>DashClawReady=true;
    public void ResetFormCooldown()=>FormCooldown=0;
    public void PayHealth(float cost)=>health=Math.Max(1,health-Math.Max(0,cost));
    private float TraitValue(PowerStat stat,float basis)
    {
        foreach(var id in evolutions) basis=ApplyModifiers(ActPowerCatalog.Evolution(id).Modifiers,stat,basis);
        return basis;
    }
    private float ResolveBuild(PowerStat stat,float value)
    {
        if(health<=1 && health>0)
        {
            if(stat==PowerStat.AllDamage)value*=TraitValue(PowerStat.CriticalDamage,1);
            if(stat==PowerStat.MoveSpeed)value*=TraitValue(PowerStat.CriticalSpeed,1);
        }
        if(health>0 && health<=TraitValue(PowerStat.LowHpThreshold,0))
        {
            var conditional=stat switch {
                PowerStat.AttackSpeed=>PowerStat.LowHpAttackSpeed,PowerStat.MoveSpeed=>PowerStat.LowHpMoveSpeed,
                PowerStat.AllDamage=>PowerStat.LowHpDamage,PowerStat.HurtInvincibility=>PowerStat.LowHpInvincibility,
                _=>(PowerStat?)null };
            if(conditional is PowerStat c)value*=TraitValue(c,1);
        }
        foreach(var effect in buildEffects.Values)value=ApplyModifiers(effect.Modifiers,stat,value);
        return value;
    }
    private void TickBuild(float dt)
    {
        foreach(var id in buildEffects.Keys.ToArray())
        {
            var effect=buildEffects[id];
            if(effect.Remaining<=dt)buildEffects.Remove(id);
            else buildEffects[id]=effect with {Remaining=effect.Remaining-dt};
        }
        if(Remaining(BuildBuff.Barrier)<=0)BarrierCharges=0;
    }
    public float ConsumeClawBoost()
    {
        float value=ArmedClawMultiplier; ArmedClawMultiplier=1;
        if(RhythmReady){value*=Resolve(PowerStat.RhythmBonus,1);RhythmReady=false;}
        if(DashClawReady){value*=Resolve(PowerStat.DashClawBonus,1);DashClawReady=false;}
        return value;
    }
    public void NoteMeleeHit()
    {
        int required=(int)Resolve(PowerStat.RhythmHits,0);
        if(required<=0 || RhythmReady)return;
        if(++RhythmHits>=required){RhythmHits=0;RhythmReady=true;}
    }
    public void NoteBuildKill()
    {
        if(Remaining(BuildBuff.Predation)>0 && PredatorBudget>0)
        { float healing=Math.Min(PredatorBudget,PredatorHealPerKill);HealFraction(healing);PredatorBudget-=healing; }
        int threshold=(int)Resolve(PowerStat.KillHealThreshold,0);
        if(threshold>0 && ++PredationKills>=threshold){PredationKills=0;Heal(1);}
    }
    public void EnterRoom(string key)
    {
        if(!healedRooms.Add(key))return;
        if(health<MaximumHealth*.5f)Heal((int)Resolve(PowerStat.LowRoomHealing,0));
    }
    public void NoteDodge()
    {
        float duration=Resolve(PowerStat.DodgeBonusDuration,0);
        if(duration>0)StartBuild(BuildBuff.Dodge,duration,"闪避猎手",Multiply(PowerStat.AllDamage,Resolve(PowerStat.DodgeBonus,1)));
    }
    private void NoteFormChanged(PrimalForm previous)
    {
        float duration=Resolve(PowerStat.FormAdaptDuration,0);
        if(duration>0)StartBuild(BuildBuff.Adaptive,duration,"适应性基因",Multiply(PowerStat.AllDamage,Resolve(PowerStat.FormAdaptBonus,1)));
        duration=Resolve(PowerStat.InheritanceDuration,0);
        if(duration<=0)return;
        PowerStat stat=previous switch {PrimalForm.Raptor=>PowerStat.AttackSpeed,PrimalForm.Tyrannosaur=>PowerStat.MeleeDamage,
            PrimalForm.Pterosaur=>PowerStat.ProjectileSpeed,PrimalForm.Ankylosaur=>PowerStat.TailRange,_=>PowerStat.AllDamage};
        float old=ApplyModifiers(ActPrimalCatalog.Form(previous).Modifiers,stat,1);
        StartBuild(BuildBuff.Inheritance,duration,"极速适应",Multiply(stat,1+Math.Max(0,old-1)*Resolve(PowerStat.InheritanceStrength,0)));
    }
    private void CopyBuild(ActRunState copy)
    {
        foreach(var pair in buildEffects)copy.buildEffects[pair.Key]=pair.Value;
        copy.healedRooms.UnionWith(healedRooms);copy.BarrierCharges=BarrierCharges;
        copy.RhythmHits=RhythmHits;copy.RhythmReady=RhythmReady;copy.DashClawReady=DashClawReady;
        copy.PredationKills=PredationKills;copy.PredatorBudget=PredatorBudget;copy.PredatorHealPerKill=PredatorHealPerKill;copy.ArmedClawMultiplier=ArmedClawMultiplier;
    }
}
