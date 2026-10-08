using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

internal static class ActFormVisual
{
    public static Color Tint(PrimalForm form)=>form switch
    {PrimalForm.Raptor=>Color.FromArgb(122,200,164),PrimalForm.Tyrannosaur=>Color.FromArgb(218,130,110),
     PrimalForm.Pterosaur=>Color.FromArgb(136,188,226),PrimalForm.Ankylosaur=>Color.FromArgb(208,184,118),_=>ActHudRenderer.Gold};
    public static RewardEmblem Emblem(PrimalForm form)=>form switch
    {PrimalForm.Raptor=>RewardEmblem.Speed,PrimalForm.Tyrannosaur=>RewardEmblem.Claws,
     PrimalForm.Pterosaur=>RewardEmblem.Wings,PrimalForm.Ankylosaur=>RewardEmblem.Scales,_=>RewardEmblem.Hunter};
    public static RectangleF HudBounds(float height)=>new(18,16,68,88);
    private static readonly Dictionary<PrimalForm,Bitmap> portraits=[];
    public static void DrawHudPortrait(Graphics g,RectangleF bounds,PrimalForm form)
    {
        if(!portraits.TryGetValue(form,out var bitmap))
        {
            bitmap=new Bitmap(72,64);
            using(var art=Graphics.FromImage(bitmap))
            {
                PixelSprites.DrawDinoTinted(art,16,4,0,false,false,Brushes.Black,Brushes.White);
                art.TranslateTransform(16,4);
                DrawAccents(art,form,0,true,0);
            }
            portraits.Add(form,bitmap);
        }
        var saved=g.Save();g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(bitmap,new RectangleF(bounds.X,bounds.Y,bounds.Width,68));g.Restore(saved);
    }
    public static void Draw(Graphics g,ActEngine game)
    {
        var form=game.Run.CurrentForm;if(form==PrimalForm.Base || game.IsDead)return;
        if(game.InvulnerabilityRemaining>0 && game.Pose!=ActPose.Hurt && (int)(game.InvulnerabilityRemaining*16)%2==0)return;
        var save=g.Save();g.TranslateTransform(game.PlayerX+(game.Facing<0?game.PlayerWidth:0),game.PlayerY);
        if(game.Facing<0)g.ScaleTransform(-1,1);
        float bob=game.Pose==ActPose.Run?MathF.Round(MathF.Sin(game.PoseElapsed*14)*2):0;
        if(game.Pose==ActPose.Dash)g.TranslateTransform(0,10);
        DrawAccents(g,form,bob,game.Grounded,game.AnimationClock);
        using var pen=new Pen(Tint(form),2);
        if(game.IsAttacking || game.StompImpactRemaining>0)
        {
            pen.Width=form==PrimalForm.Tyrannosaur?4:2;
            if(game.TailElapsed>0)
            {
                float reach=96*game.Run.Resolve(PowerStat.TailRange,1);
                g.DrawArc(pen,27-reach,27-reach/3,reach*2,reach/3+10,12,156);
            }
            else g.DrawArc(pen,36,10,39,36,-65,125);
        }
        g.Restore(save);
    }
    private static void DrawAccents(Graphics g,PrimalForm form,float bob,bool grounded,float clock)
    {
        if(form==PrimalForm.Base)return;
        using var brush=new SolidBrush(Tint(form));using var pen=new Pen(Tint(form),2);
        if(form==PrimalForm.Raptor)
            for(int i=0;i<3;i++)g.FillPolygon(brush,new PointF[]{new(14+i*5,23+bob),new(7+i*5,12+bob),new(11+i*5,25+bob)});
        else if(form==PrimalForm.Tyrannosaur)
        {
            for(int i=0;i<3;i++)g.FillRectangle(brush,24+i*4,25+i*3+bob,2,8);
            g.FillRectangle(brush,34,4+bob,12,2);
        }
        else if(form==PrimalForm.Pterosaur)
        {
            float wing=grounded?10:18+MathF.Sin(clock*9)*5;
            g.DrawLines(pen,new PointF[]{new(21,28+bob),new(5,28-wing+bob),new(-5,24-wing+bob),new(14,33+bob)});
            for(int i=0;i<3;i++)g.DrawLine(pen,20,29+bob,3+i*5,25-wing+i*4+bob);
        }
        else for(int i=0;i<4;i++)g.FillRectangle(brush,9+i*6,21+bob+(i==0?3:0),5,5);
    }
}
