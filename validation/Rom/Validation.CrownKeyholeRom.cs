using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareCrownKeyholeRom()
    {
        int fixture = 0;
        foreach (bool owned in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.LoseTreasure(0x43);
            if (owned) _inventory.GiveTreasure(0x43,1);
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword);
            _saveData.SetRoomFlag(0,0x0a,0x80,false);
            LoadValidationRoom(0,0x0a);
            var allocation = RetainKeyholeControllerRom();
            var gate = _roomEvents.Get<CrownDungeonEntranceEvent>();
            _player.WarpTo(new(120,72)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120,24)) != 0xec,
                "Crown keyhole must use original room$0:$0a floor below tile$17:$ec.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,120,72);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x90; rom[0xd242] = 0x11;
            rom[0xd24b] = 24; rom[0xd24d] = 120;
            // The room's second object, $8a:$00/v$07, deletes in state0
            // without Essence5. Execute that native gate before descendants.
            FailIf((_saveData.ReadWramByte(WramAddress.wEssencesObtained) & 0x10) != 0,
                "Crown entrance fixture requires the pre-Essence5 room-entry branch.");
            rom[0xd340] = 1; rom[0xd341] = 0x8a; rom[0xd343] = 7;
            rom[0xcc35] = rom[0xcc46] = (byte)_sound.ActiveMusic;
            var camera = rom.CreateMenuView();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0,phases = 0;
            void Step(int count = 1,int angle = 0xff,int held = 0,int pressed = 0)
            {
                int sampled = 0;
                string[] Buttons(int mask) => new[] { "attack", "item" }.Where((_,bit) => (mask & (1 << bit)) != 0).ToArray();
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : Vector2.Up,
                    Buttons(held),Buttons(pressed),
                    batched:batch,afterUpdate:() =>
                {
                    rom.UpdateGameplay(sampled++ == 0 ? pressed : 0,held | (angle == 0xff ? 0 : 0x40),angle,_entities.FrameCounter);
                    rom[0xc48c] = rom[0xc48d] = 0;
                    camera.UpdateScreenShake();
                    string context = $"Crown$0:$0a owned={owned}, batch={batch}, update={++update}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(allocation.Initialized != (rom[0xd244] != 0) || allocation.Finished || rom[0xd240] == 0,
                        context + ": placed $90:$11 initialization/lifetime differs.");
                    FailIf(_keyholes.RemainingPushFrames != rom[0xcc6a] ||
                        _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + ": contact clock/hint/modal differs.");
                    FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) != rom[0xcfc0] ||
                        gate.BlocksGameplay != ((rom[0xcc8a] & 0x81) != 0) ||
                        _roomEvents.MenusDisabled != (rom[0xcc02] != 0) ||
                        gate.BlocksGameplay && gate.Counter != rom[0xd246],
                        context + $": shared signal/locks/event clock runtime={gate.BlocksGameplay}/{gate.Counter}, ROM=${rom[0xcc8a]:x2}/{rom[0xd246]}.");
                    FailIf(_player.IsAttacking != (rom[0xd200] != 0 && rom[0xd201] == 0x05),
                        context + ": Sword parent allocation differs.");
                    FailIf(_entities.ScreenShakeCounter != rom[0xcd18] ||
                        _entities.HorizontalScreenShakeCounter != rom[0xcd19] ||
                        _roomCamera.Offset != new Vector2(unchecked((sbyte)rom[0xc48d]),unchecked((sbyte)rom[0xc48c])),
                        context + ": shared Y/X shake clock/camera differs.");
                    for (int room = 0; room < 256; room++)
                        FailIf(_saveData.GetRoomFlags(0,room) != rom[0xc700+room],context + $": room flag${room:x2} differs.");
                    var keys = _entities.Entities<OverworldKeyUseEffect>();
                    var slots = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40).Where(slot => rom[slot] != 0).ToArray();
                    var keySlots = slots.Where(slot => rom[slot+1] == 0x18).ToArray();
                    FailIf(keys.Count != keySlots.Length,context + ": key sprite lifetime differs.");
                    for (int i = 0; i < keySlots.Length; i++)
                    {
                        var key = keys[i]; int slot = keySlots[i];
                        FailIf(_entities.InteractionSlot(key) != (slot >> 8)-0xd0 ||
                            key.State != rom[slot+4] || key.Counter != rom[slot+6] ||
                            (key.ZFixed & 0xffff) != rom.Word(slot+0xe) || (key.SpeedZ & 0xffff) != rom.Word(slot+0x14) ||
                            key.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                            key.Visible != ((rom[slot+0x1a] & 0x80) != 0),context + ": key slot/state/clock/XYZ/speed/visibility differs.");
                    }
                    var puffs = _entities.Entities<PuzzlePuffEffect>();
                    var puffSlots = slots.Where(slot => rom[slot+1] == 0x05).ToArray();
                    FailIf(puffs.Count != puffSlots.Length,context + ": opening puff lifetime differs.");
                    for (int i = 0; i < puffSlots.Length; i++)
                    {
                        var puff = puffs[i]; int slot = puffSlots[i];
                        FailIf(_entities.InteractionSlot(puff) != (slot >> 8)-0xd0 ||
                            puff.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                            puff.Initialized != (rom[slot+4] != 0) || puff.CurrentParameter != rom[slot+0x21] ||
                            puff.Visible != ((rom[slot+0x1a] & 0x80) != 0),context + $": puff${slot>>8:x2} slot/XY/animation/flicker differs.");
                    }
                    int nativePhase = rom.Sounds.Count(cue => cue == SoundId.SndDoorClose);
                    FailIf(gate.Phase != nativePhase,context + ": opening phase differs.");
                    if (nativePhase != 0 && _currentRoom.GetMetatile(new(120,24)) == 0xec)
                    {
                        // Native helpers write only these source rectangles;
                        // untouched initial BG bytes are outside this fixture.
                        int firstRow = nativePhase == 1 ? 3 : nativePhase == 2 ? 1 : 0;
                        for (int y = firstRow; y < 4; y++)
                        for (int x = 12; x < 18; x++)
                            FailIf(_currentRoom.GetBackgroundSubtileForValidation(x,y) != rom.BackgroundTile(x,y) ||
                                _currentRoom.GetBackgroundAttributeForValidation(x,y) != rom.BackgroundAttribute(x,y),
                                context + $": native opening BG tile/attribute ({x},{y}) differs.");
                    }
                    if (nativePhase > phases)
                    {
                        phases = nativePhase;
                        FailIf(gate.Counter != 30 || puffs.Count != 4 ||
                            !puffs.Select(puff => puff.Position).SequenceEqual(new[] { new Vector2(96,32),new(112,32),new(128,32),new(144,32) }) ||
                            _entities.ScreenShakeCounter != 14 || randomDraws() != (phases-1)*30+2,
                            context + ": opening helper must spawn four $05:$81 puffs, wait30 and shake15 in Y/X order.");
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + $": gameplay cues/RNG differ: runtime={string.Join(',',sounds.Requests.Where(cue => cue != SoundId.SndText))}, ROM={string.Join(',',rom.Sounds)}; draws={random.Calls-seed.Calls}/{rom.RandomCalls}.");
                });
            }
            long randomDraws() => _random.CaptureState().Calls-seed.Calls;
            Step();
            FailIf(rom[0xd340] != 0,"Original $8a/v$07 must release its slot before the keyhole approach.");
            for (int wait = 0; !gate.BlocksGameplay && !_dialogue.IsOpen &&
                (!owned || rom[0xcc6a] != 2) && wait < 80; wait++) Step(angle:0);
            if (!owned)
            {
                FailIf(!_dialogue.IsOpen || rom.TextGeneration != 1 || rom.Word(0xcba2) != 0x5509 ||
                    _dialogue.CurrentMessage != "Huh? This has a\nkeyhole.","Missing Crown Key must show source TX_5109.");
                Step(3,0); _dialogue.Close(); rom[0xcba0] = 0; Step(24,0);
                FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || gate.BlocksGameplay,
                    "Repeated missing-key contact must not restart the hint or opening.");
                continue;
            }
            FailIf(rom[0xcc6a] != 2,"Crown keyhole must reach its final doubled contact update through the original wall.");
            Step(angle:0,held:1,pressed:1);
            FailIf(!gate.BlocksGameplay || !_inventory.HasTreasure(0x43) || _player.CutsceneControlled ||
                sounds.Requests.Contains(SoundId.SndCtrlStopMusic),
                "Successful checkcfc0bit must yield before music and retain normal Link, treasure$43 and the controller slot.");
            Step(13); _dialogue.ShowMessage("Opening pause.",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; gate.BlocksGameplay && wait < 220; wait++) Step();
            FailIf(gate.BlocksGameplay || _entities.Entities<OverworldKeyUseEffect>().Count != 0 ||
                _currentRoom.GetMetatile(new(120,24)) != 0xee || !_inventory.HasTreasure(0x43) ||
                phases != 3 || randomDraws() != 90 || sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Crown opening must complete its three frames, shared tail, 90 RNG draws and retained key/controller.");
            Step(4); Step(4,0); Step(held:1,pressed:1);
            FailIf(!_player.IsAttacking,"Fresh Sword use must resume after source enableinput.");
        }
    }
}
