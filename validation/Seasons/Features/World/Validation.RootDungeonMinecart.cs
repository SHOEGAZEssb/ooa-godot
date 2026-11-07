namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonMinecartRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                // Dungeon-entry/static initialization is verified separately.
                // Freeze hostiles while isolating this two-room vehicle handoff.
                StaticObjectState.LoadDungeon(s.Memory,1);s.DebugTravel(4,0x15);
                s.Memory.SetField("wDisabledObjects",4);s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                s.Link.WarpTo(new(120,88));s.Link.Face(Vector2I.Up);app.Step(3,Vector2.Zero,batched:batched);
                var waiting=s.Entities.Entities<MinecartInteractionRoomEntity>().Single();
                FailIf(s.Entities.Entities<ZolCharacter>().Count!=4 || s.Entities.RoomEnemyCount!=4 ||
                    s.Entities.Entities<LightableTorchRoomEntity>().Count!=2 || s.Entities.Entities<MinibossPortal>().Count!=0 ||
                    waiting.Position!=new Vector2(120,40) || waiting.State!=1 || waiting.Direction!=0 ||
                    s.Room.Layout[0x07]!=0x7c || s.Collision.Collides(s.Link.Position),
                    "$4:15 must construct four Zols, two torches, closed north rail and its original stationary cart; the undefeated miniboss portal deletes.");
                int at=0xd040+s.Entities.NativeInteractionSlot(waiting)*0x100;
                var native=new BraceletRom(s.Room,s.Link.PrecisePosition,0,s.Inventory.ActiveRing,game:OracleGame.Seasons);
                native.LoadSave(s.Save);native.LoadDungeon(1);native.InitializeWalkingAnimation();
                native[0xd004]=0;native[0xd009]=unchecked((byte)s.Link.NativePlanarAngle);
                native[0xd010]=unchecked((byte)s.Link.NativePlanarSpeed);
                foreach(string field in new[]{"wActiveGroup","wActiveRoom","wScrollMode","wDisabledObjects","wLinkPushingDirection","wPushingAgainstTileCounter"})
                    native.SetNativeByte(s.Memory.FieldAddress(field),s.Memory.ReadField(field));
                native.SetNativeByte(s.Memory.FieldAddress("wActiveGroup"),4);
                native.SetNativeByte(s.Memory.FieldAddress("wActiveRoom"),0x15);
                native.SetNativeByte(at,1);native.SetNativeByte(at+1,0x16);
                native.SetNativeByte(at+0x0b,40);native.SetNativeByte(at+0x0d,120);
                native.SetNativeByte(at+0x16,0x80);native.SetNativeByte(at+0x17,0xcd);
                for(int offset=0;offset<128;offset++)native.SetNativeByte(0xcd80+offset,s.Memory.ReadWramByte(0xcd80+offset));
                native.AdvanceInteractions(s.Entities.FrameCounter);
                native.UpdateLinkItems(s.Entities.FrameCounter,0,interactions:true);
                native.ClearSounds();s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();
                int update=0;bool compare=true,sawBoarding=false;
                void Step(int count,Vector2 input=default)=>app.Step(count,input,batched:batched,afterUpdate:()=>
                {
                    if(!compare)return;
                    native.UpdateLinkItems(s.Entities.FrameCounter,input==Vector2.Up?0x40:0,interactions:true);update++;
                    sawBoarding|=s.Link.MinecartJumpActive;
                    FailIf(s.Link.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                        s.Link.ItemCreationZFixed!=unchecked((short)native.Word(0xd00e)),
                        $"Seasons cart/batch{batched}/update{update}: Link XYZ differs: {s.Link.PrecisePosition}/{s.Link.ItemCreationZFixed}; native{native.Word(0xd00c)/256f},{native.Word(0xd00a)/256f}/{unchecked((short)native.Word(0xd00e))}.");
                    foreach(int offset in new[]{0,1,4,8,9,0x0a,0x0b,0x0c,0x0d,0x0e,0x0f,0x10,0x20,0x21,0x22,0x23,0x30,0x31,0x32,0x35,0x36})
                        FailIf(s.Memory.ReadWramByte(0xd100+offset)!=native[0xd100+offset],
                            $"Seasons cart/update{update}: native D1 byte${offset:x2} differs: ${s.Memory.ReadWramByte(0xd100+offset):x2}/${native[0xd100+offset]:x2}.");
                    for(int offset=0;offset<128;offset++)FailIf(s.Memory.ReadWramByte(0xcd80+offset)!=native.ReadNativeByte(0xcd80+offset),
                        $"Seasons cart/update{update}: static byte${0xcd80+offset:x4} differs.");
                    FailIf(s.Sound.PlayRequestsFor(SoundId.SndMinecart)!=native.Sounds.Count(cue=>cue==SoundId.SndMinecart),
                        "Seasons mounted movement must consume the native sound countdown.");
                });
                for(int tick=0;tick<100&&!s.Link.MinecartRideActive;tick++)Step(1,Vector2.Up);
                FailIf(!s.Link.MinecartRideActive || !sawBoarding || s.Memory.ReadWramByte(0xcd80)!=0 ||
                    s.Entities.Entities<MinecartRoomEntity>().Count!=1 || s.Entities.EntityAdapters<MinecartCollisionRoomEntity>().Count()!=1,
                    "Pushing from the original platform must jump aboard D1, delete its static record and allocate independent ITEM$1d.");
                Step(8);
                var body=s.Entities.Entities<MinecartRoomEntity>().Single();
                // Declare a reversed native cart on the same original rail to
                // exercise platform return without a whole-dungeon cart route.
                ((INativeSpecialObjectRoomEntity)body).NativeSpecialObjectPosition=new(120,24);
                s.Memory.SetWramByte(0xd108,2);s.Memory.SetWramByte(0xd109,0x10);s.Memory.SetWramByte(0xd130,2);
                native.Word(0xd10a,24*256);native.Word(0xd10c,120*256);native[0xd108]=2;native[0xd109]=0x10;native[0xd130]=2;
                for(int tick=0;tick<100&&(s.Link.MinecartRideActive || s.Link.MinecartJumpActive);tick++)Step(1);
                Step(3);
                FailIf(s.Link.MinecartRideActive || s.Link.MinecartJumpActive || s.Memory.ReadWramByte(0xcd80)!=3 ||
                    s.Entities.Entities<MinecartInteractionRoomEntity>().Count!=1 || s.Entities.Entities<MinecartRoomEntity>().Count!=0 ||
                    s.Entities.EntityAdapters<MinecartCollisionRoomEntity>().Count()!=0 || s.Collision.Collides(s.Link.Position),
                    "Returning to platform$37 must restore one static INTERAC$16, retire D1/ITEM$1d and land Link on original floor.");
                compare=false;
                for(int tick=0;tick<120&&!s.Link.MinecartRideActive;tick++)Step(1,Vector2.Up);
                FailIf(!s.Link.MinecartRideActive,"The same cart must admit another real boarding action after dismount.");
                body=s.Entities.Entities<MinecartRoomEntity>().Single();
                var collision=s.Entities.EntityAdapters<MinecartCollisionRoomEntity>().Single();
                int collisionSlot=s.Entities.DynamicItemSlotOf(collision);
                for(int tick=0;tick<100&&!s.Scrolling;tick++)Step(1);
                FailIf(!s.Scrolling || s.RoomId!=0x0f || s.Entities.Entities<KeeseCharacter>().Count!=5,
                    "The ridden cart must open $4:15's north minecart door and follow the imported neighbor into $4:0f.");
                var incoming=s.Entities.Entities<KeeseCharacter>().ToArray();var positions=incoming.Select(enemy=>enemy.Position).ToArray();
                Step(8);
                FailIf(!s.Scrolling || !incoming.Select(enemy=>enemy.Position).SequenceEqual(positions),
                    "Destination Keese must remain frozen while the persistent cart and collision ITEM scroll.");
                for(int tick=0;tick<120&&s.Scrolling;tick++)Step(1);
                for(int tick=0;tick<90&&s.RoomId==0x0f&&s.Room.Layout[0xa7]!=0x7e;tick++)Step(1);
                FailIf(s.Scrolling || s.RoomId!=0x0f || !s.Link.MinecartRideActive ||
                    !ReferenceEquals(body,s.Entities.Entities<MinecartRoomEntity>().Single()) || collision.Finished ||
                    s.Entities.DynamicItemSlotOf(collision)!=collisionSlot || s.Room.Layout[0xa7]!=0x7e ||
                    s.Entities.Entities<DungeonSwitchRoomEntity>().Count!=1 || s.Entities.Entities<SwitchTileTogglerRoomEntity>().Count!=1,
                    $"Arrival must retain D1/ITEM, close the cleared entry rail and construct the switch/track consumer: room${s.RoomId:x2}, scroll{s.Scrolling}, ride{s.Link.MinecartRideActive}, same{ReferenceEquals(body,s.Entities.Entities<MinecartRoomEntity>().Single())}, item{collision.Finished}/{s.Entities.DynamicItemSlotOf(collision):x2}/{collisionSlot:x2}, door${s.Room.Layout[0xa7]:x2}, switch{s.Entities.Entities<DungeonSwitchRoomEntity>().Count}, rail{s.Entities.Entities<SwitchTileTogglerRoomEntity>().Count}, Link{s.Link.Position}, cart{body.Position}.");
            }
            finally {root.Free();}
        }
    }
}
