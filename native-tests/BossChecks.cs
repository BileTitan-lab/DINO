using DinoDash.Desktop;

internal static class BossChecks
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static ActEngine Arena(int chapter)
    {
        var game = new ActEngine(73);
        game.LoadMapForDebug(chapter,73,false,true);
        foreach(var enemy in game.Enemies) if(enemy!=game.Boss) enemy.Dormant=true;
        game.Layout.Mechanisms.Clear();
        game.Layout.Hazards.Clear();
        return game;
    }
    private static void Tick(ActEngine game, float seconds, bool protect = true)
    {
        for(int i=0;i<(int)MathF.Ceiling(seconds/ActTuning.Step);i++)
        {
            if(protect) game.Run.SetHealth(game.MaximumHealth);
            game.Update(ActTuning.Step);
        }
    }
    private static void Start(ActEngine game, BossMove move)
    {
        var b=game.Boss;
        b.State=ActEnemyState.Chase;b.StateTime=1;
        b.AttackCount=Array.IndexOf(ActBossCatalog.Get(b).Moves,move);
        game.Update(ActTuning.Step);
        Require(b.State==ActEnemyState.Windup && b.BossMove==move,"Move selection failed");
    }
    public static void Run(Action<string,Action> check)
    {
        check("ACT four chapter bosses have distinct identities and move sets, debug spawns in their arena",()=>
        {
            var identities=new HashSet<ActBossId>();var moves=new HashSet<BossMove>();
            for(int chapter=1;chapter<=4;chapter++)
            {
                var game=Arena(chapter);var b=game.Boss;identities.Add(b.BossId);
                foreach(var move in ActBossCatalog.Get(b).Moves) Require(moves.Add(move),"Duplicate boss move");
                Require(b.Health==(int)MathF.Ceiling(ActBossCatalog.Get(b).Health*ActChapterPressure.Health[chapter]),"Boss health configuration");
                Require(game.PlayerX>=game.Layout.BossArenaLeft && game.PlayerX<b.X,"Debug boss placement");
                Require(game.Health==5 && game.Grounded,"Debug start must be safe");
            }
            Require(identities.Count==4 && moves.Count==12,"Four complete bosses required");
        });
        check("ACT each new boss executes every move at both health phases and returns to recovery",()=>
        {
            for(int chapter=2;chapter<=4;chapter++) foreach(bool phase2 in new[]{false,true})
            foreach(var move in ActBossCatalog.Definitions[chapter-1].Moves)
            {
                var game=Arena(chapter);var b=game.Boss;if(phase2)b.Health=b.MaxHealth/2;
                Start(game,move);bool attacked=false,recovered=false;
                for(int i=0;i<480;i++)
                {
                    game.Run.SetHealth(5);game.Update(ActTuning.Step);
                    attacked|=b.State==ActEnemyState.Attack;
                    if(attacked && b.State==ActEnemyState.Recover){recovered=true;break;}
                    Require(float.IsFinite(b.X)&&float.IsFinite(b.Y),"Invalid position");
                    Require(b.X>=game.Layout.BossArenaLeft-1 && b.X+b.Width<=game.Layout.BossArenaRight+1,"Boss left arena");
                }
                Require(attacked && recovered,$"{chapter}/{move}/{phase2} stuck at {b.State}");
            }
        });
        check("ACT leaps lock their landing, leave the floor, land correctly and release terrain control",()=>
        {
            foreach(int chapter in new[]{3,4})
            {
                var game=Arena(chapter);var b=game.Boss;
                Start(game,chapter==3?BossMove.Pounce:BossMove.MeteorLeap);
                float target=b.TargetX;game.PlacePlayerForTest(target-150,248);
                Tick(game,ActBossCatalog.Get(b).Windup+.42f);
                Require(b.BossAirborne && b.Y<b.TargetY-60,"Leap remained grounded");
                Require(b.TargetX==target,"Landing tracked player during windup");
                Tick(game,.48f);
                Require(!b.BossAirborne && (Math.Abs(b.Y+b.Height-300)<2 || game.Solids.Any(s=>
                    Math.Abs(b.Y+b.Height-s.Top)<2 && b.X>=s.Left-1 && b.X+b.Width<=s.Right+1)),"Leap did not settle on terrain or platform");
            }
        });
        check("ACT boss hazard warnings cannot hurt, danger has safe gaps, death clears attacks",()=>
        {
            foreach(var pair in new[]{(2,BossMove.Rockfall),(3,BossMove.RootBurst),(4,BossMove.Fissure)})
            {
                var game=Arena(pair.Item1);Start(game,pair.Item2);
                Tick(game,ActBossCatalog.Get(game.Boss).Windup+.06f);
                Require(game.BossHazards.Count>=2,"No hazards spawned");
                var hazards=game.BossHazards.OrderBy(h=>h.Bounds.Left).ToArray();
                for(int i=1;i<hazards.Length;i++) Require(hazards[i].Bounds.Left-hazards[i-1].Bounds.Right>=54,"No player-width safe gap");
                var h=hazards[0];game.PlacePlayerForTest(h.Bounds.Left,248);
                int hp=game.Health;Tick(game,.35f,false);
                Require(game.Health==hp && !h.Active,"Warning caused damage");
                game.Boss.State=ActEnemyState.Dead;game.Boss.Health=0;
                game.Update(ActTuning.Step);
                Require(game.BossHazards.Count==0 && game.EnemyShots.Count==0,"Attacks outlived boss");
            }
        });
        check("ACT roots damage only during eruption and respect shield and invulnerability",()=>
        {
            var game=Arena(3);Start(game,BossMove.RootBurst);
            Tick(game,ActBossCatalog.Get(game.Boss).Windup+.05f);
            var h=game.BossHazards.First();game.Boss.State=ActEnemyState.Patrol;
            game.Run.AddShield(1);game.PlacePlayerForTest(h.Bounds.Left+1,248);
            Tick(game,.98f,false);
            Require(game.Health==5 && game.Run.ShieldCharges==0,"Shield did not absorb roots");
            Tick(game,.12f,false);Require(game.Health==5,"Continuous hazard bypassed protection");
        });
        check("ACT feather fans aim across heights and late chapters add half-health volleys",()=>
        {
            foreach(int chapter in new[]{3,4})
            {
                var game=Arena(chapter);var b=game.Boss;
                game.PlacePlayerForTest(b.X-200,60);
                var move=chapter==3?BossMove.Feathers:BossMove.FlameFan;
                Start(game,move);Tick(game,ActBossCatalog.Get(b).Windup+.19f);
                Require(game.EnemyShots.Count>0 && game.EnemyShots.Any(s=>s.Vy<0),"Volley did not aim upward");
                var enraged=Arena(chapter);enraged.Boss.Health=enraged.Boss.MaxHealth/2;
                Start(enraged,move);Tick(enraged,2.05f);
                Require(enraged.Boss.VolleyCount==(chapter==3?2:3),"Phase-two volley missing");
            }
        });
        check("ACT amber slows boss hazards, map reset removes all pending patterns",()=>
        {
            var game=Arena(2);Start(game,BossMove.Rockfall);Tick(game,1.02f);
            var h=game.BossHazards.First();float before=h.Time;
            game.Run.StartEnemySlow(3);Tick(game,.3f);
            Require(h.Time-before<.18f && h.Time-before>.12f,"Hazards did not slow");
            game.LoadMapForDebug(4,73,true,true);
            Require(game.BossHazards.Count==0 && game.EnemyShots.Count==0,"Reset leaked patterns");
        });
        check("ACT airborne boss death drops evolution and bones on reachable floor",()=>
        {
            var game=Arena(3);var b=game.Boss;Start(game,BossMove.Pounce);
            Tick(game,1.15f);Require(b.BossAirborne,"Expected airborne boss");
            b.Health=1;
            var damage=typeof(ActEngine).GetMethod("DamageEnemy",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            damage.Invoke(game,[b,1,0f,false,false]);
            Require(b.State==ActEnemyState.Dead && !b.BossAirborne,"Boss still airborne after death");
            Require(Math.Abs(b.Y+b.Height-300)<1 && game.BossEvolutionReward is not null,"Reward stranded in air");
        });
    }
}
