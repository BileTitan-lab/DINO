using System.Drawing;

namespace DinoDash.Desktop;

internal sealed record RoomTransitionDefinition(string Id,string Name,int Shape,float[] RecoverySockets);

// Short, hand-authored passages share level entrance/exit heights. The run
// chooses a whole profile, independently of its combat room and reward doors.
internal static class ActRoomTransitions
{
    public static readonly RoomTransitionDefinition[] Pool =
    [
        new("stone-spine","风脊短坡",0,[40,180,335]),
        new("brook-hollow","浅溪凹地",1,[40,180,335]),
        new("broken-bridge","断桥碎岩",2,[40,180,335]),
        new("boulder-lift","浮石渡道",3,[40,180,335]),
        new("sheltered-cave","背风洞道",4,[40,180,335]),
        new("forked-gallery","双层石廊",5,[40,180,335]),
        new("terrace-trail","天然石阶",6,[40,180,335]),
        new("sunken-pocket","沉岩回坡",7,[40,180,335]),
        new("wind-arch","风口孤台",8,[95,210,265]),
        new("quiet-clearing","开阔补给坪",9,[70,180,290])
    ];
    private static RoomTransitionDefinition[] Themed(string prefix,string[] names)=>names.Select((name,index)=>
        new RoomTransitionDefinition(prefix+"-"+index,name,index,Pool[index].RecoverySockets.ToArray())).ToArray();
    public static readonly RoomTransitionDefinition[] WetlandPool=Themed("marsh-passage",
        ["芦苇缓坡","浅水苇滩","沉木小径","林间沙洲","苇根短廊","溪岸小路","河滩石阶","湿地回坡","雾中苇坪","静水补给地"]);
    public static readonly RoomTransitionDefinition[] ForestPool=Themed("forest-passage",
        ["巨根短脊","菌光凹谷","断枝渡道","藤架升降台","盘根洞道","树冠双层道","树根阶梯","沉根竖井","叶影孤台","林荫补给坪"]);
    public static readonly RoomTransitionDefinition[] VolcanoPool=Themed("volcano-passage",
        ["余烬坡脊","冷岩凹地","熔隙渡桥","浮岩热流","黑曜短廊","岩层双道","灰岩阶梯","沉岩回坡","断岩孤台","冷灰补给坪"]);
    public static IReadOnlyList<RoomTransitionDefinition> PoolFor(int chapter)=>chapter switch
    {1=>WetlandPool,2=>Pool,3=>ForestPool,4=>VolcanoPool,_=>throw new ArgumentOutOfRangeException(nameof(chapter))};

    public static void Select(ChapterRoomRun run)
    {
        var choices=PoolFor(run.Chapter).Where(p=>p.Id!=run.TransitionId).ToArray();
        run.TransitionId=choices[run.RandomFor(0x72719).Next(choices.Length)].Id;
    }

    public static RoomTransitionDefinition Find(string id)=>Pool.Concat(WetlandPool).Concat(ForestPool).Concat(VolcanoPool).First(p=>p.Id==id);

    public static void Build(ActLevelLayout map,string id)
    {
        var profile=Find(id);
        float root=ChapterRoomTuning.FirstAreaEnd;
        void Ground(string name,float left,float right,float y0,float y1)=>
            map.Terrain.Add(new(profile.Id+":"+name,root+left,root+right,y0,y1));
        void Platform(float left,float y,float width)=>map.Solids.Add(new(root+left,y,width,14));
        if(id.StartsWith("marsh-passage"))
        {
            float dip=profile.Shape%3==0?320:profile.Shape%3==1?275:300;
            Ground("湿地入口",0,100,300,dip);Ground("湿地中段",100,260,dip,dip);
            Ground("湿地出口",260,360,dip,300);
            if(profile.Shape is 2 or 3 or 5 or 8)Platform(120,180,120);
            return;
        }
        switch(profile.Shape)
        {
            case 0:
                Ground("登脊坡",0,100,300,245);Ground("石脊",100,240,245,245);
                Ground("背坡",240,360,245,300);break;
            case 1:
                Ground("溪口坡",0,80,300,360);Ground("溪底",80,260,360,360);
                Ground("溪出口",260,360,360,300);
                Platform(40,205,100);Platform(225,210,110);break;
            case 2:
                Ground("近岸",0,65,300,300);Ground("桥中岩",145,225,300,300);
                Ground("远岸",305,360,300,300);
                Platform(76,275,58);Platform(238,260,56);break;
            case 3:
                Ground("浮石下路",0,360,300,300);
                Platform(30,205,100);Platform(250,205,100);
                map.MovingPlatforms.Add(new(new(root+146,180,86,14),ActPlatformMotion.Vertical,40,3.5f));break;
            case 4:
                Ground("洞底",0,360,300,300);
                map.Solids.Add(new(root+45,112,270,44));break;
            case 5:
                Ground("下层入口",0,85,300,380);Ground("下层廊道",85,270,380,380);
                Ground("汇合坡",270,360,380,300);
                Platform(15,230,145);Platform(196,220,144);break;
            case 6:
                Ground("阶前",0,60,300,300);Ground("一级坡",60,100,300,270);
                Ground("一级台",100,170,270,270);Ground("二级坡",170,210,270,240);
                Ground("二级台",210,285,240,240);Ground("回落坡",285,360,240,300);break;
            case 7:
                Ground("沉岩入口",0,60,300,390);Ground("低谷",60,195,390,390);
                Ground("陡回坡",195,280,390,260);Ground("回坡顶",280,310,260,260);
                Ground("出口坡",310,360,260,300);break;
            case 8:
                Ground("近风口",0,100,300,300);Ground("孤台前坡",100,190,300,260);
                Ground("孤台",190,240,260,260);Ground("孤台后坡",240,330,260,300);
                Ground("远风口",330,360,300,300);
                Platform(20,190,55);Platform(290,185,55);break;
            case 9:
                Ground("补给石坪",0,360,300,300);break;
        }
        if(id.StartsWith("forest-passage") && profile.Shape==9)
            Platform(90,185,165);
        if(id.StartsWith("volcano-passage") && profile.Shape==2)
        {
            map.Mechanisms.Add(new(ActMechanismKind.Lava,root+65,475,80));
            map.Mechanisms.Add(new(ActMechanismKind.Lava,root+225,475,80));
        }
    }

    public static IEnumerable<float> RecoveryPositions(ActLevelLayout map,string id)
    {
        foreach(float offset in Find(id).RecoverySockets)
        {
            float x=ChapterRoomTuning.FirstAreaEnd+offset;
            if(ActSecondMap.GroundAt(map,x) is not float floor)continue;
            var display=new RectangleF(x-26,floor-54,52,54);
            if(map.Solids.Any(s=>s.IntersectsWith(display)) ||
                map.MovingPlatforms.Any(p=>p.Origin.IntersectsWith(display)))continue;
            yield return x;
        }
    }
}
