using System.Drawing;

namespace DinoDash.Desktop;

// Arm silhouettes for the posable dinosaur.
internal enum DinoArm { Rest, Cocked, Extended, Raised, Flail }

// Leg silhouettes. Run0-Run3 are the original four-frame stride.
internal enum DinoLegs { Run0, Run1, Run2, Run3, Tucked, Dangling, Crouched, Braced }

// One frame of the player dinosaur, described as offsets from the neutral
// standing silhouette. Everything snaps to the two-pixel grid when drawn, so
// every combination stays crisp pixel art.
internal struct DinoPose
{
    public float BodyDy;              // whole torso + head drop (positive = down)
    public float HeadDx, HeadDy;      // extra head offset on top of BodyDy
    public float TailSwing;           // -2..2, positive lifts the tail tip
    public DinoLegs Legs;
    public DinoArm Arm;
    public bool MouthOpen;
    public bool EyeClosed;
    public bool EyeHurt;
}

// All coordinates are world pixels on a two-pixel grid. Decorative tails and
// wing tips extend beyond the forgiving body collision boxes intentionally.
//
// Every sprite takes an ink and a paper brush so the FX layer can reuse the
// exact geometry for afterimages. Every enemy sprite is authored facing right
// inside its collision box; the caller mirrors it for Facing < 0.
internal static class PixelSprites
{
    private static float Snap(float value) => MathF.Round(value * .5f) * 2f;

    // ================================================================ player

    internal static void DrawDino(Graphics g, float x, float y, int frame,
        bool crouching, bool airborne) =>
        DrawDinoTinted(g, x, y, frame, crouching, airborne, Brushes.Black, Brushes.White);

    // Legacy entry point, kept for the compact sprite and for afterimages.
    internal static void DrawDinoTinted(Graphics g, float x, float y, int frame,
        bool crouching, bool airborne, Brush ink, Brush paper)
    {
        if (crouching)
        {
            DrawDinoCompact(g, x, y, frame, ink, paper);
            return;
        }
        DrawDinoPosed(g, x, y, new DinoPose
        {
            Legs = airborne ? DinoLegs.Tucked : (DinoLegs)(frame & 3)
        }, ink, paper);
    }

    // Low, forward-reaching dash / dive silhouette.
    internal static void DrawDinoCompact(Graphics g, float x, float y, int frame,
        Brush ink, Brush paper)
    {
        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        Block(0, 6, 4, 8);
        Block(4, 10, 6, 8);
        Block(8, 12, 8, 10);
        Block(14, 8, 26, 16);
        Block(20, 6, 18, 4);
        Block(10, 18, 30, 8);
        Block(36, 2, 18, 18);
        Block(40, 0, 14, 4);
        Block(54, 4, 4, 14);
        Block(38, 18, 14, 4);
        Cut(44, 4, 4, 4);
        Cut(48, 14, 10, 2);
        Cut(30, 18, 6, 2);
        Block(32, 20, 4, 4);
        if ((frame & 1) == 0)
        {
            Block(16, 24, 4, 6); Block(16, 28, 10, 2); Block(30, 24, 8, 2);
        }
        else
        {
            Block(14, 24, 8, 2); Block(28, 24, 4, 6); Block(28, 28, 10, 2);
        }
    }

    // The neutral pose reproduces the original silhouette exactly; every other
    // pose is that silhouette with its parts nudged along the pixel grid.
    internal static void DrawDinoPosed(Graphics g, float x, float y, DinoPose pose,
        Brush ink, Brush paper)
    {
        float body = Snap(pose.BodyDy);
        float hx = Snap(pose.HeadDx);
        float hy = body + Snap(pose.HeadDy);
        float swing = Snap(pose.TailSwing);

        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        // --- head: stepped snout, single eye, narrow mouth ---------------
        Block(28 + hx, 0 + hy, 20, 2);
        Block(24 + hx, 2 + hy, 26, 20);
        Block(48 + hx, 6 + hy, 4, 12);
        Block(26 + hx, 22 + hy, 18, 4);
        if (pose.MouthOpen)
        {
            // Gape cut into the snout; the lower lip stays attached so the
            // head never separates from the chin.
            Cut(36 + hx, 14 + hy, 16, 4);
            Cut(46 + hx, 18 + hy, 6, 2);
            Cut(40 + hx, 12 + hy, 2, 2); // fang
        }
        else
        {
            Cut(38 + hx, 18 + hy, 14, 2);
            Cut(46 + hx, 20 + hy, 4, 2);
        }
        if (pose.EyeHurt)
        {
            // Squeezed-shut eye: a horizontal slit with a crease above.
            Cut(30 + hx, 8 + hy, 8, 2);
            Cut(32 + hx, 4 + hy, 2, 2);
        }
        else if (pose.EyeClosed) Cut(32 + hx, 8 + hy, 4, 2);
        else Cut(32 + hx, 6 + hy, 4, 4);

        // --- neck and torso ---------------------------------------------
        // The neck always spans from under the jaw to the chest, so lifting
        // or lowering the head can never open a gap.
        float neckTop = 20f + MathF.Min(body, hy);
        Block(22 + hx * .5f, neckTop, 14, 34 + body - neckTop);
        Block(18, 26 + body, 16, 14);
        Block(14, 30 + body, 20, 14);
        Block(16, 42 + body, 16, 4);

        // --- tail: the tip travels furthest, the root barely moves -------
        Block(0, 22 + body - swing * 1.5f, 4, 8);
        Block(4, 26 + body - swing, 4, 8);
        Block(8, 30 + body - swing * .5f, 10, 8);
        Block(12, 34 + body, 8, 6);

        // --- arm ---------------------------------------------------------
        switch (pose.Arm)
        {
            case DinoArm.Cocked:
                // Drawn back and up, claws ready.
                Block(30, 26 + body, 8, 4);
                Block(34, 20 + body, 4, 8);
                Block(36, 18 + body, 4, 2);
                break;
            case DinoArm.Extended:
                // Thrown forward past the snout line.
                Block(32, 28 + body, 16, 4);
                Block(46, 26 + body, 4, 8);
                Cut(32, 32 + body, 2, 2);
                break;
            case DinoArm.Raised:
                // Uppercut follow-through.
                Block(32, 26 + body, 10, 4);
                Block(40, 18 + body, 4, 10);
                Block(42, 16 + body, 4, 2);
                break;
            case DinoArm.Flail:
                Block(32, 26 + body, 8, 4);
                Block(38, 20 + body, 4, 8);
                break;
            default:
                Block(32, 28 + body, 10, 4);
                Block(40, 30 + body, 2, 6);
                Cut(32, 34 + body, 2, 4);
                break;
        }

        // --- legs: feet stay pinned to the ground line on grounded poses -
        switch (pose.Legs)
        {
            case DinoLegs.Tucked:
                Block(16, 44 + body, 4, 4); Block(12, 46 + body, 8, 2);
                Block(28, 44 + body, 4, 4); Block(28, 46 + body, 8, 2);
                break;
            case DinoLegs.Dangling:
                Block(16, 44 + body, 4, 6); Block(14, 50 + body, 8, 2);
                Block(28, 44 + body, 4, 6); Block(28, 50 + body, 8, 2);
                break;
            case DinoLegs.Crouched:
                // Knees bent; body has dropped, feet stay on the ground.
                Block(14, 44 + body, 8, 50 - 44 - body);
                Block(12, 50, 12, 2);
                Block(28, 44 + body, 6, 50 - 44 - body);
                Block(28, 50, 12, 2);
                break;
            case DinoLegs.Braced:
                // Wide stance for heavy swings.
                Block(12, 44 + body, 4, 50 - 44 - body); Block(8, 50, 10, 2);
                Block(30, 44 + body, 4, 50 - 44 - body); Block(30, 50, 10, 2);
                break;
            case DinoLegs.Run1:
                Block(14, 44 + body, 6, 4); Block(10, 46 + body, 8, 4);
                Block(28, 44 + body, 4, 50 - 44 - body); Block(28, 50, 10, 2);
                break;
            case DinoLegs.Run2:
                Block(16, 44 + body, 4, 4); Block(12, 46 + body, 8, 2);
                Block(28, 44 + body, 4, 50 - 44 - body); Block(28, 50, 10, 2);
                break;
            case DinoLegs.Run3:
                Block(16, 44 + body, 4, 50 - 44 - body); Block(16, 50, 10, 2);
                Block(28, 44 + body, 6, 4); Block(32, 46 + body, 8, 4);
                break;
            default:
                Block(16, 44 + body, 4, 50 - 44 - body); Block(16, 50, 10, 2);
                Block(28, 44 + body, 4, 4); Block(30, 46 + body, 8, 2);
                break;
        }
    }

    // Collapse sequence for the player's death: 0 = knocked back,
    // 1 = knees buckling, 2 = on its side.
    internal static void DrawDinoDeath(Graphics g, float x, float y, int stage)
    {
        if (stage <= 0)
        {
            DrawDinoPosed(g, x, y, new DinoPose
            {
                HeadDx = -4, HeadDy = -2, TailSwing = 2,
                Legs = DinoLegs.Dangling, Arm = DinoArm.Flail,
                MouthOpen = true, EyeHurt = true
            }, Brushes.Black, Brushes.White);
            return;
        }
        if (stage == 1)
        {
            DrawDinoPosed(g, x, y, new DinoPose
            {
                BodyDy = 6, HeadDx = -2, HeadDy = 6, TailSwing = -2,
                Legs = DinoLegs.Crouched, Arm = DinoArm.Rest, EyeHurt = true
            }, Brushes.Black, Brushes.White);
            return;
        }
        // Lying on its side, head resting on the ground.
        g.FillRectangle(Brushes.Black, x + 2f, y + 32f, 48f, 18f);
        g.FillRectangle(Brushes.Black, x + 34f, y + 24f, 22f, 18f);
        g.FillRectangle(Brushes.Black, x + 54f, y + 28f, 4f, 10f);
        g.FillRectangle(Brushes.Black, x - 14f, y + 42f, 18f, 6f);
        g.FillRectangle(Brushes.Black, x - 22f, y + 46f, 10f, 4f);
        g.FillRectangle(Brushes.Black, x + 14f, y + 28f, 6f, 4f);
        g.FillRectangle(Brushes.Black, x + 26f, y + 26f, 6f, 6f);
        // Closed eye and slack jaw.
        g.FillRectangle(Brushes.White, x + 42f, y + 30f, 6f, 2f);
        g.FillRectangle(Brushes.White, x + 44f, y + 38f, 10f, 2f);
    }

    // ================================================================ enemies

    // Two-legged gait shared by the raptor and the spitter. Each leg runs the
    // same four-phase cycle (plant forward, push, plant back, swing) with the
    // other leg half a cycle behind, so the stride never pops.
    private static void BipedLegs(Graphics g, Brush ink, float x, float y,
        float hipY, float groundY, int frame, float backX, float frontX)
    {
        static (float dx, float lift) Phase(int p) => (p & 3) switch
        {
            0 => (3f, 0f), 1 => (0f, 0f), 2 => (-3f, 0f), _ => (0f, 4f)
        };
        foreach ((float legX, int phase) in new[] { (backX, frame + 2), (frontX, frame) })
        {
            (float dx, float lift) = Phase(phase);
            float foot = groundY - 2f - lift;
            g.FillRectangle(ink, x + legX + dx, y + hipY, 6f, MathF.Max(2f, foot - hipY));
            g.FillRectangle(ink, x + legX + dx - 1f, y + foot, 9f, 2f);
        }
    }

    // ------------------------------------------------------------ raptor
    // Collision box 36 x 42. Lean, forward-pitched hunter with a long tail.
    internal static void DrawMeleeEnemy(Graphics g, float x, float y,
        ActEnemyState state, float t, Brush ink, Brush paper)
    {
        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        float body = 0f, headX = 0f, headY = 0f, tail = 0f;
        bool jaw = false, armOut = false, squint = false, walking = false;
        int frame = 0;
        switch (state)
        {
            case ActEnemyState.Patrol:
            case ActEnemyState.Chase:
                walking = true;
                frame = (int)(t * (state == ActEnemyState.Chase ? 13f : 8f)) & 3;
                body = frame is 1 or 3 ? -2f : 0f;
                tail = frame < 2 ? 2f : -2f;
                break;
            case ActEnemyState.Windup:
            {
                // Coil: hips sink, head draws back, jaw parts late in the windup.
                float k = Math.Clamp(t / ActEnemyTuning.MeleeWindup, 0f, 1f);
                body = Snap(k * 4f);
                headX = -Snap(k * 4f);
                tail = Snap(k * 4f);
                jaw = k > .6f;
                break;
            }
            case ActEnemyState.Recover:
                if (t < .14f)
                {
                    // Strike: lunge through the snap.
                    headX = 6f; body = 2f; jaw = true; armOut = true; tail = -2f;
                }
                else
                {
                    float k = Math.Clamp((t - .14f) / .3f, 0f, 1f);
                    headX = Snap(4f * (1f - k)); body = Snap(2f * (1f - k));
                }
                break;
            case ActEnemyState.Hurt:
                headX = -4f; headY = -2f; tail = 4f; squint = true; jaw = true;
                break;
        }

        // Tail, tapering back and out of the box.
        Block(-2, 14 + body - tail * .5f, 10, 6);
        Block(-12, 16 + body - tail, 12, 4);
        Block(-22, 18 + body - tail * 1.5f, 12, 4);
        Block(-30, 20 + body - tail * 2f, 8, 2);
        // Torso, pitched forward.
        Block(4, 10 + body, 22, 16);
        Block(8, 8 + body, 18, 4);
        Block(10, 24 + body, 16, 6);
        // Neck and head.
        float hx = headX, hy = body + headY;
        Block(22 + hx * .5f, 4 + hy, 8, 10);
        Block(24 + hx, -4 + hy, 14, 10);
        Block(36 + hx, -2 + hy, 6, 6);
        if (squint) Cut(30 + hx, -1 + hy, 5, 2);
        else Cut(30 + hx, -2 + hy, 3, 3);
        if (jaw)
        {
            Block(28 + hx, 8 + hy, 12, 3);
            Cut(34 + hx, 4 + hy, 8, 2);
            Cut(30 + hx, 8 + hy, 2, 2); // teeth
            Cut(36 + hx, 8 + hy, 2, 2);
        }
        else Block(28 + hx, 6 + hy, 12, 2);
        // Sickle-clawed arm.
        if (armOut)
        {
            Block(24, 14 + body, 14, 3);
            Block(36, 12 + body, 3, 7);
        }
        else
        {
            Block(22, 16 + body, 6, 3);
            Block(26, 17 + body, 3, 6);
        }
        // Legs.
        if (walking)
            BipedLegs(g, ink, x, y, 28f + body, 42f, frame, 10f, 20f);
        else
        {
            float hip = 28f + body;
            Block(8, hip, 6, 40 - hip); Block(6, 40, 10, 2);
            Block(22, hip, 6, 40 - hip); Block(22, 40, 10, 2);
        }
    }

    // ------------------------------------------------------------ spitter
    // Collision box 36 x 42. Upright, dorsal frill that rises as it charges.
    internal static void DrawRangedEnemy(Graphics g, float x, float y,
        ActEnemyState state, float t, Brush ink, Brush paper)
    {
        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        float body = 0f, lean = 0f, frill = 0f;
        bool mouth = false, glow = false, squint = false, walking = false;
        int frame = 0;
        switch (state)
        {
            case ActEnemyState.Patrol:
                // Idle breathing.
                body = (int)(t * 2.4f) % 2 == 0 ? 0f : 2f;
                break;
            case ActEnemyState.Chase:
                walking = true;
                frame = (int)(t * 10f) & 3;
                body = frame is 1 or 3 ? -2f : 0f;
                break;
            case ActEnemyState.Windup:
            {
                float k = Math.Clamp(t / ActEnemyTuning.RangedWindup, 0f, 1f);
                frill = Snap(k * 6f);
                lean = -Snap(k * 3f);
                body = Snap(k * 2f);
                mouth = k > .35f;
                // Throat pulse quickens toward release.
                glow = (int)(t * (8f + k * 18f)) % 2 == 0;
                break;
            }
            case ActEnemyState.Recover:
                if (t < .12f) { lean = 4f; mouth = true; frill = 6f; }
                else frill = Snap(6f * Math.Clamp(1f - (t - .12f) / .5f, 0f, 1f));
                break;
            case ActEnemyState.Hurt:
                lean = -4f; squint = true; mouth = true;
                break;
        }

        // Stubby tail.
        Block(-4, 20 + body, 10, 6);
        Block(-14, 23 + body, 12, 4);
        Block(-22, 26 + body, 10, 2);
        // Dorsal frill: taller spines in the middle.
        for (int spine = 0; spine < 4; spine++)
        {
            float h = 4f + (spine is 1 or 2 ? 2f : 0f) + frill * (spine is 1 or 2 ? 1f : .6f);
            Block(6 + spine * 5, 6 + body - h, 3, h);
        }
        // Rounded body.
        Block(4, 6 + body, 22, 22);
        Block(2, 10 + body, 26, 14);
        Cut(8, 18 + body, 12, 6); // pale throat sac
        // Head.
        float hx = lean;
        Block(20 + hx, 0 + body, 14, 12);
        Block(30 + hx, 2 + body, 6, 8);
        if (squint) Cut(26 + hx, 3 + body, 5, 2);
        else Cut(27 + hx, 2 + body, 3, 3);
        if (mouth)
        {
            Cut(30 + hx, 7 + body, 6, 3);
            if (glow) Block(32 + hx, 8 + body, 2, 1);
        }
        // Throat glow while charging.
        if (glow) Block(12, 20 + body, 6, 2);
        // Forelimb.
        Block(24, 16 + body, 6, 3);
        Block(28, 17 + body, 3, 5);
        // Legs.
        if (walking)
            BipedLegs(g, ink, x, y, 28f + body, 42f, frame, 8f, 18f);
        else
        {
            float hip = 28f + body;
            Block(8, hip, 6, 40 - hip); Block(6, 40, 10, 2);
            Block(18, hip, 6, 40 - hip); Block(18, 40, 10, 2);
        }
    }

    // ------------------------------------------------------------ pterosaur
    // Collision box 44 x 28. Side view in the classic runner style: a single
    // membrane wing beats from the shoulder through up -> mid -> down -> mid,
    // so the silhouette stays readable at a glance.
    internal static void DrawFlyingEnemy(Graphics g, float x, float y,
        ActEnemyState state, float t, Brush ink, Brush paper)
    {
        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        int wing;       // 0 up, 1 mid-high, 2 down, 3 mid-low, 4 folded
        float pitch = 0f;
        bool talons = false, beakOpen = false, squint = false;
        switch (state)
        {
            case ActEnemyState.Windup:
                // Hover and aim: short rapid beats, head tipped toward the target.
                wing = (int)(t * 16f) % 2 == 0 ? 1 : 3;
                pitch = 2f;
                beakOpen = t > ActEnemyTuning.FlyingWindup * .5f;
                break;
            case ActEnemyState.Attack:
                wing = 4; pitch = 4f; talons = true; beakOpen = true;
                break;
            case ActEnemyState.Recover:
                wing = (int)(t * 15f) & 3; // labouring climb, faster beat
                break;
            case ActEnemyState.Hurt:
                wing = 0; squint = true; beakOpen = true; pitch = -2f;
                break;
            default:
                wing = (int)(t * 10f) & 3;
                break;
        }
        // The downstroke lifts the body; the upstroke lets it sink.
        // The torso is anchored to the 44 x 28 hurtbox, independently of the
        // decorative wing tips. Only the wings flap; the feet end at y + 28.
        float bob = 0f;
        float hp = bob + pitch;

        // Tail and body.
        Block(0, 14 + bob, 8, 2);
        Block(6, 10 + bob, 24, 12);
        Block(10, 8 + bob, 18, 2);
        Block(10, 22 + bob, 16, 2);
        // Neck, head and swept-back crest.
        Block(24, 10 + bob + pitch * .5f, 8, 6);
        Block(28, 4 + hp, 10, 10);
        Block(24, 4 + hp, 8, 2);
        Block(18, 2 + hp, 8, 2);
        // Long beak; the lower mandible drops when open.
        Block(38, 8 + hp, 6, 2);
        if (beakOpen) Block(38, 12 + hp, 6, 2);
        else Block(38, 10 + hp, 6, 2);
        if (squint) Cut(31, 7 + hp, 4, 1);
        else Cut(32, 6 + hp, 2, 2);
        // Legs: tucked in flight, talons forward in the dive.
        if (talons)
        {
            Block(14, 22 + bob, 2, 4); Block(14, 26 + bob, 6, 2);
            Block(20, 22 + bob, 2, 4); Block(20, 26 + bob, 6, 2);
        }
        else
        {
            Block(14, 24f, 2, 2); Block(20, 24f, 2, 2);
            Block(14, 26f, 5, 2); Block(20, 26f, 5, 2);
        }

        // Wing: stepped membrane, leading edge toward the head.
        switch (wing)
        {
            case 0: // full upstroke
                Block(12, 6 + bob, 14, 4);
                Block(10, 2 + bob, 12, 4);
                Block(8, -2 + bob, 10, 4);
                Block(6, -6 + bob, 8, 4);
                Block(6, -10 + bob, 4, 4);
                break;
            case 1: // passing high
                Block(12, 6 + bob, 14, 4);
                Block(6, 4 + bob, 12, 2);
                Block(2, 2 + bob, 8, 2);
                break;
            case 2: // full downstroke
                Block(12, 20 + bob, 14, 4);
                Block(10, 24 + bob, 12, 4);
                Block(8, 28 + bob, 10, 4);
                Block(6, 32 + bob, 8, 2);
                Block(6, 34 + bob, 4, 2);
                break;
            case 3: // passing low
                Block(12, 18 + bob, 14, 4);
                Block(6, 22 + bob, 12, 2);
                Block(2, 24 + bob, 8, 2);
                break;
            default: // folded back along the body for the dive
                Block(4, 8 + bob, 22, 4);
                Block(-8, 10 + bob, 14, 2);
                break;
        }
    }

    // ------------------------------------------------------------ ankylosaur
    // Collision box 68 x 54. Low, armoured, club-tailed charger.
    internal static void DrawHeavyEnemy(Graphics g, float x, float y,
        ActEnemyState state, float t, int health, int maxHealth,
        Brush ink, Brush paper)
    {
        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        float body = 0f, headX = 0f, headY = 0f, club = 0f;
        int legFrame = 0;
        bool scrape = false, dazed = false, squint = false, walking = false;
        switch (state)
        {
            case ActEnemyState.Chase:
                walking = true;
                legFrame = (int)(t * 6f) & 1;
                body = legFrame == 0 ? 0f : 2f;
                club = legFrame == 0 ? 2f : -2f;
                break;
            case ActEnemyState.Windup:
            {
                // Head drops, front foot paws the ground, tail cocks high.
                float k = Math.Clamp(t / ActEnemyTuning.HeavyWindup, 0f, 1f);
                body = Snap(k * 4f);
                headY = Snap(k * 6f);
                headX = -Snap(k * 2f);
                club = Snap(k * 8f);
                scrape = true;
                legFrame = (int)(t * 12f) & 1;
                break;
            }
            case ActEnemyState.Attack:
                walking = true;
                legFrame = (int)(t * 14f) & 1;
                headX = 4f; headY = 6f; body = 2f; club = -4f;
                break;
            case ActEnemyState.Recover:
                dazed = t < .9f;
                headY = 8f; club = -6f;
                body = (int)(t * 3f) % 2 == 0 ? 2f : 4f;
                break;
            case ActEnemyState.Hurt:
                headY = -4f; headX = -2f; squint = true; body = -2f;
                break;
            default:
                body = (int)(t * 1.6f) % 2 == 0 ? 0f : 2f;
                break;
        }

        // Club tail.
        Block(-4, 20 + body, 12, 10);
        Block(-14, 20 + body - club * .5f, 12, 8);
        Block(-24, 18 + body - club, 12, 8);
        Block(-36, 14 + body - club * 1.5f, 14, 14);
        Block(-38, 16 + body - club * 1.5f, 2, 10);
        Cut(-32, 18 + body - club * 1.5f, 4, 2);
        // Armoured shell, stepped dome.
        Block(8, 10 + body, 44, 6);
        Block(4, 14 + body, 52, 18);
        Block(2, 20 + body, 58, 10);
        // Osteoderm rows.
        for (int plate = 0; plate < 5; plate++)
        {
            Cut(8 + plate * 9, 16 + body, 5, 2);
            Block(10 + plate * 9, 6 + body, 4, 4);
        }
        for (int plate = 0; plate < 4; plate++)
            Cut(12 + plate * 10, 24 + body, 5, 2);
        // Cracked shell at half health or lower.
        if (maxHealth > 0 && health * 2 <= maxHealth)
        {
            Cut(28, 10 + body, 2, 6);
            Cut(30, 14 + body, 2, 4);
            Cut(26, 18 + body, 2, 4);
        }
        // Head: low, wide, beaked.
        float hx = headX, hy = body + headY;
        Block(52 + hx, 16 + hy, 12, 12);
        Block(60 + hx, 20 + hy, 8, 8);
        Block(66 + hx, 24 + hy, 4, 4);
        Block(54 + hx, 12 + hy, 6, 4); // brow horn
        if (squint) Cut(58 + hx, 20 + hy, 5, 2);
        else Cut(59 + hx, 19 + hy, 3, 3);
        // Four stumpy legs, feet pinned to the ground line (y + 54).
        float hip = 30f + body;
        for (int leg = 0; leg < 2; leg++)
        {
            bool lifted = walking && ((leg + legFrame) & 1) == 1;
            float lx = leg == 0 ? 8f : 40f;
            if (scrape && leg == 1) lx += legFrame == 0 ? 4f : -2f;
            float foot = 52f - (lifted ? 4f : 0f) - (scrape && leg == 1 && legFrame == 0 ? 2f : 0f);
            Block(lx, hip, 10, MathF.Max(2f, foot - hip));
            Block(lx - 2f, foot, 14, 2);
        }
        // Dazed: three dots circling above the head.
        if (dazed)
        {
            for (int star = 0; star < 3; star++)
            {
                float a = t * 7f + star * 2.094f;
                Block(56 + hx + MathF.Cos(a) * 9f, 2 + hy + MathF.Sin(a) * 3f, 2, 2);
            }
        }
    }

    // ------------------------------------------------------------ tyrant (boss)
    // Collision box 126 x 100. Every move has its own body language.
    internal static void DrawBossEnemy(Graphics g, float x, float y,
        ActEnemyState state, float t, BossMove move, int health, int maxHealth,
        Brush ink, Brush paper)
    {
        void Block(float dx, float dy, float w, float h) =>
            g.FillRectangle(ink, x + dx, y + dy, w, h);
        void Cut(float dx, float dy, float w, float h) =>
            g.FillRectangle(paper, x + dx, y + dy, w, h);

        bool phase2 = maxHealth > 0 && health * 2 <= maxHealth;
        float haste = phase2 ? .8f : 1f;
        float body = 0f, headX = 0f, headY = 0f, tail = 0f;
        bool jaw = false, armsUp = false, squint = false, stomp = false, walking = false;

        switch (state)
        {
            case ActEnemyState.Chase:
                // Hold a breathing stalking stance while reading the player.
                body = Snap(2f + MathF.Sin(t * 5f) * 2f);
                tail = Snap(MathF.Sin(t * 4f) * 3f);
                break;
            case ActEnemyState.Windup:
            {
                float k = Math.Clamp(t / (ActEnemyTuning.BossWindup * haste), 0f, 1f);
                switch (move)
                {
                    case BossMove.Charge:
                        // Head drops to ram height, jaws open.
                        headX = Snap(k * 6f); headY = Snap(k * 12f);
                        body = Snap(k * 4f); tail = Snap(k * 6f);
                        jaw = k > .5f;
                        break;
                    case BossMove.Tail:
                        // Wind the tail high, head turns away.
                        float coil = .5f - .5f * MathF.Cos(k * MathF.PI);
                        tail = Snap(coil * 16f); headX = -Snap(coil * 6f);
                        body = Snap(coil * 2f);
                        break;
                    default:
                        // Rear up before the quake.
                        body = -Snap(k * 10f); headY = -Snap(k * 4f);
                        armsUp = true; jaw = k > .4f;
                        break;
                }
                break;
            }
            case ActEnemyState.Attack:
                switch (move)
                {
                    case BossMove.Charge:
                        walking = true;
                        headX = 8f; headY = 12f; body = 4f; tail = 6f; jaw = true;
                        break;
                    case BossMove.Tail:
                        // Tail whips from high to low across the swing.
                        float swing = Math.Clamp(t / (.38f * haste), 0f, 1f);
                        float sweep = .5f - .5f * MathF.Cos(swing * MathF.PI);
                        tail = Snap(16f - 28f * sweep);
                        body = Snap(2f * (1f - sweep));
                        headX = -6f;
                        break;
                    default:
                        body = 6f; headY = 4f; jaw = true; stomp = true;
                        break;
                }
                break;
            case ActEnemyState.Recover:
                // Heaving breaths; jaw hangs open on the exhale.
                body = (int)(t * 4f) % 2 == 0 ? 2f : 4f;
                headY = 6f; jaw = (int)(t * 4f) % 2 == 1;
                if (move == BossMove.Tail)
                {
                    float settle = MathF.Sin(Math.Clamp(t / (.32f * haste), 0f, 1f) * MathF.PI * .5f);
                    tail = Snap(-12f * (1f - settle));
                    headX = Snap(-6f * (1f - settle));
                    headY = Snap(6f * settle);
                    body = Snap(body * settle);
                }
                break;
            case ActEnemyState.Hurt:
                headX = -6f; headY = -4f; squint = true; jaw = true;
                break;
            default:
                body = Snap(2f + MathF.Sin(t * 2.5f) * 2f);
                headY = Snap(MathF.Sin(t * 2.5f + .6f) * 2f);
                tail = Snap(MathF.Sin(t * 2.5f - .5f) * 3f);
                break;
        }

        // Tail: thick root, tip travels furthest.
        Block(-4, 36 + body, 20, 16);
        Block(-22, 38 + body - tail * .4f, 20, 12);
        Block(-38, 40 + body - tail * .8f, 18, 10);
        Block(-52, 42 + body - tail * 1.2f, 16, 8);
        Block(-62, 44 + body - tail * 1.6f, 12, 6);
        Block(-70, 46 + body - tail * 2f, 10, 4);
        // Torso.
        Block(10, 26 + body, 66, 36);
        Block(16, 20 + body, 56, 8);
        Block(20, 60 + body, 50, 6);
        // Dorsal scutes.
        for (int scute = 0; scute < 5; scute++)
            Block(22 + scute * 10, 16 + body, 6, 4);
        using var bossAccent = new SolidBrush(ActBossVisual.Accent(ActBossId.Tyrant));
        for (int scute = 0; scute < 5; scute++)
            g.FillRectangle(bossAccent, x + 22 + scute * 10, y + 16 + body, 4, 2);
        // Pale belly plate; phase two adds three raked claw scars on the flank.
        Cut(52, 38 + body, 18, 16);
        if (phase2)
        {
            for (int scar = 0; scar < 3; scar++)
                for (int step = 0; step < 4; step++)
                    Cut(26 + scar * 7 + step * 2, 30 + body + step * 4, 2, 4);
        }
        // Neck and head.
        float hx = headX, hy = body + headY;
        Block(66 + hx * .5f, 12 + hy, 20, 20);
        Block(76 + hx, 2 + hy, 34, 22);
        Block(80 + hx, -2 + hy, 26, 4);
        Block(108 + hx, 6 + hy, 6, 12);
        // Broken crown and fossil-coloured brow retain the original tyrant base.
        Block(78 + hx, -8 + hy, 6, 8);
        Block(89 + hx, -12 + hy, 8, 12);
        Block(102 + hx, -6 + hy, 6, 8);
        g.FillRectangle(bossAccent, x + 86 + hx, y + 2 + hy, 13, 2);
        // Eye: larger and ringed in phase two.
        if (squint) Cut(88 + hx, 7 + hy, 8, 2);
        else if (phase2)
        {
            Cut(86 + hx, 4 + hy, 8, 6);
            Block(89 + hx, 6 + hy, 2, 2);
        }
        else Cut(88 + hx, 5 + hy, 5, 5);
        // Jaw.
        if (jaw)
        {
            Block(80 + hx, 28 + hy, 30, 6);
            Cut(84 + hx, 22 + hy, 28, 6);
            for (int tooth = 0; tooth < 5; tooth++)
            {
                Block(86 + hx + tooth * 5, 22 + hy, 2, 2);
                Block(88 + hx + tooth * 5, 26 + hy, 2, 2);
            }
        }
        else
        {
            Block(80 + hx, 24 + hy, 30, 6);
            Cut(92 + hx, 22 + hy, 18, 2);
        }
        // Tiny arms.
        if (armsUp)
        {
            Block(76, 30 + body, 8, 4);
            Block(82, 24 + body, 4, 8);
        }
        else
        {
            Block(76, 36 + body, 10, 4);
            Block(84, 38 + body, 4, 8);
        }
        // Legs; feet always meet the ground at y + 100.
        float hip = 60f + body;
        for (int leg = 0; leg < 2; leg++)
        {
            float stride = walking ? MathF.Sin(t * 23f + leg * MathF.PI) : 0f;
            float lx = (leg == 0 ? 18f : 50f) + Snap(stride * 7f);
            if (stomp) lx += leg == 0 ? -4f : 4f;
            float foot = 96f - Snap(Math.Max(0f, stride) * 9f);
            Block(lx, hip, 18, 14);
            Block(lx + 4f, hip + 12f, 12, MathF.Max(2f, foot - hip - 12f));
            Block(lx + 2f, foot, 22, 4);
            Cut(lx + 20f, foot + 2f, 2, 2);
        }
    }
}
