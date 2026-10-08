using System.Reflection;
using System.Drawing.Imaging;
using DinoDash.Desktop;

internal static class ExpandedBuildVisualChecks
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static Bitmap Paint(ActPowerRewardDialog dialog)
    {
        dialog.PerformLayout();var surface=dialog.Controls[0];var image=new Bitmap(surface.Width,surface.Height);
        using var g=Graphics.FromImage(image);
        surface.GetType().GetMethod("OnPaint",Flags)!.Invoke(surface,[new PaintEventArgs(g,surface.ClientRectangle)]);
        return image;
    }
    public static void Run()
    {
        ActRareFeedback.Enabled=false;
        try
        {
            foreach(var size in new[]{new Size(780,540),new Size(1280,720)})
            foreach(int selection in new[]{0,2})
            {
                var game=new ActEngine(91);game.OfferEvolutions(EvolutionSource.BossReward);
                var offers=(List<EvolutionId>)typeof(ActEngine).GetField("evolutionOffers",Flags)!.GetValue(game)!;
                offers.Clear();offers.AddRange([EvolutionId.SplinteringSpikes,EvolutionId.AdaptiveGene,EvolutionId.SkeletalProliferation]);
                using var canvas=new GameCanvas(game){Size=size,Dimmed=false};using var scene=canvas.RenderRewardScene();
                using var dialog=new ActPowerRewardDialog(game,scene,scene){ClientSize=size};
                dialog.AdvanceTransition(.3f);
                if(dialog.Reveal is <.45f or >.47f)throw new Exception("Rare reveal must last .65 seconds");
                using(var unfolding=Paint(dialog))unfolding.Save(Path.Combine(AppContext.BaseDirectory,$"boss-evolution-unfold-{size.Width}.png"));
                dialog.AdvanceTransition(.36f);
                using(var complete=Paint(dialog))complete.Save(Path.Combine(AppContext.BaseDirectory,$"boss-evolution-reward-{size.Width}.png"));
                typeof(ActPowerRewardDialog).GetMethod("Choose",Flags)!.Invoke(dialog,[selection]);
                if(!dialog.IsFadingOut || game.PendingPowerReward!=ActRewardChoice.None || game.PowerCueRare!=(selection==2))
                    throw new Exception("Boss reward selection cue or fade missing");
                using var outgoing=Paint(dialog); // Cleared offers must not invalidate card snapshots.
                dialog.AdvanceTransition(.12f);using var midpoint=Paint(dialog);
                midpoint.Save(Path.Combine(AppContext.BaseDirectory,$"boss-evolution-outgoing-{size.Width}-{selection}.png"));
                dialog.AdvanceTransition(.13f);
                if(dialog.DialogResult!=DialogResult.OK)throw new Exception("Boss reward did not finish fading");
            }
            var engine=new ActEngine(27);
            foreach(var definition in ActPowerCatalog.Evolutions)engine.Run.AddEvolution(definition.Id,EvolutionSource.BossReward);
            engine.Run.SetHealth(1);engine.Run.AddForm(PrimalForm.Tyrannosaur);engine.Run.SwitchForm(PrimalForm.Tyrannosaur);
            foreach(var art in new[]{BattleArtId.BloodRage,BattleArtId.PrimalBarrier,BattleArtId.SpikeOverload,BattleArtId.CounterInstinct,BattleArtId.GeneRampage,BattleArtId.AfterimageHunt,BattleArtId.PredationRecovery})
            {engine.Run.AddArt(art);engine.UseBattleArt(0);}
            engine.Run.AddArt(BattleArtId.PiercingSpear);engine.Run.AddArt(BattleArtId.SkySlam);
            foreach(var size in new[]{new Size(780,540),new Size(1280,720)})
            {
                using var canvas=new GameCanvas(engine){Size=size,Dimmed=false};using var image=new Bitmap(size.Width,size.Height);
                canvas.DrawToBitmap(image,canvas.ClientRectangle);image.Save(Path.Combine(AppContext.BaseDirectory,$"expanded-build-hud-{size.Width}.png"));
                float scale=ActHudRenderer.UiScale(size,canvas.DeviceDpi),start=size.Width/scale-256+52;
                for(int i=0;i<engine.Run.Evolutions.Count;i++)
                {
                    var p=new Point((int)((start+i%6*28+10)*scale),(int)((166+i/6*29+10)*scale));
                    if(ActHudRenderer.EvolutionAt(p,size,canvas.DeviceDpi,engine.Run)?.Id!=engine.Run.Evolutions.Order().ElementAt(i))
                        throw new Exception("Expanded evolution icon inaccessible at index "+i);
                }
            }
            using(var atlas=new Bitmap(600,600))using(var g=Graphics.FromImage(atlas))
            {
                g.Clear(ActUiTheme.Panel);
                foreach(var art in ActPowerCatalog.Arts)
                    ActRewardIcons.Draw(g,new RectangleF((int)art.Id%5*120+24,(int)art.Id/5*120+24,72,72),ActRewardIcons.For(art.Id),ActUiTheme.Gold);
                atlas.Save(Path.Combine(AppContext.BaseDirectory,"expanded-art-icons.png"));
            }
            using(var measure=new Bitmap(1,1))using(var g=Graphics.FromImage(measure))
            using(var font=new Font("Microsoft YaHei UI",16,FontStyle.Regular,GraphicsUnit.Pixel))
            {
                foreach(var text in ActPowerCatalog.Arts.Select(a=>a.Description).Concat(ActPowerCatalog.Evolutions.Select(e=>e.Description)))
                    if(g.MeasureString(text,font,222).Height>122)throw new Exception("Expanded reward description overflows: "+text);
            }
            byte[] wave=ActRareFeedback.CreateWave();
            if(wave.Length!=22094 || System.Text.Encoding.ASCII.GetString(wave,0,4)!="RIFF")throw new Exception("Invalid acquisition audio PCM");
            using(var image=new Bitmap(960,360))using(var g=Graphics.FromImage(image))
            {
                for(int i=0;i<200;i++)
                {
                    engine.Run.AddArt(BattleArtId.BoneRain,0);engine.UseBattleArt(0);
                    ActPowerVisuals.Draw(g,engine,i*.05f);
                }
            }
            Console.WriteLine("PASS Boss rare DNA/card reveal and selection fade, 25 art icons, 28 hoverable traits, runtime HUD and embedded audio.");
        }
        finally { ActRareFeedback.Enabled=true; }
    }
}
