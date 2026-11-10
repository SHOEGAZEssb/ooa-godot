
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsTalkNpcPredicates()
    {
        var root = StartSeasonsAfterImpa(false);
        try
        {
            var session = root._seasonsOpening!;
            var app = new ApplicationValidationFixture(root);
            session.Memory.SetField("wRoomStateModifier",0);
            // miscNpcs.s:conditionalHoronNPCLookupTable, independently transcribed.
            int[][] lady = [[0], [1,2,3,9], [4], [5], [6,7], [8], [10]];
            int[] ladyRooms = [0xc6, 0xd6, 0xe6, 0xd6, 0xd9, 0xd6, 0xc6];
            int[][] man = [[0,1,10], [2], [3], [9], [4], [5,6,7,8]];
            int[] manRooms = [0xd6, 0xc6, 0xf8, 0xe9, 0xd9, 0xf7];
            for (int stage = 0; stage <= 10; stage++)
            {
                Stage(stage);
                foreach (var family in new (int Id,int Base,int[][] Stages,int[] Rooms)[] {
                    (0x2d,0x80,lady,ladyRooms),(0x37,0x80,man,manRooms),
                    (0x3c,0x80,[Enumerable.Range(0,11).ToArray()],[0xc5]),
                    (0x3d,0x80,[Enumerable.Range(0,11).ToArray()],[0xe7]),
                    (0x3e,0,[[0,1,2],[3],[9],[4,5,10],[6,7,8]],[0xe9,0xe9,0xf6,0xe9,0xd6]),
                    (0x3e,0x10,[[0,1],[2,3,9,4,5,10],[6,7],[8]],[0xc5,0xd6,0xe9,0xe9]),
                    (0x3e,0x20,[Enumerable.Range(0,11).ToArray()],[0xc6]),
                    (0x80,0,[[0,1,2,3,9],[4],[5,6,7,8],[10]],[0xf7,0xe9,0xc6,0xf7])})
                for (int subid = 0; subid < family.Stages.Length; subid++)
                {
                    Enter(0, family.Rooms[subid]);
                    bool visible = Actors().Any(actor => actor.Npc.Record.Id == family.Id && actor.Npc.Record.SubId == (subid | family.Base));
                    FailIf(visible != family.Stages[subid].Contains(stage),
                        $"Seasons ${family.Id:x2}:${subid|family.Base:x2} stage ${stage:x2}: native Horon visibility differs.");
                }
            }
            foreach (var sample in new (int Essences,int Flag,int State)[] { (0,-1,0), (8,-1,1), (0x80,-1,2), (0,0x1f,3), (0,0x28,4) })
            {
                Stage(0); Essences(sample.Essences);
                if (sample.Flag >= 0) session.Save.SetGlobalFlag(sample.Flag);
                Enter(1, 0x9f);
                var actors = Actors().Where(actor => actor.Npc.Record.Id == 0x39).ToArray();
                FailIf(actors.Length != 1 || actors[0].Npc.Record.SubId != sample.State,
                    $"Seasons $1:9f treasure hunters: expected native Sunken stage ${sample.State:x2}.");
                session.Save.SetGlobalFlag(0x28, !session.Save.HasGlobalFlag(0x28)); app.Step(2, Vector2.Zero);
                FailIf(actors[0].Finished, "Sunken visibility is an initialization predicate, not a live deletion gate.");
                session.Save.SetGlobalFlag(0x28,sample.State==4);
                foreach(var family in new (int Id,int Base,int Group,int[] Rooms)[]{
                    (0x36,0x80,0,[0x5d,0x4d,0x4d,0x5d,0x4d]),
                    (0x2e,0,0,[0x5d,0x5d,0x6d,0x6d,0x5d]),
                    (0x3e,0x30,0,[0x5e,0x5e,0x5e,0x5e,0x5e]),
                    (0x8a,0,1,[0x9d,0x5e,0x9d,0x9d,0xd8])})
                for(int subid=0;subid<5;subid++)
                {
                    int group=family.Id==0x8a&&subid is 1 or 4?0:family.Group;
                    Enter(group,family.Rooms[subid]);
                    FailIf(Actors().Any(actor=>actor.Npc.Record.Id==family.Id&&actor.Npc.Record.SubId==(subid|family.Base))!=(subid==sample.State),
                        $"Seasons Sunken ${family.Id:x2}:${subid|family.Base:x2} stage ${sample.State:x2}: visibility differs.");
                }
            }
            foreach(var sample in new (int Essences,bool Kidnapped,int? Subid)[]{(0,false,null),(8,false,1),(0x80,false,2),(2,true,null),(4,true,3)})
            {
                Stage(0);Essences(sample.Essences);session.Save.SetGlobalFlag(0x1f,sample.Kidnapped);
                Enter(5,0xbd);var diver=Actors().Where(actor=>actor.Npc.Record.Id==0x8d).ToArray();
                FailIf(diver.Length!=(sample.Subid.HasValue?1:0)||sample.Subid.HasValue&&diver[0].Npc.Record.SubId!=sample.Subid,
                    "Master Diver talk variants require the native third-essence gate before Sunken-stage selection.");
            }
            foreach (bool destroyed in new[] { false, true })
            {
                Stage(0); Essences(8); session.Save.SetGlobalFlag(0x16, destroyed);
                foreach (int room in new[] { 0x5e, 0x6e })
                {
                    Enter(0, room);
                    FailIf(Actors().Any(actor => actor.Npc.Record.Id == 0x3e && actor.Npc.Record.SubId == 0x31) != (room == (destroyed ? 0x6e : 0x5e)),
                        $"Seasons $0:{room:x2} boy $3e:$31: Moblin's Keep room predicate differs.");
                }
                foreach (var room in new[] { (Group:0, Id:0x5d), (Group:1, Id:0xb6) })
                {
                    Enter(room.Group, room.Id);
                    FailIf(Actors().Any(actor => actor.Npc.Record.Id == 0x8b && actor.Npc.Record.SubId == 1) != (room.Group == (destroyed ? 0 : 1)),
                        $"Seasons ${room.Group:x1}:{room.Id:x2} wife $8b:$01: Moblin's Keep room predicate differs.");
                }
            }
            Stage(0); Enter(1, 3);
            FailIf(Actors().Count(actor => actor.Npc.Record.Id == 0x30) != 2, "Seasons $1:03 needs both source-ordered volcano Subrosians.");
            session.Save.SetGlobalFlag(0x28); Enter(1, 3);
            FailIf(Actors().Any(actor => actor.Npc.Record.Id == 0x30), "Finished-game stubScript must delete $30:$15/$16 during native initialization.");

            // ambi.s:func_740a compares the essence byte, not its population count.
            foreach (bool linked in new[] {false,true})
            foreach (var sample in new (int Essences,bool Pirates,int Subid,int Group,int Room)[]
                {(0,false,0,1,0xb3),(4,false,1,0,0x5e),(0x10,false,2,0,0xfb),(0,true,4,1,0x8a)})
            {
                Stage(0);Essences(sample.Essences);session.Save.SetGlobalFlag(0x13,sample.Pirates);
                session.Memory.SetField("wIsLinkedGame",(byte)(linked?1:0));
                Enter(sample.Group,sample.Room);
                FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0xb8 && actor.Npc.Record.SubId==sample.Subid)!=linked,
                    $"Seasons Ambi ${sample.Subid:x2}: linked stage visibility differs.");
            }
            foreach (bool asked in new[]{false,true})
            foreach (bool rescued in new[]{false,true})
            {
                Stage(0);session.Save.SetGlobalFlag(0x22,asked);session.Save.SetGlobalFlag(0x23,rescued);
                Enter(0,0xc6);
                foreach(int id in new[]{0x9d,0x2a})
                    FailIf(Actors().Any(actor=>actor.Npc.Record.Id==id)!=(asked&&!rescued),
                        $"Seasons ${id:x2} panic dialogue: asked/rescued visibility differs.");
                Enter(1,0x82);
                FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0x9d)==(asked&&!rescued),
                    "Seasons Impa $9d:$01 must complement the panic Impa $05 gate.");
            }
            foreach(int stage in new[]{6,7,8,9,10})
            {
                Stage(stage);Enter(1,0x82);
                FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0x9d)!=(stage is 8 or 9),
                    $"Seasons Impa $9d:$01 linked/finished stage ${stage:x2} differs.");
            }
            foreach(bool left in new[]{false,true})
            {
                Stage(0);session.Save.SetGlobalFlag(0x13,left);
                Enter(1,0x72);FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0x40)==left,"Roof pirate must disappear when the pirates leave.");
                Enter(1,0x8b);FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0x42)==left,$"Upstairs Subrosian must disappear when the pirates leave: left={left}, flag={session.Save.HasGlobalFlag(0x13)}, room={session.Room.Group:x1}:{session.Room.Id:x2}, actors={string.Join(',',Actors().Select(actor=>$"{actor.Npc.Record.Id:x2}:{actor.Npc.Record.SubId:x2}"))}.");
                Enter(1,0x8a);FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0x42)!=left,"Downstairs Subrosian must appear when the pirates leave.");
            }
            foreach(bool docked in new[]{false,true})
            {
                Stage(0);session.Save.SetRoomFlag(0,0xe2,0x40,docked);session.Save.SetGlobalFlag(0x17,docked);
                Enter(0,0xe2);FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0xb1)!=docked,"Ship $b1:$10 needs its source room-flag $40 docking gate.");
                Enter(0,0xf2);FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0xb1)!=docked,"Ship $b1:$11 needs GLOBALFLAG_PIRATE_SHIP_DOCKED.");
            }
            foreach(bool finished in new[]{false,true})
            {
                Stage(finished?10:0);Enter(0,0xd6);
                foreach(int id in new[]{0xa5,0x4e})FailIf(Actors().Any(actor=>actor.Npc.Record.Id==id)!=finished,
                    $"Seasons ending NPC ${id:x2} must sample GLOBALFLAG_FINISHEDGAME on initialization.");
                if(finished)
                {
                    FailIf(session.Memory.ReadWramByte(0xcc1d)!=0xa5||session.Memory.ReadField("wLoadedTreeGfxIndex")!=0xa5,
                        "Ending Din must publish her source extra-graphics request and loaded-tree slot.");
                }
            }

            NativeNpcRoomEntity[] Actors() => session.Entities.EntityAdapters<NativeNpcRoomEntity>().Where(actor => !actor.Finished).ToArray();
            void Enter(int group, int room) { EnterSeasonsNpcRoom(session,group,room); app.Step(4, Vector2.Zero); }
            void Stage(int stage)
            {
                foreach (int flag in new[] { 0x18,0x19,0x1e,0x1f,0x23,0x28,0x16,0x22 }) session.Save.SetGlobalFlag(flag, false);
                session.Save.SetLinkedGame(stage is >= 6 and <= 9);
                session.Memory.SetField("wIsLinkedGame",(byte)(stage is >=6 and <=9?1:0));
                Essences(stage switch { 2=>1, 3=>2, 4=>0x1f, 5=>0x80, >=6 and <=9=>2, _=>0 });
                int selected = stage switch { 1=>0x18, 6=>0x1e, 7=>0x19, 8=>0x1f, 10=>0x28, _=>-1 };
                if (selected >= 0) session.Save.SetGlobalFlag(selected);
                FailIf(SeasonsNpcStoryState.HoronStage(session.Save,session.Memory) != stage, $"Seasons checkNPCStage fixture failed to select ${stage:x2}.");
            }
            void Essences(int value)
            {
                session.Save.WriteField("wEssencesObtained",(byte)value);
                session.Save.WriteField("wObtainedTreasureFlags",(byte)((session.Save.ReadField("wObtainedTreasureFlags",8)&0xfe)|(value!=0?1:0)),8);
            }
        }
        finally { root.Free(); }
    }

    private void ValidateSeasonsTalkNpcConversations()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!; var app = new ApplicationValidationFixture(root);
                foreach (var sample in new (int Group,int Room,int Id,int Subid,int Text)[] {
                    (0,0xc6,0x2d,0x80,0x1600),
                    (1,0x9f,0x39,0,0x1b00), (1,3,0x30,0x15,0x3e01), (1,0x72,0x40,9,0x3a0f),
                    (1,0x85,0xc2,0,0x3101), (1,0x9d,0x8a,0,0x1d00),
                    (0,0xc5,0x3c,0x80,0x1200),(0,0xe7,0x3d,0x80,0x1000),(0,0xf7,0x80,0,0x1100),
                    (0,0x5d,0x36,0x80,0x1900),(0,0x5d,0x2e,0,0x1c00),(1,0xb6,0x8b,0,0x1800),
                    (0,0xe9,0x3e,0,0x1500),(0,0xc5,0x3e,0x10,0x1400),
                    (1,0x1a,0x32,0,0x2706),(1,6,0x33,0x80,0x3e05),(1,0x14,0x33,0x83,0x3e07),
                    (1,0xa0,0x30,0x0c,0x2b0f),(5,0xc9,0x3b,2,0x3702),(5,0xc8,0x3b,0x14,0x3704),
                    (1,0x8b,0x42,0,0x3a10),(4,0x16,0x38,0,0x3300),
                    (0,0xd6,0xa5,8,0x3d18),(0,0xd6,0x4e,0x0a,0x3d19),
                    (0,0x71,0x27,2,0x5208),(0,0xb7,0x25,0,0x0b34),(0,0xb7,0x25,1,0x0b34),(0,0xb7,0x26,1,0x0b36),
                    (5,0xbd,0x8d,1,0x3402) })
                {
                    session.Save.SetGlobalFlag(0x28,sample.Id is 0xa5 or 0x4e);
                    if(sample.Id is 0x25 or 0x26)session.Save.SetRoomFlag(0,0xb7,0x40,sample.Subid!=0);
                    if(sample.Id==0x8d){session.Save.WriteField("wEssencesObtained",8);session.Save.WriteField("wObtainedTreasureFlags",1,8);}
                    EnterSeasonsNpcRoom(session,sample.Group,sample.Room); app.Step(5, Vector2.Zero, batched:batched);
                    var actor = session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(entity =>
                        !entity.Finished && entity.Npc.Record.Id == sample.Id && entity.Npc.Record.SubId == sample.Subid);
                    // Approach from a real floor point outside the actor's blocking box.
                    var approaches=new[]{Vector2I.Up,Vector2I.Left,Vector2I.Down,Vector2I.Right}
                        .Select(direction=>(Direction:direction,Radius:(int)(direction.X==0?actor.Npc.LinkBlockingBounds.Size.Y:actor.Npc.LinkBlockingBounds.Size.X)/2))
                        .Where(approach=>Enumerable.Range(approach.Radius+2,5).All(distance=>
                        {
                            Vector2 point=actor.Npc.Position-(Vector2)approach.Direction*distance;
                            return point.X>=6 && point.Y>=6 && point.X<session.Room.Width-6 && point.Y<session.Room.Height-6 && !session.Collision.Collides(point) &&
                                new[]{new Vector2(-5,-2),new Vector2(5,5)}.All(offset=>session.Room.GetTerrainInfo(point+offset).Type is not
                                    (TerrainType.Hole or TerrainType.WarpHole or TerrainType.Water or TerrainType.Lava));
                        })).ToArray();
                    FailIf(approaches.Length==0,$"Seasons ${sample.Group:x1}:{sample.Room:x2} has no unobstructed NPC approach through its native geometry at {actor.Npc.Position}.");
                    var direction=approaches[0].Direction;Vector2 start=actor.Npc.Position-(Vector2)direction*(approaches[0].Radius+6);
                    session.Link.WarpTo(start);session.Link.Face(direction);
                    for(int update=0;update<20&&actor.FindTalkTarget(session.Link) is null;update++)
                        app.Step(1,(Vector2)direction,batched:batched);
                    FailIf(actor.Npc.BlocksLinkCenter(session.Link.PrecisePosition), "NPC approach must stop outside its native blocking box.");
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        app.Step(1, Vector2.Zero, ["attack"], ["attack"], batched:batched);
                        for (int update = 0; update < (sample.Id==0x27?100:sample.Id==0xa5?40:8) && !session.Dialogue.IsOpen; update++) app.Step(1, Vector2.Zero, batched:batched);
                        int expected = repeat==1?(sample.Id==0x8a?0x1d01:sample.Id==0x27?0x5209:sample.Id==0x3e&&sample.Subid==0x10?0x1401:sample.Text):sample.Text;
                        FailIf(!session.Dialogue.IsOpen || session.Dialogue.SourceTextId != expected,
                            $"Seasons ${sample.Group:x1}:{sample.Room:x2} ${sample.Id:x2}:${sample.Subid:x2} repeat {repeat}: expected TX_{expected:x4}, got {session.Dialogue.SourceTextId:x4}; Link={session.Link.Position}, NPC={actor.Npc.Position}, target={actor.FindTalkTarget(session.Link)!=null}, mask=${session.DisabledObjects:x2}.");
                        Vector2 held = session.Link.PrecisePosition; app.Step(4, Vector2.Right, batched:batched);
                        FailIf(session.Link.PrecisePosition != held, "NPC dialogue must freeze Link in both scheduler modes.");
                        session.Dialogue.Close(); app.Step(sample.Id==0x27?48:sample.Id==0x4e?16:5, Vector2.Zero, batched:batched);
                    }
                }
            }
            finally { root.Free(); }
        }
    }

    private void ValidateSeasonsTalkNpcNativeUpdates()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched);
                NativeNpcRoomEntity Actor(int id)=>session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor=>!actor.Finished&&actor.Npc.Record.Id==id);
                EnterSeasonsNpcRoom(session,0,0xe9);Step(5);var boy=Actor(0x3e);
                FailIf(session.Entities.EntityAdapters<SeasonsBoyPlaymateRoomEntity>().Count()!=2,
                    "miscBoyNpcs.s $3e -> ball $83 -> dog $82 must allocate separate native interaction slots.");
                boy.WriteByte(0x76,1);Step(1);
                FailIf(session.Memory.ReadWramByte(0xcceb)!=1 || boy.ReadByte(0x45)!=1,
                    "Boy throw boundary must publish $cceb=$01 on its native slot visit.");
                bool returned=false;
                for(int update=0;update<400;update++)
                {Step(1);if(boy.ReadByte(0x45)==0){returned=true;break;}}
                FailIf(!returned || boy.ReadByte(0x76) is <0x78 or >0xb7,
                    "Dog must return the ball, publish $cceb=$02, and reset the boy's source random timer.");
                boy.WriteByte(0x76,1);Step(1);
                FailIf(boy.ReadByte(0x45)!=1,"Boy/dog handoff must repeat after completion.");

                EnterSeasonsNpcRoom(session,1,0x25);Step(5);var subrosian=Actor(0xc8);
                subrosian.WriteByte(0x78,1);Step(1);
                FailIf(subrosian.ReadByte(0x77)!=1 || session.Entities.EntityAdapters<SeasonsNpcBoomerangRoomEntity>().Count()!=1,
                    "Boomerang Subrosian must allocate $c9 and publish its outstanding-child signal.");
                Step(90);
                FailIf(subrosian.ReadByte(0x77)!=0 || session.Entities.EntityAdapters<SeasonsNpcBoomerangRoomEntity>().Any() ||
                    subrosian.ReadByte(0x78) is <1 or >0x7b,
                    "Native $c9 must return, release its slot, and rearm its producer.");

                session.Memory.SetField("wRoomStateModifier",0);
                EnterSeasonsNpcRoom(session,0,0xc6);Step(5);boy=Actor(0x3e);
                var states=new System.Collections.Generic.HashSet<int>();bool signalled=false,released=false;
                for(int update=0;update<600;update++)
                {
                    Step(1);states.Add(boy.ReadByte(0x45));
                    if(session.Memory.ReadWramByte(0xccc3)!=0)signalled=true;
                    if(signalled&&session.Memory.ReadWramByte(0xccc3)==0)released=true;
                    if(states.Count==13)break;
                }
                FailIf(states.Count!=13||!signalled||!released,
                    $"Spring boy/flower must traverse $00-$0c and complete the $ccc3 producer/consumer cycle; got {string.Join(',',states.Order())}.");

                EnterSeasonsNpcRoom(session,0,0x5d);Step(5);
                for(int update=0;update<600&&!session.Entities.EntityAdapters<SeasonsNpcBubbleRoomEntity>().Any();update++)Step(1);
                FailIf(session.Entities.EntityAdapters<SeasonsFickleGirlEffectRoomEntity>().Count()!=1 ||
                    !session.Entities.EntityAdapters<SeasonsNpcBubbleRoomEntity>().Any(),
                    "Fickle girl must retain a separate wand and produce live PART$32 bubbles through the part phase.");
                var bubble=session.Entities.EntityAdapters<SeasonsNpcBubbleRoomEntity>().First();
                var swordApproach=new[]{Vector2I.Up,Vector2I.Left,Vector2I.Down,Vector2I.Right}.First(direction=>
                {
                    Vector2 point=bubble.Position-(Vector2)direction*16;
                    return point.X>=6&&point.Y>=6&&point.X<session.Room.Width-6&&point.Y<session.Room.Height-6&&
                        !session.Collision.Collides(point)&&session.Room.GetTerrainInfo(point).Hazard==HazardType.None;
                });
                session.Link.WarpTo(bubble.Position-(Vector2)swordApproach*16);session.Link.Face(swordApproach);
                session.Inventory.GiveTreasure(TreasureId.Sword,1);session.Inventory.EquipB(TreasureId.Sword);
                app.Step(1,Vector2.Zero,["item"],["item"],batched:batched);Step(40);session.Inventory.EquipB(0);
                FailIf(!bubble.Finished,"A bubble collision must enter its source pop animation and free the part.");

                EnterSeasonsNpcRoom(session,1,0x84);
                var birdOwner=session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor=>actor.Npc.Record.SubId==0);
                var bird=(KnowItAllBirdCharacter)birdOwner.Npc;
                FailIf(session.Entities.EntityAdapters<NativeNpcRoomEntity>().Count()!=10,
                    "Know-It-All Birds' Hut must construct all ten source-ordered birds.");
                int initializationCalls=session.Random.Calls;Step(1);
                FailIf(!bird.Initialized||bird.TurnCounter!=30||session.Random.Calls!=initializationCalls+10,
                    "Seasons bird state zero must initialize its native counter and consume one shared RNG byte per source slot.");
                int idleCalls=session.Random.Calls;Step(29);
                FailIf(bird.TurnCounter!=1||session.Random.Calls!=idleCalls,"Bird idle must retain the source 30-update random boundary.");
                Step(1);
                FailIf(bird.TurnCounter!=30||session.Random.Calls!=idleCalls+10,
                    "All ten native bird slots must consume exactly one shared RNG byte on the turn boundary.");
                session.Link.WarpTo(bird.Position+new Vector2(22,0));session.Link.Face(Vector2I.Left);
                FailIf(session.Collision.Collides(session.Link.Position),"Bird conversation must start from the hut's walkable floor.");
                app.Step(10,Vector2.Left,batched:batched);
                for(int repeat=0;repeat<2;repeat++)
                {
                    app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(4);
                    FailIf(!session.Dialogue.ChoiceActive||session.Dialogue.SourceTextId!=0x3200||!bird.Talking||session.DisabledObjects!=0x91,
                        "Seasons bird TX_3200 must open its native question, hop during text, and publish mask $91.");
                    session.Dialogue.SubmitChoiceForValidation(repeat==0?1:0);
                    if(repeat==1)
                    {
                        Step(32);
                        FailIf(!session.Dialogue.IsOpen||session.Dialogue.SourceTextId!=0x320a,"Yes must select TX_320a after the source 30-update wait.");
                        session.Dialogue.Close();
                    }
                    Step(4);
                    FailIf(session.DisabledObjects!=0||bird.Talking||bird.ZFixed!=0,
                        "Bird completion must release mask $91 and its native talking/hop state.");
                }
                app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(4);
                EnterSeasonsNpcRoom(session,1,0x85);Step(4);
                FailIf(session.DisabledObjects!=0||session.Dialogue.IsOpen,
                    "Leaving a talking bird must cancel its script/dialogue and release the shared object mask.");

                session.Memory.SetField("wIsLinkedGame",1);
                EnterSeasonsNpcRoom(session,1,0xb3);Step(1);var ambi=Actor(0xb8);
                FailIf(ambi.Npc.ZIndex!=ObjectDrawPriority.FixedHighPriorityZIndex,
                    "ambi.s state $00 must retain objectSetVisible80 until the next native visit.");
                Step(1);
                FailIf(ambi.Npc.ZIndex!=ObjectDrawPriority.RelativeToLink(ambi.Npc.Position.Y,session.Link.Position.Y),
                    "Ambi state $01 must resume native priority relative to Link.");
            }
            finally{root.Free();}
        }
    }

    private void ValidateSeasonsTalkNpcBindings()
    {
        // Positioned source rows in mainData.s, including mutually selected story alternatives.
        const string placements="""
            0:4d 3681,3682,3684
            0:5d 3680,3683,8b01,8b02,8b03,8b04,2e00,2e01,2e04
            0:5e 3e30,3e31,3e32,3e33,3e34,8a01,b801
            0:6d 2e02,2e03
            0:6e 3e31
            0:71 2702
            0:b7 2500,2501,2601
            0:c5 3c80,3e10
            0:c6 3e20,2d80,2d86,3781,8002,9d05,2a0b
            0:d6 3780,2d81,2d83,2d85,3e04,3e11,a508,4e0a
            0:d8 8a04
            0:d9 2d84,3784
            0:e2 b110
            0:e6 2d82
            0:e7 3d80
            0:e9 3783,8001,3e00,3e01,3e03,3e12,3e13
            0:f2 b111
            0:f6 3e02
            0:f7 8000,8003,3785
            0:f8 3782
            0:fb b802
            1:03 3015,3016
            1:05 3017,3018
            1:06 3380
            1:11 3201
            1:14 3383
            1:15 3019
            1:17 3202
            1:1a 3200
            1:20 301a
            1:22 301b
            1:25 301c,c800,3203
            1:26 301d
            1:31 301e
            1:37 3204
            1:41 301f
            1:42 3020
            1:43 300e
            1:47 3205
            1:52 3021
            1:54 300f
            1:55 3022
            1:56 300a,300b
            1:65 3007
            1:66 3008
            1:67 3024
            1:72 4009
            1:75 3009
            1:76 3006
            1:82 9d01
            1:84 2a08,2a09,2a06,2a07,2a04,2a05,2a02,2a03,2a00,2a01
            1:85 c200
            1:8a 4201,b804
            1:8b 4200
            1:8e 3004,3005
            1:91 e500,e501
            1:96 3023
            1:9d 8a00,8a02,8a03
            1:9e 3002,3003
            1:9f 3900,3901,3902,3903,3904
            1:a0 300c
            1:a1 300d
            1:af 3786
            1:b3 b800
            1:b6 8b00,8b01
            4:16 3800
            4:6b 3800
            5:30 3800
            5:bd 8d01,8d02,8d03
            5:c8 3b14,3b15
            5:c9 3b20,3b11,3b02,3b13
            5:d4 b112,b113,b114,b115,b116
            """;
        var rows=placements.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)
            .Select(line=>line.Split(' ')).ToArray();
        var seen=new System.Collections.Generic.HashSet<string>();
        var root=StartSeasonsAfterImpa(false);
        try
        {
            var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
            session.Memory.SetField("wRoomStateModifier",0);
            session.Save.SetRoomFlag(0,0xe2,0x40);session.Save.SetGlobalFlag(0x17);
            for(int stage=0;stage<14;stage++)
            {
                foreach(int flag in new[]{0x13,0x16,0x18,0x19,0x1e,0x1f,0x22,0x23,0x28})session.Save.SetGlobalFlag(flag,false);
                session.Memory.SetField("wIsLinkedGame",(byte)(stage is >=6 and <=9 or 11 or 13?1:0));
                int essences=stage switch{2=>1,3=>2,4=>0x1f,5=>0x80,8=>4,>=6 and <=9=>2,11=>4,12=>8,13=>0x10,_=>0};
                session.Save.WriteField("wEssencesObtained",(byte)essences);
                session.Save.WriteField("wObtainedTreasureFlags",(byte)(essences!=0?1:0),8);
                int selectedFlag=stage switch{1=>0x18,6=>0x1e,7=>0x19,8=>0x1f,10=>0x28,_=>-1};
                if(selectedFlag>=0)session.Save.SetGlobalFlag(selectedFlag);
                session.Save.SetGlobalFlag(0x13,stage>=5&&stage!=9&&stage<11);session.Save.SetGlobalFlag(0x16,stage==12);
                session.Save.SetGlobalFlag(0x22,stage==8);
                session.Save.SetRoomFlag(0,0xb7,0x40,stage>=1);
                foreach(var row in rows)
                {
                    int group=Convert.ToInt32(row[0][..1],16),room=Convert.ToInt32(row[0][2..],16);
                    EnterSeasonsNpcRoom(session,group,room);app.Step(6,Vector2.Zero);
                    foreach(var actor in session.Entities.EntityAdapters<NativeNpcRoomEntity>().Where(actor=>!actor.Finished &&
                        actor is not (SeasonsMakuTreeRoomEntity or SeasonsPortalSpawnerRoomEntity or SeasonsTempleFallenTextRoomEntity)))
                    {
                        string record=$"{actor.Npc.Record.Id:x2}{actor.Npc.Record.SubId:x2}";
                        FailIf(!row[1].Split(',').Contains(record),$"Unexpected Seasons talk-only source row {row[0]} ${record}.");
                        seen.Add(row[0]+" "+record);
                        bool ordinary = actor.Npc.Record.Id is 0x2d or 0x33 or 0x36 or 0x37 or 0x38 or 0x39 or 0x3c or 0x3d;
                        FailIf(ordinary && (actor.GetType() != typeof(NativeNpcRoomEntity) ||
                            actor.Npc.Record.Implementation != NpcImplementationClassification.OrdinaryGeneric),
                            $"Seasons {row[0]} ${record}: miscNPC source family requires the common handler and imported script.");
                        FailIf(actor.Script is not {HasState:true},$"Seasons {row[0]} ${record} has no live native script lane.");
                    }
                }
            }
            var missing=rows.SelectMany(row=>row[1].Split(',').Select(record=>row[0]+" "+record)).Where(key=>!seen.Contains(key)).ToArray();
            FailIf(missing.Length!=0,$"Seasons talk-only rows never constructed under their story predicates: {string.Join(',',missing)}.");
            FailIf(seen.Count!=149,$"Expected 149 positioned talk-only source records, got {seen.Count}.");
        }
        finally{root.Free();}
    }

    private void ValidateSeasonsSokraMittensAndBooks()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched);
                NativeNpcRoomEntity[] Actors()=>session.Entities.EntityAdapters<NativeNpcRoomEntity>().Where(actor=>!actor.Finished).ToArray();
                foreach(bool summer in new[]{false,true})
                foreach(bool finished in new[]{false,true})
                {
                    session.Save.WriteField("wObtainedSeasons",(byte)(summer?2:0));session.Save.SetGlobalFlag(0x28,finished);
                    session.Save.SetRoomFlag(0,0x71,0x40);EnterSeasonsNpcRoom(session,0,0x71);Step(5);
                    FailIf(Actors().Any(actor=>actor.Npc.Record.Id==0x27)!=( !summer&&!finished),"Sokra $27:$02 summer/finished visibility differs.");
                    FailIf(session.Save.HasRoomFlag(0,0x71,0x40)!=(finished||!summer),"Sokra must preserve the source room-flag $40 set/clear order.");
                }
                session.Save.SetGlobalFlag(0x28,false);session.Save.WriteField("wObtainedSeasons",0);
                EnterSeasonsNpcRoom(session,0,0x71);Step(5);
                for(int update=0;update<64&&!session.Entities.EntityAdapters<FloatingMusicNoteRoomEntity>().Any();update++)Step(1);
                var snore=session.Entities.EntityAdapters<FloatingMusicNoteRoomEntity>().Single();
                FailIf(((NpcCharacter)snore.Node).Record.SubId!=0||((NpcCharacter)snore.Node).Record.TileBase!=0,
                    "Sokra must allocate native floating-image $a0:$00 with snore graphics, not a music note.");
                Step(70);FailIf(!snore.Finished,"A source snore must expire after 70 eligible native updates.");

                foreach(bool rescued in new[]{false,true})
                {
                    session.Save.SetRoomFlag(0,0xb7,0x40,rescued);EnterSeasonsNpcRoom(session,0,0xb7);Step(5);
                    var cat=Actors().Single(actor=>actor.Npc.Record.Id==0x25);
                    FailIf(cat.Npc.Record.SubId!=(rescued?1:0)||Actors().Any(actor=>actor.Npc.Record.Id==0x26)!=rescued,
                        "Mittens and the post-trade owner must select their original room-flag $40 placements.");
                    if(!rescued)
                    {
                        session.Memory.SetWramByte(0xcceb,1);Step(40);
                        FailIf(cat.ReadByte(0x78)!=3||session.Memory.ReadWramByte(0xcceb)!=0,
                            "Mittens must consume the shared quest signal, complete its native jump and clear $cceb.");
                    }
                }
                EnterSeasonsNpcRoom(session,1,0x91);Step(5);
                void ReadyChoice()
                {
                    for(int update=0;update<1200&&!session.Dialogue.ChoiceCursorVisible;update++)
                        app.Step(1,Vector2.Zero,update%2==0?["attack"]:[],update%2==0?["attack"]:[],batched:batched);
                    FailIf(!session.Dialogue.ChoiceCursorVisible,"Ring book prompt did not reach native choice input.");
                    Step(1);
                }
                foreach(int subid in new[]{0,1})
                {
                    var book=Actors().Single(actor=>actor.Npc.Record.Id==0xe5&&actor.Npc.Record.SubId==subid);
                    session.Link.WarpTo(book.Npc.Position+new Vector2(0,18));session.Link.Face(Vector2I.Up);
                    FailIf(session.Collision.Collides(session.Link.Position),"Ring book approach must start on the room's actual floor.");
                    app.Step(3,Vector2.Up,batched:batched);
                    for(int repeat=0;repeat<2;repeat++)
                    {
                        app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(3);
                        FailIf(!session.Dialogue.ChoiceActive||session.Dialogue.SourceTextId!=(subid==0?0x3020:0x3019)||
                            session.Dialogue.TextboxFlagsForValidation!=0x0a,"Ring book must open its source bottom-positioned non-exitable prompt.");
                        ReadyChoice();
                        app.Step(1,Vector2.Zero,["move_right"],["move_right"],batched:batched);Step(1);
                        app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(8);
                        FailIf(session.Dialogue.IsOpen||session.DisabledObjects!=0,"Ring book No must resume its retained text thread and permit another conversation.");
                    }
                    app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(3);ReadyChoice();
                    app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(3);
                    FailIf(session.Dialogue.SourceTextId!=(subid==0?0x3025:0x301a),"Ring book Read must select its source explanation/options.");
                    if(subid==0)
                    {
                        ReadyChoice();
                        for(int option=0;option<2;option++){app.Step(1,Vector2.Zero,["move_right"],["move_right"],batched:batched);Step(1);}
                        app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(8);
                        FailIf(session.Dialogue.IsOpen||book.Finished,"Book option $02 must survive both native option comparisons and fall through the earlier reset label.");
                    }
                    else
                    {
                        FailIf(!session.Dialogue.CurrentMessage.Contains("not work.",StringComparison.Ordinal),"TX_301a must retain its complete multi-page ring-secret explanation.");
                        session.Dialogue.Close();Step(4);
                    }
                }
            }
            finally{root.Free();}
        }
    }

    private void ValidateSeasonsMiscNpcState()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.Save.SetGlobalFlag(0x28, false);
                session.Save.SetGlobalFlag(0x18, false);
                session.Memory.SetField("wIsLinkedGame", 0);
                EnterSeasonsNpcRoom(session, 0, 0xe7);
                var fountain = session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor => actor.Npc.Record.Id == 0x3d);
                int address = ((0xd0 + session.Entities.NativeInteractionSlot(fountain)) << 8) | 0x40;
                app.Step(1, Vector2.Zero, batched: batched);
                // miscNPC_state0 animates without dispatching this script.
                FailIf(fountain.GetType() != typeof(NativeNpcRoomEntity) || fountain.ReadByte(0x44) != 1 ||
                    fountain.Script!.CurrentCommandIndex != 0 || fountain.ReadByte(0x66) != 0 || fountain.ReadByte(0x67) != 0,
                    "Seasons $3d:$80 must initialize in the common slot without running fountainOldManScript yet.");
                app.Step(5, Vector2.Zero, batched: batched);
                FailIf(fountain.ReadByte(0x66) != 3 || fountain.ReadByte(0x67) != 11 || fountain.Npc.TextId != 0x1000,
                    "fountainOldManScript sets native radii $03/$0b and TX_1000 before waiting for A.");
                session.Memory.SetWramByte(address + 0x0a, 0x80);
                session.Memory.SetWramByte(address + 0x0c, 0x40);
                fountain.Script.SetActorCoordinates("Self", fountain.ReadByte(0x4b), fountain.ReadByte(0x4d));
                FailIf(fountain.Npc.Position != new Vector2(fountain.ReadByte(0x4d) + 0.25f, fountain.ReadByte(0x4b) + 0.5f) ||
                    ((Node2D)fountain.Npc).Position != fountain.Npc.Position,
                    "Native coordinate-high writes must preserve the existing 8.8 fractions and project them to the node.");
                // Inject the source's retained signal; normal floor approaches/repeat input are covered by Conversations.
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    session.Memory.SetWramByte(address + 0x32, 1);
                    session.Memory.SetWramByte(address + 0x33, 0x10);
                    session.Memory.SetWramByte(address + 0x31, 1);
                    app.Step(1, Vector2.Zero, batched: batched);
                    FailIf(!session.Dialogue.IsOpen || session.Dialogue.SourceTextId != 0x1001 || fountain.ReadByte(0x71) != 0,
                        "checkabutton/showloadedtext must consume the shared byte and read live TX_1001, including its message.");
                    FailIf(!session.Dialogue.CurrentMessage.Contains("giant", StringComparison.Ordinal),
                        "A raw textID change must select TX_1001 content rather than the prior cached message.");
                    session.Dialogue.Close(); app.Step(3, Vector2.Zero, batched: batched);
                }
                session.Save.SetGlobalFlag(0x18);
                app.Step(1, Vector2.Zero, batched: batched);
                FailIf(fountain.Finished || fountain.ReadByte(0x42) != 0, "Horon selection belongs to state zero, not save-change refresh.");
                EnterSeasonsNpcRoom(session, 0, 0xe7); app.Step(6, Vector2.Zero, batched: batched);
                var next = session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor => actor.Npc.Record.Id == 0x3d);
                FailIf(!fountain.Npc.IsRetired || ReferenceEquals(fountain.Npc.NativeState, next.Npc.NativeState) || next.Npc.TextId != 0x1001,
                    "Room re-entry must retire the old binding and independently select Horon stage $01.");
                next.WriteByte(0x71, 1); app.Step(1, Vector2.Zero, batched: batched);
                FailIf(!session.Dialogue.IsOpen, "The replacement binding must remain talkable before room cancellation.");
                EnterSeasonsNpcRoom(session, 1, 0x06); app.Step(1, Vector2.Zero, batched: batched);
                FailIf(session.Dialogue.IsOpen || !next.Npc.IsRetired, "Room cancellation must close the retiring actor's dialogue.");
                var bather = session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor => actor.Npc.Record.Id == 0x33);
                // miscNPC_state0 calls func_572c twice: palette write, then initcollisions/checkabutton.
                FailIf(bather.ReadByte(0x5c) != 1 || bather.ReadByte(0x66) != 6 || bather.Script!.CurrentCommandIndex != 2,
                    "Bather $33:$80 must retain both script calls on its initialization visit.");
                next.Script!.Cancel();
                FailIf(bather.ReadByte(0x44) != 1 || bather.ReadByte(0x5c) != 1,
                    "A retired binding must not clear the replacement native allocation.");
                // miscNPC_scriptTable selects the farmer's choice script at Horon stage $02.
                session.Save.WriteField("wEssencesObtained", 1);
                session.Save.WriteField("wObtainedTreasureFlags", (byte)(session.Save.ReadField("wObtainedTreasureFlags", 8) | 1), 8);
                EnterSeasonsNpcRoom(session, 0, 0xc5); app.Step(3, Vector2.Zero, batched: batched);
                var farmer = session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor => actor.Npc.Record.Id == 0x3c);
                farmer.WriteByte(0x71, 1); app.Step(2, Vector2.Zero, batched: batched);
                for (int update = 0; update < 1200 && !session.Dialogue.ChoiceCursorVisible; update++)
                    app.Step(1, Vector2.Zero, update % 2 == 0 ? ["attack"] : [], update % 2 == 0 ? ["attack"] : [], batched: batched);
                FailIf(!session.Dialogue.ChoiceCursorVisible || session.Dialogue.SourceTextId != 0x1201,
                    $"oldLadyFarmerScript_text2 must reach its source choice: stage=${farmer.ReadByte(0x42):x2}, cmd={farmer.Script!.CurrentCommandIndex}, text=TX_{session.Dialogue.SourceTextId:x4}.");
                session.Dialogue.SubmitChoiceForValidation(1);
                for (int update = 0; update < 4 && farmer.ReadByte(0x46) == 0; update++) app.Step(1, Vector2.Zero, batched: batched);
                FailIf(farmer.ReadByte(0x46) != 30 || farmer.Script!.NativeCounter1 != 30,
                    "The farmer's answeredYes branch starts one shared 30-update wait.");
                farmer.WriteByte(0x46, 2); app.Step(1, Vector2.Zero, batched: batched);
                FailIf(farmer.ReadByte(0x46) != 1, "A native write to the active script wait must be consumed once.");
                session.Dialogue.ShowGameplayMessage("Freeze the native wait.", 64);
                app.Step(3, Vector2.Zero, batched: batched);
                FailIf(farmer.ReadByte(0x46) != 1, "Text freezes the same native wait byte.");
                session.Dialogue.Close(); app.Step(1, Vector2.Zero, batched: batched);
                FailIf(farmer.ReadByte(0x46) != 0 || session.Dialogue.SourceTextId != 0x1203,
                    "The live zero boundary must continue directly to the farmer's TX_1203.");
                session.Dialogue.Close(); app.Step(3, Vector2.Zero, batched: batched);
                EnterSeasonsNpcRoom(session, 4, 0x16); app.Step(5, Vector2.Zero, batched: batched);
                var elder = session.Entities.EntityAdapters<NativeNpcRoomEntity>().Single(actor => actor.Npc.Record.Id == 0x38);
                elder.WriteByte(0x49, 0xff); elder.WriteByte(0x6b, 2);
                app.Step(2, Vector2.Zero, batched: batched);
                FailIf(elder.ReadByte(0x49) != 0xff || elder.ReadByte(0x6b) != 0,
                    "npcFaceLinkAndAnimate must consume its live two-update facing lock before changing angle.");
                app.Step(1, Vector2.Zero, batched: batched);
                FailIf(elder.ReadByte(0x49) == 0xff || elder.ReadByte(0x6b) != 29,
                    "A facing change must publish angle and the source's post-decrement $1d cooldown.");
            }
            finally { root.Free(); }
        }
    }

    private static void EnterSeasonsNpcRoom(SeasonsOpeningSession session,int group,int room)
    {
        session.DebugTravel(group,room);
        // DebugTravel's first collision-free point may be a hole, water, or a warp.
        var floor=(from y in Enumerable.Range(1,session.Room.Height/16-2)
                   from x in Enumerable.Range(1,session.Room.Width/16-2)
                   let point=new Vector2(x*16+8,y*16+8)
                   where !session.Collision.Collides(point) &&
                       new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}.All(offset=>
                           session.Room.GetTerrainInfo(point+offset) is {Type:TerrainType.Normal,Hazard:HazardType.None})
                   orderby point.DistanceSquaredTo(new(session.Room.Width/2,session.Room.Height/2))
                   select point).First();
        session.Link.WarpTo(floor);
    }
}
