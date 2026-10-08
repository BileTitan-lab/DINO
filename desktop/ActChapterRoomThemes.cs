using System.Drawing;

namespace DinoDash.Desktop;

// Each chapter owns a deck of authored room profiles. Battle policy, rewards
// and event-module dispatch stay shared; no complete chapter is sliced blindly.
internal static partial class ActChapterRooms
{
    public static readonly ChapterRoomDefinition[] WetlandPool =
    [
        new("marsh-entry","苇海浅滩",ChapterRoomType.Combat,1,0),
        new("marsh-bank","芦苇河岸",ChapterRoomType.Combat,1,1),
        new("marsh-trail","沉木小径",ChapterRoomType.Terrain,1,2),
        new("marsh-island","雾中沙洲",ChapterRoomType.Terrain,1,3),
        new("marsh-grove","林荫台地",ChapterRoomType.Heights,2,4),
        new("marsh-stream","浅溪石阶",ChapterRoomType.Terrain,1,5),
        new("marsh-roots","苇根通路",ChapterRoomType.Branch,2,6),
        new("marsh-nest","古巢栈道",ChapterRoomType.Heights,2,7),
        new("marsh-hunters","猎群苇坪",ChapterRoomType.Elite,3,8),
        new("marsh-watch","沼泽瞭望地",ChapterRoomType.Heights,2,9)
    ];
    public static readonly ChapterRoomDefinition[] ForestPool =
    [
        new("forest-root-valley","盘根深谷",ChapterRoomType.Branch,1,0),
        new("forest-crown-steps","树冠阶庭",ChapterRoomType.Heights,1,1),
        new("forest-hollow","空心根道",ChapterRoomType.Branch,2,2),
        new("forest-leaf-bridge","叶影断廊",ChapterRoomType.Terrain,2,3),
        new("forest-fungi","幽荧菌谷",ChapterRoomType.Heights,1,4),
        new("forest-nest","鸟巢回廊",ChapterRoomType.Heights,2,5),
        new("forest-shaft","垂藤竖井",ChapterRoomType.Branch,2,6),
        new("forest-root-ridge","双峰藤谷",ChapterRoomType.Terrain,2,7),
        new("forest-hunters","树冠猎场",ChapterRoomType.Elite,3,8),
        new("forest-falling","枯枝落庭",ChapterRoomType.Hazard,3,9)
    ];
    public static readonly ChapterRoomDefinition[] VolcanoPool =
    [
        new("volcano-ash","余烬坡地",ChapterRoomType.Combat,1,0),
        new("volcano-ridge","黑曜石台地",ChapterRoomType.Terrain,1,1),
        new("volcano-valley","灰烬竖井",ChapterRoomType.Branch,2,2),
        new("volcano-bridge","熔湖断桥",ChapterRoomType.Terrain,2,3),
        new("volcano-vents","脉动喷口",ChapterRoomType.Hazard,2,4),
        new("volcano-rocks","崩岩回廊",ChapterRoomType.Hazard,2,5),
        new("volcano-fork","玄武岩双层道",ChapterRoomType.Heights,2,6),
        new("volcano-lift","热流阶桥",ChapterRoomType.Heights,2,7),
        new("volcano-collapse","崩塌熔桥",ChapterRoomType.Hazard,3,8),
        new("volcano-forge","熔炉禁庭",ChapterRoomType.Elite,3,9)
    ];
    public static IReadOnlyList<ChapterRoomDefinition> PoolFor(int chapter)=>chapter switch
    {1=>WetlandPool,2=>CanyonPool,3=>ForestPool,4=>VolcanoPool,_=>throw new ArgumentOutOfRangeException(nameof(chapter))};

    public static string BossRoomName(int chapter)=>chapter switch
    {1=>"苇海霸主巢穴",2=>"岩甲古兽巢穴",3=>"巨木之心",4=>"熔炉祭坛",_=>"首领巢穴"};

    private static readonly EnemyGroup[] TeachingGroups =
    [new("marsh-scouts",[ActEnemyKind.Melee,ActEnemyKind.Melee]),
     new("marsh-lookout",[ActEnemyKind.Melee,ActEnemyKind.Ranged])];
    private static readonly EnemyGroup[] WetlandMixed =
    [new("marsh-wings",[ActEnemyKind.Melee,ActEnemyKind.Flying]),
     new("marsh-guards",[ActEnemyKind.Melee,ActEnemyKind.Ranged]),
     new("marsh-heavy",[ActEnemyKind.Heavy,ActEnemyKind.Melee])];
    private static readonly EnemyGroup[] ForestHigh =
    [new("forest-canopy",[ActEnemyKind.Melee,ActEnemyKind.Ranged,ActEnemyKind.Flying]),
     new("forest-flock",[ActEnemyKind.Flying,ActEnemyKind.Melee,ActEnemyKind.Flying])];
    private static readonly EnemyGroup[] ForestLow =
    [new("forest-root-guards",[ActEnemyKind.Melee,ActEnemyKind.Heavy,ActEnemyKind.Melee]),
     new("forest-root-watch",[ActEnemyKind.Ranged,ActEnemyKind.Melee,ActEnemyKind.Heavy])];
    private static readonly EnemyGroup[] VolcanoOpen =
    [new("volcano-heavy-watch",[ActEnemyKind.Heavy,ActEnemyKind.Melee,ActEnemyKind.Ranged]),
     new("volcano-flank",[ActEnemyKind.Melee,ActEnemyKind.Flying,ActEnemyKind.Heavy])];
    private static readonly EnemyGroup[] VolcanoHazard =
    [new("volcano-vent-watch",[ActEnemyKind.Melee,ActEnemyKind.Ranged]),
     new("volcano-bridge-flock",[ActEnemyKind.Flying,ActEnemyKind.Melee,ActEnemyKind.Ranged])];
    private static EnemyGroup[] GroupsFor(ChapterRoomRun run,ChapterRoomDefinition room,int area)=>
        run.Chapter switch
        {
            1=>area==0 || run.Depth==1?TeachingGroups:WetlandMixed,
            3=>room.Type is ChapterRoomType.Heights or ChapterRoomType.Branch?ForestHigh:ForestLow,
            4=>room.Type is ChapterRoomType.Hazard or ChapterRoomType.Terrain?VolcanoHazard:VolcanoOpen,
            _=>LightGroups
        };

    // Avoid ground enemies being authored into the gap or below a platform.
    private static float SupportedSpawn(ActLevelLayout map,float x,float width)
    {
        for(int shift=0;shift<=180;shift+=15)
            foreach(float candidate in new[]{x-shift,x+shift})
                if(ActSecondMap.GroundAt(map,candidate+width/2) is float y &&
                    !map.Solids.Any(s=>s.Height>16 && s.IntersectsWith(new(candidate,y-55,width,55))))
                    return candidate;
        throw new InvalidOperationException("Enemy socket has no safe supporting terrain.");
    }

    private static void BuildThemedTerrain(ActLevelLayout map,ChapterRoomRun run,ChapterRoomDefinition room)
    {
        map.Terrain.Add(new("入口安全地",0,360,300,300));
        Area(360,ChapterRoomTuning.FirstAreaEnd,0);
        ActRoomTransitions.Build(map,run.TransitionId);
        Area(ChapterRoomTuning.SecondAreaStart,ChapterRoomTuning.SecondAreaEnd,1);
        if(run.Tier==BattleRoomTier.Hard)
        {
            map.Terrain.Add(new("战场间缓坡",ChapterRoomTuning.SecondAreaEnd,ChapterRoomTuning.ThirdAreaStart,300,300));
            Area(ChapterRoomTuning.ThirdAreaStart,ChapterRoomTuning.ThirdAreaEnd,2);
        }
        float end=run.Tier==BattleRoomTier.Hard?ChapterRoomTuning.ThirdAreaEnd:ChapterRoomTuning.SecondAreaEnd;
        map.Terrain.Add(new("出口前安全地",end,map.Width-ChapterRoomTuning.ExitHallLength,300,300));
        void Area(float left,float right,int index)
        {
            // Profiles are manually placed in a 1000-unit socket. Only their
            // horizontal scale changes to fit the shorter third battle socket.
            int shape=index==0?room.Shape:(room.Shape+index*3)%10;
            var section=new RoomTerrainSocket(map,left,right,$"{room.Id}:{index}");
            if(run.Chapter==1)WetlandTerrain(section,shape);
            else if(run.Chapter==3)ForestTerrain(section,shape);
            else VolcanoTerrain(section,shape,run.RandomFor(0x4500+index).Next(2));
        }
    }

    private sealed class RoomTerrainSocket(ActLevelLayout map,float left,float right,string id)
    {
        private float Scale=>(right-left)/1000;
        public void Land(float x0,float y0,float x1,float y1)=>map.Terrain.Add(new(id,left+x0*Scale,left+x1*Scale,y0,y1));
        public void Shelf(float x,float y,float w)=>map.Solids.Add(new(left+x*Scale,y,w*Scale,14));
        public void Moving(float x,float y,float w,ActPlatformMotion motion,float amplitude=25)=>
            map.MovingPlatforms.Add(new(new RectangleF(left+x*Scale,y,w*Scale,14),motion,amplitude*Scale,4.8f,left+80*Scale));
        public void Mechanism(ActMechanismKind kind,float x,float y,float w=65)=>
            map.Mechanisms.Add(new(kind,left+x*Scale,y,w*Scale));
    }

    private static void WetlandTerrain(RoomTerrainSocket s,int shape)
    {
        // Small banks, walkable slopes and optional logs keep the teaching map readable.
        switch(shape)
        {
            case 1:case 5:
                s.Land(0,300,260,330);s.Land(260,330,680,330);s.Land(680,330,1000,300);break;
            case 2:case 6:
                s.Land(0,300,350,270);s.Land(350,270,720,270);s.Land(720,270,1000,300);break;
            case 3:case 8:
                s.Land(0,300,200,300);s.Land(200,300,430,260);s.Land(430,260,660,300);s.Land(660,300,1000,300);break;
            default:s.Land(0,300,1000,300);break;
        }
        if(shape>=4){s.Shelf(140,195,150);s.Shelf(420,145,155);s.Shelf(720,205,140);}
        else if(shape is 2 or 3)s.Shelf(480,175,175);
    }

    private static void ForestTerrain(RoomTerrainSocket s,int shape)
    {
        switch(shape)
        {
            case 0:case 4:case 6:
                s.Land(0,300,250,430);s.Land(250,430,730,430);s.Land(730,430,1000,300);
                s.Shelf(80,220,120);s.Shelf(270,145,160);s.Shelf(690,170,160);s.Shelf(875,215,90);
                s.Moving(470,200,130,shape==6?ActPlatformMotion.Falling:ActPlatformMotion.Horizontal,35);
                s.Mechanism(ActMechanismKind.Mushroom,580,430);break;
            case 1:case 7:
                s.Land(0,300,300,225);s.Land(300,225,650,200);s.Land(650,200,1000,300);
                s.Shelf(120,140,120);s.Shelf(375,65,145);s.Shelf(710,100,155);
                s.Moving(555,85,100,ActPlatformMotion.Vertical,20);break;
            case 2:
                s.Land(0,300,180,400);s.Land(180,400,780,400);s.Land(780,400,1000,300);
                s.Shelf(70,220,100);s.Shelf(235,125,155);s.Shelf(650,130,160);
                s.Shelf(430,245,155);s.Moving(470,70,110,ActPlatformMotion.Falling);break;
            case 3:case 5:
                s.Land(0,300,270,300);s.Land(270,300,760,400);s.Land(760,400,1000,300);
                s.Shelf(90,195,140);s.Shelf(290,100,135);s.Shelf(700,190,135);
                s.Moving(500,135,140,ActPlatformMotion.Horizontal,45);
                s.Moving(865,215,100,ActPlatformMotion.Vertical,25);break;
            default:
                s.Land(0,300,320,410);s.Land(320,410,750,410);s.Land(750,410,1000,300);
                s.Shelf(70,195,120);s.Shelf(300,115,150);s.Shelf(725,170,140);
                s.Moving(510,165,145,shape==9?ActPlatformMotion.Falling:ActPlatformMotion.Vertical,35);
                s.Mechanism(ActMechanismKind.Mushroom,360,410);break;
        }
    }

    private static void VolcanoTerrain(RoomTerrainSocket s,int shape,int state)
    {
        switch(shape)
        {
            case 0:case 1:case 9:
                s.Land(0,300,270,220);s.Land(270,220,600,220);s.Land(600,220,1000,300);
                s.Shelf(710,130,160);
                if(shape==9)s.Mechanism(state==0?ActMechanismKind.Rock:ActMechanismKind.Eruption,460,220,55);break;
            case 2:case 6:
                s.Land(0,300,240,420);s.Land(240,420,740,420);s.Land(740,420,1000,300);
                s.Shelf(90,205,120);s.Shelf(305,120,160);s.Shelf(705,185,150);
                s.Moving(505,165,135,ActPlatformMotion.Vertical,35);
                s.Mechanism(ActMechanismKind.Updraft,410,420);break;
            case 3:case 8:
                s.Land(0,300,365,300);s.Land(535,300,1000,300);
                s.Shelf(380,225,135);
                s.Mechanism(ActMechanismKind.Lava,365,460,170);
                if(shape==8){s.Moving(690,185,150,ActPlatformMotion.Collapse);s.Shelf(160,195,125);}
                else s.Moving(715,180,120,ActPlatformMotion.Horizontal,30);break;
            case 4:case 5:
                s.Land(0,300,240,260);s.Land(240,260,700,260);s.Land(700,260,1000,300);
                s.Shelf(80,145,120);s.Shelf(660,145,165);
                s.Mechanism(shape==4 && state==0?ActMechanismKind.Eruption:ActMechanismKind.Rock,445,260,60);break;
            case 7:
                s.Land(0,300,300,330);s.Land(300,330,700,330);s.Land(700,330,1000,300);
                s.Shelf(90,200,135);s.Shelf(685,160,160);
                s.Moving(365,170,140,ActPlatformMotion.Horizontal,30);
                s.Mechanism(ActMechanismKind.Updraft,535,330);break;
        }
    }
}
