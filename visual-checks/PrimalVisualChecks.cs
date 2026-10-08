using DinoDash.Desktop;
using System.Reflection;

internal static class PrimalVisualChecks
{
    private static void Save(Form dialog,string name)
    {
        _=dialog.Handle;dialog.PerformLayout();
        using var root=new Panel{Size=dialog.ClientSize,BackColor=dialog.BackColor,ForeColor=dialog.ForeColor,Font=dialog.Font};
        var children=dialog.Controls.Cast<Control>().ToArray();
        foreach(var child in children)root.Controls.Add(child);
        root.PerformLayout();
        using var picture=new Bitmap(root.Width,root.Height);root.DrawToBitmap(picture,root.ClientRectangle);
        picture.Save(Path.Combine(AppContext.BaseDirectory,name));
        foreach(var child in children)dialog.Controls.Add(child);
    }
    public static void Run()
    {
        CheckSelectionFadeOut();
        foreach(var size in new[]{new Size(780,540),new Size(1280,720)})
        {
            var g=new ActEngine(9);g.EnterRestStop(99);g.OfferMutations();
            var offers=(List<MutationId>)typeof(ActEngine).GetField("mutationOffers",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(g)!;
            offers.Clear();offers.AddRange([MutationId.Brutality,MutationId.UnstableCells,MutationId.BrittleShell]);
            using var canvas=new GameCanvas(g){Size=size,Dimmed=false};
            using var scene=canvas.RenderRewardScene();
            using(var dialog=new ActPowerRewardDialog(g,scene,scene){Size=size})
            {
                dialog.CreateControl();dialog.AdvanceTransition(1);
                Save(dialog,$"mutation-reward-{size.Width}.png");
            }
            g.ChooseMutation(0);g.OfferForms();
            using(var dialog=new ActPowerRewardDialog(g,scene,scene){Size=size})
            {
                dialog.CreateControl();dialog.AdvanceTransition(1);
                Save(dialog,$"form-reward-{size.Width}.png");
            }
        }
        var game=new ActEngine(2);game.Run.AddForm(PrimalForm.Ankylosaur);game.Run.AddForm(PrimalForm.Raptor);
        game.Run.SwitchForm(PrimalForm.Ankylosaur);game.Run.TakeDamage(.5f);
        using(var canvas=new GameCanvas(game){Size=new Size(1280,720),Dimmed=false})
        using(var picture=new Bitmap(1280,720))
        {canvas.DrawToBitmap(picture,canvas.ClientRectangle);picture.Save(Path.Combine(AppContext.BaseDirectory,"form-hud-half-heart.png"));}
        using(var library=new ActFormLibraryDialog(game))Save(library,"form-library.png");
        using(var measure=new Bitmap(1,1))using(var graphics=Graphics.FromImage(measure))
        using(var font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel))
        {
            var descriptions=ActPrimalCatalog.Forms.Select(f=>f.Description).Concat(ActPrimalCatalog.Mutations.Select(m=>$"代价：{m.Cost}\n收益：{m.Benefit}"));
            foreach(var text in descriptions)
                if(graphics.MeasureString(text,font,232).Height>122)throw new Exception("Primal reward text overflows: "+text);
        }
        Console.WriteLine("PASS mutation/form reward cards at two sizes, text bounds, library and fractional-heart HUD.");
    }

    private static void CheckSelectionFadeOut()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var choose=typeof(ActPowerRewardDialog).GetMethod("Choose",flags)!;
        Bitmap Paint(ActPowerRewardDialog dialog)
        {
            dialog.PerformLayout();
            var surface=dialog.Controls[0];
            var frame=new Bitmap(surface.Width,surface.Height);
            using var graphics=Graphics.FromImage(frame);
            // Invoke the real paint handler directly so rendering exceptions fail
            // this console check instead of displaying a WinForms error dialog.
            surface.GetType().GetMethod("OnPaint",flags)!.Invoke(surface,
                [new PaintEventArgs(graphics,surface.ClientRectangle)]);
            return frame;
        }
        string Digest(Bitmap frame)
        {
            using var bytes=new MemoryStream();
            frame.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()));
        }
        void ConfirmFade(ActEngine game,ActPowerRewardDialog dialog,int selection,string file)
        {
            dialog.AdvanceTransition(1);
            using var before=Paint(dialog);
            choose.Invoke(dialog,[selection]);
            if(!dialog.IsFadingOut || game.PendingPowerReward!=ActRewardChoice.None ||
                game.FormOffers.Count!=0 || game.MutationOffers.Count!=0)
                throw new Exception("Reward selection did not consume its offers before fade-out");
            using var selected=Paint(dialog);
            if(Digest(before)!=Digest(selected))
                throw new Exception("Consuming a reward changed or removed its outgoing cards");
            var saved=game.ExportRunState();
            choose.Invoke(dialog,[selection]);
            if(game.Run.Forms.Count!=saved.Forms.Count || game.Run.Mutations.Count!=saved.Mutations.Count ||
                game.Health!=saved.Health || game.MaximumHealth!=saved.MaximumHealth)
                throw new Exception("Repeated input applied a reward twice during fade-out");
            dialog.AdvanceTransition(.12f);
            if(dialog.Reveal is <.49f or >.51f || dialog.DialogResult==DialogResult.OK)
                throw new Exception("Primal reward skipped the fade-out midpoint");
            using var midpoint=Paint(dialog);
            if(!string.IsNullOrEmpty(file))midpoint.Save(Path.Combine(AppContext.BaseDirectory,file));
            dialog.ClientSize=new Size(dialog.ClientSize.Width+32,dialog.ClientSize.Height+18);
            using var resized=Paint(dialog);
            dialog.AdvanceTransition(.13f);
            if(dialog.DialogResult!=DialogResult.OK)
                throw new Exception("Primal reward did not close after fade-out");
        }
        foreach(var size in new[]{new Size(780,540),new Size(1280,720)})
        {
            for(int count=1;count<=3;count++)for(int selection=0;selection<count;selection++)
            {
                var game=new ActEngine(9);
                if(!game.OfferForms())throw new Exception("Form fixture has no offers");
                var offers=(List<PrimalForm>)typeof(ActEngine).GetField("formOffers",flags)!.GetValue(game)!;
                offers.RemoveRange(count,offers.Count-count);
                var acquired=game.FormOffers[selection];
                using var dialog=new ActPowerRewardDialog(game){ClientSize=size};
                ConfirmFade(game,dialog,selection,count==3&&selection==0?$"form-selection-fade-{size.Width}.png":"");
                if(!game.Run.Forms.Contains(acquired))throw new Exception("Chosen form was not retained");
            }
            for(int selection=0;selection<3;selection++)
            {
                var game=new ActEngine(9);game.EnterRestStop(99);game.OfferMutations();
                var offers=(List<MutationId>)typeof(ActEngine).GetField("mutationOffers",flags)!.GetValue(game)!;
                offers.Clear();offers.AddRange([MutationId.BloodGene,MutationId.HollowFrame,MutationId.Brutality]);
                var acquired=game.MutationOffers[selection];
                using var dialog=new ActPowerRewardDialog(game){ClientSize=size};
                ConfirmFade(game,dialog,selection,selection==0?$"mutation-selection-fade-{size.Width}.png":"");
                if(!game.Run.HasMutation(acquired))throw new Exception("Chosen mutation was not retained");
            }
        }
        Console.WriteLine("PASS form/mutation selection redraw after offer consumption, 1/2/3 cards, duplicate input, resize and fade-out completion.");
    }
}
