namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonSwitchRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                s.Memory.SetField("wSwitchState",0x80);s.DebugTravel(4,0x0f);
                s.Memory.SetField("wDisabledObjects",4);
                s.Inventory.GiveTreasure(TreasureId.Sword,1);s.Inventory.EquipA(TreasureId.Sword);s.Inventory.EquipB(0);
                s.Link.WarpTo(new(56,40));s.Link.Face(Vector2I.Down);app.Step(3,Vector2.Zero,batched:batched);
                var part=s.Entities.Entities<DungeonSwitchRoomEntity>().Single();
                var rail=s.Entities.Entities<SwitchTileTogglerRoomEntity>().Single();
                FailIf(part.SwitchMask!=1 || part.PackedPosition!=0x33 || part.CollisionZ!=-6 ||
                    s.Entities.Entities<KeeseCharacter>().Count!=5 || s.Room.Layout[0x33]!=0x0a || s.Room.Layout[0x74]!=0x5c ||
                    s.Collision.Collides(s.Link.Position),"$4:0f must construct its five Keese, original PART$05:$01 at$33 and INTERAC$78:$01 track consumer at$74; Sword use starts on floor.");
                var native=new BraceletRom(s.Room,s.Link.PrecisePosition,2,s.Inventory.ActiveRing,game:OracleGame.Seasons);
                native.LoadSave(s.Save);native.LoadDungeon(1);native.InitializeWalkingAnimation();
                native.SetNativeByte(s.Memory.FieldAddress("wActiveGroup"),4);
                native.SetNativeByte(s.Memory.FieldAddress("wActiveRoom"),0x0f);
                foreach(string field in new[]{"wSwitchState","wDisabledObjects","wScrollMode"})
                    native.SetNativeByte(s.Memory.FieldAddress(field),s.Memory.ReadField(field));
                int partAt=0xd0c0+Enumerable.Range(0,8).Single(slot=>ReferenceEquals(s.Entities.ResolvePartSlot(slot),part))*0x100;
                int railAt=0xd040+s.Entities.NativeInteractionSlot(rail)*0x100;
                native.SetNativeByte(partAt,1);native.SetNativeByte(partAt+1,5);native.SetNativeByte(partAt+2,1);
                native.SetNativeByte(partAt+0x0b,56);native.SetNativeByte(partAt+0x0d,56);
                native.SetNativeByte(railAt,1);native.SetNativeByte(railAt+1,0x78);native.SetNativeByte(railAt+2,1);
                native.SetNativeByte(railAt+0x0b,0x74);native.SetNativeByte(railAt+0x0d,0x13);
                native.AdvanceParts();native.AdvanceInteractions(s.Entities.FrameCounter);
                native.ClearSounds();s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();
                int update=0;
                void Step(int count,bool attack=false)=>app.Step(count,Vector2.Zero,
                    held:attack?["attack"]:[],pressed:attack?["attack"]:[],batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(s.Entities.FrameCounter,attack?1:0,interactions:true,parts:true);update++;
                    FailIf(s.Memory.ReadField("wSwitchState")!=native.ReadNativeByte(s.Memory.FieldAddress("wSwitchState")) ||
                        s.Room.Layout[0x33]!=native[0xcf33] || s.Room.Layout[0x74]!=native[0xcf74] ||
                        part.HitLockout!=Math.Max(0,-unchecked((sbyte)native.ReadNativeByte(partAt+0x2b))) ||
                        s.Sound.PlayRequestsFor(SoundId.SndSwitch)!=native.Sounds.Count(cue=>cue==SoundId.SndSwitch),
                        $"Seasons rail/batch{batched}/update{update}: switch state, delayed PART hit, track tile, lockout or cue differs.");
                });
                for(int hit=0;hit<2;hit++)
                {
                    Step(1,true);Step(36);
                    byte expected=(byte)(hit==0?0x81:0x80);
                    FailIf(s.Memory.ReadField("wSwitchState")!=expected || s.Room.Layout[0x33]!=(hit==0?0x0b:0x0a) ||
                        s.Room.Layout[0x74]!=(hit==0?0x5a:0x5c),
                        "Repeated real Sword contact must toggle only bit$01 and replace the $74 rail through table row$13.");
                }
                s.Memory.SetField("wSwitchState",0x81);s.DebugTravel(4,0x15);s.DebugTravel(4,0x0f);
                FailIf(s.Room.Layout[0x33]!=0x0b || s.Room.Layout[0x74]!=0x5a,
                    "Seasons replaceSwitchTiles must restore the two native room$0f/bit$01 writes before any object update on re-entry.");
            }
            finally {root.Free();}
        }
    }
}
