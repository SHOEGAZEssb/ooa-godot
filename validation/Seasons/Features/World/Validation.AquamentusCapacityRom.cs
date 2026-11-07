namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsAquamentusCapacityRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int parentSlot in new[]{0,15})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x12);s.Link.WarpTo(new(120,136));s.Link.BeginCutsceneControl();s.Memory.SetField("wDisabledObjects",2);
                var parent=entities.EntityAdapters<AquamentusRoomEntity>().Single();
                if(parentSlot!=0)
                {
                    SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_enemySlots").Remove(parent);
                    SomariaPrivate<HashSet<int>>(entities,"_reservedEnemySlots").Remove(0);
                    entities.RegisterEnemySlot(parent,parentSlot);
                }
                var seed=entities.Random.CaptureState();var native=SharedNativeEnemyReference(s.Rooms,s.Link,0x78);native.LoadDungeon(1);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc0e]=0x8f;
                native.SetNativeByte(s.Memory.FieldAddress("wScrollMode"),1);native.SetNativeByte(s.Memory.FieldAddress("wNumEnemies"),1);
                native.SetNativeByte(s.Memory.FieldAddress("wDisabledObjects"),2);
                int at=0xd080+parentSlot*256;native[at]=0x11;native[at+1]=0x78;native[at+11]=80;native[at+13]=176;
                var blockers=new Dictionary<int,IRoomEntity>();
                foreach(int slot in Enumerable.Range(0,16).Where(slot=>slot!=parentSlot))
                {
                    var held=new EnemyAiSlotReservation();entities.RegisterEnemySlot(held,slot);entities.AddEntity(held);blockers.Add(slot,held);
                    int address=0xd080+slot*256;native[address]=1;native[address+1]=0x70;native[address+4]=1;native[address+0x29]=1;
                }
                int update=0;byte camera=unchecked((byte)-(int)entities.ToScreen(Vector2.Zero).Y);
                void Compare()
                {
                    native[0xffa8]=camera;native.AdvanceEnemies(entities.FrameCounter);native.AdvanceParts();update++;
                    var actors=entities.Entities<AquamentusCharacter>();
                    FailIf(actors.Count!=Enumerable.Range(0,16).Count(slot=>native[0xd080+slot*256]!=0&&native[0xd081+slot*256]==0x78),
                        "Aquamentus capacity must preserve the spawner until all three checked child allocations are available.");
                    foreach(var actor in actors)
                    {
                        int address=0xd080+actor.Slot*256;
                        FailIf(actor.State!=native[address+4]||actor.SubId!=native[address+2]||actor.Counter!=native[address+6]||
                            actor.Position!=new Vector2(native.Word(address+12)/256f,native.Word(address+10)/256f)||
                            actor.ZFixed!=unchecked((short)native.Word(address+14))||actor.CollisionEnabled!=((native[address+0x24]&128)!=0),
                            $"Aquamentus parent${parentSlot:x2}, update{update}, slot${actor.Slot:x2}: native child birth/initialization differs.");
                    }
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                        entities.RoomEnemyCount!=1||native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies"))!=1,
                        "The Aquamentus three-slot admission gate must retain one allocation and consume no retry RNG.");
                    camera=unchecked((byte)-(int)entities.ToScreen(Vector2.Zero).Y);
                    if(update==1){s.Memory.SetField("wcc93",0);native.SetNativeByte(s.Memory.FieldAddress("wcc93"),0);}
                }
                void Release(int slot)
                {
                    entities.RetireEntity(blockers[slot]);for(int offset=0;offset<64;offset++)native[0xd080+slot*256+offset]=0;
                }
                app.Step(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
                int first=parentSlot==0?1:0;Release(first);Release(first+1);
                app.Step(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(entities.Entities<AquamentusCharacter>().Count!=1,"Two available slots must not start Aquamentus's indivisible three-child sequence.");
                Release(first+2);app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(entities.Entities<AquamentusCharacter>().Count!=3||entities.Entities<AquamentusCharacter>().Any(actor=>actor.State!=(parentSlot==0?8:0)),
                    "The three admitted children must initialize in the same pass only when their slots follow the deleted spawner.");
                app.Step(4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            finally {root.Free();}
        }
    }
}
