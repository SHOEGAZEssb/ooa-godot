namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsSubrosiaPortal()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var s = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                s.Save.SetRoomFlag(0, 0xcb, 0x80); s.Save.SetRoomFlag(0, 0x9a, 0x40);
                s.Inventory.GiveTreasure(TreasureId.Sword, 1);
                s.Inventory.EquipA(TreasureId.Sword); s.Inventory.EquipB(0);
                s.Memory.SetField("wRoomStateModifier", (byte)s.Data.World.DefaultSeason(s.Data.World.RoomPack(0x9a), s.Save.ReadField("wAnimalCompanion")));
                s.DebugTravel(0, 0x9a);
                var portal = s.Entities.EntityAdapters<SeasonsPortalSpawnerRoomEntity>().Single();
                FailIf(s.Room.GetPackedStorageMetatile(0x33) != 0xc5,
                    "Rosa's completed chase must leave its covered portal at $0:9a:$33.");
                var approach = (Direction: Vector2.Left, Position: new Vector2(104,56));
                FailIf(s.Collision.Collides(approach.Position), "The garden's eastern entrance must be collision-valid.");
                s.Link.WarpTo(approach.Position); s.Link.Face((Vector2I)approach.Direction);
                for (int swing = 0; swing < 4 && s.Room.GetPackedStorageMetatile(0x33) != 0xe6; swing++)
                {
                    app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"]);
                    app.Step(32, Vector2.Zero, batched: batched);
                    for (int walk = 0; walk < 24 && s.Link.PrecisePosition.DistanceTo(new(56,56)) > 16; walk++)
                    {
                        Vector2 before = s.Link.PrecisePosition;
                        app.Step(1, approach.Direction);
                        if (s.Link.PrecisePosition == before) break;
                    }
                }
                FailIf(s.Room.GetPackedStorageMetatile(0x33) != 0xe6 || !portal.Npc.Visible || !s.Save.HasRoomFlag(0,0x9a,8),
                    $"Actual sword swings through the garden must uncover Rosa's portal at $0:9a:$33: tile=${s.Room.GetPackedStorageMetatile(0x33):x2}, Link={s.Link.Position}.");
                int health = s.Inventory.HealthQuarters;
                int visits = 0;
                while (!s.Transitions.IsTransitioning && visits++ < 90) app.Step(1, approach.Direction);
                FailIf(!s.Transitions.IsTransitioning, "Link must enter the uncovered portal through the garden's real approach.");
                void CompletePortal(int group, int room, Vector2 position)
                {
                    int ticks = 0, substates = 0;
                    var native = new BraceletRom(s.Room, s.Link.PrecisePosition, group == 1 ? 3 : 0,
                        s.Inventory.ActiveRing, game: OracleGame.Seasons);
                    native.LoadSave(s.Save);
                    native.InitializeWalkingAnimation();
                    native.SetNativeByte(0xc2ef,0);
                    native.SetNativeByte(0xcc03,s.Memory.ReadField("wCutsceneState"));
                    native.SetNativeByte(0xcc49,(byte)s.Rooms.ActiveGroup); native.SetNativeByte(0xcc4c,(byte)s.RoomId);
                    native.SetNativeByte(0xcc55,0xff); native.SetNativeByte(0xcc62,(byte)s.Sound.NativeRoomMusic);
                    native.SetNativeByte(0xcc63,(byte)group); native.SetNativeByte(0xcc64,(byte)(group == 1 ? 6 : 0x1a));
                    native.SetNativeByte(0xcc65,8); native.SetNativeByte(0xd004,0x0a);
                    native.SetNativeByte(s.Memory.FieldAddress("wRoomStateModifier"),s.Memory.ReadField("wRoomStateModifier"));
                    for(int address=0xcbb3;address<=0xcbbf;address++) native.SetNativeByte(address,s.Memory.ReadWramByte(address));
                    for(int address=0xc300;address<0xc400;address++) native.SetNativeByte(address,s.Memory.ReadWramByte(address));
                    var seed = s.Random.CaptureState(); native.SetNativeByte(0xff94,seed.Rng1); native.SetNativeByte(0xff95,seed.Rng2);
                    native.ClearSounds(); s.Sound.AttachPlayRequestAudit(); s.Sound.ClearPlayRequestAudit();
                    bool finished = false;
                    void Compare()
                    {
                        if(finished)return;
                        ticks++; substates |= 1 << s.Memory.ReadWramByte(0xd005);
                        bool wave = native.ReadNativeByte(0xc2ef)==0x15 &&
                            (native.ReadNativeByte(0xcc03)==1 && native.ReadNativeByte(0xcbb3)==1 ||
                             native.ReadNativeByte(0xcc03)==2 && native.ReadNativeByte(0xcbb3)==0);
                        native.AdvanceSubrosiaBeforeLink(s.FrameCounter); native.AdvanceSubrosiaLink();
                        foreach(int offset in new[]{4,5,6,8,0x0e,0x0f,0x14,0x15})
                            FailIf(s.Memory.ReadWramByte(0xd000+offset)!=native.ReadNativeByte(0xd000+offset),
                                $"Subrosia ${group:x1}:${room:x2}/tick{ticks}: Link byte${offset:x2} differs: port${s.Memory.ReadWramByte(0xd000+offset):x2}, ROM${native.ReadNativeByte(0xd000+offset):x2}.");
                        foreach(string field in new[]{"wCutsceneState","wGenericCutscene.cbb3","wGenericCutscene.cbbc","wGenericCutscene.cbbd","wDisabledObjects","wMenuDisabled"})
                            FailIf(s.Memory.ReadField(field)!=native.ReadNativeByte(s.Memory.FieldAddress(field)),
                                $"Subrosia tick{ticks}: {field} differs: port${s.Memory.ReadField(field):x2}, ROM${native.ReadNativeByte(s.Memory.FieldAddress(field)):x2}.");
                        FailIf(s.Link.PrecisePosition != new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                            s.Rooms.ActiveGroup!=native.ReadNativeByte(0xcc49) || s.RoomId!=native.ReadNativeByte(0xcc4c),
                            $"Subrosia tick{ticks}: native room or exact Link position differs.");
                        if(wave)
                            for(int address=0xc300;address<0xc400;address++)
                                FailIf(s.Memory.ReadWramByte(address)!=native.ReadNativeByte(address),
                                    $"Subrosia tick{ticks}: inverted native wave differs at ${address:x4}.");
                        foreach(int cue in new[]{SoundId.SndFadeOut,SoundId.SndCtrlStopSfx})
                            FailIf(s.Sound.PlayRequestsFor(cue)!=native.Sounds.Count(sound=>sound==cue),
                                $"Subrosia tick{ticks}: cue ${cue:x2} differs.");
                        int paletteMode = native.ReadNativeByte(s.Memory.FieldAddress("wPaletteThread_mode"));
                        FailIf(s.Transitions.NativePaletteThreadMode != paletteMode,
                            $"Subrosia tick{ticks}: native delayed palette-thread lifetime differs.");
                        if(paletteMode!=0)
                            FailIf(!Mathf.IsEqualApprox(root._scene.WarpFade.Color.A, Math.Min((int)native.ReadNativeByte(0xc2ff),31)/31f),
                                $"Subrosia tick{ticks}: source palette offset $c2ff must drive the visible white fade.");
                        finished=!s.Transitions.IsTransitioning;
                    }
                    while (s.Transitions.IsTransitioning && ticks < 400)
                    {
                        app.Step(batched ? 4 : 1, Vector2.Zero, batched: batched, afterUpdate: () =>
                        { Compare(); });
                    }
                    FailIf(ticks >= 400 || s.Rooms.ActiveGroup != group || s.RoomId != room || s.Link.PrecisePosition != position ||
                        s.DisabledObjects != 0 || s.Memory.ReadField("wMenuDisabled") != 0 || s.Link.SubrosiaWarpActive ||
                        s.Link.FacingVector != Vector2I.Down || (substates & 0xfe) != 0xfe || s.Inventory.HealthQuarters != health,
                        $"Subrosia portal must complete all eight native Link substates at ${group:x1}:${room:x2}, XY={position}: actual ${s.Rooms.ActiveGroup:x1}:${s.RoomId:x2}, XY={s.Link.PrecisePosition}, substateMask=${substates:x2}, tick={ticks}.");
                }
                CompletePortal(1, 0x05, new(72,40));
                FailIf(s.Entities.EntityAdapters<SeasonsPortalSpawnerRoomEntity>().Count() != 1 ||
                    s.Entities.EntityAdapters<SeasonsTalkNpcRoomEntity>().Count(actor => actor.Npc.Record.Id == 0x30) != 2 ||
                    s.Entities.EntityAdapters<SeasonsTempleFallenTextRoomEntity>().Single().Npc.Visible,
                    "Subrosia $1:05 must load its placed portal, two Subrosians and the source's uninitialized invisible $6b:$26 script owner.");
                app.Step(8, Vector2.Zero, batched: batched);
                FailIf(s.Transitions.IsTransitioning || s.Memory.ReadField("wEnteredWarpPosition") != 0x24,
                    "Arrival must suppress an immediate return while Link remains on the portal.");
                app.Step(16, Vector2.Down, batched: batched);
                visits = 0;
                while (!s.Transitions.IsTransitioning && visits++ < 90) app.Step(1, Vector2.Up);
                FailIf(!s.Transitions.IsTransitioning, "Leaving and approaching the Subrosia portal again must select the native return warp.");
                CompletePortal(0, 0x9a, new(56,56));
                app.Step(8, Vector2.Zero, batched: batched);
                FailIf(s.Transitions.IsTransitioning || !s.Entities.EntityAdapters<SeasonsPortalSpawnerRoomEntity>().Single().Npc.Visible,
                    "Returning to $0:9a must preserve the uncovered portal without immediately retriggering it.");
            }
            finally { root.Free(); }
            CheckSeasonsSubrosiaCancellation(batched);
        }
    }
    private void CheckSeasonsSubrosiaCancellation(bool batched)
    {
        foreach (int updates in new[] { 0, 20, 150 })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var s = root._seasonsOpening!;
                s.DebugTravel(0, 0x9a); s.Link.WarpTo(new(104,56));
                // Stage only the source request: physical portal approach is covered above.
                s.Transitions.ApplyWarp(s.Link, new(0,0x9a,0x33,0,8,1,6,0,0,1,DestinationIsIndex:true));
                new ApplicationValidationFixture(root).Step(updates,Vector2.Zero,batched:batched);
                FailIf(!s.Link.SubrosiaWarpActive,"Cancellation fixture must retain the pending/source/arrival warp owner.");
                if(updates==20)
                { s.Memory.SetField("wDisabledObjects",0x44); s.Memory.SetField("wMenuDisabled",0x20); }
                s.Transitions.Dispose();
                FailIf(s.Link.SubrosiaWarpActive || s.Memory.ReadField("wLinkForceState") != 0 ||
                    s.Memory.ReadField("wDisabledObjects") != (updates==20?0x44:0) ||
                    s.Memory.ReadField("wMenuDisabled") != (updates==20?0x20:0) ||
                    s.Memory.ReadField("wCutsceneTrigger") == 0x15 || s.Memory.ReadWramByte(0xc2ef) == 0x15 ||
                    s.Transitions.NativePaletteFadeActive || SomariaPrivate<bool>(s.RoomView,"_waveActive"),
                    "Retiring a Subrosia warp must clear owned input/effects/requests while preserving later foreign mask writes.");
            }
            finally { root.Free(); }
        }
    }
}
