using System.Reflection;
using System.Drawing.Imaging;
using DinoDash.Desktop;

internal static class ChapterRoomVisualChecks
{
    public static void Run()
    {
        using var atlas=new Bitmap(1800,1200);
        using var sheet=Graphics.FromImage(atlas);
        sheet.Clear(Color.FromArgb(22,27,31));
        int index=0;
        foreach(var room in ActChapterRooms.CanyonPool)
        {
            var g=new ActEngine(47);g.LoadMapForDebug(2,47,false,roomAdventure:true);
            g.Run.RoomJourney!.CurrentRoomId=room.Id;
            typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(g,[null]);
            g.PlacePlayerForTest(900,(g.TerrainYAt(927)??300)-52);
            using var canvas=new GameCanvas(g){Size=new Size(1280,720),Dimmed=false};
            canvas.ResetFx();
            for(int sample=0;sample<4;sample++)
            {
                canvas.Tick(ActTuning.Step);
                using var frame=canvas.RenderRewardScene();
                if(sample==0)sheet.DrawImage(frame,new Rectangle(index%3*600,index/3*300,600,300));
            }
            index++;
        }
        atlas.Save(Path.Combine(AppContext.BaseDirectory,"chapter-room-atlas.png"),ImageFormat.Png);
        var exitGame=new ActEngine(47);exitGame.LoadMapForDebug(2,47,false,roomAdventure:true);
        foreach(var area in exitGame.CombatAreas)
        {area.Cleared=true;foreach(var enemy in area.Enemies)enemy.State=ActEnemyState.Dead;}
        exitGame.Run.RoomJourney!.RewardGranted=true;
        float hall=exitGame.WorldWidth-ChapterRoomTuning.ExitHallLength;
        exitGame.PlacePlayerForTest(hall+260,248);
        using var exits=new GameCanvas(exitGame){Size=new Size(1280,720),Dimmed=false};
        exits.ResetFx();exits.Tick(ActTuning.Step);
        using var exitImage=exits.RenderRewardScene();
        exitImage.Save(Path.Combine(AppContext.BaseDirectory,"chapter-room-exits.png"),ImageFormat.Png);
        // Render the compact transition while both battles are still uncleared.
        var passage=new ActEngine(47);passage.LoadMapForDebug(2,47,false,roomAdventure:true);
        float px=(passage.CombatAreas[0].Right+passage.CombatAreas[1].Left)/2;
        passage.PlacePlayerForTest(px,(passage.TerrainYAt(px+27)??300)-52);
        using var transitionCanvas=new GameCanvas(passage){Size=new Size(1280,720),Dimmed=false};
        transitionCanvas.ResetFx();transitionCanvas.Tick(ActTuning.Step);
        using var transitionImage=transitionCanvas.RenderRewardScene();
        transitionImage.Save(Path.Combine(AppContext.BaseDirectory,"chapter-room-transition.png"),ImageFormat.Png);
        using var transitionAtlas=new Bitmap(1800,1200);
        using var transitionSheet=Graphics.FromImage(transitionAtlas);
        using var heading=new Font("Microsoft YaHei UI",13,FontStyle.Bold);
        transitionSheet.Clear(Color.FromArgb(22,27,31));
        index=0;
        foreach(var profile in ActRoomTransitions.Pool)
        {
            var sample=new ActEngine(47);sample.LoadMapForDebug(2,47,false,roomAdventure:true);
            sample.Run.RoomJourney!.TransitionId=profile.Id;
            typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(sample,[null]);
            float x=ChapterRoomTuning.FirstAreaEnd+180;
            sample.PlacePlayerForTest(x-27,(sample.TerrainYAt(x)??300)-52);
            using var view=new GameCanvas(sample){Size=new Size(1280,720),Dimmed=false};
            view.ResetFx();view.Tick(ActTuning.Step);
            using var frame=view.RenderRewardScene();
            var cell=new Rectangle(index%3*600,index/3*300,600,300);
            transitionSheet.DrawImage(frame,cell);
            transitionSheet.FillRectangle(Brushes.Black,cell.X,cell.Y,600,26);
            transitionSheet.DrawString(profile.Name,heading,Brushes.Bisque,cell.X+8,cell.Y+3);
            index++;
        }
        transitionAtlas.Save(Path.Combine(AppContext.BaseDirectory,"chapter-transition-atlas.png"),ImageFormat.Png);
        var hard=new ActEngine(47);hard.LoadMapForDebug(2,47,false,roomAdventure:true);
        hard.Run.RoomJourney!.Tier=BattleRoomTier.Hard;hard.Run.RoomJourney.Reward=RouteReward.Evolution;
        typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(hard,[null]);
        if(hard.CombatAreas.Count!=3)throw new Exception("High difficulty third battlefield missing");
        hard.PlacePlayerForTest(hard.CombatAreas[2].Left+170,248);
        using(var view=new GameCanvas(hard){Size=new Size(1280,720),Dimmed=false})
        {
            view.ResetFx();view.Tick(ActTuning.Step);
            using var frame=view.RenderRewardScene();
            frame.Save(Path.Combine(AppContext.BaseDirectory,"chapter-hard-third-battle.png"),ImageFormat.Png);
        }
        var ante=new ActEngine(47);ante.LoadMapForDebug(2,47,false,roomAdventure:true);
        ante.Run.RoomJourney!.Stage=RoomJourneyStage.Antechamber;
        typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(ante,[null]);
        ante.PlacePlayerForTest(ChapterRoomTuning.AntechamberMerchantX-27,248);
        using(var view=new GameCanvas(ante){Size=new Size(1280,720),Dimmed=false})
        {
            view.ResetFx();view.Tick(ActTuning.Step);
            using var frame=view.RenderRewardScene();
            frame.Save(Path.Combine(AppContext.BaseDirectory,"chapter-room-antechamber.png"),ImageFormat.Png);
        }
        if(!ante.InteractWithRest() || !ante.ShopRequested)throw new Exception("Antechamber merchant cannot open");
        using(var shop=new ActRestShopDialog(ante))
        {
            if(shop.Controls.OfType<ActUiPanel>().Count()!=3)throw new Exception("Antechamber merchant lost product cards");
            UiThemeChecks.SaveDialog(shop,"chapter-antechamber-shop.png");
        }
        RenderAllChapters();
        Console.WriteLine("PASS ten canyon rooms, compact transitions, tiered reward exits, hard third battle and antechamber merchant rendered.");
    }
    private static void RenderAllChapters()
    {
        using var atlas=new Bitmap(1800,1200);
        using var sheet=Graphics.FromImage(atlas);
        sheet.Clear(Color.FromArgb(22,27,31));
        using var heading=new Font("Microsoft YaHei UI",13,FontStyle.Bold);
        for(int chapter=1;chapter<=4;chapter++)
        {
            foreach(var definition in ActChapterRooms.PoolFor(chapter))
            foreach(var tier in new[]{BattleRoomTier.Normal,BattleRoomTier.Hard})
            {
                var sample=new ActEngine(47);sample.LoadMapForDebug(chapter,47,false,roomAdventure:true);
                sample.Run.RoomJourney!.CurrentRoomId=definition.Id;sample.Run.RoomJourney.Tier=tier;
                sample.Run.RoomJourney.Reward=tier==BattleRoomTier.Hard?RouteReward.Evolution:RouteReward.Bones;
                typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(sample,[null]);
                using var canvas=new GameCanvas(sample){Size=new Size(1280,720),Dimmed=false};
                canvas.ResetFx();
                foreach(var area in sample.CombatAreas)
                {
                    float x=area.Left+(area.Right-area.Left)*.5f;
                    sample.PlacePlayerForTest(x,(sample.TerrainYAt(x+27)??300)-52);
                    for(int frame=0;frame<3;frame++)
                    {canvas.Tick(ActTuning.Step);using var image=canvas.RenderRewardScene();}
                }
            }
            // A chapter storyboard: first battlefield, second battlefield, fixed antechamber.
            var game=new ActEngine(47);game.LoadMapForDebug(chapter,47,false,roomAdventure:true);
            game.Run.RoomJourney!.CurrentRoomId=ActChapterRooms.PoolFor(chapter)[3].Id;
            typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,[null]);
            for(int column=0;column<3;column++)
            {
                if(column==2)
                {
                    game.Run.RoomJourney!.Stage=RoomJourneyStage.Antechamber;
                    typeof(ActEngine).GetMethod("ResetMap",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(game,[null]);
                    game.PlacePlayerForTest(550,248);
                }
                else
                {
                    var area=game.CombatAreas[column];float x=area.Left+(area.Right-area.Left)*.5f;
                    game.PlacePlayerForTest(x,(game.TerrainYAt(x+27)??300)-52);
                }
                using var view=new GameCanvas(game){Size=new Size(1280,720),Dimmed=false};
                view.ResetFx();view.Tick(ActTuning.Step);
                using var image=view.RenderRewardScene();
                var cell=new Rectangle(column*600,(chapter-1)*300,600,300);
                sheet.DrawImage(image,cell);sheet.FillRectangle(Brushes.Black,cell.X,cell.Y,600,26);
                sheet.DrawString(ActChapters.Name(chapter)+" · "+(column==2?"Boss 前庭":$"战场 {column+1}"),heading,Brushes.Bisque,cell.X+8,cell.Y+3);
            }
        }
        atlas.Save(Path.Combine(AppContext.BaseDirectory,"all-chapter-room-atlas.png"),ImageFormat.Png);
        Console.WriteLine("PASS all four chapter decks: 80 normal/hard room builds, each battlefield at three frames and themed merchant antechambers.");
    }
}
