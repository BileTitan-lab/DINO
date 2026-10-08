using System.Drawing;

namespace DinoDash.Desktop;

internal sealed class ActParticle
{
    public float X, Y, Vx, Vy, Life;
    public float Size;
}

internal sealed partial class ActEngine
{
    private readonly List<ActParticle> particles = [];
    private int impactSerial;
    public IReadOnlyList<ActParticle> Particles => particles;
    public float HitStopRemaining { get; private set; }
    public float ShakeRemaining { get; private set; }

    private void ResetFeedback()
    {
        particles.Clear();
        HitStopRemaining = ShakeRemaining = 0f;
        impactSerial = 0;
        ClashSerial = 0;
        ClashX = ClashY = 0f;
    }

    private void UpdateFeedback(float dt)
    {
        HitStopRemaining = Math.Max(0f, HitStopRemaining - dt);
        ShakeRemaining = Math.Max(0f, ShakeRemaining - dt);
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            ActParticle p = particles[i];
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Vy += 380f * dt;
            p.Life -= dt;
            if (p.Life <= 0f) particles.RemoveAt(i);
        }
    }

    private void AddImpact(float x, float y, bool heavy, bool death = false)
    {
        HitStopRemaining = Math.Max(HitStopRemaining,
            death ? .12f : heavy ? ActTuning.HeavyHitStop : ActTuning.LightHitStop);
        ShakeRemaining = Math.Max(ShakeRemaining, death ? .32f : heavy ? .2f : .08f);
        int count = death ? 16 : heavy ? 10 : 5;
        for (int i = 0; i < count; i++)
        {
            float angle = (i * 2.39996f + impactSerial) % (2f * MathF.PI);
            float speed = (heavy ? 95f : 65f) + (i % 3) * 24f;
            particles.Add(new ActParticle { X = x, Y = y,
                Vx = MathF.Cos(angle) * speed,
                Vy = MathF.Sin(angle) * speed - 45f,
                Life = death ? .55f : .25f + i % 3 * .05f,
                Size = heavy ? 4f : 3f });
        }
        impactSerial++;
    }

}
