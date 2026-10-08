using System.Drawing;

namespace DinoDash.Desktop;

// Final pass over hand-authored chapter modules. Slope height must be checked
// along a platform's full width (and full travel), never only at its centre.
internal static class ActPlatformLayout
{
    public const float MinimumGroundClearance = 60f;

    public static RectangleF TravelBounds(ActDynamicPlatform platform)
    {
        RectangleF b = platform.Origin;
        return platform.Motion switch
        {
            ActPlatformMotion.Horizontal => new RectangleF(b.X - platform.Amplitude,
                b.Y, b.Width + platform.Amplitude * 2, b.Height),
            ActPlatformMotion.Vertical => new RectangleF(b.X, b.Y - platform.Amplitude,
                b.Width, b.Height + platform.Amplitude * 2),
            // Falling and collapse platforms are only usable at their reset
            // position. Once released, they deliberately pass through terrain.
            _ => b
        };
    }

    public static float LowestFloorAbove(ActLevelLayout map, RectangleF footprint)
    {
        float lowest = float.PositiveInfinity;
        foreach (ActTerrainSegment segment in map.Terrain)
        {
            float left = Math.Max(footprint.Left, segment.Left);
            float right = Math.Min(footprint.Right, segment.Right);
            if (right <= left) continue;
            lowest = Math.Min(lowest, Math.Min(segment.YAt(left), segment.YAt(right)));
        }
        return lowest;
    }

    public static void Finalize(ActLevelLayout map)
    {
        for (int i = 0; i < map.Solids.Count; i++)
        {
            RectangleF original = map.Solids[i];
            float floor = LowestFloorAbove(map, original);
            float rise = original.Bottom + MinimumGroundClearance - floor;
            if (rise <= 0) continue;
            map.Solids[i] = new RectangleF(original.X, original.Y - rise,
                original.Width, original.Height);
            MoveAttachedPickupsAndEnemies(map, original, rise);
        }
        foreach (ActDynamicPlatform moving in map.MovingPlatforms)
        {
            float floor = LowestFloorAbove(map, TravelBounds(moving));
            float rise = TravelBounds(moving).Bottom + MinimumGroundClearance - floor;
            if (rise <= 0) continue;
            RectangleF b = moving.Origin;
            moving.SetOrigin(new RectangleF(b.X, b.Y - rise, b.Width, b.Height));
        }
        Validate(map);
    }

    public static void Validate(ActLevelLayout map)
    {
        var boxes = map.Solids.Concat(map.MovingPlatforms.Select(TravelBounds)).ToArray();
        for (int i = 0; i < boxes.Length; i++)
        {
            RectangleF box = boxes[i];
            float floor = LowestFloorAbove(map, box);
            if (box.Bottom > floor - MinimumGroundClearance + .02f)
                throw new InvalidOperationException(
                    $"Platform {i} too close to terrain: {box}, floor={floor}");
            for (int j = 0; j < i; j++)
            {
                RectangleF intersection = RectangleF.Intersect(box, boxes[j]);
                if (intersection.Width > .02f && intersection.Height > .02f)
                    throw new InvalidOperationException(
                        $"Platforms {j} and {i} overlap: {boxes[j]} / {box}");
            }
        }
    }

    private static void MoveAttachedPickupsAndEnemies(ActLevelLayout map,
        RectangleF oldPlatform, float rise)
    {
        for (int i = 0; i < map.Enemies.Count; i++)
        {
            ActEnemy enemy = map.Enemies[i];
            float center = enemy.X + enemy.Width / 2;
            if (enemy.Kind == ActEnemyKind.Flying || center < oldPlatform.Left ||
                center >= oldPlatform.Right || Math.Abs(enemy.HomeY + enemy.Height - oldPlatform.Top) > 2)
                continue;
            map.Enemies[i] = new ActEnemy
            {
                Kind = enemy.Kind, X = enemy.X, HomeX = enemy.HomeX,
                Y = enemy.Y - rise, HomeY = enemy.HomeY - rise,
                Health = enemy.Health, MaxHealth = enemy.MaxHealth,
                Facing = enemy.Facing, State = enemy.State, Dormant = enemy.Dormant
            };
        }
        for (int i = 0; i < map.Rewards.Count; i++)
        {
            ActReward reward = map.Rewards[i];
            if (reward.X < oldPlatform.Left || reward.X >= oldPlatform.Right ||
                Math.Abs(reward.Y - (oldPlatform.Top - 28)) > 2) continue;
            map.Rewards[i] = new ActReward(reward.Kind, reward.X, reward.Y - rise,
                reward.RoomIndex) { Collected = reward.Collected,
                    AvailableAfter = reward.AvailableAfter };
        }
    }
}
