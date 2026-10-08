using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// Display-only meter. Values come from the engine; interpolation and pulses
// never feed back into health or energy.
internal sealed class ActHudMeter : Control
{
    private readonly string caption;
    private readonly bool health;
    private int current;
    private int maximum = 5;
    private int previous;
    private float transition;
    private float clock;
    private bool initialized;

    public ActHudMeter(string caption, bool health)
    {
        this.caption = caption;
        this.health = health;
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = false;
        BackColor = ActVisualTheme.Paper;
        ForeColor = ActVisualTheme.Ink;
        Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    public void SetValue(int value, int limit, bool snap = false)
    {
        limit = Math.Max(1, limit);
        value = Math.Clamp(value, 0, limit);
        if (!initialized || snap)
        {
            previous = current = value;
            transition = 0f;
            initialized = true;
        }
        else if (value != current)
        {
            previous = current;
            current = value;
            transition = .5f;
        }
        maximum = limit;
        AccessibleName = $"{caption} {current} / {maximum}";
        Invalidate();
    }

    public void Tick(float dt)
    {
        transition = Math.Max(0f, transition - dt);
        clock += dt;
        if (transition > 0f || (health && current == 1)) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        int pad = 10;
        int width = Math.Max(1, Width - pad * 2);
        TextRenderer.DrawText(g, caption, Font, new Rectangle(pad, 3, width, 21),
            ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, $"{current} / {maximum}", Font,
            new Rectangle(pad, 3, width, 21), ForeColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        float gap = 5f;
        float cellWidth = Math.Min(42f, Math.Max(4f,
            (width - gap * (maximum - 1)) / maximum));
        float top = 28f;
        float height = Math.Clamp(Height - top - 8f, 8f, 23f);
        float fade = transition / .5f;
        for (int i = 0; i < maximum; i++)
        {
            float x = pad + i * (cellWidth + gap);
            bool filled = i < current;
            g.DrawRectangle(ActVisualTheme.InkPen, x, top, cellWidth, height);
            if (filled)
            {
                g.FillRectangle(ActVisualTheme.InkBrush, x + 2f, top + 2f,
                    Math.Max(1f, cellWidth - 3f), Math.Max(1f, height - 3f));
                // A cut-out highlights one resource unit without relying on a font glyph.
                if (cellWidth > 15f && height > 12f)
                    g.FillRectangle(ActVisualTheme.PaperBrush,
                        x + cellWidth / 2f - 2f, top + 5f, 4f,
                        health ? 4f : height - 9f);
                if (i >= previous && transition > 0f)
                    g.FillRectangle(ActVisualTheme.Fade(Color.White, (int)(fade * 200f)),
                        x + 2f, top + 2f, Math.Max(1f, cellWidth - 3f), height - 3f);
            }
            else if (i < previous && transition > 0f)
                g.FillRectangle(ActVisualTheme.Fade(ActVisualTheme.Decor, (int)(fade * 200f)),
                    x + 2f, top + 2f, Math.Max(1f, cellWidth - 3f), height - 3f);
            else
                g.DrawLine(ActVisualTheme.DecorPen, x + 4f, top + height - 4f,
                    x + Math.Max(4f, cellWidth - 4f), top + 4f);
        }
        if (health && current == 1 && (int)(clock * 3f) % 2 == 0)
            g.DrawRectangle(ActVisualTheme.InkPen, pad - 3f, top - 3f,
                cellWidth + 6f, height + 6f);
    }
}
