using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom3efSeaFairyRom()
    {
        var data = new SeaFairyDatabase();
        foreach (var text in data.Commands.OfType<CutsceneShowTextCommand>())
            RunDialogueRom(text.TextId, text.Message, 3, batched: true, accelerate: true);
        RunDialogueRom(0x4109, data.Thanks, 3, batched: true, accelerate: true);

        foreach (bool batched in new[] { false, true })
        {
            foreach (int subid in new[] { 0x0a, 0x0b, 0x0c, 0x0d })
            {
                ReinitializeGameplayForValidation(); LoadValidationRoom(3, 0xef);
                _roomEvents.Get<SeaFairyEvent>().Cancel();
                _entities.LoadCutsceneRoom(3, _rooms.CurrentRoom, includeTimePortals: false);
                _runtimeState.SetWramByte(WramAddress.wTmpcfc0, 0);
                var reference = new SomariaRom(_saveData, _random.CaptureState(), _rooms.CurrentRoom, 0, 0x50, 0x68);
                reference[0xd240] = 1; reference[0xd241] = 0xab; reference[0xd242] = (byte)subid;
                reference[0xd24b] = 0x28; reference[0xd24d] = 0x28; reference[0xcfc0] = 0;
                _entities.Spawn(new CleanSeasZoraSpawn(data, new(0x28, 0x28), subid));
                var zora = _entities.EntityAdapters<CleanSeasZoraRoomEntity>().Single();
                int tick = 0;
                StepGameplayUpdates(330, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    reference.AdvanceInteractions(_entities.FrameCounter); tick++;
                    FailIf(zora.Node.Position != new Vector2(reference[0xd24d], reference[0xd24b]) ||
                        (SomariaPrivate<int>(zora, "_z") & 0xffff) != reference.Word(0xd24e) ||
                        _runtimeState.ReadWramByte(WramAddress.wTmpcfc0) != reference[0xcfc0] ||
                        SomariaPrivate<int>(zora, "_state") != reference[0xd244],
                        $"Clean-seas $ab:${subid:x2} native XY/Z/state/signal differs at update {tick}, batch={batched}: " +
                        $"port={zora.Node.Position}/{SomariaPrivate<int>(zora, "_z") & 0xffff:x4}/" +
                        $"{SomariaPrivate<int>(zora, "_state")}/{_runtimeState.ReadWramByte(WramAddress.wTmpcfc0):x2}, " +
                        $"ROM={reference[0xd24d]},{reference[0xd24b]}/{reference.Word(0xd24e):x4}/{reference[0xd244]}/{reference[0xcfc0]:x2}.");
                });
            }
            foreach (bool powder in new[] { false, true })
            {
                ReinitializeGameplayForValidation(); LoadValidationRoom(3, 0xef);
                _roomEvents.Get<SeaFairyEvent>().Cancel();
                _entities.LoadCutsceneRoom(3, _rooms.CurrentRoom, includeTimePortals: false);
                var reference = new SomariaRom(_saveData, _random.CaptureState(), _rooms.CurrentRoom, 0, 0x50, 0x68);
                reference[0xd0c0] = 1; reference[0xd0c1] = 0x26; reference[0xd0c3] = powder ? (byte)1 : (byte)0;
                reference[0xd0cb] = 0x28; reference[0xd0cd] = 0x50;
                _entities.Spawn(new SeaPowderSpawn(data, new(0x50, 0x28), powder));
                var part = _entities.EntityAdapters<SeaPowderRoomEntity>().Single();
                int tick = 0;
                StepGameplayUpdates(180, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    reference.AdvanceParts(_entities.FrameCounter); tick++;
                    bool nativeActive = reference[0xd0c0] != 0;
                    FailIf(!part.Finished != nativeActive || nativeActive &&
                        ((SomariaPrivate<int>(part, "_x") & 0xffff) != reference.Word(0xd0cc) ||
                         (SomariaPrivate<int>(part, "_z") & 0xffff) != reference.Word(0xd0ce) ||
                         SomariaPrivate<int>(part, "_counter") != reference[0xd0c6]),
                        $"Sea powder $26 var03={(powder ? 1 : 0)} XY/Z/counter/lifetime differs at update {tick}, batch={batched}.");
                });
            }
            ReinitializeGameplayForValidation();
            LoadValidationRoom(3, 0xef);
            _roomEvents.Get<SeaFairyEvent>().Cancel();
            _entities.LoadCutsceneRoom(3, _rooms.CurrentRoom, includeTimePortals: false);
            _player.WarpTo(new(0x50, 0x68));
            _inventory.EquipA(0); _inventory.EquipB(0);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x11);
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 1);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _rooms.CurrentRoom, 0, 0x50, 0x68);
            rom[0xcc8a] = 0x11; rom[0xcc02] = 1;
            rom[0xd240] = 1; rom[0xd241] = 0xd5; rom[0xd242] = 1;
            rom[0xd24b] = 0x28; rom[0xd24d] = 0x50;
            bool fade = false;
            _entities.Spawn(new CleansedFairySpawn(data, new(0x50, 0x28), () => fade = true, () => { }));
            var queen = _entities.EntityAdapters<CleansedFairyRoomEntity>().Single();
            int update = 0;
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    // Execute native object code and scripts. This bounded fixture
                    // controls only textbox lifetime; complete text is compared above.
                    rom[0xcba0] = (byte)(_dialogue.IsOpen ? 1 : 0);
                    rom.AdvanceParts(_entities.FrameCounter);
                    rom.AdvanceInteractions(_entities.FrameCounter);
                    string label = $"Room3:ef $d5:$01 batch={batched}, update{++update}";
                    FailIf(queen.State != rom[0xd245] || queen.Counter != rom[0xd246] ||
                        queen.Height != unchecked((sbyte)rom[0xd24f]) || queen.Angle != rom[0xd249] ||
                        queen.Node.Position != new Vector2(rom[0xd24d], rom[0xd24b]),
                        label + $": state/counter/Z/angle/XY port={queen.State}/{queen.Counter}/{queen.Height}/{queen.Angle}/{queen.Node.Position}, " +
                        $"ROM={rom[0xd245]}/{rom[0xd246]}/{unchecked((sbyte)rom[0xd24f])}/{rom[0xd249]}/{rom[0xd24d]},{rom[0xd24b]}.");
                });
            }
            Step(181); Step(10); _dialogue.Close();
            for (int i = 0; i < 180 && !fade; i++) Step(1);
            FailIf(!fade || queen.State != 3 || rom[0xc4ab] == 0,
                "Clean-US $d5:$01 must reach its white-fade request after TX_4109 and the circle/rise tail.");

            ReinitializeGameplayForValidation(); LoadValidationRoom(3, 0xef);
            _saveData.SetGlobalFlag(0x30, false);
            _roomEvents.Get<SeaFairyEvent>().Cancel();
            var clean = _roomEvents.Get<CleanSeasEvent>();
            rom = new SomariaRom(_saveData, _random.CaptureState(), _rooms.CurrentRoom, 2, 0x50, 0x68);
            var native = SomariaPrivate<FrontendRom>(rom, "_rom");
            rom[0xc2ef] = 0x1e; rom[0xcc03] = 0;
            // Enter after the queen's already-compared white fade has finished.
            clean.BeginFade(); _transitions.CancelNativeWhiteFade(); clean.Begin();
            update = 0;
            void CleanStep(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom[0xcc00] = (byte)_entities.FrameCounter;
                rom.AdvanceWarpPalette();
                if (rom[0xc2ef] == 0x1e) native.Call(0x7619, 3);
                rom.AdvanceParts(_entities.FrameCounter); rom.AdvanceInteractions(_entities.FrameCounter);
                rom.AdvanceTileGraphics(); update++;
                string label = $"CUTSCENE_CLEAN_SEAS $1e update{update}, batch={batched}";
                int phase = rom[0xcc03] == 6 ? 4 : rom[0xcbb3];
                FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30] ||
                    clean.NativeActive != (rom[0xc2ef] == 0x1e) || clean.NativeActive &&
                    (clean.RoomIndex + 1 != (rom[0xcc03] == 6 ? 5 : rom[0xcc03]) || clean.Phase != phase ||
                     clean.Phase > 0 && clean.Counter != rom[0xcbb4]),
                    label + $": room/state/phase/counter port={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}/" +
                    $"{clean.RoomIndex + 1}/{clean.Phase}/{clean.Counter}, ROM={rom[0xcc2d]:x}:{rom[0xcc30]:x2}/" +
                    $"{rom[0xcc03]}/{phase}/{rom[0xcbb4]}.");
                if (!clean.NativeActive) return;
                FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02], label + ": native object/menu masks differ.");
                FailIf(_transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0) ||
                    _runtimeState.ReadWramByte(WramAddress.wTmpcfc0) != rom[0xcfc0],
                    label + $": palette/signal port={_transitions.NativePaletteFadeActive}/${_runtimeState.ReadWramByte(WramAddress.wTmpcfc0):x2}, " +
                    $"ROM=${rom[0xc4ab]:x2}/${rom[0xcfc0]:x2}, roomIndex={clean.RoomIndex}, phase={clean.Phase}, counter={clean.Counter}.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95], label + ": shared powder RNG differs.");
                for (int y = 0; y < _rooms.CurrentRoom.HeightInTiles; y++)
                for (int x = 0; x < _rooms.CurrentRoom.WidthInTiles; x++)
                    FailIf(_rooms.CurrentRoom.GetPackedStorageMetatile((byte)(y * 16 + x)) != rom[0xcf00 + y * 16 + x],
                        label + $": polluted tile replacement differs at ${y * 16 + x:x2}.");
            });
            while (clean.NativeActive && update < 4000) CleanStep(batched ? 7 : 1);
            FailIf(clean.NativeActive || !_saveData.HasGlobalFlag(0x30) || (rom[0xc6d6] & 1) == 0,
                "Native sea-cleaning comparison did not reach $30 completion.");
            foreach (int address in new[] { 0xc62b, 0xc62c, 0xc62d, 0xc62e, 0xc62f, 0xc630, 0xc631, 0xc632, 0xc633, 0xc634, 0xc636, 0xc637 })
                FailIf(_saveData.ReadWramByte(address) != rom[address],
                    $"CUTSCENE_CLEAN_SEAS death checkpoint byte ${address:x4} differs from the ROM.");
        }
        GD.Print("Compared room3:ef complete text, $ab:$0a-$0d, $26 powder, $d5:$01 and $1e cleanup rooms/counters/palettes/tiles/RNG with clean-US ROM.");
    }
}
