using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DinoDash.Desktop;

internal sealed class ActMainMenu : Control
{
    internal readonly Button Adventure = new TitleButton("开始冒险");
    internal readonly Button Settings = new TitleButton("设置");
    internal readonly Button ExitGame = new TitleButton("退出游戏");
    private Bitmap? destination;
    private Bitmap? sourceScene;
    private float age;
    private float fade = -1;
    internal bool Transitioning => fade >= 0;

    public ActMainMenu()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Dock = DockStyle.Fill;
        ImeMode = ImeMode.Disable;
        BackColor = Color.FromArgb(18, 28, 26);
        Controls.AddRange([Adventure, Settings, ExitGame]);
        Adventure.BackColor=ActUiTheme.Gold;
        Adventure.TabIndex = 0; Settings.TabIndex = 1; ExitGame.TabIndex = 2;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        float s = Math.Min(Width / 1280f, Height / 720f);
        int h = Math.Max(38, (int)(57 * s));
        int y = (int)(Height * .49f);
        foreach (Button button in new[] { Adventure, Settings, ExitGame })
        {
            button.SetBounds((int)(Width * .105f), y,
                Math.Max(210, (int)(Width * .265f)), h);
            float pixels = Math.Max(15, 22 * s);
            if (Math.Abs(button.Font.Size - pixels) > .01f)
            {
                Font old = button.Font;
                button.Font = new Font("Microsoft YaHei UI", pixels,
                    FontStyle.Bold, GraphicsUnit.Pixel);
                old.Dispose();
            }
            y += h + Math.Max(9, (int)(14 * s));
        }
    }

    internal void StartTransition(Bitmap scene)
    {
        destination?.Dispose();
        destination = scene;
        fade = 0;
        foreach (Control child in Controls) child.Visible = false;
        Invalidate();
    }

    internal bool Advance(float dt)
    {
        age += dt;
        if (Transitioning) fade = Math.Min(1.2f, fade + dt);
        Invalidate();
        return fade >= 1.2f;
    }
    internal void StartSceneTransition(Bitmap source, Bitmap target)
    {
        sourceScene?.Dispose(); sourceScene = source;
        StartTransition(target);
        Visible = true; BringToFront();
    }

    internal void FinishTransition()
    {
        Visible = false;
        destination?.Dispose(); destination = null;
        sourceScene?.Dispose(); sourceScene = null;
        fade = -1;
    }

    internal void ShowMenu()
    {
        FinishTransition();foreach(Control child in Controls)child.Visible=true;
        Visible=true;BringToFront();Adventure.Focus();Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { destination?.Dispose(); sourceScene?.Dispose(); }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        if (Transitioning && fade < .5f && sourceScene != null)
        {
            g.DrawImage(sourceScene, ClientRectangle);
            using var veil = new SolidBrush(Color.FromArgb((int)(255*fade/.5f),Color.Black));
            g.FillRectangle(veil, ClientRectangle);
            return;
        }
        if (Transitioning && fade >= .5f && destination != null)
        {
            float reveal = Math.Clamp((fade - .5f) / .7f, 0, 1);
            // From black through a monochrome image, gradually restore colour.
            float c = reveal, d = 1 - c;
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix(new[] {
                new[] {c + .299f*d, .299f*d, .299f*d, 0, 0},
                new[] {.587f*d, c + .587f*d, .587f*d, 0, 0},
                new[] {.114f*d, .114f*d, c + .114f*d, 0, 0},
                new[] {0f, 0, 0, 1, 0}, new[] {0f, 0, 0, 0, 1}}));
            g.DrawImage(destination, ClientRectangle, 0, 0, destination.Width,
                destination.Height, GraphicsUnit.Pixel, attributes);
            using var veil = new SolidBrush(Color.FromArgb((int)(255 * (1-reveal)), Color.Black));
            g.FillRectangle(veil, ClientRectangle);
            return;
        }
        ActUiTheme.Landscape(g,ClientRectangle);
        var saved=g.Save();g.ScaleTransform(Width/1280f,Height/720f);
        using(var veil=new LinearGradientBrush(new Rectangle(0,0,1280,720),Color.FromArgb(150,ActUiTheme.Ink),Color.FromArgb(15,ActUiTheme.Ink),0f))g.FillRectangle(veil,0,0,1280,720);
        ActUiTheme.Frame(g,new RectangleF(103,329,399,273),alpha:205);
        ActUiTheme.Footer(g,new RectangleF(108,552,390,45));
        ActUiTheme.Text(g,"恐龙行动",new RectangleF(120,150,650,100),70,ActUiTheme.Paper,true);
        ActUiTheme.Text(g,"D I N O   /   P R I M A L   E X P E D I T I O N",new RectangleF(132,260,660,35),15,ActUiTheme.Muted);
        using(var line=new Pen(ActUiTheme.Gold,2))g.DrawLine(line,134,310,216,310);
        using(var rock=new SolidBrush(Color.FromArgb(23,31,40)))g.FillPolygon(rock,[new PointF(680,720),new(752,580),new(1050,574),new(1280,515),new(1280,720)]);
        var dinosaur=g.Save();g.TranslateTransform(826,365+MathF.Sin(age*1.4f)*2);g.ScaleTransform(4,4);
        using(var ink=new SolidBrush(Color.FromArgb(19,28,36)))using(var eye=new SolidBrush(ActUiTheme.Paper))
            PixelSprites.DrawDinoTinted(g,0,0,0,false,false,ink,eye);
        g.Restore(dinosaur);
        using(var dust=new SolidBrush(Color.FromArgb(100,ActUiTheme.Paper)))for(int i=0;i<18;i++)g.FillRectangle(dust,570+i*73%680,180+(i*91+age*6)%450,2,2);
        g.Restore(saved);
        using var footerFont = new Font("Microsoft YaHei UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
        using var footerInk = new SolidBrush(Color.FromArgb(160,167,151));
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far };
        g.DrawString("开发阶段，不代表最后品质", footerFont, footerInk,
            new RectangleF(12, Height-40, Width-28, 24), right);
        if (Transitioning)
        {
            using var black = new SolidBrush(Color.FromArgb((int)(255*Math.Clamp(fade/.5f,0,1)),Color.Black));
            g.FillRectangle(black, ClientRectangle);
        }
    }

    private sealed class TitleButton:ActUiButton
    {
        public TitleButton(string text)
        {Text=text;TextAlign=ContentAlignment.MiddleLeft;Font=new Font("Microsoft YaHei UI",22,FontStyle.Bold,GraphicsUnit.Pixel);}
    }
}

