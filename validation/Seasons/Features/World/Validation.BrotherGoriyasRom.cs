namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsBrotherGoriyasRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                s.DebugTravel(4,0x0b);s.Link.WarpTo(new(120,136));s.Link.BeginCutsceneControl();entities.DisableLinkCollisionsAndMenu();
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                FailIf(entities.Entities<BrotherGoriyaCharacter>().Count!=1||entities.RoomEnemyCount!=1,
                    "The original $4:0b before-event stream must place one counted Brother Goriya $70:$00.");
                var seed=entities.Random.CaptureState();
                var native=SharedNativeEnemyReference(s.Rooms,s.Link,0x70);native.LoadDungeon(1);
                native[0xcc0b]=0x73; // PART$38's sheet is resident for the AI comparison.
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                native.SetNativeByte(s.Memory.FieldAddress("wActiveGroup"),4);native.SetNativeByte(s.Memory.FieldAddress("wActiveRoom"),0x0b);
                native.SetNativeByte(s.Memory.FieldAddress("wScrollMode"),1);
                native.SetNativeByte(s.Memory.FieldAddress("wNumEnemies"),1);
                native.SetNativeByte(s.Memory.FieldAddress("wActiveMusic2"),(byte)s.Sound.NativeRoomMusic);
                native.SetNativeByte(s.Memory.FieldAddress("wActiveMusic"),(byte)s.Sound.NativeActiveMusic);
                native[0xd080]=0x11;native[0xd081]=0x70;native[0xd08b]=40;native[0xd08d]=72;
                byte textBefore=0;int update=0;bool sawPart=false,sawCatch=false,sawIntro=false,sawDeletedReceiver=false;
                var states=new HashSet<int>();s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();native.ClearSounds();
                app.Step(1100,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.SetNativeByte(s.Memory.FieldAddress("wTextIsActive"),textBefore);
                    native.SetNativeByte(s.Memory.FieldAddress("wDisabledObjects"),s.Memory.ReadField("wDisabledObjects"));
                    native.Word(0xd00a,(int)(s.Link.PrecisePosition.Y*256));native.Word(0xd00c,(int)(s.Link.PrecisePosition.X*256));
                    int target=s.Memory.Game==OracleGame.Seasons?0xffae:0xffb0;
                    native[target]=(byte)s.Link.Position.Y;native[target+1]=(byte)s.Link.Position.X;
                    native[target+2]=(byte)s.Link.Position.Y;native[target+3]=(byte)s.Link.Position.X;
                    native.AdvanceEnemies(entities.FrameCounter);native.AdvanceParts();update++;
                    var actors=entities.Entities<BrotherGoriyaCharacter>();
                    int count=Enumerable.Range(0,16).Count(slot=>native[0xd080+slot*256]!=0);
                    FailIf(actors.Count!=count,$"Goriya update{update}: enemy allocation {actors.Count}/{count}.");
                    foreach(var actor in actors)
                    {
                        int at=0xd080+actor.Slot*256;states.Add(actor.State);
                        FailIf(actor.SubId!=native[at+2]||actor.State!=native[at+4]||actor.Substate!=native[at+5]||
                            actor.Counter!=native[at+6]||actor.Counter2!=native[at+7]||actor.Angle!=native[at+9]||actor.Speed!=native[at+0x10]||
                            actor.Position!=new Vector2(native.Word(at+0xc)/256f,native.Word(at+0xa)/256f)||
                            actor.Animation.RemainingUpdates!=native[at+0x20]||actor.AnimationParameter!=native[at+0x21]||
                            actor.BoomerangHigh!=native[at+0x19]||actor.HasBoomerang!=(native[at+0x30]!=0)||
                            actor.Visible!=((native[at+0x1a]&128)!=0)||actor.CollisionEnabled!=((native[at+0x24]&128)!=0),
                            $"Goriya batch{batched}/update{update}/slot${actor.Slot:x2}: sub{actor.SubId}/{native[at+2]}, state{actor.State}:{actor.Substate}/{native[at+4]}:{native[at+5]}, counter{actor.Counter}:{actor.Counter2}/{native[at+6]}:{native[at+7]}, angle{actor.Angle}/{native[at+9]}, speed{actor.Speed}/{native[at+0x10]}, XY{actor.Position}/{native.Word(at+0xc)/256f},{native.Word(at+0xa)/256f}, animation{actor.Animation.RemainingUpdates}/{native[at+0x20]}, ptr${actor.BoomerangHigh:x2}/${native[at+0x19]:x2}.");
                    }
                    var parts=entities.EntityAdapters<BrotherGoriyaBoomerangRoomEntity>().ToArray();
                    sawDeletedReceiver|=actors.Count==0&&parts.Length!=0;
                    int partCount=Enumerable.Range(0,16).Count(slot=>native[0xd0c0+slot*256]!=0&&native[0xd0c1+slot*256]==0x38);
                    FailIf(parts.Length!=partCount,$"Goriya update{update}: PART$38 allocation/lifetime differs.");
                    foreach(var part in parts)
                    {
                        int slot=Enumerable.Range(0,16).Single(slot=>ReferenceEquals(entities.ResolvePartSlot(slot),part));
                        int at=0xd0c0+slot*256;sawPart=true;
                        FailIf(part.State!=native[at+4]||part.Counter!=native[at+6]||part.Angle!=native[at+9]||
                            part.Position!=new Vector2(native.Word(at+0xc)/256f,native.Word(at+0xa)/256f),
                            $"Goriya PART$38 update{update}: state{part.State}/{native[at+4]}, counter{part.Counter}/{native[at+6]}, angle{part.Angle}/{native[at+9]}, XY{part.Position}/{native.Word(at+0xc)/256f},{native.Word(at+0xa)/256f}.");
                    }
                    FailIf(entities.RoomEnemyCount!=native.ReadNativeByte(s.Memory.FieldAddress("wNumEnemies")) ||
                        entities.EntityAdapters<BossDeathExplosionRoomEntity>().Count()!=
                            Enumerable.Range(0,16).Count(slot=>native[0xd0c0+slot*256]!=0&&native[0xd0c1+slot*256]==4),
                        $"Goriya update{update}: counted enemies/explosions differ.");
                    sawCatch|=sawPart&&parts.Length==0;sawIntro|=s.Dialogue.SourceTextId==0x2f00;
                    var random=entities.Random.CaptureState();
                    FailIf(random.Rng1!=native[0xff94]||random.Rng2!=native[0xff95]||random.Calls-seed.Calls!=native.RandomCalls,
                        $"Goriya update{update}: ordered shared RNG differs.");
                    FailIf(s.Sound.NativeActiveMusic!=native.ReadNativeByte(s.Memory.FieldAddress("wActiveMusic")),
                        $"Goriya update{update}: native active music differs from the sound owner.");
                    foreach(int sound in new[]{SoundId.SndCtrlStopMusic,0x2d,s.Sound.NativeRoomMusic})
                        FailIf(s.Sound.PlayRequestsFor(sound)!=native.Sounds.Count(cue=>cue==sound),$"Goriya update{update}: sound${sound:x2} differs.");
                    if(update==20)s.Link.WarpTo(new(120,104));
                    if(update==30)s.Dialogue.Close();
                    if(update==320)s.Link.WarpTo(new(32,48));
                    if(update==440)s.Link.WarpTo(new(120,104));
                    if(update==700)
                    {
                        // AI/death input staging; Sword contact is exercised separately.
                        var actor=actors.Single(actor=>actor.SubId==0);actor.Health=0;
                        native[0xd0a9+actor.Slot*256]=0;
                        s.Memory.SetField("wDisabledObjects",8);
                    }
                    if(update==825)s.Memory.SetField("wDisabledObjects",0);
                    textBefore=(byte)(entities.TextActiveSource()?1:0);
                });
                string summary=string.Join(';',entities.Entities<BrotherGoriyaCharacter>().Select(actor=>$"{actor.State}:{actor.Substate}@{actor.Position}"));
                FailIf(!sawIntro||!sawPart||!sawCatch||!new[]{8,9,10,11}.All(states.Contains),
                    $"The paired native comparison must cover intro, approach, circulation/wander and a complete boomerang transfer: intro{sawIntro}, part{sawPart}, catch{sawCatch}, states{string.Join(',',states)}, text{entities.TextActiveSource()}, actors{summary}.");
                FailIf(entities.RoomEnemyCount!=0||entities.Entities<BrotherGoriyaCharacter>().Count!=0||
                    !s.Save.HasRoomFlag(4,0x0b,0x80)||entities.LinkCollisionsAndMenuDisabled||
                    entities.EntityAdapters<MinibossPortalRoomEntity>().Count()!=1||!sawDeletedReceiver||
                    entities.EntityAdapters<BrotherGoriyaBoomerangRoomEntity>().Any()||
                    s.Sound.ActiveMusic!=s.Sound.NativeRoomMusic,
                    "Both original death tails and counted explosions must hand off to one enabled miniboss portal.");
                s.Link.EndCutsceneControl();entities.EnableLinkCollisionsAndMenu();
            }
            finally {root.Free();}
        }
    }
}
