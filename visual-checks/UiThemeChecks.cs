using System.Reflection;
using DinoDash.Desktop;

internal static class UiThemeChecks
{
    private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class DialogSurface(Form dialog):Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {typeof(ActUiDialog).GetMethod("OnPaintBackground",Flags)!.Invoke(dialog,[e]);}
    }
    internal static void SaveDialog(Form dialog,string file)
    {
        _=dialog.Handle;dialog.PerformLayout();
        using var root=new DialogSurface(dialog){Size=dialog.ClientSize,Font=dialog.Font,ForeColor=dialog.ForeColor,BackColor=dialog.BackColor};
        var children=dialog.Controls.Cast<Control>().ToArray();
        foreach(var c in children)root.Controls.Add(c);
        root.PerformLayout();
        using var image=new Bitmap(root.Width,root.Height);root.DrawToBitmap(image,root.ClientRectangle);
        image.Save(Path.Combine(AppContext.BaseDirectory,file));
        foreach(var c in children)dialog.Controls.Add(c);
    }
    public static void Run()
    {
        using(var settings=new SettingsDialog(new ActBindings()))SaveDialog(settings,"ui-settings.png");
        using(var debug=new ActMapDebugDialog(3,42))SaveDialog(debug,"ui-map-debug.png");
        var game=new ActEngine(42);game.EnterRestStop(19);
        using(var hitCanvas=new GameCanvas(game){Size=new Size(1280,720)})
        {
            int clicks=0;hitCanvas.FormLibraryRequested+=()=>clicks++;
            float scale=ActHudRenderer.UiScale(hitCanvas.Size,hitCanvas.DeviceDpi);
            var hit=ActFormVisual.HudBounds(720/scale);
            typeof(Control).GetMethod("OnMouseClick",Flags)!.Invoke(hitCanvas,[new MouseEventArgs(MouseButtons.Left,1,(int)((hit.X+20)*scale),(int)((hit.Y+20)*scale),0)]);
            typeof(Control).GetMethod("OnMouseClick",Flags)!.Invoke(hitCanvas,[new MouseEventArgs(MouseButtons.Left,1,(int)(160*scale),(int)(170*scale),0)]);
            if(clicks!=1)throw new Exception("Form library must open only from the new portrait hitbox");
        }
        using(var portraits=new Bitmap(360,100))
        {
            using var g=Graphics.FromImage(portraits);g.Clear(Color.SlateGray);
            foreach(var definition in ActPrimalCatalog.Forms)ActFormVisual.DrawHudPortrait(g,new RectangleF((int)definition.Id*72,10,68,88),definition.Id);
            portraits.Save(Path.Combine(AppContext.BaseDirectory,"hud-form-portraits.png"));
        }
        game.RestStop!.Stock.Clear();game.RestStop.Stock.AddRange([new(ShopKind.Healing,35),new(ShopKind.Art,50,BattleArtId.BoneStorm),new(ShopKind.Energy,25)]);
        game.RestStop.Facilities.Clear();game.RestStop.Facilities.Add(new(RestFacilityKind.Merchant,550));
        game.PlacePlayerForTest(523,248);game.InteractWithRest();
        using(var shop=new ActRestShopDialog(game))SaveDialog(shop,"ui-shop.png");
        using var form=new GameForm(42){ClientSize=new Size(1280,720)};
        _=form.Handle;form.PerformLayout();
        var type=typeof(GameForm);
        type.GetMethod("BeginRun",Flags)!.Invoke(form,[false]);
        type.GetMethod("Pause",Flags)!.Invoke(form,null);
        var canvas=(GameCanvas)type.GetField("canvas",Flags)!.GetValue(form)!;
        var parent=canvas.Parent;canvas.Parent=null;
        using(var image=new Bitmap(canvas.Width,canvas.Height)){canvas.DrawToBitmap(image,canvas.ClientRectangle);image.Save(Path.Combine(AppContext.BaseDirectory,"ui-pause.png"));}
        parent!.Controls.Add(canvas);
        var home=(Button)type.GetField("pauseHome",Flags)!.GetValue(form)!;
        typeof(Control).GetMethod("OnClick",Flags)!.Invoke(home,[EventArgs.Empty]);
        if((ActPlayState)type.GetField("state",Flags)!.GetValue(form)! !=ActPlayState.MainMenu)throw new Exception("Pause home did not return to title");
        var run=(ActEngine)type.GetField("engine",Flags)!.GetValue(form)!;
        if(run.Run.Forms.Count!=1||run.Run.Mutations.Count!=0)throw new Exception("Returning to title did not end previous run");
        Console.WriteLine("PASS unified UI full-dialog renders and pause-to-title lifecycle.");
    }
}
