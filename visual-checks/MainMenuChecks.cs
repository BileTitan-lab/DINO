using System.Drawing.Imaging;
using System.Reflection;
using DinoDash.Desktop;

internal static class MainMenuChecks
{
    public static void Run()
    {
        foreach (Size size in new[] { new Size(780,540), new Size(1280,720), new Size(1920,1080) })
        {
            using var menu = new ActMainMenu { Dock = DockStyle.None, Size = size };
            if (menu.Controls.Count != 3) throw new Exception("Title menu must have three actions");
            foreach (Control button in menu.Controls)
            {
                if (!menu.ClientRectangle.Contains(button.Bounds)) throw new Exception("Title action clipped");
                if (TextRenderer.MeasureText(button.Text, button.Font).Width > button.Width - 45)
                    throw new Exception("Title action text clipped");
            }
            using var image = new Bitmap(size.Width,size.Height);
            menu.DrawToBitmap(image,menu.ClientRectangle);
            image.Save(Path.Combine(AppContext.BaseDirectory,$"title-{size.Width}.png"),ImageFormat.Png);
            var destination = new Bitmap(size.Width,size.Height);
            using (var g = Graphics.FromImage(destination)) g.Clear(Color.FromArgb(70,140,90));
            menu.StartTransition(destination);
            if (menu.Controls.Cast<Control>().Any(c => c.Visible)) throw new Exception("Title actions remain active during fade");
            if (menu.Advance(.5f)) throw new Exception("Fade completed too soon");
            menu.DrawToBitmap(image,menu.ClientRectangle);
            if (image.GetPixel(size.Width/2,size.Height/2).R > 2) throw new Exception("Fade never reaches black");
            menu.Advance(.35f);
            menu.DrawToBitmap(image,menu.ClientRectangle);
            image.Save(Path.Combine(AppContext.BaseDirectory,$"title-fade-{size.Width}.png"),ImageFormat.Png);
            if (!menu.Advance(.36f)) throw new Exception("Fade failed to finish");
            menu.FinishTransition();
            if (size.Width == 780)
            {
                using var source = new Bitmap(size.Width,size.Height);
                using (var sg = Graphics.FromImage(source)) sg.Clear(Color.FromArgb(190,90,70));
                var target = new Bitmap(size.Width,size.Height);
                using (var tg = Graphics.FromImage(target)) tg.Clear(Color.FromArgb(70,150,100));
                menu.StartSceneTransition((Bitmap)source.Clone(),target);
                menu.Advance(.25f);
                menu.DrawToBitmap(image,menu.ClientRectangle);
                Color first = image.GetPixel(size.Width/2,size.Height/2);
                if (first.R <= first.G) throw new Exception("Scene departure does not show the old map");
                menu.Advance(.25f);
                menu.DrawToBitmap(image,menu.ClientRectangle);
                if (image.GetPixel(size.Width/2,size.Height/2).R > 2) throw new Exception("Scene transition does not reach black");
                menu.Advance(.7f);
                menu.DrawToBitmap(image,menu.ClientRectangle);
                Color arrived=image.GetPixel(size.Width/2,size.Height/2);
                if (arrived.G <= arrived.R) throw new Exception("Scene arrival does not show the new map");
                menu.FinishTransition();
            }
        }
        using var form = new GameForm(12345);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var engine = (ActEngine)typeof(GameForm).GetField("engine",flags)!.GetValue(form)!;
        if ((ActPlayState)typeof(GameForm).GetField("state",flags)!.GetValue(form)! != ActPlayState.MainMenu)
            throw new Exception("Game does not start at title menu");
        typeof(GameForm).GetMethod("StartAdventure",flags)!.Invoke(form,null);
        if ((ActPlayState)typeof(GameForm).GetField("state",flags)!.GetValue(form)! != ActPlayState.Entering || engine.RunTime != 0)
            throw new Exception("Adventure begins before transition");
        using var travelForm=new GameForm(12345);
        var travelEngine=(ActEngine)typeof(GameForm).GetField("engine",flags)!.GetValue(travelForm)!;
        typeof(GameForm).GetMethod("BeginRun",flags)!.Invoke(travelForm,[false]);
        travelEngine.Run.AddBones(44);
        travelEngine.EnterRestStop(55);
        travelEngine.PlacePlayerForTest(travelEngine.ExitX-27,248);
        for(int i=0;i<(int)Math.Ceiling(3.1f/ActTuning.Step);i++)travelEngine.Update(ActTuning.Step);
        if(!travelEngine.IsComplete)throw new Exception("Rest exit did not complete countdown");
        typeof(GameForm).GetMethod("BeginSceneTravel",flags)!.Invoke(travelForm,null);
        if(travelEngine.MapIndex!=2 || travelEngine.IsResting || travelEngine.Run.BoneShards!=44 ||
            (ActPlayState)typeof(GameForm).GetField("state",flags)!.GetValue(travelForm)! != ActPlayState.Entering)
            throw new Exception("GameForm chapter transition lost run state");
        Console.WriteLine("PASS title, monochrome map fades and GameForm rest-to-chapter transition.");
    }
}
