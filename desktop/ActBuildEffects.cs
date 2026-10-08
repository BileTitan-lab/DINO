using System.Drawing;

namespace DinoDash.Desktop;

internal enum BuildDamageKind { Claw, Tail, Slam, Projectile, Skill, Proc }
internal sealed class BuildPulse
{
    public float X,Y,Delay,Life,Damage,Radius,Vx,Knockback;
    public bool Fired,GroundWave;
    public readonly HashSet<ActEnemy> Hit=[];
}
internal sealed class BuildRain(BattleArtDefinition art)
{
    public BattleArtDefinition Art { get; }=art;
    public float Delay=art.Delay;
}

// Spatial effects belong to a map. Passive counters and timed modifiers live
// in RunState, so changing maps does not erase the player's build.
internal sealed partial class ActEngine
{
    private readonly List<BuildPulse> buildPulses=[];
    private readonly HashSet<ActEnemy> rushHits=[];
    private BattleArtDefinition? rushingArt,skyArt;
    private readonly List<BuildRain> pendingRains=[];
    private float skyRise,stompStartFeet;
    private bool wasDashing;
    public IReadOnlyList<BuildPulse> BuildPulses=>buildPulses;
    public float BoneRainWarning=>pendingRains.Count==0?0:pendingRains.Min(r=>r.Delay);
    private static PowerModifier Buff(PowerStat stat,float value)=>new(stat,PowerOperation.Multiply,value);
    private static void RegisterExpandedArts()
    {
        ArtEffects[BattleArtId.HunterRush]=(g,a)=>{g.CancelCombat();g.rushingArt=a;g.rushHits.Clear();g.DashRemaining=a.Duration;g.BeginBuildDash();};
        ArtEffects[BattleArtId.TyrantSmash]=(g,a)=>g.Run.ArmClaw(a.Strength);
        ArtEffects[BattleArtId.TailResonance]=(g,a)=>g.SkillTail(a);
        ArtEffects[BattleArtId.SkySlam]=(g,a)=>{g.CancelCombat();g.DashRemaining=0;g.skyArt=a;g.skyRise=a.Delay;g.VelocityY=-a.Speed;g.Grounded=false;g.jumpCut=true;};
        ArtEffects[BattleArtId.BoneRain]=(g,a)=>g.pendingRains.Add(new(a));
        ArtEffects[BattleArtId.PiercingSpear]=(g,a)=>g.SkillProjectile(a,false);
        ArtEffects[BattleArtId.SeekingSpikes]=(g,a)=>g.SkillProjectile(a,true);
        ArtEffects[BattleArtId.SpikeOverload]=(g,a)=>g.Run.StartBuild(BuildBuff.Overload,a.Duration,a.Name);
        ArtEffects[BattleArtId.CounterInstinct]=(g,a)=>g.Run.StartBuild(BuildBuff.Counter,a.Duration,a.Name);
        ArtEffects[BattleArtId.BloodRage]=(g,a)=>{g.Run.PayHealth(a.Cost);g.Run.StartBuild(BuildBuff.BloodRage,a.Duration,a.Name,Buff(PowerStat.MeleeDamage,a.Strength),Buff(PowerStat.AttackSpeed,a.SecondaryStrength));};
        ArtEffects[BattleArtId.PredationRecovery]=(g,a)=>g.Run.StartPredation(a.Strength,a.Duration,a.SecondaryStrength);
        ArtEffects[BattleArtId.TerrorRoar]=(g,a)=>g.FearEnemies(a);
        ArtEffects[BattleArtId.AfterimageHunt]=(g,a)=>g.Run.StartBuild(BuildBuff.Afterimages,a.Duration,a.Name);
        ArtEffects[BattleArtId.PrimalBarrier]=(g,a)=>g.Run.StartBarrier((int)a.Strength,a.Duration);
        ArtEffects[BattleArtId.LifeBurn]=(g,a)=>{g.Run.PayHealth(a.Cost);g.RangedEnergy=g.MaximumEnergy;g.Run.StartBuild(BuildBuff.LifeBurn,a.Duration,a.Name,Buff(PowerStat.AllDamage,a.Strength));};
        ArtEffects[BattleArtId.GeneRampage]=(g,a)=>{
            PowerStat[] choices=[PowerStat.MeleeDamage,PowerStat.ProjectileDamage,PowerStat.TailDamage,PowerStat.StompDamage];
            int choice=g.powerRandom.Next(choices.Length);
            string[] names=["基因暴走 · 近战","基因暴走 · 骨刺","基因暴走 · 尾扫","基因暴走 · 下坠"];
            g.Run.StartBuild(BuildBuff.GeneRampage,a.Duration,names[choice],Buff(choices[choice],a.Strength));};
        ArtEffects[BattleArtId.EmergencyMolt]=(g,a)=>{
            g.Run.ResetFormCooldown();g.CancelCombat();g.DashRemaining=0;g.HurtPoseRemaining=0;
            var owned=g.Run.Forms.OrderBy(f=>(int)f).ToArray();int i=Array.IndexOf(owned,g.Run.CurrentForm);
            if(owned.Length>1)g.Run.SwitchForm(owned[(i+1)%owned.Length]);
            g.Run.StartBuild(BuildBuff.Emergency,a.Duration,a.Name,Buff(PowerStat.AllDamage,a.Strength));};
    }
    private void ResetBuildMap()
    {
        buildPulses.Clear();rushHits.Clear();pendingRains.Clear();rushingArt=skyArt=null;
        skyRise=0;wasDashing=false;stompStartFeet=PlayerY+PlayerHeight;
    }
    private float BuildDashSpeed=>rushingArt?.Speed??ActTuning.DashSpeed;
    private void BeginBuildDash(){wasDashing=true;}
    private void FinishBuildDash()
    {
        if(!wasDashing || DashRemaining>0)return;
        wasDashing=false;rushingArt=null;Run.ArmDashClaw();
        if(Run.Remaining(BuildBuff.Afterimages)>0)
        {
            var art=ActPowerCatalog.Art(BattleArtId.AfterimageHunt);
            buildPulses.Add(new(){X=PlayerX+PlayerWidth/2,Y=PlayerY+PlayerHeight/2,
                Delay=art.Delay,Life=ActBuildTuning.AfterimageLife,Damage=art.Strength,Radius=art.Radius});
        }
    }
    private IEnumerable<ActEnemy> Nearby(float x,float y,float radius)=>enemies.Where(e=>e.State!=ActEnemyState.Dead&&!e.Dormant&&
        Math.Abs(e.X+e.Width/2-x)<=radius+e.Width/2 && Math.Abs(e.Y+e.Height/2-y)<=radius+e.Height/2).ToArray();
    private void SkillTail(BattleArtDefinition art)
    {
        tailElapsed=.17f;tailHit=true;attackElapsed=0;attackRecovery=0;
        float x=PlayerX+PlayerWidth/2,y=PlayerY+PlayerHeight/2;
        foreach(var enemy in Nearby(x,y,art.Radius*Run.Resolve(PowerStat.TailRange,1)))
        {
            DamageEnemyTyped(enemy,Run.Resolve(PowerStat.TailDamage,art.Strength),art.Knockback*Run.Resolve(PowerStat.TailKnockback,1),true,false,BuildDamageKind.Tail);
            if(enemy.Kind!=ActEnemyKind.Boss && enemy.State!=ActEnemyState.Dead){enemy.Y-=4;enemy.VerticalSpeed=-ActBuildTuning.TailLaunchSpeed;}
        }
        AddImpact(x,y,true);
    }
    private void SkillProjectile(BattleArtDefinition art,bool seeking)
    {
        int count=seeking?art.Projectiles:1;
        for(int i=0;i<count;i++)
        {
            float angle=(i-(count-1)*.5f)*.16f;
            projectiles.Add(new(){X=PlayerX+PlayerWidth/2,Y=PlayerY+24,Direction=Facing,
                Vx=Facing*MathF.Cos(angle)*art.Speed*Run.Resolve(PowerStat.ProjectileSpeed,1),Vy=MathF.Sin(angle)*art.Speed,
                Damage=Run.Resolve(PowerStat.ProjectileDamage,art.Strength),Homing=seeking,Special=true,
                PierceRemaining=art.Pierce+(int)Run.Resolve(PowerStat.ProjectilePierce,0),
                Life=(seeking?ActBuildTuning.SeekingLife:ActBuildTuning.SpearLife)*Run.Resolve(PowerStat.ProjectileLife,1),Scale=seeking?.8f:1.7f});
        }
    }
    private void FearEnemies(BattleArtDefinition art)
    {
        foreach(var enemy in Nearby(PlayerX+PlayerWidth/2,PlayerY+PlayerHeight/2,art.Radius))
        {
            if(enemy.Kind==ActEnemyKind.Boss)continue;
            enemy.FearRemaining=art.Duration;enemy.FearSpeed=art.Speed;enemy.State=ActEnemyState.Recover;enemy.StateTime=0;
            enemyShots.RemoveAll(s=>ReferenceEquals(s.Source,enemy));
        }
        AddImpact(PlayerX+PlayerWidth/2,PlayerY+20,true);
    }
    private bool UpdateFear(ActEnemy enemy,float dt)
    {
        if(enemy.FearRemaining<=0)return false;
        enemy.FearRemaining=Math.Max(0,enemy.FearRemaining-dt);
        int direction=enemy.X+enemy.Width/2<PlayerX+PlayerWidth/2?-1:1;
        float old=enemy.X;
        enemy.X=Math.Clamp(enemy.X+direction*enemy.FearSpeed*dt*EnemySpeedRatio,0,WorldWidth-enemy.Width);
        if(solids.Any(s=>s.IntersectsWith(enemy.Bounds)))enemy.X=old;
        if(UsesTerrain && TerrainYAt(enemy.X+enemy.Width/2) is not float)enemy.X=old;
        enemy.Facing=-direction;enemy.State=ActEnemyState.Recover;enemy.StateTime=0;
        FollowTerrain(enemy,dt);return true;
    }
    private bool TryBuildCounter()
    {
        if(Run.Remaining(BuildBuff.Counter)<=0)return false;
        Run.StopBuild(BuildBuff.Counter);InvulnerabilityRemaining=ActBuildTuning.CounterInvincibility;
        var art=ActPowerCatalog.Art(BattleArtId.CounterInstinct);
        foreach(var enemy in Nearby(PlayerX+PlayerWidth/2,PlayerY+24,art.Radius))DamageEnemyTyped(enemy,art.Strength,art.Knockback,true,false,BuildDamageKind.Skill);
        AddImpact(PlayerX+PlayerWidth/2,PlayerY+24,true);return true;
    }
    private void UpdateBuildCombat(float dt)
    {
        if(rushingArt is { } rush && DashRemaining>0)
            foreach(var enemy in Nearby(PlayerX+PlayerWidth/2,PlayerY+24,rush.Radius))
                if(rushHits.Add(enemy))DamageEnemyTyped(enemy,rush.Strength,rush.Knockback,true,false,BuildDamageKind.Skill);
        FinishBuildDash();
        if(skyArt!=null && skyRise>0)
        {
            skyRise-=dt;
            if(skyRise<=0){stomping=true;stompStartFeet=PlayerY+PlayerHeight;VelocityY=850;}
        }
        for(int index=pendingRains.Count-1;index>=0;index--)
        {
            var pending=pendingRains[index];var rain=pending.Art;
            pending.Delay-=dt;
            if(pending.Delay<=0)
            {
                float left=Math.Max(0,CameraX+40),right=Math.Min(WorldWidth,CameraX+ActTuning.ViewWidth-40);
                for(int i=0;i<rain.Projectiles;i++)
                {
                    float x=left+(right-left)*(i+.5f)/rain.Projectiles;
                    float y=PlayerY-ActBuildTuning.RainHeight;
                    projectiles.Add(new(){X=x,Y=y,Vx=0,Vy=rain.Speed,Direction=1,Special=true,
                        Damage=Run.Resolve(PowerStat.ProjectileDamage,rain.Strength),Life=2,
                        PierceRemaining=(int)Run.Resolve(PowerStat.ProjectilePierce,0)});
                }
                pendingRains.RemoveAt(index);
            }
        }
        foreach(var pulse in buildPulses.ToArray())
        {
            pulse.Delay-=dt;pulse.Life-=dt;
            if(pulse.Delay<=0)
            {
                pulse.X+=pulse.Vx*dt;
                if(pulse.GroundWave && TerrainYAt(pulse.X) is float floor)pulse.Y=floor-18;
                if(pulse.GroundWave || !pulse.Fired)
                    foreach(var enemy in Nearby(pulse.X,pulse.Y,pulse.Radius))
                        if(pulse.Hit.Add(enemy))DamageEnemyTyped(enemy,pulse.Damage,pulse.Knockback,false,false,BuildDamageKind.Proc);
                if(!pulse.Fired){pulse.Fired=true;AddImpact(pulse.X,pulse.Y,true);}
            }
            if(pulse.Life<=0)buildPulses.Remove(pulse);
        }
    }
    private void QueueEcho(ActEnemy enemy,float damage)
    {
        if(damage<=0)return;
        buildPulses.Add(new(){X=enemy.X+enemy.Width/2,Y=enemy.Y+enemy.Height/2,
            Delay=ActBuildTuning.EchoDelay,Life=ActBuildTuning.EchoLife,Radius=ActBuildTuning.EchoRadius,Damage=damage,Knockback=100});
    }
    private void NoteBuildHit(ActEnemy enemy,BuildDamageKind kind,bool killed,bool melee)
    {
        if(kind is BuildDamageKind.Claw or BuildDamageKind.Tail or BuildDamageKind.Slam)Run.NoteMeleeHit();
        if(kind==BuildDamageKind.Tail)QueueEcho(enemy,Run.Resolve(PowerStat.TailEchoDamage,0));
        if(kind==BuildDamageKind.Tail || kind==BuildDamageKind.Slam || kind==BuildDamageKind.Claw&&comboStep==3)
            QueueEcho(enemy,Run.Resolve(PowerStat.HeavyEchoDamage,0));
        if(kind!=BuildDamageKind.Proc && Run.Remaining(BuildBuff.Dodge)>0)Run.StopBuild(BuildBuff.Dodge);
        if(!killed || !enemy.BuildRewardEligible || enemy.BuildKillClaimed)return;
        enemy.BuildKillClaimed=true;Run.NoteBuildKill();
        if(melee)
            RangedEnergy=Math.Min(MaximumEnergy,RangedEnergy+(int)Run.Resolve(PowerStat.MeleeKillEnergy,0));
    }
    private void GroundSlamWaves()
    {
        float damage=Run.Resolve(PowerStat.SlamWaveDamage,0),range=Run.Resolve(PowerStat.SlamWaveRange,0);
        if(damage<=0 || range<=0 || !Grounded)return;
        foreach(int d in new[]{-1,1})buildPulses.Add(new(){X=PlayerX+PlayerWidth/2,Y=PlayerY+PlayerHeight-18,
            Vx=d*ActBuildTuning.GroundWaveSpeed,Radius=35,Damage=damage,Life=range/ActBuildTuning.GroundWaveSpeed,
            GroundWave=true,Knockback=180});
    }
    private bool FreeBoneSpikes=>Run.Remaining(BuildBuff.Overload)>0 || Run.Resolve(PowerStat.EnergyCost,1)<=0;
    private void FragmentSpike(BoneSpike source)
    {
        if(source.IsFragment)return;
        int count=(int)Run.Resolve(PowerStat.ProjectileFragments,0);
        for(int i=0;i<count;i++)
        {
            float angle=(i+1)*MathF.PI*2/count;
            projectiles.Add(new(){X=source.X,Y=source.Y,Direction=MathF.Cos(angle)<0?-1:1,
                Vx=MathF.Cos(angle)*ActBuildTuning.FragmentSpeed,Vy=MathF.Sin(angle)*ActBuildTuning.FragmentSpeed,
                Damage=source.Damage*Run.Resolve(PowerStat.FragmentDamageRatio,1),Special=true,IsFragment=true,
                PierceRemaining=(int)Run.Resolve(PowerStat.ProjectilePierce,0),Life=ActBuildTuning.FragmentLife,Scale=.55f});
        }
    }
}
