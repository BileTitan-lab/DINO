namespace DinoDash.Desktop;

internal static class ActRewardTuning
{
    public const int HealAmount = 2;
    public const float DamageBuffMultiplier = 1.5f;
    public const float MoveBuffMultiplier = 1.25f;
    public const float BuffDuration = 20f;
}

internal sealed partial class ActEngine
{
    public float DamageBuffRemaining { get; private set; }
    public float MoveBuffRemaining { get; private set; }
    public float DamageMultiplier => DamageBuffRemaining > 0f
        ? ActRewardTuning.DamageBuffMultiplier : 1f;
    public float MoveSpeedMultiplier => MoveBuffRemaining > 0f
        ? ActRewardTuning.MoveBuffMultiplier : 1f;

    private void ResetRewards()
    {
        DamageBuffRemaining = 0f;
        MoveBuffRemaining = 0f;
    }

    private void UpdateRewardTimers(float dt)
    {
        DamageBuffRemaining = Math.Max(0f, DamageBuffRemaining - dt);
        MoveBuffRemaining = Math.Max(0f, MoveBuffRemaining - dt);
    }

    private void ActivateAmbushRooms()
    {
        foreach (RoomSelection room in layout.Rooms)
        {
            if (room.Activated || PlayerX < room.Node.StartX + 200f) continue;
            room.Activated = true;
            foreach (ActEnemy enemy in enemies.Where(enemy =>
                enemy.HomeX >= room.Node.StartX && enemy.HomeX < room.Node.EndX))
                enemy.Dormant = false;
        }
    }

    public bool IsRewardUnlocked(ActReward reward)
    {
        if (reward.RoomIndex < 0) return true;
        RoomSelection room = layout.Rooms.First(candidate =>
            candidate.Node.Index == reward.RoomIndex);
        return enemies.All(enemy => enemy.HomeX < room.Node.StartX ||
            enemy.HomeX >= room.Node.EndX || enemy.State == ActEnemyState.Dead);
    }

    private void UpdateRewards()
    {
        foreach (ActReward reward in layout.Rewards)
        {
            if (reward.Collected || RunTime < reward.AvailableAfter ||
                PendingPowerReward != ActRewardChoice.None || !IsRewardUnlocked(reward) ||
                !PlayerBounds.IntersectsWith(reward.Bounds)) continue;
            // Keep a room's optional recovery available for a later return.
            if(IsRoomJourney && (reward.Kind==ActRewardKind.Heal && Health>=MaximumHealth ||
                reward.Kind==ActRewardKind.FullRangedEnergy && RangedEnergy>=MaximumEnergy))continue;
            reward.Collected = true;
            switch (reward.Kind)
            {
                case ActRewardKind.Heal:
                    Run.Heal(ActRewardTuning.HealAmount);
                    break;
                case ActRewardKind.FullRangedEnergy:
                    RangedEnergy = MaximumEnergy;
                    break;
                case ActRewardKind.DamageBuff:
                    DamageBuffRemaining = ActRewardTuning.BuffDuration;
                    break;
                case ActRewardKind.MoveBuff:
                    MoveBuffRemaining = ActRewardTuning.BuffDuration;
                    break;
                case ActRewardKind.BattleArt:
                    OfferBattleArts();
                    break;
                case ActRewardKind.Evolution:
                    OfferEvolutions(ReferenceEquals(reward,BossEvolutionReward)?EvolutionSource.BossReward:EvolutionSource.NormalReward);
                    break;
                case ActRewardKind.Form:
                    OfferForms();
                    break;
                default:
                    throw new NotSupportedException($"Reward {reward.Kind} is reserved for a later version.");
            }
            AddImpact(reward.X, reward.Y, false);
        }
    }

    private void UpdateHazards()
    {
        foreach (ActHazard hazard in layout.Hazards)
        {
            if (!hazard.IsActive(RunTime)) continue;
            if (PlayerBounds.IntersectsWith(hazard.Bounds(RunTime)))
                HurtPlayer();
        }
    }
}
