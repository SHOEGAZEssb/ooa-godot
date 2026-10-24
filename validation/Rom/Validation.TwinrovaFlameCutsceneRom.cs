using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaFlameCutsceneRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf1, 0x80, false);
            LoadValidationRoom(5, 0xf5);
            foreach (var enemy in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray()) _entities.RetireEntity(enemy);
            _entities.SetNativeRoomEnemyCount(1); // The split pair's native count survives clearWramBank1.
            _player.WarpTo(new(0x78, 0x78)); _player.Face(Vector2I.Up); _transitions.ResetCamera();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x78);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcdd1] = 1;
            Vector2 origin = -_transitions.WorldToGameplayScreen(Vector2.Zero);
            rom[0xffac] = (byte)origin.X; rom[0xffaa] = (byte)origin.Y;
            rom[0xc2ef] = 0x18; rom[0xcc03] = 0;
            var owner = _roomEvents.Get<RoomOfRitesBattleEvent>(); owner.Begin(0x18);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0; bool sawCalm = false, sawAngry = false, sawRestart = false;
            // Individual cutscene boundary, not a dungeon playthrough. Retain
            // the native caller on its terminal update, then finish the fade.
            StepGameplayUpdates(400, Vector2.Zero, batched: batched, afterUpdate: CompareNative);
            while (owner.NativeActive && update < 650)
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: CompareNative);
            _sound.SetRequestObserver(null);
            FailIf(!sawCalm || !sawAngry || !sawRestart, "Flame cutscene missed an original phase or its fresh enemy handoff.");
            StepGameplayUpdates(80, Vector2.Zero, batched: batched);
            FailIf(owner.HasState || _transitions.NativePaletteFadeActive, "Flame cutscene retained ownership after the return fade.");
            owner.Cancel(); owner.Cancel();

            void CompareNative()
            {
                    rom.AdvanceTwinrovaFlameCutscene(_entities.FrameCounter); update++;
                    FailIf(owner.NativeActive != (rom[0xc2ef] == 0x18) || owner.State != rom[0xcc03] && owner.NativeActive ||
                        _rooms.CurrentRoom.Id != rom[0xcc30] || _transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0),
                        $"Flame cutscene update{update}: state={owner.State}/{rom[0xcc03]}, active={owner.NativeActive}/{rom[0xc2ef]:x2}, room={_rooms.CurrentRoom.Id:x2}/{rom[0xcc30]:x2}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]:x2}.");
                    FailIf(_player.Position != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                        _player.Visible != ((rom[0xd01a] & 128) != 0),
                        $"Flame cutscene update{update}: Link={_player.Position}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, visible={_player.Visible}/{rom[0xd01a]:x2}.");
                    foreach (var flame in _entities.EntityAdapters<TwinrovaFlameRoomEntity>())
                    {
                        int at = 0xd040 + _entities.NativeInteractionSlot(flame)*256;
                        FailIf(rom[at+1] != 0xa9 || flame.Subid != rom[at+2] || flame.State != rom[at+4] ||
                            flame.Visible != ((rom[at+0x1a]&128)!=0) || flame.Position != new Vector2(rom[at+13],rom[at+11]),
                            $"Flame cutscene update{update},frame={_entities.FrameCounter}: interaction${at:x4} flame differs; id={rom[at+1]:x2},sub={flame.Subid}/{rom[at+2]},state={flame.State}/{rom[at+4]},visible={flame.Visible}/{rom[at+0x1a]:x2},pos={flame.Position}/{rom[at+13]},{rom[at+11]}; slots={string.Join(';',Enumerable.Range(2,14).Select(s=>$"{s:x1}:{rom[0xd040+s*256]:x2},{rom[0xd041+s*256]:x2},{rom[0xd042+s*256]:x2}"))}.");
                        sawCalm |= flame.Subid < 6; sawAngry |= flame.Subid >= 6;
                    }
                    var debris = _entities.EntityAdapters<GrassDebrisRoomEntity>().ToArray();
                    int nativeDebris = Enumerable.Range(2,14).Count(slot => rom[0xd040+slot*256]!=0 && rom[0xd041+slot*256]==0);
                    FailIf(debris.Length != nativeDebris || debris.Any(effect =>
                        rom[0xd041+_entities.NativeInteractionSlot(effect)*256] != 0 ||
                        rom[0xd040+_entities.NativeInteractionSlot(effect)*256] == 0),
                        $"Flame cutscene update{update}: cleared-ID grass debris occupancy/lifetime differs.");
                    FailIf(_entities.ScreenShakeCounter != rom[0xcd18] || _entities.HorizontalScreenShakeCounter != rom[0xcd19] ||
                        -_transitions.WorldToGameplayScreen(Vector2.Zero) != new Vector2(rom[0xffac],rom[0xffaa]),
                        $"Flame cutscene update{update}: native camera/shake differs; camera={-_transitions.WorldToGameplayScreen(Vector2.Zero)}/{rom[0xffac]},{rom[0xffaa]},shake={_entities.ScreenShakeCounter},{_entities.HorizontalScreenShakeCounter}/{rom[0xcd18]},{rom[0xcd19]}.");
                    FailIf(_entities.RoomEnemyCount != rom[0xcdd1] || _runtimeState.ReadWramByte(0xcbb4) != rom[0xcbb4] || _saveData.GetRoomFlags(5,0x60) != rom[0xca60] ||
                        RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls) || !sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Flame cutscene update{update}: count={_runtimeState.ReadWramByte(0xcbb4)}/{rom[0xcbb4]}, save={_saveData.GetRoomFlags(5,0x60):x2}/{rom[0xca60]:x2}, RNG={_random.Calls-seed.Calls}/{rom.RandomCalls}, sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                    if (!owner.NativeActive)
                    {
                        var twins = _entities.EntityAdapters<TwinrovaRoomEntity>().OrderBy(t=>t.Slot).ToArray();
                        FailIf(twins.Length != 2 || twins.Any(t=> (t.Actor.Var03&128)==0) || _entities.RoomEnemyCount != 2,
                            "cutscene18_state5 must allocate a fresh counted ENEMY $03 with var03=$80 and its uncounted twin.");
                        foreach(var twin in twins)
                        {
                            int at=0xd080+twin.Slot*256;
                            FailIf(twin.Actor.State!=rom[at+4] || twin.Actor.Subid!=rom[at+2] || twin.Actor.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f),
                                "Flame cutscene fresh enemy initialization differs from the terminal native object pass.");
                        }
                        sawRestart = true;
                    }
            }
        }
        GD.Print("Validated Twinrova flame cutscene room/object clears, ordered actors, flicker, exact timers, shared RNG/audio and fresh fight handoff against the clean ROM.");
    }
}
