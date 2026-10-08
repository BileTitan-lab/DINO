using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

internal static class ActRoomJourneyVisual
{
    public static string RewardName(RouteReward reward)=>reward switch
    {
        RouteReward.Evolution=>"进化特质",RouteReward.BattleArt=>"战技",
        RouteReward.Bones=>"骨片",RouteReward.Heal=>"鸡腿补给",
        RouteReward.Form=>"原始形态",_=>"未知奖励"
    };
    public static void DrawWorld(Graphics g,ActEngine game,float time,ActBindings bindings)
    {
        if(!game.IsRoomJourney)return;
        var journey=game.Run.RoomJourney!;
        foreach(var mechanism in game.Layout.Mechanisms.Where(_=>game.MapIndex<3))
        {
            float x=mechanism.X+mechanism.Width/2;
            using var shade=new SolidBrush(Color.FromArgb(mechanism.Warning?130:50,44,31,25));
            g.FillEllipse(shade,x-35,mechanism.Y-5,70,10);
            if(mechanism.Warning)
            {Label(g,"!",x,mechanism.Y-50);using var warn=new Pen(ActUiTheme.Gold,2);g.DrawEllipse(warn,x-38,mechanism.Y-7,76,14);}
            if(mechanism.Active)
            {
                var b=mechanism.Bounds;
                using var rock=new SolidBrush(Color.FromArgb(69,59,52));
                g.FillPolygon(rock,[new PointF(b.Left,b.Top+10),new PointF(b.Left+12,b.Top),new PointF(b.Right,b.Top+5),new PointF(b.Right-3,b.Bottom),new PointF(b.Left+5,b.Bottom-3)]);
            }
        }
        if(journey.Stage==RoomJourneyStage.Antechamber)
        {
            ActRestVisual.DrawFacilities(g,game,game.RoomFacilities,time,bindings);
            Door(g,new(0,game.ExitX,300,RouteReward.Bones,BattleRoomTier.Normal),true,time,
                ActChapterRooms.BossRoomName(game.MapIndex),"停留 3 秒进入 Boss");return;
        }
        if(journey.Stage==RoomJourneyStage.Boss)return;
        foreach(var exit in game.RoomExits)
        {
            bool final=journey.Depth>=journey.TotalRooms;
            Door(g,exit,game.RoomExitReady,time,final?"巢穴前庭":RewardName(exit.Reward),
                final?"通往 Boss 前房":exit.Dangerous?"高难房间 · 3 处战斗":"普通房间 · 2 处战斗");
        }
    }
    private static void Door(Graphics g,RoomExit door,bool active,float time,string title,string caption)
    {
        float x=door.X,y=door.FeetY;
        using var wall=new SolidBrush(Color.FromArgb(83,74,60));
        using var inner=new SolidBrush(Color.FromArgb(210,25,31,34));
        using var edge=new Pen(active?ActUiTheme.Gold:ActUiTheme.Muted,3);
        g.FillRectangle(wall,x-42,y-94,84,94);
        g.FillRectangle(inner,x-33,y-85,66,85);
        g.DrawArc(edge,x-33,y-85,66,60,180,180);
        g.DrawLine(edge,x-33,y-55,x-33,y);g.DrawLine(edge,x+33,y-55,x+33,y);
        if(active)
        {
            using var glow=new SolidBrush(Color.FromArgb(38+(int)(MathF.Sin(time*3)*10),ActUiTheme.Gold));
            g.FillEllipse(glow,x-32,y-65,64,60);
        }
        var box=new RectangleF(x-23,y-75,46,46);
        if(door.Reward==RouteReward.Form)ActFormVisual.DrawHudPortrait(g,box,PrimalForm.Base);
        else if(door.Reward==RouteReward.Unknown)Label(g,"?",x,y-66);
        else if(door.Reward==RouteReward.Bones)
        {
            using var bone=new Pen(ActUiTheme.Gold,7){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(bone,x-13,y-49,x+13,y-60);
            using var tip=new SolidBrush(ActUiTheme.Gold);
            foreach(float bx in new[]{x-13,x+13})g.FillEllipse(tip,bx-5,y-60+(bx<x?11:0)-5,10,10);
        }
        else ActRewardIcons.Draw(g,box,door.Reward switch
        {RouteReward.Evolution=>RewardEmblem.Evolution,RouteReward.Heal=>RewardEmblem.Food,_=>RewardEmblem.BattleArt},ActUiTheme.Gold);
        Label(g,(door.Dangerous?"! ":"")+title,x,y-120);
        Label(g,active?caption:"完成全部战斗后开启",x,y+10);
    }
    private static void Label(Graphics g,string text,float x,float y)
    {
        using var font=new Font("Microsoft YaHei UI",11,FontStyle.Bold,GraphicsUnit.Pixel);
        using var back=new SolidBrush(Color.FromArgb(220,25,31,34));
        using var ink=new SolidBrush(ActUiTheme.Paper);
        using var align=new StringFormat{Alignment=StringAlignment.Center};
        g.FillRectangle(back,x-110,y-2,220,23);
        g.DrawString(text,font,ink,new RectangleF(x-108,y,216,22),align);
    }
}
