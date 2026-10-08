namespace DinoDash.Desktop;

// Region discovery is presentation state only; room types and encounter
// boundaries stay internal to the level generator.
internal sealed class ActRegionPresentation : IDisposable
{
    private readonly HashSet<int> discovered = [];
    private readonly Font titleFont = new("Microsoft YaHei UI", 21f,
        FontStyle.Bold, GraphicsUnit.Pixel);
    private float elapsed = 4f;
    private int currentRegion = -1;
    private IReadOnlyList<ActZone> zones = ActLevel.Zones;
    public string Title { get; private set; } = "";
    public float Opacity => Math.Clamp(Math.Min(elapsed / .4f, (3.6f - elapsed) / .9f), 0f, 1f);

    public void Update(float playerX, float dt)
    {
        int region = 0;
        for (int i = 1; i < zones.Count; i++)
            if (playerX >= zones[i].X) region = i;
        if (currentRegion != region)
        {
            currentRegion = region;
            if (discovered.Add(region))
            {
                Title = zones[region].Name;
                elapsed = 0f;
            }
            else elapsed = 4f;
        }
        elapsed += Math.Max(0, dt);
    }

    public void Reset()
    {
        discovered.Clear();
        currentRegion = -1;
        Title = "";
        elapsed = 4f;
    }

    public void SetZones(IReadOnlyList<ActZone> value)
    {
        zones = value;
        Reset();
    }

    public void Draw(Graphics g, Size size, int dpi)
    {
        if (Opacity <= 0f) return;
        float scale = ActHudRenderer.UiScale(size, dpi);
        var saved = g.Save();
        g.ScaleTransform(scale, scale);
        float center = size.Width / scale / 2f;
        int alpha = (int)(Opacity * 255);
        g.FillRectangle(ActVisualTheme.Fade(ActHudRenderer.Panel, (int)(Opacity * 200)),
            center - 98, 23, 196, 57);
        using var format = new StringFormat { Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center };
        g.DrawString(Title, titleFont, ActVisualTheme.Fade(ActVisualTheme.Paper, alpha),
            new RectangleF(center - 90, 28, 180, 37), format);
        g.FillRectangle(ActVisualTheme.Fade(ActHudRenderer.Gold, alpha), center - 27, 70, 54, 1);
        g.Restore(saved);
    }

    public void Dispose() => titleFont.Dispose();
}
