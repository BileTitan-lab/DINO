using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DinoDash.Desktop;

// Shared presentation primitives. No gameplay state is changed by this layer.
internal static class ActUiTheme
{
    private static readonly Dictionary<(RewardEmblem,int),Bitmap> icons=[];
    public static readonly Color Ink=Color.FromArgb(29,35,43), Panel=Color.FromArgb(43,51,61);
    public static readonly Color Paper=Color.FromArgb(237,240,242), Muted=Color.FromArgb(165,176,190);
    public static readonly Color Gold=Color.FromArgb(236,197,127), Edge=Color.FromArgb(130,144,159);
    public static readonly Color Good=Color.FromArgb(146,213,150), Bad=Color.FromArgb(228,147,146);
    public static void Frame(Graphics g,RectangleF r,bool selected=false,int alpha=222)
    {
        if(r.Width<=0||r.Height<=0)return;
        using var path=new GraphicsPath();float c=4;
        path.AddPolygon([new PointF(r.X+c,r.Y),new(r.Right-c,r.Y),new(r.Right-c,r.Y+c),new(r.Right,r.Y+c),
            new(r.Right,r.Bottom-c),new(r.Right-c,r.Bottom-c),new(r.Right-c,r.Bottom),new(r.X+c,r.Bottom),
            new(r.X+c,r.Bottom-c),new(r.X,r.Bottom-c),new(r.X,r.Y+c),new(r.X+c,r.Y+c)]);
        using var fill=new LinearGradientBrush(r,Color.FromArgb(alpha,Panel),Color.FromArgb(alpha,Ink),90);
        g.FillPath(fill,path);
        using var line=new Pen(selected?Gold:Color.FromArgb(165,Edge),selected?2:1);g.DrawPath(line,path);
        using var shine=new Pen(selected?Gold:Color.FromArgb(185,Paper));
        g.DrawLine(shine,r.X+5,r.Y,r.X+17,r.Y);g.DrawLine(shine,r.Right-17,r.Bottom,r.Right-5,r.Bottom);
    }
    public static void Text(Graphics g,string text,RectangleF r,float size=16,Color? color=null,bool bold=false,bool center=false)
    {
        using var f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
        using var b=new SolidBrush(color??Paper);
        using var format=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter};
        g.DrawString(text,f,b,r,format);
    }
    public static void Landscape(Graphics g,RectangleF r,bool dark=false)
    {
        var saved=g.Save();g.SetClip(r,CombineMode.Intersect);g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/960,r.Height/540);
        using var sky=new LinearGradientBrush(new Rectangle(0,0,960,540),Color.FromArgb(122,138,155),Color.FromArgb(181,190,194),90);
        g.FillRectangle(sky,0,0,960,540);
        for(int layer=0;layer<3;layer++)
        {
            using var b=new SolidBrush(Color.FromArgb(106-layer*22,124-layer*23,144-layer*24));
            for(int i=-1;i<8;i++) {float x=i*190+layer*54;float y=110+layer*102+(i%3)*24;
                g.FillPolygon(b,[new PointF(x-110,540),new(x+55,y),new(x+145,y+115),new(x+190,y+86),new(x+360,540)]);}
        }
        using var cloud=new SolidBrush(Color.FromArgb(70,Paper));
        for(int i=0;i<5;i++){int x=90+i*205,y=47+i%2*55;g.FillRectangle(cloud,x,y,65,14);g.FillRectangle(cloud,x+17,y-17,30,25);}
        Footer(g,new RectangleF(0,392,960,148),2);
        if(dark){using var veil=new SolidBrush(Color.FromArgb(100,Ink));g.FillRectangle(veil,0,0,960,540);}
        g.Restore(saved);
    }
    public static void Footer(Graphics g,RectangleF r,int variant=0)
    {
        var s=g.Save();g.SetClip(r,CombineMode.Intersect);
        using var b=new SolidBrush(Color.FromArgb(175,14,22,30));
        float floor=r.Bottom-3;g.FillRectangle(b,r.X,floor,r.Width,3);
        for(int i=0;i<12;i++)
        {
            float x=r.X+8+i*r.Width/11,h=12+(i*17%34);
            g.FillRectangle(b,x,floor-h,4,h);g.FillRectangle(b,x-7,floor-h*.6f,8,4);
            g.FillRectangle(b,x-9,floor-h*.6f-10,3,12);g.FillRectangle(b,x+3,floor-h*.8f,9,3);g.FillRectangle(b,x+10,floor-h*.8f-7,3,10);
        }
        if(variant==1){float x=r.Right-128;g.FillPolygon(b,[new PointF(x-65,floor-42),new(x+12,floor-65),new(x+82,floor-42)]);g.FillRectangle(b,x-50,floor-42,5,42);g.FillRectangle(b,x+64,floor-42,5,42);g.FillRectangle(b,x-30,floor-19,72,5);}
        else if(variant==2){float x=r.X+r.Width*.5f;g.FillPolygon(b,[new PointF(x-30,floor),new(x-38,floor-20),new(x-22,floor-53),new(x,floor-69),new(x+26,floor-48),new(x+36,floor-17),new(x+26,floor)]);}
        FormIcon(g,new RectangleF(r.Right-135,floor-51,64,48),PrimalForm.Base,Color.FromArgb(190,12,19,27));
        g.Restore(s);
    }
    public static void Icon(Graphics g,RectangleF r,RewardEmblem emblem,Color? color=null)
    {
        // Reduce the existing emblem geometry to a single crisp pixel silhouette.
        Color c=color??Paper;
        var key=(emblem,c.ToArgb());
        if(!icons.TryGetValue(key,out var sprite))
        {
            sprite=new Bitmap(40,40);using(var p=Graphics.FromImage(sprite))ActRewardIcons.Draw(p,new RectangleF(1,1,38,38),emblem,Paper);
            for(int y=0;y<40;y++)for(int x=0;x<40;x++){var pixel=sprite.GetPixel(x,y);sprite.SetPixel(x,y,pixel.A>80&&pixel.GetBrightness()>.30f?c:Color.Transparent);}
            icons.Add(key,sprite);
        }
        var state=g.Save();g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(sprite,r);g.Restore(state);
    }
    public static void FormIcon(Graphics g,RectangleF r,PrimalForm form,Color? color=null)
    {
        var s=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/64,r.Height/48);g.SmoothingMode=SmoothingMode.None;
        using var body=new SolidBrush(color??Paper);
        PointF[] shape=form==PrimalForm.Ankylosaur?
            [new(2,28),new(16,24),new(22,16),new(29,18),new(34,12),new(42,18),new(47,15),new(53,23),new(61,24),new(63,31),new(52,33),new(50,41),new(44,41),new(43,34),new(28,34),new(23,42),new(17,42),new(18,33)]:
            [new(2,20),new(6,29),new(16,32),new(28,26),new(36,18),new(39,6),new(44,3),new(58,3),new(61,8),new(61,17),new(49,17),new(48,21),new(58,21),new(55,25),new(46,24),new(41,32),new(34,35),new(34,41),new(41,43),new(41,46),new(29,46),new(27,36),new(22,40),new(22,46),new(15,46),new(16,34),new(7,32),new(2,27)];
        g.FillPolygon(body,shape);
        if(form==PrimalForm.Pterosaur)g.FillPolygon(body,[new PointF(33,26),new(4,7),new(13,29),new(27,34),new(47,25),new(64,10)]);
        if(form==PrimalForm.Raptor)g.FillPolygon(body,[new PointF(26,30),new(0,24),new(17,35)]);
        if(form==PrimalForm.Tyrannosaur)g.FillRectangle(body,38,9,25,13);
        using var eye=new SolidBrush((color??Paper).GetBrightness()<.4f?Paper:Ink);g.FillRectangle(eye,form==PrimalForm.Ankylosaur?56:53,form==PrimalForm.Ankylosaur?25:8,3,3);g.Restore(s);
    }
}

internal class ActUiButton:Button
{
    private bool hover;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string KeyHint { get; set; }="";
    internal bool HasSymbol=>Text is "暂停" or "按键设置" or "设置" or "地图调试" or "开始冒险" or "继续游戏" or "重新开始" or "返回主菜单" or "退出游戏";
    public ActUiButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=ActUiTheme.Ink;ForeColor=ActUiTheme.Paper;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        bool active=Enabled&&(hover||Focused||BackColor==ActHudRenderer.Gold);
        e.Graphics.Clear(ActUiTheme.Ink);
        if(active){using var b=new LinearGradientBrush(ClientRectangle,ActUiTheme.Gold,Color.FromArgb(171,147,105),0f);e.Graphics.FillRectangle(b,ClientRectangle);}
        else ActUiTheme.Frame(e.Graphics,new RectangleF(1,1,Width-3,Height-3));
        var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
        flags|=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
        var ink=!Enabled?ActUiTheme.Muted:active?ActUiTheme.Ink:ActUiTheme.Paper;
        int left=HasSymbol?39:12,right=KeyHint.Length>0?47:12;
        if(HasSymbol)DrawSymbol(e.Graphics,new Rectangle(13,(Height-18)/2,18,18),ink);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(left,2,Math.Max(1,Width-left-right),Height-4),ink,flags);
        if(KeyHint.Length>0)
        {
            using var p=new Pen(ink);e.Graphics.DrawRectangle(p,Width-39,(Height-22)/2,32,22);
            using var f=new Font("Microsoft YaHei UI",10,FontStyle.Regular,GraphicsUnit.Pixel);
            TextRenderer.DrawText(e.Graphics,KeyHint,f,new Rectangle(Width-38,(Height-22)/2,31,22),ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
    }
    private void DrawSymbol(Graphics g,Rectangle r,Color c)
    {
        var s=g.Save();g.TranslateTransform(r.X,r.Y);using var b=new SolidBrush(c);using var p=new Pen(c,2);
        if(Text=="暂停"){g.FillRectangle(b,2,1,5,16);g.FillRectangle(b,11,1,5,16);}
        else if(Text is "按键设置" or "设置")
        {g.DrawEllipse(p,4,4,10,10);for(int i=0;i<8;i++){float a=i*MathF.PI/4;g.DrawLine(p,9+MathF.Cos(a)*6,9+MathF.Sin(a)*6,9+MathF.Cos(a)*9,9+MathF.Sin(a)*9);}}
        else if(Text=="地图调试"){g.DrawPolygon(p,[new Point(1,3),new(6,1),new(12,4),new(17,2),new(17,15),new(12,17),new(6,14),new(1,16)]);g.DrawLine(p,6,1,6,14);g.DrawLine(p,12,4,12,17);}
        else if(Text=="重新开始"){g.DrawArc(p,2,2,14,14,20,300);g.FillPolygon(b,[new Point(11,0),new(17,1),new(16,7)]);}
        else if(Text=="返回主菜单"){g.FillPolygon(b,[new Point(0,8),new(9,0),new(18,8)]);g.FillRectangle(b,3,7,12,11);}
        else if(Text=="退出游戏"){g.DrawLine(p,2,1,2,17);g.DrawLine(p,6,9,17,9);g.DrawLines(p,[new Point(12,4),new(17,9),new(12,14)]);}
        else g.FillPolygon(b,[new Point(3,1),new(16,9),new(3,17)]);
        g.Restore(s);
    }
}

internal class ActUiPanel:Panel
{
    public ActUiPanel(){DoubleBuffered=true;ResizeRedraw=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override void OnPaintBackground(PaintEventArgs e){base.OnPaintBackground(e);ActUiTheme.Frame(e.Graphics,new RectangleF(1,1,Width-3,Height-3));ActUiTheme.Footer(e.Graphics,new RectangleF(3,Height-66,Width-6,63));}
}

internal class ActUiDialog:Form
{
    private Bitmap? background;
    public ActUiDialog(){DoubleBuffered=true;BackColor=ActUiTheme.Panel;ForeColor=ActUiTheme.Paper;}
    protected override void OnControlAdded(ControlEventArgs e)
    {base.OnControlAdded(e);if(e.Control is Label label)label.BackColor=Color.Transparent;}
    protected override void OnLoad(EventArgs e)
    {
        if(Owner is { } owner && owner.ClientSize.Width>0 && owner.ClientSize.Height>0)
        {
            background=new Bitmap(owner.ClientSize.Width,owner.ClientSize.Height);
            owner.DrawToBitmap(background,new Rectangle(Point.Empty,background.Size));
        }
        base.OnLoad(e);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if(background is null)ActUiTheme.Landscape(e.Graphics,ClientRectangle,true);
        else e.Graphics.DrawImage(background,ClientRectangle,new Rectangle((background.Width-Width)/2,(background.Height-Height)/2,Width,Height),GraphicsUnit.Pixel);
        ActUiTheme.Frame(e.Graphics,new RectangleF(3,3,Width-7,Height-7));ActUiTheme.Footer(e.Graphics,new RectangleF(6,Height-78,Width-12,72));
    }
    protected override void Dispose(bool disposing){if(disposing)background?.Dispose();base.Dispose(disposing);}
}

