namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonMakuRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;s.Inventory.GiveTreasure(TreasureId.Essence,0);s.Save.SetRoomFlag(0,0x96,0x80);
                s.DebugTravel(0,0x96);s.Link.WarpTo(new(72,88));s.Link.Face(Vector2I.Up);
                FailIf(s.Collision.Collides(s.Link.Position),"The isolated Maku comparison must stand on original floor outside the entrance warp.");
                var entities=s.Entities;var maku=entities.EntityAdapters<SeasonsRemoteMakuRoomEntity>().Single();
                int at=((0xd0+entities.NativeInteractionSlot(maku))<<8)|0x40;
                var native=new BraceletRom(s.Room,s.Link.Position,0,s.Inventory.ActiveRing,game:OracleGame.Seasons);native.LoadSave(s.Save);
                native.LoadPaletteHeader(0x0f);
                foreach(string field in new[]{"wActiveGroup","wActiveRoom","wDungeonIndex","wScrollMode"})native.SetNativeByte(s.Memory.FieldAddress(field),s.Memory.ReadField(field));
                native.SetNativeByte(at,1);native.SetNativeByte(at+1,0x22);native.SetNativeByte(at+11,72);native.SetNativeByte(at+13,72);
                native.ClearSounds();s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();
                var app=new ApplicationValidationFixture(root);int updates=0;bool sawText=false,sawLeaf=false,sawSparkle=false;
                bool declaredAudio=false,sawAudioAlias=false,checkAudioAlias=false;
                entities.NativeSoundAllocationAliasWritten+=(address,value)=>
                {
                    if(address!=0xc07b||sawAudioAlias)return;
                    sawAudioAlias=checkAudioAlias=true;
                    FailIf(!declaredAudio||value!=0||s.Sound.ReadNativeAllocationAlias(address)!=0,
                        "makuLeaf's post-playSound alias must decrement the authoritative audio wait $c07b from1 to0.");
                };
                void Compare()
                {
                    updates++;native.AdvancePaletteThread();native.AdvanceInteractions(entities.FrameCounter);
                    if(checkAudioAlias)
                    {
                        checkAudioAlias=false;
                        FailIf(native.ReadNativeByte(0xc07b)!=0,"The native Maku leaf must perform the same audio wait alias before resetting its sparkle delay.");
                    }
                    FailIf(s.DisabledObjects!=native.ReadNativeByte(s.Memory.FieldAddress("wDisabledObjects"))||
                        s.Memory.ReadField("wMenuDisabled")!=native.ReadNativeByte(s.Memory.FieldAddress("wMenuDisabled"))||
                        s.Save.ReadField("wMakuMapTextPresent")!=native.ReadNativeByte(s.Memory.FieldAddress("wMakuMapTextPresent"))||
                        s.Transitions.NativePaletteThreadMode!=native.ReadNativeByte(s.Memory.FieldAddress("wPaletteThread_mode")),
                        $"Remote Maku update{updates}: input/map/palette thread differs: objects${s.DisabledObjects:x2}/${native.ReadNativeByte(s.Memory.FieldAddress("wDisabledObjects")):x2}, palette${s.Transitions.NativePaletteThreadMode:x2}/${native.ReadNativeByte(s.Memory.FieldAddress("wPaletteThread_mode")):x2}.");
                    foreach(var leaf in entities.EntityAdapters<SeasonsMakuLeafRoomEntity>())
                    {
                        sawLeaf=true;int slot=entities.NativeInteractionSlot(leaf);int page=(0xd0+slot)<<8;
                        foreach(int offset in new[]{0x42,0x44,0x46,0x49,0x4a,0x4b,0x4c,0x4d,0x50,0x7a,0x7b,0x7c})
                            FailIf(s.Memory.ReadWramByte(page+offset)!=native.ReadNativeByte(page+offset),
                                $"Remote Maku update{updates}/leafslot${slot:x2}/byte${offset:x2}: ${s.Memory.ReadWramByte(page+offset):x2}/${native.ReadNativeByte(page+offset):x2}; native enabled/id/sub/state ${native.ReadNativeByte(page+0x40):x2}/${native.ReadNativeByte(page+0x41):x2}/${native.ReadNativeByte(page+0x42):x2}/${native.ReadNativeByte(page+0x44):x2}, XY{leaf.Position}, counter${s.Memory.ReadWramByte(page+0x46):x2}/${native.ReadNativeByte(page+0x46):x2}.");
                    }
                    foreach(var adapter in entities.EntityAdapters<InteractionSparkleRoomEntity>())
                    {
                        sawSparkle=true;int slot=entities.NativeInteractionSlot(adapter);int sparkle=((0xd0+slot)<<8)|0x40;
                        FailIf(native.ReadNativeByte(sparkle+1)!=0x84||adapter.Node.Position!=new Vector2(native.Word(sparkle+12)>>8,native.Word(sparkle+10)>>8),
                            $"Remote Maku update{updates}: native $84:$02 component movement differs.");
                    }
                    foreach(int sound in new[]{0x1e,0x83})FailIf(s.Sound.PlayRequestsFor(sound)!=native.Sounds.Count(value=>value==sound),
                        $"Remote Maku update{updates}: native sound${sound:x2} differs.");
                    if(s.Hud.StatusBarHidden&&native.ReadNativeByte(0xc2ff)!=0)
                    {
                        var color=s.Hud.HiddenStatusBarColorForValidation;
                        int packed=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(packed!=(native.FadingBackgroundColor(2)&0x7fff),$"Remote Maku update{updates}: blank status strip must follow the exact native palette fade.");
                    }
                    if(s.Dialogue.IsOpen)
                    {
                        sawText=true;FailIf(s.Dialogue.SourceTextId!=0x1704||native.ReadNativeByte(0xcba3)!=0x1b||native.ReadNativeByte(0xcba2)!=4,
                            $"The native D1 branch must resolve TX_1704 through the call/table/helper/return chain: update{updates}, port{s.Dialogue.SourceTextId:x4}, native{native.ReadNativeByte(0xcba3):x2}{native.ReadNativeByte(0xcba2):x2}, active{native.ReadNativeByte(0xcba0):x2}.");
                    }
                }
                for(int tick=0;tick<700&&!s.Dialogue.IsOpen;tick++)
                {
                    if(!declaredAudio&&entities.EntityAdapters<SeasonsMakuLeafRoomEntity>().Any(leaf=>
                        s.Memory.ReadWramByte(((0xd0+entities.NativeInteractionSlot(leaf))<<8)|0x7c)==1))
                    {
                        // Audio updates separately. Declare its live channel
                        // inputs to exercise the clean-US HL alias's zero branch.
                        s.Sound.WriteNativeAllocationAlias(0xc04d,0);s.Sound.WriteNativeAllocationAlias(0xc07b,1);
                        native.SetNativeByte(0xc04d,0);native.SetNativeByte(0xc07b,1);declaredAudio=true;
                    }
                    int stride=batched&&sawAudioAlias?2:1;
                    app.Step(stride,Vector2.Zero,batched:batched,afterUpdate:Compare);
                }
                FailIf(!sawText||!sawLeaf||!sawSparkle||!sawAudioAlias,"The remote Maku comparison must cover its live leaves, sparkles, audio alias and dialogue.");
                app.Step(8,Vector2.Zero,batched:batched,afterUpdate:Compare);s.Dialogue.Close();native.SetNativeByte(s.Memory.FieldAddress("wTextIsActive"),0);
                app.Step(150,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(s.DisabledObjects!=0||!s.Save.HasRoomFlag(0,0x96,0x40)||entities.EntityAdapters<SeasonsRemoteMakuRoomEntity>().Any()||native.ReadNativeByte(at)!=0,
                    "Remote Maku must release both native controls, delete and retain its room bit$40 after completion.");
            }
            finally {root.Free();}
        }
    }
}
