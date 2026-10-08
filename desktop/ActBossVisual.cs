using System.Drawing;
using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// Art is drawn on a two-pixel grid. Body parts interpolate through anticipation,
// strike and recovery; all poses use the same 126 x 100 contact envelope.
internal static class ActBossVisual
{
    internal static Color Accent(ActBossId id) => id switch
    {
        ActBossId.Stoneback => Color.FromArgb(195, 166, 112),
        ActBossId.CanopyReaper => Color.FromArgb(161, 194, 133),
        ActBossId.CinderKing => Color.FromArgb(246, 153, 83),
        _ => Color.FromArgb(211, 205, 165)
    };
    private static float Snap(float value) => MathF.Round(value / 2) * 2;
    private static float Ease(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    internal static void DrawBody(Graphics g, ActBossId id, float x, float y,
        ActEnemyState state, float t, BossMove move, int hp, int maxHp, Brush ink, Brush paper)
    {
        if (id == ActBossId.Tyrant)
        {
            PixelSprites.DrawBossEnemy(g, x, y, state, t, move, hp, maxHp, ink, paper);
            return;
        }
        var save = g.Save();
        g.TranslateTransform(MathF.Round(x), MathF.Round(y));
        var def = ActBossCatalog.Definitions[(int)id];
        float tempo = hp * 2 <= maxHp ? def.PhaseTwoTempo : 1;
        float wind = state == ActEnemyState.Windup ? Ease(t / (def.Windup * tempo)) : 0;
        float strike = state == ActEnemyState.Attack ? 1 : 0;
        float settle = state == ActEnemyState.Recover ? 1 - Ease(t / (def.Recovery * tempo)) : 0;
        float breath = MathF.Sin(t * 3) * 1.5f;
        float recoil = state == ActEnemyState.Hurt ? -6 * (1 - Ease(t / .22f)) : 0;
        using var accent = new SolidBrush(Accent(id));
        using var shade = new SolidBrush(id == ActBossId.CinderKing ? Color.FromArgb(73, 45, 40) :
            id == ActBossId.CanopyReaper ? Color.FromArgb(48, 67, 46) : Color.FromArgb(70, 65, 53));
        void B(Brush brush, float bx, float by, float w, float h) => g.FillRectangle(brush, Snap(bx), Snap(by), Math.Max(2, Snap(w)), Math.Max(2, Snap(h)));
        void P(Brush brush, params PointF[] points) => g.FillPolygon(brush, points.Select(p => new PointF(Snap(p.X), Snap(p.Y))).ToArray());
        void Limb(Brush brush, float ax, float ay, float bx, float by, float thickness)
        {
            int n = Math.Max(1, (int)(Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by)) / 2));
            for (int i = 0; i <= n; i++) B(brush, ax + (bx - ax) * i / n - thickness / 2,
                ay + (by - ay) * i / n - thickness / 2, thickness, thickness);
        }

        if (id == ActBossId.Stoneback)
        {
            bool rolling = state == ActEnemyState.Attack && move == BossMove.Roll;
            float tuck = move == BossMove.Roll ? wind * 9 + strike * 10 + settle * 8 : 0;
            float slam = move == BossMove.Rockfall ? -wind * 7 + strike * 4 + settle * 3 : 0;
            float body = Snap(breath + tuck + slam);
            if (rolling)
            {
                // Armoured wheel: rotating plates, compressed legs and a central eye.
                var rotation = g.Save();
                g.TranslateTransform(63, 57); g.RotateTransform(t * 720);
                P(ink, new(-47,-18),new(-30,-42),new(14,-44),new(44,-21),new(46,18),new(20,42),new(-22,42),new(-47,17));
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4;
                    float cx = MathF.Cos(a) * 31, cy = MathF.Sin(a) * 31;
                    P(shade, new(cx-10,cy-8),new(cx+6,cy-12),new(cx+12,cy+7),new(cx-7,cy+10));
                    Limb(accent, cx-6,cy-8,cx+6,cy-7,2);
                }
                B(accent,-8,-8,16,16); B(ink,-2,-4,4,8);
                g.Restore(rotation);
            }
            else
            {
                // Low quadruped silhouette with articulated tail club and layered plates.
                float club = move == BossMove.Club ? wind * -1.2f : -.15f;
                if (move == BossMove.Club && state == ActEnemyState.Attack) club = -1.2f + Ease(t / .3f) * 3.5f;
                if (move == BossMove.Club && state == ActEnemyState.Recover) club = -.15f + settle * 2.45f;
                float tailX = 15 - MathF.Cos(club) * 57, tailY = 63 + body + MathF.Sin(club) * 26;
                if(move==BossMove.Club && state==ActEnemyState.Attack)
                { tailX=-42+280*Ease(t/.28f); tailY=68+MathF.Sin(Math.Clamp(t/.28f,0,1)*MathF.PI)*13; }
                if(move==BossMove.Club && state==ActEnemyState.Recover)
                { tailX=-42+280*settle; tailY=63+5*settle; }
                Limb(shade,18,60+body,tailX,tailY,12);
                P(ink,new(tailX-17,tailY-10),new(tailX+9,tailY-14),new(tailX+18,tailY+2),new(tailX+5,tailY+16),new(tailX-18,tailY+10));
                B(accent,tailX-10,tailY-7,14,4);
                for(int leg=0;leg<4;leg++)
                {
                    float lx=17+leg*23, lift=state==ActEnemyState.Chase ? Math.Max(0,MathF.Sin(t*7+leg*MathF.PI))*4 : 0;
                    B(leg%2==0?shade:ink,lx,70+body,14,26-body-lift); B(ink,lx-2,96-lift,20,4);
                    B(accent,lx+10,96-lift,4,2);
                }
                P(ink,new(3,60+body),new(13,30+body),new(34,14+body),new(72,12+body),new(96,33+body),new(108,73+body),new(22,81+body));
                for(int row=0;row<3;row++) for(int col=0;col<4;col++)
                {
                    float px=18+col*19+row*2, py=28+row*14+body;
                    P(shade,new(px,py),new(px+8,py-9),new(px+18,py),new(px+15,py+10),new(px+3,py+10));
                    Limb(accent,px+2,py,px+8,py-7,2);
                }
                float hx = recoil + (move==BossMove.Roll ? -wind*10 : 0);
                B(ink,91+hx,46+body,31,27); B(ink,112+hx,57+body,14,12);
                P(accent,new(100+hx,47+body),new(101+hx,32+body),new(110+hx,48+body));
                B(paper,112+hx,52+body,5,4); B(accent,114+hx,68+body,12,2);
            }
        }
        else if (id == ActBossId.CanopyReaper)
        {
            float crouch = move == BossMove.Pounce ? wind*12 + strike*5 + settle*9 : 0;
            float body = Snap(breath+crouch);
            float fan = move == BossMove.Feathers ? wind*.7f + strike : .18f;
            float slash = move == BossMove.RootBurst ? wind*.9f + strike*MathF.Sin(Math.Min(1,t/.35f)*MathF.PI) : 0;
            // Feather train, crown and long, hooked forearms distinguish this from the tyrant.
            for(int i=0;i<6;i++)
            {
                float fy=45+body+i*4, sway=MathF.Sin(t*3-i*.5f)*3;
                P(i%2==0?shade:ink,new(30,fy),new(-38-i*4,18+i*7+sway),new(-25-i*4,28+i*7+sway),new(32,fy+7));
                Limb(accent,8,fy,-27-i*4,25+i*7+sway,2);
            }
            for(int leg=0;leg<2;leg++)
            {
                float lift = state==ActEnemyState.Attack && move==BossMove.Pounce ? 15 :
                    state==ActEnemyState.Chase ? Math.Max(0,MathF.Sin(t*9+leg*MathF.PI))*7 : 0;
                float lx=32+leg*29;
                Limb(shade,lx,54+body,lx-6,77+body/2,11);
                Limb(ink,lx-6,77+body/2,lx+7,96-lift,7); B(ink,lx+3,96-lift,24,4);
                P(paper,new(lx+19,96-lift),new(lx+28,91-lift),new(lx+27,98-lift));
            }
            P(ink,new(17,34+body),new(42,20+body),new(72,29+body),new(77,62+body),new(38,72+body),new(20,60+body));
            for(int i=0;i<4;i++) P(shade,new(29+i*9,32+body),new(34+i*9,27+body),new(43+i*9,59+body),new(32+i*9,54+body));
            float hx = recoil + (move==BossMove.Pounce ? wind*5 : 0);
            B(ink,62+hx,16+body,19,31); B(ink,73+hx,7+body,27,24);
            P(ink,new(95+hx,15+body),new(122+hx,25+body),new(116+hx,32+body),new(96+hx,30+body));
            for(int i=0;i<5;i++) P(i%2==0?accent:shade,new(78+i*4+hx,12+body),new(50+i*7+hx,-21+i*2+body),new(85+i*4+hx,11+body));
            B(paper,89+hx,15+body,7,4); B(ink,93+hx,15+body,2,2);
            Limb(accent,103+hx,29+body,118+hx,28+body,2);
            for(int arm=0;arm<2;arm++)
            {
                float ax=53+arm*15, ay=36+body, ex=ax+19+fan*18, ey=ay+18-fan*36-slash*33;
                Limb(arm==0?shade:ink,ax,ay,ex,ey,9);
                float handX=ex+14+slash*12, handY=Math.Min(76,ey+17-slash*12);
                Limb(ink,ex,ey,handX,handY,6);
                for(int claw=0;claw<3;claw++)
                    P(paper,new(handX+claw*5,handY),new(handX+claw*5+3,handY+17),new(handX+claw*5+10,handY+21),new(handX+claw*5+6,handY+7));
                for(int feather=0;feather<4;feather++)
                    P(shade,new(ax+feather*5,ay),new(ax-7+feather*5-fan*13,ay+16+feather*4),new(ax+8+feather*5,ay+7));
            }
        }
        else
        {
            float rear = move==BossMove.Fissure ? -wind*9 + strike*4 + settle*4 : 0;
            float body=Snap(breath+rear+(move==BossMove.MeteorLeap?wind*8+settle*7:0));
            float breathOpen=move==BossMove.FlameFan ? wind*6+strike*10+settle*5 : 2;
            // Crocodilian jaws, a serrated sail and incandescent cracks.
            for(int i=0;i<7;i++) B(ink,-4-i*9,55+body+i*2+MathF.Sin(t*3-i*.3f)*2,16,Math.Max(4,17-i*2));
            for(int i=0;i<6;i++) P(shade,new(20+i*11,33+body),new(22+i*11,-14-MathF.Sin(i*.55f)*17+body),new(35+i*11,32+body));
            for(int i=0;i<5;i++) Limb(accent,27+i*11,25+body,27+i*11,-11-MathF.Sin(i*.55f)*14+body,2);
            P(ink,new(6,40+body),new(25,26+body),new(71,28+body),new(87,51+body),new(70,75+body),new(23,75+body));
            for(int i=0;i<4;i++)
            {
                float cx=26+i*12;
                Limb(accent,cx,37+body,cx+6,46+body,2); Limb(accent,cx+6,46+body,cx+2,58+body,2);
                B(shade,cx-3,60+body,10,6);
            }
            for(int leg=0;leg<2;leg++)
            {
                float lx=23+leg*37, lift=state==ActEnemyState.Attack && move==BossMove.MeteorLeap ? 13 :
                    state==ActEnemyState.Chase ? Math.Max(0,MathF.Sin(t*6+leg*MathF.PI))*5:0;
                Limb(shade,lx,64+body,lx+5,81,17); Limb(ink,lx+5,81,lx+3,95-lift,12);
                B(ink,lx-1,95-lift,27,5); for(int toe=0;toe<3;toe++) B(accent,lx+12+toe*5,96-lift,3,2);
            }
            float hx=recoil+(state==ActEnemyState.Attack && move==BossMove.FlameFan?4:0);
            B(ink,68+hx,23+body,24,30); B(ink,81+hx,13+body,28,23);
            B(ink,104+hx,22+body,23,13); B(ink,90+hx,38+body+breathOpen,36,6);
            P(accent,new(83+hx,15+body),new(78+hx,-1+body),new(93+hx,12+body));
            B(accent,95+hx,19+body,6,4);
            B(shade,103+hx,34+body,21,Math.Max(2,breathOpen+4));
            for(int tooth=0;tooth<5;tooth++)
            { B(paper,103+hx+tooth*5,34+body,2,4); B(accent,102+hx+tooth*5,36+body+breathOpen,2,3); }
            Limb(ink,72,50+body,88,58+body,8); Limb(ink,88,58+body,91,72+body,5);
            if(state==ActEnemyState.Attack && move==BossMove.FlameFan)
                for(int i=0;i<5;i++)
                    P(i%2==0?accent:paper,new(125+i*4,34+body),new(137+i*8,26+body+i*3),new(135+i*4,41+body));
        }
        // Hurt/death flashes are supplied by the caller. Phase two adds a living
        // pulse to the signature colour rather than replacing the silhouette.
        if(hp>0 && hp*2<=maxHp)
        {
            using var glow=new SolidBrush(Color.FromArgb(80+(int)(MathF.Sin(t*8)*35),Accent(id)));
            B(glow,58,40,8,8);
        }
        g.Restore(save);
    }

    internal static void DrawIntent(Graphics g, ActEnemy boss)
    {
        bool wind=boss.State==ActEnemyState.Windup;
        bool leap=ActBossCatalog.IsLeap(boss.BossMove);
        if(!wind && !(leap && boss.State==ActEnemyState.Attack)) return;
        Color color=Accent(boss.BossId);
        using var pen=new Pen(color,2);
        using var fill=new SolidBrush(Color.FromArgb(48,color));
        float cx=boss.X+boss.Width/2, feet=boss.Y+boss.Height;
        if(leap)
        {
            float target=boss.TargetX+boss.Width/2, floor=boss.TargetY+boss.Height;
            g.FillEllipse(fill,target-70,floor-9,140,18);g.DrawEllipse(pen,target-70,floor-9,140,18);
            g.DrawLine(pen,target-9,floor-20,target+9,floor-20);
            g.DrawLine(pen,target,floor-29,target,floor-11);
        }
        else if(boss.BossMove==BossMove.Club)
        {
            var r=ActEngine.ClubBounds(boss);g.FillRectangle(fill,r);g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
        }
        else if(boss.BossMove==BossMove.Roll)
        {
            float ahead=cx+boss.Facing*240;
            g.DrawLine(pen,cx,feet-8,ahead,feet-8);
            g.DrawLines(pen,new PointF[]{new(ahead-boss.Facing*12,feet-16),new(ahead,feet-8),new(ahead-boss.Facing*12,feet)});
        }
        else if(boss.BossMove is BossMove.Feathers or BossMove.FlameFan)
        {
            float mx=cx+boss.Facing*52,my=boss.Y+32;
            float a=MathF.Atan2(boss.BossAimY-my,boss.TargetX+boss.Width/2-mx);
            float spread=boss.BossMove==BossMove.Feathers?.48f:.2f;
            pen.DashStyle=DashStyle.Dash;
            foreach(float offset in new[]{-spread,0,spread}) g.DrawLine(pen,mx,my,mx+MathF.Cos(a+offset)*190,my+MathF.Sin(a+offset)*190);
        }
        if(wind)
        {
            float k=Math.Clamp(boss.StateTime/(ActBossCatalog.Get(boss).Windup*ActBossCatalog.Tempo(boss)),0,1);
            using var bar=new SolidBrush(color);
            g.FillRectangle(Brushes.Black,cx-18,boss.Y-19,36,5);g.FillRectangle(bar,cx-18,boss.Y-19,36*k,5);
            g.DrawLines(pen,new PointF[]{new(cx-5,boss.Y-32),new(cx,boss.Y-26),new(cx+5,boss.Y-32)});
        }
    }

    internal static void DrawHazards(Graphics g, ActEngine game)
    {
        foreach(var h in game.BossHazards)
        {
            var r=h.Bounds;var color=Accent(h.Source.BossId);
            using var bright=new SolidBrush(color);using var soft=new SolidBrush(Color.FromArgb(h.Active?150:55,color));
            using var pen=new Pen(color,2);
            if(!h.Active)
            {
                g.FillRectangle(soft,r.Left,r.Bottom-5,r.Width,5);
                g.DrawRectangle(pen,r.Left,r.Bottom-8,r.Width,8);
                g.FillRectangle(bright,r.Left,r.Bottom-3,r.Width*h.Progress,3);
                if(h.Kind==BossHazardKind.FallingRock)
                {
                    pen.DashStyle=DashStyle.Dash;g.DrawLine(pen,r.Left+r.Width/2,r.Top,r.Left+r.Width/2,r.Bottom-12);
                    g.DrawLines(pen,new PointF[]{new(r.Left+r.Width/2-5,r.Top+8),new(r.Left+r.Width/2,r.Top+14),new(r.Left+r.Width/2+5,r.Top+8)});
                }
                continue;
            }
            float p=Math.Clamp((h.Time-h.Warning)/h.Duration,0,1);
            if(h.Kind==BossHazardKind.FallingRock)
            {
                float cy=r.Top+Math.Min(1,p*1.6f)*(r.Height-24);
                g.FillRectangle(soft,r.Left+12,r.Top,r.Width-24,cy-r.Top+8);
                g.FillPolygon(Brushes.Black,new PointF[]{new(r.Left+5,cy),new(r.Right-9,cy-5),new(r.Right,cy+15),new(r.Right-10,cy+26),new(r.Left,cy+20)});
                g.DrawLine(pen,r.Left+7,cy+3,r.Right-10,cy);
            }
            else
            {
                float height=r.Height*Math.Min(1,p*6);
                for(int i=0;i<5;i++)
                {
                    float x=r.Left+i*r.Width/5;
                    g.FillPolygon(i%2==0?soft:bright,new PointF[]{new(x,r.Bottom),new(x+4,r.Bottom-height*(.65f+i%2*.35f)),new(x+r.Width/5,r.Bottom)});
                }
            }
        }
    }

    internal static void DrawProjectile(Graphics g, EnemyShot shot)
    {
        var saved=g.Save();g.TranslateTransform(shot.X+6,shot.Y+4);
        g.RotateTransform(MathF.Atan2(shot.Vy,shot.Vx)*180/MathF.PI);
        bool feather=shot.Source!.BossId==ActBossId.CanopyReaper;
        using var brush=new SolidBrush(Accent(shot.Source.BossId));
        g.FillPolygon(Brushes.Black,new PointF[]{new(-10,-4),new(6,-4),new(10,0),new(4,5),new(-10,4)});
        if(feather)
        {
            g.FillPolygon(brush,new PointF[]{new(-9,-2),new(5,-2),new(9,0),new(-9,2)});
            for(int i=0;i<3;i++)g.FillRectangle(Brushes.Black,-8+i*4,-4,2,4);
        }
        else
        {g.FillRectangle(brush,-6,-3,13,6);g.FillRectangle(Brushes.LightYellow,0,-2,5,4);g.FillRectangle(brush,-13,-1,5,2);}
        g.Restore(saved);
    }
}
