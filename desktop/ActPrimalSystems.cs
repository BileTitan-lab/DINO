namespace DinoDash.Desktop;

internal sealed partial class ActEngine
{
    private readonly List<MutationId> mutationOffers=[];
    private readonly List<PrimalForm> formOffers=[];
    public IReadOnlyList<MutationId> MutationOffers=>mutationOffers;
    public IReadOnlyList<PrimalForm> FormOffers=>formOffers;
    public int FormStepRequested { get; set; }
    public bool FormMenuRequested { get; set; }
    public ActReward? BossFormReward { get; private set; }
    public int MaximumEnergy=>Run.MaximumEnergy;
    public bool CanSwitchForm=>!IsDead && PendingPowerReward==ActRewardChoice.None && !IsAttacking && !Stomping && HurtPoseRemaining<=0 && DashRemaining<=0 && Run.FormCooldown<=0;
    public bool SwitchForm(PrimalForm id)
    {
        if(!CanSwitchForm || !Run.SwitchForm(id)) return false;
        Notice($"切换为{ActPrimalCatalog.Form(id).Name}");
        AddImpact(PlayerX+PlayerWidth/2,PlayerY+26,false);return true;
    }
    private void HandleFormInput()
    {
        if(FormStepRequested!=0)
        {
            var owned=Run.Forms.Order().ToArray();int index=Array.IndexOf(owned,Run.CurrentForm);
            SwitchForm(owned[(index+Math.Sign(FormStepRequested)+owned.Length)%owned.Length]);
        }
        FormStepRequested=0;
    }
    public bool OfferForms()
    {
        if(PendingPowerReward!=ActRewardChoice.None)return false;
        formOffers.Clear();
        if(Run.Forms.Count>=ActPrimalCatalog.FormCapacity)return OfferEvolutions();
        formOffers.AddRange(ActPrimalCatalog.Forms.Where(f=>!Run.Forms.Contains(f.Id))
            .OrderBy(_=>powerRandom.Next()).Take(3).Select(f=>f.Id));
        if(formOffers.Count==0)return OfferEvolutions();
        PendingPowerReward=ActRewardChoice.Form;ClearInput();return true;
    }
    public bool ChooseForm(int index)
    {
        if(PendingPowerReward!=ActRewardChoice.Form || index<0 || index>=formOffers.Count)return false;
        if(!Run.AddForm(formOffers[index]))return false;
        Notice($"获得{ActPrimalCatalog.Form(formOffers[index]).Name} · 打开形态库可切换");
        formOffers.Clear();PendingPowerReward=ActRewardChoice.None;return true;
    }
    public bool MutationEligible(MutationId id)
    {
        if(!IsResting || IsDead || MapIndex>=ActChapters.Count || Run.HasMutation(id))return false;
        var d=ActPrimalCatalog.Mutation(id);
        return MaximumHealth>d.MaxHpCost && Run.BattleArtCount>=d.ArtCost &&
            ActPowerCatalog.EligibleEvolutions(Run,EvolutionSource.Mutation).Count()>=d.EvolutionCount &&
            (!d.FireCost || RestStop!.Facilities.Any(f=>f.Kind==RestFacilityKind.Fire && !f.Used)) &&
            (d.Duration!=MutationDuration.NextMap || MapIndex<ActChapters.Count);
    }
    public bool OfferMutations()
    {
        if(!IsResting || PendingPowerReward!=ActRewardChoice.None)return false;
        mutationOffers.Clear();mutationOffers.AddRange(ActPrimalCatalog.Mutations.Where(d=>MutationEligible(d.Id))
            .OrderBy(_=>powerRandom.Next()).Take(3).Select(d=>d.Id));
        if(mutationOffers.Count==0){Notice("没有可用异变");return false;}
        PendingPowerReward=ActRewardChoice.Mutation;ClearInput();return true;
    }
    public bool ChooseMutation(int index)
    {
        if(PendingPowerReward!=ActRewardChoice.Mutation || index<0 || index>=mutationOffers.Count)return false;
        var id=mutationOffers[index];if(!MutationEligible(id))return false;
        var d=ActPrimalCatalog.Mutation(id);
        if(d.HpCost<0)Run.SetHealth(1);
        else if(d.HpCost>0)Run.TakeDamage(Math.Min(d.HpCost,Math.Max(0,Run.ExactHealth-1)));
        if(d.ArtCost>0)
        {
            var slots=Enumerable.Range(0,2).Where(i=>Run.BattleArts[i]!=null).OrderBy(_=>powerRandom.Next()).Take(d.ArtCost).ToArray();
            foreach(int slot in slots)Run.ConsumeArt(slot);
        }
        if(d.FireCost)
        {
            var fire=RestStop!.Facilities.First(f=>f.Kind==RestFacilityKind.Fire);
            fire.Used=true;fire.DrainedByMutation=true;
        }
        Run.AddMutation(id);
        foreach(var trait in ActPowerCatalog.EligibleEvolutions(Run,EvolutionSource.Mutation).OrderBy(_=>powerRandom.Next()).Take(d.EvolutionCount).ToArray())Run.AddEvolution(trait.Id,EvolutionSource.Mutation);
        RangedEnergy=Math.Min(RangedEnergy,MaximumEnergy);
        mutationOffers.Clear();PendingPowerReward=ActRewardChoice.None;
        Notice(d.Duration==MutationDuration.NextMap?"异变已获得 · 下一张地图开始生效":$"异变：{d.Name}");return true;
    }
}

internal static class ActChapterPressure
{
    public static readonly float[] Health=[1,1,1.5f,2,2];
    public static readonly float[] Aggression=[1,1,1.15f,1.3f,1.45f];
    public static readonly float[] ExtraUnits=[0,0,.2f,.35f,.5f];
}

internal sealed partial class ActEngine
{
    private void ApplyChapterPressure()
    {
        if(IsResting || MapIndex<2)return;
        var pool=enemies.Where(e=>e.Kind is ActEnemyKind.Melee or ActEnemyKind.Ranged && e.HomeX<layout.BossArenaLeft-400).ToArray();
        int target=(int)MathF.Ceiling(pool.Length*ActChapterPressure.ExtraUnits[MapIndex]);
        var rng=new Random(Seed^0x317ac);
        foreach(var source in pool.Where(_=>!IsRoomJourney).OrderBy(_=>rng.Next()))
        {
            if(target<=0)break;
            foreach(float offset in new[]{110f,-110f,170f,-170f})
            {
                float x=source.HomeX+offset;
                float? ground=TerrainYAt(x+18);
                if(ground is not float y || Math.Abs(y-source.HomeY-source.Height)>30 ||
                    supplies.Any(c=>x>c.Left-160 && x<c.Right+160) ||
                    enemies.Any(e=>Math.Abs(e.HomeX-x)<65) ||
                    solids.Any(s=>s.IntersectsWith(new System.Drawing.RectangleF(x,y-42,36,40))) ||
                    layout.Hazards.Any(h=>Math.Abs(h.X-x)<80) || layout.Mechanisms.Any(m=>Math.Abs(m.X-x)<100))continue;
                var room=layout.Rooms.FirstOrDefault(r=>source.HomeX>=r.Node.StartX && source.HomeX<r.Node.EndX);
                if(room!=null && (x<room.Node.StartX+50 || x>room.Node.EndX-80))continue;
                var added=new ActEnemy{Kind=source.Kind,X=x,Y=y-42,HomeX=x,HomeY=y-42,
                    Health=source.MaxHealth,MaxHealth=source.MaxHealth,Dormant=source.Dormant};
                enemies.Insert(enemies.Count-1,added);target--;break;
            }
        }
        foreach(var enemy in enemies)
            enemy.Health=enemy.MaxHealth=(int)MathF.Ceiling(enemy.MaxHealth*ActChapterPressure.Health[MapIndex]);
    }
}
