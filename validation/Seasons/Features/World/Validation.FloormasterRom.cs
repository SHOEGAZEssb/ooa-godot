namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsFloormasterRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x17);s.Link.WarpTo(new(120,88));s.Link.BeginCutsceneControl();entities.DisableLinkCollisionsAndMenu();
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                FailIf(s.Collision.Collides(s.Link.Position)||entities.Entities<FloormasterCharacter>().Count!=1||entities.RoomEnemyCount!=1,
                    "The source $4:17 stream must place one counted $35:$00 controller at ($00,$40).");
                var seed=entities.Random.CaptureState();var native=SharedNativeEnemyReference(s.Rooms,s.Link,0x35);native.LoadDungeon(1);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                foreach(var field in new[]{("wActiveGroup",4),("wActiveRoom",0x17),("wNumEnemies",1),("wScrollMode",1)})
                    native.SetNativeByte(s.Memory.FieldAddress(field.Item1),(byte)field.Item2);
                native[0xd080]=0x11;native[0xd081]=0x35;native[0xd08b]=64;
                bool text=false,sawRetreat=false;int update=0,maximumHands=0;
                entities.TextActiveSource=()=>text;
                app.Step(650,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    update++;
                    native.SetNativeByte(s.Memory.FieldAddress("wTextIsActive"),(byte)(text?1:0));
                    native.Word(0xd00a,(int)(s.Link.PrecisePosition.Y*256));native.Word(0xd00c,(int)(s.Link.PrecisePosition.X*256));
                    native[0xd009]=unchecked((byte)s.Link.LinkMovementAngle);native[0xd024]=(byte)(s.Link.PatchCollisionsEnabled?0x80:0);
                    native[0xffae]=(byte)s.Link.Position.Y;native[0xffaf]=(byte)s.Link.Position.X;
                    native.AdvanceEnemies(entities.FrameCounter);native.AdvanceParts();
                    var hands=entities.EntityAdapters<FloormasterRoomEntity>().ToArray();maximumHands=Math.Max(maximumHands,hands.Length-1);
                    FailIf(hands.Length!=Enumerable.Range(0,16).Count(slot=>native[0xd080+slot*256]!=0),
                        $"Seasons Floormaster update{update}: physical ENEMY allocation differs.");
                    foreach(var adapter in hands)
                    {
                        var hand=(FloormasterCharacter)adapter.Node;int at=0xd080+adapter.Slot*256;sawRetreat|=hand.State==13;
                        FailIf(hand.Record.SubId!=native[at+2]||hand.State!=native[at+4]||hand.Counter!=native[at+6]||
                            hand.Angle!=native[at+9]||hand.Speed!=native[at+16]||
                            hand.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            hand.ZFixed!=unchecked((short)native.Word(at+14))||hand.Health!=native[at+0x29]||
                            hand.Animation.RemainingUpdates!=native[at+0x20]||hand.AnimationParameter!=native[at+0x21]||
                            hand.Visible!=((native[at+0x1a]&128)!=0)||hand.CollisionEnabled!=((native[at+0x24]&128)!=0),
                            $"Seasons Floormaster update{update}/slot${adapter.Slot:x2}: state{hand.State}/{native[at+4]}, counter{hand.Counter}/{native[at+6]}, angle{hand.Angle}/{native[at+9]}, XY{hand.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{hand.ZFixed}/{unchecked((short)native.Word(at+14))}, animation{hand.Animation.RemainingUpdates}:{hand.AnimationParameter}/{native[at+0x20]}:{native[at+0x21]}.");
                        if(hand.Record.SubId==0)
                            FailIf(hand.LiveChildren!=native[at+0x30]||hand.RemainingChildren!=native[at+0x33]||
                                hand.ChildSubId!=native[at+0x34]||hand.LastLinkPosition!=new Vector2(native[at+0x32],native[at+0x31]),
                                "Floormaster child birth/retreat must publish the native live count, kill quota and previous Link coordinates.");
                    }
                    var random=entities.Random.CaptureState();
                    FailIf(random.Rng1!=native[0xff94]||random.Rng2!=native[0xff95]||random.Calls-seed.Calls!=native.RandomCalls||
                        entities.RoomEnemyCount!=native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies")),
                        $"Seasons Floormaster update{update}: shared RNG or counted-controller ownership differs.");
                    if(update==2)text=true;
                    if(update==6)text=false;
                });
                FailIf(!sawRetreat||maximumHands!=3||entities.Entities<FloormasterCharacter>().Single(hand=>hand.Record.SubId==0).RemainingChildren!=4,
                    "The source controller must cap three live hands and retain its four-kill quota through timed retreats.");
                s.Link.EndCutsceneControl();
            }
            finally {root.Free();}
        }
        foreach(bool batched in new[]{false,true})
        foreach(int parentSlot in new[]{0,15})CheckSeasonsFloormasterCapacity(batched,parentSlot);
    }

    private void CheckSeasonsFloormasterCapacity(bool batched,int parentSlot)
    {
        var root=StartSeasonsAfterImpa(batched);
        try
        {
            var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
            s.DebugTravel(4,0x17);s.Link.WarpTo(new(120,88));s.Link.BeginCutsceneControl();entities.DisableLinkCollisionsAndMenu();
            var parent=entities.EntityAdapters<FloormasterRoomEntity>().Single();
            if(parentSlot!=0)
            {
                SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_enemySlots").Remove(parent);
                SomariaPrivate<HashSet<int>>(entities,"_reservedEnemySlots").Remove(0);
                entities.RegisterEnemySlot(parent,parentSlot);
            }
            var seed=entities.Random.CaptureState();var native=SharedNativeEnemyReference(s.Rooms,s.Link,0x35);native.LoadDungeon(1);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            native.SetNativeByte(s.Memory.FieldAddress("wNumEnemies"),1);
            native.SetNativeByte(s.Memory.FieldAddress("wScrollMode"),1);
            int at=0xd080+parentSlot*256;
            native[at]=0x11;native[at+1]=0x35;native[at+11]=64;
            var blockers=new Dictionary<int,IRoomEntity>();
            foreach(int slot in Enumerable.Range(0,16).Where(slot=>slot!=parentSlot))
            {
                var held=new EnemyAiSlotReservation();entities.RegisterEnemySlot(held,slot);entities.AddEntity(held);blockers.Add(slot,held);
                int address=0xd080+slot*256;
                native[address]=1;native[address+1]=0x70;native[address+4]=1;native[address+0x29]=1; // Goriya state1 is a native no-op.
            }
            void Compare()
            {
                native.Word(0xd00a,(int)(s.Link.Position.Y*256));native.Word(0xd00c,(int)(s.Link.Position.X*256));
                native[0xd009]=unchecked((byte)s.Link.LinkMovementAngle);native[0xd024]=0x80;
                native[0xffae]=(byte)s.Link.Position.Y;native[0xffaf]=(byte)s.Link.Position.X;
                native.AdvanceEnemies(entities.FrameCounter);
                foreach(var adapter in entities.EntityAdapters<FloormasterRoomEntity>())
                {
                    var hand=(FloormasterCharacter)adapter.Node;int address=0xd080+adapter.Slot*256;
                    FailIf(hand.State!=native[address+4]||hand.Counter!=native[address+6]||hand.Record.SubId!=native[address+2]||
                        hand.Position!=new Vector2(native.Word(address+12)/256f,native.Word(address+10)/256f),
                        $"Floormaster capacity/released-slot publication differs: parent${parentSlot:x2}, slot${adapter.Slot:x2}.");
                }
                var random=entities.Random.CaptureState();var controller=(FloormasterCharacter)parent.Node;
                FailIf(controller.LiveChildren!=native[at+0x30]||controller.RemainingChildren!=native[at+0x33]||
                    entities.RoomEnemyCount!=native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies"))||
                    random.Rng1!=native[0xff94]||random.Rng2!=native[0xff95]||random.Calls-seed.Calls!=native.RandomCalls,
                    "The full-pool retry must preserve native quota, live count, room count and RNG.");
            }
            app.Step(65,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(entities.Entities<FloormasterCharacter>().Count!=1||((FloormasterCharacter)parent.Node).Counter!=1,
                "A full ENEMY pool must leave the initialized controller retrying at counter$01.");
            int released=parentSlot==0?1:0;
            entities.RetireEntity(blockers[released]);for(int offset=0;offset<64;offset++)native[0xd080+released*256+offset]=0;
            app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Compare);
            var child=entities.EntityAdapters<FloormasterRoomEntity>().Single(adapter=>adapter!=parent);
            FailIf(child.Slot!=released||((FloormasterCharacter)child.Node).State!=(parentSlot==0?8:0),
                "A released higher slot must initialize in the same pass; a lower slot waits for the next enemy walk.");
            app.Step(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
        }
        finally {root.Free();}
    }
}
