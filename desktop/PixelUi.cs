using System.ComponentModel;

namespace DinoDash.Desktop;

internal sealed class PixelPanel : Panel
{
    public PixelPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int BorderWidth { get; set; } = 3;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool BorderTop { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool BorderBottom { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool BorderLeft { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool BorderRight { get; set; } = true;

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        int width = BorderWidth;
        if (width <= 0) return;
        if (BorderTop) eventArgs.Graphics.FillRectangle(Brushes.Black, 0, 0, ClientSize.Width, width);
        if (BorderBottom) eventArgs.Graphics.FillRectangle(Brushes.Black, 0, ClientSize.Height - width, ClientSize.Width, width);
        if (BorderLeft) eventArgs.Graphics.FillRectangle(Brushes.Black, 0, 0, width, ClientSize.Height);
        if (BorderRight) eventArgs.Graphics.FillRectangle(Brushes.Black, ClientSize.Width - width, 0, width, ClientSize.Height);
    }
}

internal sealed class BrandMarkControl : Control
{
    public BrandMarkControl()
    {
        Size = new Size(34, 34);
        MinimumSize = MaximumSize = Size;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "恐龙冲刺标志";
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.FillRectangle(Brushes.Black, ClientRectangle);
        Point[] slash =
        {
            new(11, 11), new(27, 11), new(24, 22), new(8, 22)
        };
        eventArgs.Graphics.FillPolygon(Brushes.White, slash);
    }
}
