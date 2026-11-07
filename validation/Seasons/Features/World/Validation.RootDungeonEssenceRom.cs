namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonEssenceRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;s.DebugTravel(4,0x13);s.Link.WarpTo(new(120,136));s.Link.Face(Vector2I.Up);
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);var entities=s.Entities;
                FailIf(s.Collision.Collides(s.Link.Position),"The native Essence comparison must approach from $4:13's actual south floor.");
                var essence=entities.Entities<DungeonEssence>().Single();int at=((0xd0+entities.NativeInteractionSlot(essence))<<8)|0x40;
                var native=new BraceletRom(s.Room,s.Link.Position,0,s.Inventory.ActiveRing,game:OracleGame.Seasons);native.LoadSave(s.Save);
                native.LoadPaletteHeader(0x41); // PALH_TILESET_GNARLED_ROOT_DUNGEON.
                foreach(string field in new[]{"wActiveGroup","wActiveRoom","wScrollMode"})native.SetNativeByte(s.Memory.FieldAddress(field),s.Memory.ReadField(field));
                native.SetNativeByte(s.Memory.FieldAddress("wDungeonIndex"),(byte)s.Rooms.CurrentDungeonIndex);
                native.SetNativeByte(at,1);native.SetNativeByte(at+1,0x7f);native.SetNativeByte(at+11,40);native.SetNativeByte(at+13,120);
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();native.ClearSounds();
                var app=new ApplicationValidationFixture(root);int updates=0;bool darkened=false;
                var graphics=new LinkCollisionRom(game:OracleGame.Seasons);
                graphics.Call(0x04e7,bank:0,accumulator:0x0f);
                graphics.Call(0x064e,speed:31,angle:0xa8,bank:0,objectPage:0x80,objectOffset:0,dataAddress:0x4786,restoresBank:false);
                bool comparedPixels=false;
                void Compare()
                {
                    updates++;
                    native.Word(0xd00c,(int)(s.Link.PrecisePosition.X*256));native.Word(0xd00a,(int)(s.Link.PrecisePosition.Y*256));
                    native.AdvancePaletteThread();native.AdvanceParts();native.AdvanceInteractions(entities.FrameCounter);
                    if(!comparedPixels&&essence.Visible)
                    {
                        var animation=SomariaPrivate<EnemyAnimationPlayer>(essence,"_essence");
                        CompareSeasonsInteractionPixels(native,graphics,animation.CurrentTexture,animation.CurrentOffset,at,"Fertile Soil INTERAC$7f:$00",tileBase:0x14);
                        comparedPixels=true;
                    }
                    darkened|=native.ReadNativeByte(0xc4ab)==5&&native.ReadNativeByte(0xc2ff)!=0;
                    FailIf(s.Transitions.NativePaletteThreadMode!=native.ReadNativeByte(0xc4ab),$"Fertile Soil update{updates}: native darkening mode differs.");
                    if(darkened&&!s.Transitions.IsTransitioning)
                    foreach(var (palette,shade) in new[]{(2,1),(3,2),(7,3)})
                    {
                        Color color=s.Rooms.BackgroundPalettes.Resolve(palette,shade);
                        int packed=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(packed!=(native.FadingBackgroundColor(palette*4+shade)&0x7fff),
                            $"Fertile Soil update{updates}: BG{palette}/shade{shade} must use the native $f0 darkening arithmetic.");
                    }
                    var rng=entities.Random.CaptureState();
                    FailIf(SomariaPrivate<Vector2>(essence,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        (SomariaPrivate<int>(essence,"_zFixed")&0xffff)!=native.Word(at+14)||essence.Visible!=((native.ReadNativeByte(at+26)&0x80)!=0)||
                        s.Inventory.Essences!=native.ReadNativeByte(s.Memory.FieldAddress("wEssencesObtained"))||
                        s.DisabledObjects!=native.ReadNativeByte(s.Memory.FieldAddress("wDisabledObjects"))||
                        rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                        $"Fertile Soil update{updates}: native state{native.ReadNativeByte(at+4)}, position{SomariaPrivate<Vector2>(essence,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{SomariaPrivate<int>(essence,"_zFixed")}/{native.Word(at+14)}, visible{essence.Visible}/{native.ReadNativeByte(at+26):x2}, mask${s.DisabledObjects:x2}/${native.ReadNativeByte(s.Memory.FieldAddress("wDisabledObjects")):x2}, essence${s.Inventory.Essences:x2}/${native.ReadNativeByte(s.Memory.FieldAddress("wEssencesObtained")):x2}, RNG{rng.Rng2:x2}{rng.Rng1:x2}/{native[0xff95]:x2}{native[0xff94]:x2}, draws{rng.Calls-seed.Calls}/{native.RandomCalls}.");
                    foreach(int sound in new[]{SoundId.SndDropEssence,SoundId.MusGetEssence,SoundId.MusEssence,SoundId.SndFadeOut,SoundId.SndEnergyThing})
                        FailIf(s.Sound.PlayRequestsFor(sound)!=native.Sounds.Count(value=>value==sound),$"Fertile Soil update{updates}: sound${sound:x2} differs.");
                    foreach(var bead in entities.EntityAdapters<BlueEnergyBeadRoomEntity>())
                    {
                        int slot=Enumerable.Range(0,16).Single(slot=>ReferenceEquals(entities.ResolvePartSlot(slot),bead));int part=0xd0c0+slot*256;
                        FailIf(native.ReadNativeByte(part+1)!=0x53||bead.Delay!=native.ReadNativeByte(part+7)||
                            bead.Position!=new Vector2(native.Word(part+12)>>8,native.Word(part+10)>>8)||bead.Visible!=((native.ReadNativeByte(part+26)&0x80)!=0),
                            $"Fertile Soil update{updates}/PART${slot:x2}: native energy delay, movement or visibility differs.");
                    }
                }
                int stride=batched?2:1;
                for(int tick=0;tick<250&&!s.Dialogue.IsOpen;tick+=stride)app.Step(stride,Vector2.Up,batched:batched,afterUpdate:Compare);
                FailIf(!s.Dialogue.IsOpen||native.ReadNativeByte(at+4)!=5||s.Inventory.Essences!=1,"The real pedestal approach must reach the native Essence-get state5.");
                app.Step(8,Vector2.Zero,batched:batched,afterUpdate:Compare);
                s.Dialogue.Close();native.SetNativeByte(s.Memory.FieldAddress("wTextIsActive"),0);
                for(int tick=0;tick<600&&!s.Transitions.IsTransitioning;tick+=stride)app.Step(stride,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(!comparedPixels||!darkened||!s.Transitions.IsTransitioning||native.ReadNativeByte(s.Memory.FieldAddress("wWarpDestGroup"))!=0x80||
                    native.ReadNativeByte(s.Memory.FieldAddress("wWarpDestRoom"))!=0x96||native.ReadNativeByte(s.Memory.FieldAddress("wWarpDestPos"))!=0x44||
                    native.ReadNativeByte(s.Memory.FieldAddress("wWarpTransition"))!=1||native.ReadNativeByte(s.Memory.FieldAddress("wWarpTransition2"))!=0x83,
                    "Fertile Soil must publish native exit $80:$96/$44/$01/$83 on the same update.");
            }
            finally {root.Free();}
        }
    }
}
