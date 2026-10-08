using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

internal static class ActRestVisual
{
    public static void DrawScene(Graphics g, ActEngine game, Size size, float time, ActBindings bindings)
    {
        bool hot = game.MapIndex == 4;
        using var sky = new LinearGradientBrush(new Rectangle(Point.Empty,size),
            hot ? Color.FromArgb(79,61,58) : Color.FromArgb(65,83,75), Color.FromArgb(18,30,30),90f);
        g.FillRectangle(sky,new Rectangle(Point.Empty,size));
        float scale = Math.Min(size.Width/960f,size.Height/440f);
        var saved = g.Save();
        g.TranslateTransform((size.Width-960*scale)/2-game.CameraX*scale,size.Height*.75f-300*scale);
        g.ScaleTransform(scale,scale);
        using var tree = new SolidBrush(hot ? Color.FromArgb(53,45,48) :
            game.MapIndex == 2 ? Color.FromArgb(77,73,62) : Color.FromArgb(43,58,50));
        using var rock = new SolidBrush(Color.FromArgb(25,37,33));
        using var gold = new Pen(Color.FromArgb(150,137,96),2);
        for (int i=0;i<13;i++)
        {
            float x=i*165-60;
            if (hot)
            {
                g.FillPolygon(tree,[new PointF(x-27,300),new PointF(x-9,-98),
                    new PointF(x+12,-174),new PointF(x+33,-103),new PointF(x+57,300)]);
                using var seam=new Pen(Color.FromArgb(124,76,64),2);
                g.DrawLine(seam,x+9,-75,x+14,245);
            }
            else if(game.MapIndex==2)
            {
                g.FillPolygon(tree,[new PointF(x-45,300),new PointF(x-16,-47),
                    new PointF(x+28,-153),new PointF(x+70,-52),new PointF(x+106,300)]);
                using var ledge=new Pen(Color.FromArgb(117,103,82),3);
                g.DrawLine(ledge,x-32,35,x+58,28);
            }
            else
            {
                g.FillPolygon(tree,[new PointF(x,300),new PointF(x+28,-280),new PointF(x+55,-280),new PointF(x+85,300)]);
                g.FillEllipse(tree,x-90,-70,270,130);
            }
            g.FillPolygon(rock,[new PointF(x,300),new PointF(x+18,251),new PointF(x+51,246),new PointF(x+79,300)]);
        }
        g.FillRectangle(rock,0,300,1800,600);
        g.DrawLine(gold,0,300,1800,300);
        // Warm tent and nest frame the mandatory fire, independent of random facilities.
        using var cloth = new SolidBrush(Color.FromArgb(89,90,66));
        g.FillPolygon(cloth,[new PointF(235,296),new PointF(325,167),new PointF(445,296)]);
        g.FillPolygon(rock,[new PointF(287,296),new PointF(326,204),new PointF(373,296)]);
        g.DrawLines(gold,[new PointF(226,299),new PointF(325,161),new PointF(451,299)]);
        using var straw = new Pen(Color.FromArgb(138,119,78),3);
        for(int i=0;i<5;i++) g.DrawArc(straw,683-i*4,281-i*2,88+i*8,19+i*4,0,180);
        DrawFacilities(g,game,game.RestStop!.Facilities,time,bindings);
        DrawPortal(g,game,game.ExitX,300,true);
        ActSpriteAnimator.DrawDinosaur(g,game);
        ActFormVisual.Draw(g,game);
        g.Restore(saved);
    }
    public static void DrawFacilities(Graphics g,ActEngine game,IReadOnlyList<RestFacility> facilities,float time,ActBindings bindings)
    {
        using var cloth=new SolidBrush(Color.FromArgb(89,90,66));
        using var rock=new SolidBrush(Color.FromArgb(25,37,33));
        using var gold=new Pen(Color.FromArgb(150,137,96),2);
        foreach (var facility in facilities)
        {
            float x = facility.X;
            using var shadow = new SolidBrush(Color.FromArgb(85,0,0,0));
            g.FillEllipse(shadow,x-62,289,124,16);
            if(facility.Kind==RestFacilityKind.Fire)
            {
                using var path = new GraphicsPath(); path.AddEllipse(x-225,108,450,235);
                using var light = new PathGradientBrush(path) { CenterColor=Color.FromArgb(facility.Used?45:90,250,192,91),SurroundColors=[Color.Transparent] };
                g.FillPath(light,path);
                using var stone = new SolidBrush(Color.FromArgb(128,118,91));
                for(int i=0;i<7;i++) g.FillRectangle(stone,x-37+i*11,291+(i%2)*3,10,7);
                using var logs = new Pen(Color.FromArgb(90,59,38),8);
                g.DrawLine(logs,x-24,291,x+24,281); g.DrawLine(logs,x-24,281,x+24,291);
                float h=facility.Used?18:58+MathF.Sin(time*7)*5;
                using var flame = new SolidBrush(facility.Used?Color.FromArgb(166,91,54):Color.FromArgb(240,153,67));
                g.FillPolygon(flame,[new PointF(x-21,285),new PointF(x-12,285-h*.7f),new PointF(x-3,285-h*.4f),new PointF(x+5,285-h),new PointF(x+22,285)]);
                using var core = new SolidBrush(Color.FromArgb(255,222,143));
                g.FillPolygon(core,[new PointF(x-9,285),new PointF(x+2,285-h*.5f),new PointF(x+11,285)]);
                if(!facility.Used)for(int i=0;i<7;i++) g.FillRectangle(core,x+MathF.Sin(time+i*3)*22,266-(time*24+i*13)%95,2,3);
            }
            else if(facility.Kind==RestFacilityKind.Merchant)
            {
                g.FillRectangle(cloth,x-59,257,118,29);
                g.DrawLine(gold,x-60,298,x-60,196); g.DrawLine(gold,x+60,298,x+60,196);
                g.FillPolygon(cloth,[new PointF(x-76,203),new PointF(x,170),new PointF(x+76,203),new PointF(x+62,219),new PointF(x-62,219)]);
                g.DrawLines(gold,[new PointF(x-76,203),new PointF(x,170),new PointF(x+76,203)]);
                var pose=g.Save();g.TranslateTransform(x-18,215);g.ScaleTransform(.7f,.7f);
                PixelSprites.DrawDinoTinted(g,0,0,0,false,false,rock,Brushes.Beige);g.Restore(pose);
                Bone(g,x-32,252,Color.Wheat); Bone(g,x+33,252,Color.Wheat);
            }
            else if(facility.Kind==RestFacilityKind.Egg)
            {
                using var shell=new SolidBrush(facility.Used?Color.FromArgb(123,125,104):Color.FromArgb(219,209,164));
                if(!facility.Used)
                {
                    g.FillEllipse(shell,x-23,237,46,58);
                    using var spots=new SolidBrush(Color.FromArgb(169,93,143));
                    g.FillEllipse(spots,x-12,247,10,13);g.FillEllipse(spots,x+4,267,13,16);
                    using var crack=new Pen(Color.FromArgb(220,145,172),2);
                    g.DrawLines(crack,new PointF[]{new(x+1,242),new(x-6,256),new(x+6,267),new(x-2,286)});
                }
                else g.FillPolygon(shell,[new PointF(x-24,278),new PointF(x-10,287),new PointF(x,280),new PointF(x+22,277),new PointF(x+16,295),new PointF(x-16,295)]);
            }
            else
            {
                g.FillPolygon(cloth,[new PointF(x-34,296),new PointF(x-26,270),new PointF(x+27,270),new PointF(x+35,296)]);
                if(!facility.Used)
                {
                    if(facility.Kind==RestFacilityKind.Bones)for(int i=0;i<3;i++)Bone(g,x+(i-1)*14,259-(i%2)*11,Color.Wheat);
                    else ActRewardIcons.DrawPickup(g,x,248,facility.Kind==RestFacilityKind.Evolution?ActRewardKind.Evolution:ActRewardKind.BattleArt,time);
                }
            }
            Label(g,facility.Name+(facility.DrainedByMutation?" · 已被异变熄灭":facility.Used?facility.Kind==RestFacilityKind.Fire?" · 已休息":" · 已领取":""),x,facility.Kind==RestFacilityKind.Merchant?145:195);
            if(game.NearbyFacility==facility)
                Label(g,facility.DrainedByMutation?"火堆已失效":facility.Used?"本次已使用":$"[{SettingsDialog.KeyName((Keys)bindings.Interact)}] 互动",x,325);
        }
    }
    public static void DrawPortal(Graphics g,ActEngine game,float x,float y,bool rest)
    {
        using var rim=new Pen(Color.FromArgb(179,183,147),3);
        using var glow=new SolidBrush(Color.FromArgb(35,225,212,151));
        g.FillEllipse(glow,x-ActRestTuning.ExitRadius,y-12,ActRestTuning.ExitRadius*2,22);
        g.DrawArc(rim,x-30,y-99,60,108,180,180);
        g.DrawLine(rim,x-30,y-45,x-30,y);g.DrawLine(rim,x+30,y-45,x+30,y);
        Label(g,rest?game.MapIndex<ActChapters.Count?"前往 "+ActChapters.Name(game.MapIndex+1):"远征终点":"前往守夜营地",x,y-126);
        Label(g,"停留 3 秒",x,y+18);
    }
    private static void Label(Graphics g,string text,float x,float y)
    {
        using var font=new Font("Microsoft YaHei UI",11,FontStyle.Regular,GraphicsUnit.Pixel);
        using var format=new StringFormat{Alignment=StringAlignment.Center};
        using var ink=new SolidBrush(Color.FromArgb(232,221,186));
        g.DrawString(text,font,ink,new RectangleF(x-125,y,250,23),format);
    }
    public static void DrawBones(Graphics g,ActEngine game,float time)
    {
        foreach(var drop in game.BoneDrops.Where(d=>!d.Collected && Math.Abs(d.X-game.PlayerX)<1100))
            Bone(g,drop.X,drop.Y+MathF.Sin(time*3+drop.X)*2,Color.Wheat);
    }
    private static void Bone(Graphics g,float x,float y,Color color)
    {
        using var ink=new SolidBrush(color);
        g.FillRectangle(ink,x-6,y-2,12,4);
        foreach(float end in new[]{x-8,x+8}) {g.FillEllipse(ink,end-3,y-5,6,6);g.FillEllipse(ink,end-3,y,6,6);}
    }
}
