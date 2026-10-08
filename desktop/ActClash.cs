namespace DinoDash.Desktop;

internal sealed partial class ActEngine
{
    private float clashWindowRemaining;
    public int ClashSerial { get; private set; }
    public float ClashX { get; private set; }
    public float ClashY { get; private set; }

    private bool TryClashNearby()
    {
        foreach(var enemy in enemies.Where(e=>
                    e.State is (ActEnemyState.Windup or ActEnemyState.Attack) && !e.Dormant)
                .OrderBy(e=>Math.Abs(e.X+e.Width*.5f-PlayerX-PlayerWidth*.5f)))
            if(TryClash(enemy))return true;
        return false;
    }

    private bool TryClash(ActEnemy enemy)
    {
        if(clashWindowRemaining<=0f || attackElapsed<=0f || attackHit ||
            enemy.State is not (ActEnemyState.Windup or ActEnemyState.Attack) ||
            enemy.Kind==ActEnemyKind.Ranged || enemy.Dormant || !ThreatIsClashable(enemy))return false;
        float playerCenter=PlayerX+PlayerWidth*.5f,enemyCenter=enemy.X+enemy.Width*.5f;
        if(Facing*(enemyCenter-playerCenter)<-10f)return false;
        float edge=Facing>0?enemy.Bounds.Left:enemy.Bounds.Right;
        float front=Facing>0?PlayerBounds.Right:PlayerBounds.Left;
        float gap=Facing*(edge-front);
        if(gap < -Math.Min(enemy.Width,36f) || gap>ActClashTuning.Reach ||
            PlayerBounds.Top>enemy.Bounds.Bottom+12f || PlayerBounds.Bottom<enemy.Bounds.Top-12f)return false;

        ClashX=(Math.Clamp(front,enemy.Bounds.Left,enemy.Bounds.Right)+front)*.5f;
        ClashY=Math.Clamp(PlayerY+PlayerHeight*.48f,enemy.Bounds.Top,enemy.Bounds.Bottom);
        clashWindowRemaining=attackElapsed=attackBuffer=comboWindow=0f;
        attackRecovery=Math.Max(attackRecovery,.18f);
        attackHit=true;comboStep=0;
        DamageEnemy(enemy,ActClashTuning.Damage,0f,false);
        if(enemy.State!=ActEnemyState.Dead)
        {
            enemy.State=ActEnemyState.Dazed;
            enemy.StateTime=0f;
            enemy.AttackApplied=true;
            int pushDirection=Math.Sign(enemyCenter-playerCenter);
            if(pushDirection==0)pushDirection=Facing;
            enemy.KnockbackSpeed=pushDirection*(enemy.Kind==ActEnemyKind.Boss?
                ActClashTuning.BossPushSpeed:ActClashTuning.PushSpeed);
            enemy.FlashRemaining=.24f;
            if(enemy.BossAirborne)enemy.BossAirborne=false;
        }
        bossHazards.RemoveAll(h=>h.Source==enemy);
        HitStopRemaining=Math.Max(HitStopRemaining,.11f);
        ShakeRemaining=Math.Max(ShakeRemaining,.3f);
        ClashSerial++;
        return true;
    }

    private static bool ThreatIsClashable(ActEnemy enemy)
    {
        if(enemy.Kind==ActEnemyKind.Boss && enemy.BossMove is not
            (BossMove.Charge or BossMove.Tail or BossMove.Roll or BossMove.Club or BossMove.Pounce))
            return false;
        if(enemy.State==ActEnemyState.Attack)return true;
        float windup=enemy.Kind switch
        {
            ActEnemyKind.Melee=>ActEnemyTuning.MeleeWindup,
            ActEnemyKind.Heavy=>ActEnemyTuning.HeavyWindup,
            ActEnemyKind.Flying=>ActEnemyTuning.FlyingWindup,
            ActEnemyKind.Boss when enemy.BossId==ActBossId.Tyrant=>ActEnemyTuning.BossWindup*
                (enemy.Health<=enemy.MaxHealth/2?.8f:1f),
            ActEnemyKind.Boss=>ActBossCatalog.Get(enemy).Windup*ActBossCatalog.Tempo(enemy),
            _=>float.PositiveInfinity
        };
        return windup-enemy.StateTime<=ActClashTuning.TelegraphWindow;
    }
}
