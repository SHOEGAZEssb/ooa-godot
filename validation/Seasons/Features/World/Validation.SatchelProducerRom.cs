namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsSatchelProducerRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool collected in new[]{false,true})
        foreach(bool full in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                if(collected)s.Save.SetRoomFlag(6,9,0x20);
                s.DebugTravel(6,9);s.Link.WarpTo(new(40,136));s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                var producer=entities.EntityAdapters<SeasonsStaticTreasureRoomEntity>().Single();
                int parentSlot=entities.NativeInteractionSlot(producer),parentAt=0xd040+parentSlot*256;
                var seed=entities.Random.CaptureState();var native=new BraceletRom(s.Room,s.Link.Position,2,0xff,game:OracleGame.Seasons);
                native.LoadSave(s.Save);native.SetResidentObjectGraphics(1,0x5f);
                foreach(var field in new[]{("wActiveGroup",6),("wActiveRoom",9),("wDungeonIndex",1),("wScrollMode",1),("wDisabledObjects",2)})
                    native.SetNativeByte(s.Memory.FieldAddress(field.Item1),(byte)field.Item2);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                native[parentAt]=1;native[parentAt+1]=0x6b;native[parentAt+2]=0x20;native[parentAt+11]=88;native[parentAt+13]=104;
                var fillers=new List<IRoomEntity>();
                if(full)
                    while(entities.InteractionSlotAvailable)
                    {
                        var effect=new StationaryDebrisEffect();effect.Initialize(SeasonsGameContent.AssetRoot,new(24,24),0);
                        var filler=new InteractionSlotValidationEntity(effect,static _=>{});entities.AddEntity(filler);fillers.Add(filler);
                        int at=0xd040+entities.NativeInteractionSlot(filler)*256;
                        native[at]=1;native[at+1]=0x48;native[at+4]=1;
                    }
                s.Memory.SetField("wDisabledObjects",2);
                int updates=0;
                void Compare()
                {
                    updates++;native.AdvanceInteractions(entities.FrameCounter);
                    FailIf(producer.Finished!=(native[parentAt]==0),"$6b:$20 must delete on its first visit, including failed allocation.");
                    var child=entities.Entities<GroundTreasurePickup>().SingleOrDefault();
                    int nativeSlot=Enumerable.Range(0,16).FirstOrDefault(slot=>native[0xd040+slot*256]!=0&&native[0xd041+slot*256]==0x60,-1);
                    FailIf((child is not null)!=(nativeSlot>=0),$"Satchel producer full{full}/collected{collected}/update{updates}: checked child admission differs.");
                    if(child is not null)
                    {
                        int at=0xd040+nativeSlot*256;
                        FailIf(entities.InteractionSlot(child)!=nativeSlot||child.State!=PickupState.Spawning||native[at+4]!=1||
                            child.Position!=new Vector2(native[at+13],native[at+11])||child.ZFixed!=native.Word(at+14)||
                            child.Visible!=((native[at+0x1a]&128)!=0)||child.Record.SpawnMode!=0||child.Record.GrabMode!=2,
                            "The producer must retain its slot during allocation and initialize the later $60:$19 child in the same pass.");
                    }
                    var random=entities.Random.CaptureState();
                    FailIf(random.Rng1!=native[0xff94]||random.Rng2!=native[0xff95]||random.Calls-seed.Calls!=native.RandomCalls,
                        "The static Satchel producer must preserve shared RNG ordering.");
                }
                app.Step(1,Vector2.Zero,batched:batched,afterUpdate:Compare);
                if(full)
                {
                    var filler=fillers[0];int at=0xd040+entities.NativeInteractionSlot(filler)*256;
                    entities.RetireEntity(filler);for(int index=0;index<64;index++)native[at+index]=0;
                }
                app.Step(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(entities.Entities<GroundTreasurePickup>().Any()!=(!collected&&!full),
                    "An obtained item or failed allocation must remain absent after capacity is released; $6b:$20 never retries.");
                s.DebugTravel(4,0x0a);
                FailIf(entities.Entities<GroundTreasurePickup>().Count!=0||s.Link.IsHoldingItemTwoHands,
                    "Room replacement must retire an uncollected side-view treasure without granting or retaining its presentation.");
            }
            finally {root.Free();}
        }
    }
}
