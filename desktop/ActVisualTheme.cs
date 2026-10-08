using System.Drawing;
using System.Drawing.Drawing2D;

namespace DinoDash.Desktop;

// Palette and deterministic noise shared by every decorative layer.
// Gameplay code never reads anything in this file.
//
// Readability contract: Ink is reserved for things the player can touch --
// solids, hazards, enemies, projectiles, the dinosaur. Every lighter tone
// belongs to scenery or effects that cannot be collided with, so a glance at
// the value alone tells the player whether a shape matters.
internal static class ActVisualTheme
{
    public static readonly Color Paper = Color.FromArgb(243, 240, 231);
    public static readonly Color Ink = Color.FromArgb(16, 15, 19);

    public static readonly Color SkyHigh = Color.FromArgb(206, 218, 230);
    public static readonly Color SkyLow = Color.FromArgb(247, 238, 222);
    public static readonly Color Ridge = Color.FromArgb(199, 206, 214);
    public static readonly Color RidgeNear = Color.FromArgb(182, 189, 198);
    public static readonly Color Ruin = Color.FromArgb(166, 173, 182);
    public static readonly Color Grove = Color.FromArgb(128, 136, 147);
    public static readonly Color Scrub = Color.FromArgb(88, 95, 106);
    public static readonly Color Decor = Color.FromArgb(118, 123, 133);
    public static readonly Color DecorDeep = Color.FromArgb(74, 79, 89);
    public static readonly Color Fore = Color.FromArgb(40, 43, 51);

    public static readonly SolidBrush PaperBrush = new(Paper);
    public static readonly SolidBrush InkBrush = new(Ink);
    public static readonly SolidBrush RidgeBrush = new(Ridge);
    public static readonly SolidBrush RidgeNearBrush = new(RidgeNear);
    public static readonly SolidBrush RuinBrush = new(Ruin);
    public static readonly SolidBrush GroveBrush = new(Grove);
    public static readonly SolidBrush ScrubBrush = new(Scrub);
    public static readonly SolidBrush DecorBrush = new(Decor);
    public static readonly SolidBrush DecorDeepBrush = new(DecorDeep);
    public static readonly SolidBrush ForeBrush = new(Fore);
    public static readonly SolidBrush GroundBrush = new(Color.FromArgb(22, 21, 26));
    public static readonly SolidBrush GroundFaceBrush = new(Color.FromArgb(38, 38, 46));
    public static readonly SolidBrush GroundGritBrush = new(Color.FromArgb(58, 59, 69));
    public static readonly Pen InkPen = new(Ink, 1f);
    public static readonly Pen DecorPen = new(Decor, 1f);

    // Alpha variants are requested constantly by the effect layers, so they are
    // memoised instead of allocated per draw call.
    private static readonly Dictionary<int, SolidBrush> FadedBrushes = [];
    private static readonly Dictionary<long, Pen> FadedPens = [];

    public static SolidBrush Fade(Color color, int alpha)
    {
        alpha = Math.Clamp(alpha, 0, 255);
        int key = (alpha << 24) | (color.ToArgb() & 0xFFFFFF);
        if (!FadedBrushes.TryGetValue(key, out SolidBrush? brush))
            FadedBrushes[key] = brush = new SolidBrush(Color.FromArgb(alpha, color));
        return brush;
    }

    public static Pen FadePen(Color color, int alpha, float width)
    {
        alpha = Math.Clamp(alpha, 0, 255);
        long key = ((long)(int)(width * 4f) << 32) | (uint)((alpha << 24) | (color.ToArgb() & 0xFFFFFF));
        if (!FadedPens.TryGetValue(key, out Pen? pen))
            FadedPens[key] = pen = new Pen(Color.FromArgb(alpha, color), width);
        return pen;
    }

    public static Color Lerp(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }

    // Stable value noise: the same cell always produces the same scenery, so
    // decoration never shimmers as the camera scrolls back and forth.
    public static float Hash01(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393 + b * 668265263 + 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    public static int HashInt(int a, int b, int range) =>
        range <= 1 ? 0 : (int)(Hash01(a, b) * range) % range;

    public static Brush SkyBrush(float progress)
    {
        Color high = Lerp(SkyHigh, Color.FromArgb(196, 202, 220), progress);
        Color low = Lerp(SkyLow, Color.FromArgb(240, 224, 210), progress);
        return new LinearGradientBrush(
            new RectangleF(0f, -4f, ActTuning.ViewWidth, ActTuning.ViewHeight + 8f),
            high, low, LinearGradientMode.Vertical);
    }
}
