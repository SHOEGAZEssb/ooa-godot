using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareGraveyardKeyholeRom()
    {
        FailIf(!new KeyholeControllerDatabase().TryGet(0,0x5c,out var placement) ||
            placement is not { Order:1,Id:0xdc,SubId:1,Position.X:0,Position.Y:0 },
            "Original Graveyard$dc:$01 is source object1 with absent coordinate operands.");
        int fixture = 0;
        foreach (bool owned in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.LoseTreasure(0x42);
            if (owned) _inventory.GiveTreasure(0x42,1);
            _saveData.SetRoomFlag(0,0x5c,0x80,false);
            LoadValidationRoom(0,0x5c);
            var barrier = _entities.Entities<CompanionBarrierRoomEntity>().Single();
            var controller = RetainKeyholeControllerRom(expectedSlot:3,retainCompanionPlacements:true);
            FailIf(_entities.InteractionSlot(barrier) != 2 || _entities.InteractionSlot(controller.Node) != 3,
                "mainData.s room$0:$5c allocates barrier$71:$05 in$d2, then gate$dc:$01 in$d3.");
            var gate = _roomEvents.Get<GraveyardGateEvent>();
            _player.WarpTo(new(72,104)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(72,72)) != 0xec,
                "Graveyard keyhole must use original room$0:$5c floor and tile$44:$ec.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,72,104);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            // Execute both original placements, retaining the unmounted
            // barrier's real allocation before the gate and its descendants.
            rom[0xd240] = 1; rom[0xd241] = 0x71; rom[0xd242] = 5;
            rom[0xd24b] = 0x40; rom[0xd24d] = 0x98;
            rom[0xd340] = 1; rom[0xd341] = 0xdc; rom[0xd342] = 1;
            rom[0xcc35] = rom[0xcc46] = (byte)_sound.ActiveMusic;
            var camera = rom.CreateMenuView();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0,phases = 0;
            void Step(int count = 1,int angle = 0xff)
            {
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : Vector2.Up,batched:batch,afterUpdate:() =>
                {
                    rom.UpdateGameplay(0,angle == 0xff ? 0 : 0x40,angle,_entities.FrameCounter);
                    rom[0xc48c] = rom[0xc48d] = 0; // updateGfxRegs2Scroll's unshaken room basis.
                    camera.UpdateScreenShake(); // Native post-object camera phase, Y then X RNG.
                    string context = $"Graveyard$0:$5c owned={owned}, batch={batch}, update={++update}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(_keyholes.RemainingPushFrames != rom[0xcc6a] ||
                        _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + ": contact clock/hint/modal differs.");
                    FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) != rom[0xcfc0],
                        context + ": retained keyhole signal differs.");
                    FailIf(_entities.ScreenShakeCounter != rom[0xcd18] ||
                        _entities.HorizontalScreenShakeCounter != rom[0xcd19] ||
                        _roomCamera.Offset != new Vector2(unchecked((sbyte)rom[0xc48d]),unchecked((sbyte)rom[0xc48c])),
                        context + ": shared shake clock/camera offset differs.");
                    FailIf(gate.BlocksGameplay != ((rom[0xcc8a] & 0x81) != 0) ||
                        gate.BlocksGameplay && gate.Counter != rom[0xd346],
                        context + $": input/event clock runtime={gate.BlocksGameplay}/{gate.Counter}, ROM=${rom[0xcc8a]:x2}/{rom[0xd346]}.");
                    FailIf(barrier.State != rom[0xd244] || barrier.Finished || !controller.Initialized || controller.Finished,
                        context + ": original barrier/gate state or retained allocations differ.");
                    for (int room = 0; room < 256; room++)
                        FailIf(_saveData.GetRoomFlags(0,room) != rom[0xc700+room],context + $": room flag${room:x2} differs.");
                    var effects = _entities.Entities<OverworldKeyUseEffect>();
                    var keys = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
                        .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x18).ToArray();
                    FailIf(effects.Count != keys.Length,context + ": key sprite lifetime differs.");
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var key = effects[i]; int slot = keys[i];
                        FailIf(_entities.InteractionSlot(key) != (slot >> 8)-0xd0 || key.State != rom[slot+4] || key.Counter != rom[slot+6] ||
                            (key.ZFixed & 0xffff) != rom.Word(slot+0xe) || (key.SpeedZ & 0xffff) != rom.Word(slot+0x14) ||
                            key.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]),context + ": key sprite state/clock/XYZ/speed differs.");
                    }
                    var puffs = _entities.Entities<PuzzlePuffEffect>();
                    var puffSlots = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
                        .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x05).ToArray();
                    FailIf(puffs.Count != puffSlots.Length,context + ": collapse puff lifetime differs.");
                    for (int i = 0; i < puffSlots.Length; i++)
                    {
                        var puff = puffs[i]; int slot = puffSlots[i];
                        FailIf(_entities.InteractionSlot(puff) != (slot >> 8)-0xd0 || puff.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                            puff.Initialized != (rom[slot+4] != 0) || puff.CurrentParameter != rom[slot+0x21] ||
                            puff.Visible != ((rom[slot+0x1a] & 0x80) != 0),context + ": collapse puff state/XY/animation/visibility differs.");
                    }
                    // Independent scriptHelper.s birth/phase goldens.
                    if (puffs.Count != 0 && phases == 0)
                    {
                        phases = 1;
                        FailIf(gate.Counter != 45 || puffs.Count != 2 ||
                            puffs[0].Position != new Vector2(64,72) || puffs[1].Position != new Vector2(80,72) ||
                            _entities.ScreenShakeCounter != 9 || randomDraws() != 2,
                            context + ": first collapse must load45, spawn ($48,$40)/($48,$50) and begin a ten-update Y/X shake.");
                    }
                    if (phases == 1 && puffs.Count != 0 && puffs[0].Position == new Vector2(48,72))
                    {
                        phases = 2;
                        FailIf(gate.Counter != 60 || puffs.Count != 2 || puffs[1].Position != new Vector2(96,72) ||
                            _entities.ScreenShakeCounter != 9 || randomDraws() != 22,
                            context + ": second collapse must load60, spawn ($48,$30)/($48,$60) and restart Y/X shake.");
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + $": cue/RNG order differs: runtime={string.Join(',',sounds.Requests.Where(cue => cue != SoundId.SndText))}, ROM={string.Join(',',rom.Sounds)}; draws={random.Calls-seed.Calls}/{rom.RandomCalls}.");
                });
            }
            long randomDraws() => _random.CaptureState().Calls-seed.Calls;
            Step();
            for (int wait = 0; !gate.BlocksGameplay && !_dialogue.IsOpen && wait < 40; wait++) Step(angle:0);
            if (!owned)
            {
                FailIf(!_dialogue.IsOpen || _rooms.InformativeTextsShown != 0x20 ||
                    rom.TextGeneration != 1 || rom.Word(0xcba2) != 0x5509 ||
                    _dialogue.CurrentMessage != "Huh? This has a\nkeyhole.","Missing Graveyard Key must show original TX_5109 once.");
                Step(3,0); _dialogue.Close(); rom[0xcba0] = 0; Step(24,0);
                FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || gate.BlocksGameplay,
                    "Repeated missing Graveyard Key contact must retain the hint mask without opening.");
                continue;
            }
            FailIf(!gate.BlocksGameplay || !_inventory.HasTreasure(0x42) || !_saveData.HasRoomFlag(0,0x5c,0x80),
                "Graveyard Key must remain held while flag$80 acquires $81 input control.");
            Step(13);
            _dialogue.ShowMessage("Gate pause.",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; gate.BlocksGameplay && wait < 180; wait++) Step();
            FailIf(gate.BlocksGameplay || _entities.Entities<OverworldKeyUseEffect>().Count != 0 ||
                !_inventory.HasTreasure(0x42) || _currentRoom.GetMetatile(new(72,72)) != 0x3a ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1 || phases != 2 || randomDraws() != 40,
                "Graveyard gate must finish its source collapse, retire the key and retain treasure$42.");
            Step(4); Step(4,0);
            // Re-entry allocates both placed objects again. Only the gate's
            // state0 reads room flag$80 and releases its original$d3 slot.
            LoadValidationRoom(0,0x5c);
            barrier = _entities.Entities<CompanionBarrierRoomEntity>().Single();
            controller = RetainKeyholeControllerRom(expectedSlot:3,retainCompanionPlacements:true);
            FailIf(_entities.InteractionSlot(barrier) != 2 || _entities.InteractionSlot(controller.Node) != 3,
                "Opened Graveyard re-entry must allocate the original stream before state0 deletion.");
            _player.WarpTo(new(72,104)); _player.Face(Vector2I.Up);
            var reentrySeed = _random.CaptureState();
            var reentry = new SomariaRom(_saveData,reentrySeed,_currentRoom,0,72,104);
            reentry.InitializeLinkGameplay(); reentry.InitializeLinkWalkingAnimation();
            reentry[0xd240] = 1; reentry[0xd241] = 0x71; reentry[0xd242] = 5;
            reentry[0xd24b] = 0x40; reentry[0xd24d] = 0x98;
            reentry[0xd340] = 1; reentry[0xd341] = 0xdc; reentry[0xd342] = 1;
            StepGameplayUpdates(8,Vector2.Up,batched:batch,afterUpdate:() => {
                reentry.UpdateGameplay(0,0x40,0,_entities.FrameCounter);
                CompareSomariaMotionRom(reentry,"Opened Graveyard re-entry");
                FailIf(!controller.Finished || reentry[0xd340] != 0 || barrier.Finished ||
                    barrier.State != reentry[0xd244] || gate.HasState ||
                    _entities.Entities<OverworldKeyUseEffect>().Count != 0 || !_inventory.HasTreasure(0x42),
                    "Opened Graveyard state0 must delete the gate, retain the waiting barrier/key, and reject repeat opening.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != reentry[0xff94] || random.Rng2 != reentry[0xff95] ||
                    random.Calls-reentrySeed.Calls != reentry.RandomCalls,
                    "Opened Graveyard re-entry must preserve native RNG after controller deletion.");
            });
        }
    }
}
