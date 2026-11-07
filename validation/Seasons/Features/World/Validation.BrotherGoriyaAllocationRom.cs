namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsBrotherGoriyaAllocationRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int parentSlot in new[]{0,15})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x0b);s.Link.WarpTo(new(120,136));s.Link.BeginCutsceneControl();
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                var parent=entities.EntityAdapters<BrotherGoriyaRoomEntity>().Single();
                if(parentSlot!=0)
                {
                    SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_enemySlots").Remove(parent);
                    SomariaPrivate<HashSet<int>>(entities,"_reservedEnemySlots").Remove(0);
                    entities.RegisterEnemySlot(parent,parentSlot);
                }
                var seed=entities.Random.CaptureState();var native=SharedNativeEnemyReference(s.Rooms,s.Link,0x70);native.LoadDungeon(1);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                native.SetNativeByte(s.Memory.FieldAddress("wScrollMode"),1);
                native.SetNativeByte(s.Memory.FieldAddress("wNumEnemies"),1);
                int at=0xd080+parentSlot*256;
                native[at]=0x11;native[at+1]=0x70;native[at+11]=40;native[at+13]=72;
                var blockers=new Dictionary<int,IRoomEntity>();
                foreach(int slot in Enumerable.Range(0,16).Where(slot=>slot!=parentSlot))
                {
                    var held=new EnemyAiSlotReservation();entities.RegisterEnemySlot(held,slot);entities.AddEntity(held);blockers.Add(slot,held);
                    int address=0xd080+slot*256;
                    native[address]=1;native[address+1]=0x70;native[address+4]=1;native[address+0x29]=1; // Original state1 is a no-op.
                }
                int update=0;
                void Compare()
                {
                    update++;native.Word(0xd00a,(int)(s.Link.Position.Y*256));native.Word(0xd00c,(int)(s.Link.Position.X*256));
                    native.AdvanceEnemies(entities.FrameCounter);
                    foreach(var actor in entities.Entities<BrotherGoriyaCharacter>())
                    {
                        int address=0xd080+actor.Slot*256;
                        FailIf(actor.SubId!=native[address+2]||actor.State!=native[address+4]||
                            actor.Position!=new Vector2(native.Word(address+12)/256f,native.Word(address+10)/256f)||
                            actor.RelatedSlot!=(native[address+0x17]==0?-1:native[address+0x17]-0xd0),
                            $"Goriya ENEMY capacity/identity differs: parent${parentSlot:x2}, slot${actor.Slot:x2}, update{update}.");
                    }
                    var random=entities.Random.CaptureState();
                    FailIf(random.Rng1!=native[0xff94]||random.Rng2!=native[0xff95]||random.Calls-seed.Calls!=native.RandomCalls||
                        entities.RoomEnemyCount!=native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies")),
                        $"Goriya ENEMY retry must retain source RNG and counts: parent${parentSlot:x2}, update{update}.");
                }
                app.Step(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(entities.Entities<BrotherGoriyaCharacter>().Count!=1||entities.Entities<BrotherGoriyaCharacter>().Single().State!=0,
                    "A full ENEMY pool must leave the leader retrying its checked partner allocation.");
                int released=parentSlot==0?1:0;
                entities.RetireEntity(blockers[released]);for(int index=0;index<64;index++)native[0xd080+released*256+index]=0;
                app.Step(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
                var pair=entities.Entities<BrotherGoriyaCharacter>();
                FailIf(pair.Count!=2||pair.Any(actor=>actor.State!=8)||pair.Single(actor=>actor.SubId==0).Slot!=Math.Min(parentSlot,released)||
                    entities.RoomEnemyCount!=2,"The released physical slot must create the pair, with subid$00 in the earlier slot.");
                s.Link.EndCutsceneControl();
            }
            finally {root.Free();}
        }
    }
}
