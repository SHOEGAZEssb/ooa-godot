using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePushBlockHazardsRom()
    {
        int fixture = 0;
        foreach (var spec in new (bool Grave,int Level,bool Full)[] {(false,0,false),(false,2,false),(false,0,true),
            (true,0,false),(true,2,false)})
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            if (spec.Level != 0) _inventory.GiveTreasure(TreasureId.Bracelet,spec.Level);
            _inventory.EquipA(0); _inventory.EquipB(0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int group = spec.Grave ? 0 : 4, room = spec.Grave ? 0x7c : 0x45;
                LoadValidationRoom(group,room); _entities.Clear(); _roomEvents.Get<PoeEvent>().Cancel();
                // Keep the original floor/terrain; isolate unrelated room NPCs
                // and puzzle controllers from these shared block handlers.
                Vector2 center = spec.Grave ? new(88,40) : new(184,88);
                Vector2 direction = spec.Grave ? Vector2.Up : Vector2.Down;
                Vector2 start = center-direction*(spec.Grave ? 16 : 24)+new Vector2(0.25f,0.5f);
                int angle = spec.Grave ? 0 : 16;
                _player.WarpTo(start); _player.Face((Vector2I)direction);
                FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(center) != (spec.Grave ? 0xd9 : 0x1c) ||
                    _currentRoom.GetMetatile(center+direction*16) != (spec.Grave ? 0x3b : 0xf7),
                    $"Shared block hazard/grave probe must approach through its unchanged original room floor: grave={spec.Grave}, repeat={repeat}, start={start}, collision={_collision.Collides(start)}, source=${_currentRoom.GetMetatile(center):x2}, destination=${_currentRoom.GetMetatile(center+direction*16):x2}.");
                var seed = _random.CaptureState();
                var rom = new SomariaRom(_saveData,seed,_currentRoom,angle/8,(int)start.X,(int)start.Y);
                rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
                rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
                var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
                void Step(int count = 1,int input = 0xff) =>
                    StepSomariaMotionRom(rom,count,batch,input,afterUpdate:() => {
                        string context = $"Block hazard/grave={spec.Grave}, L{spec.Level}, full={spec.Full}, repeat={repeat}, batch={batch}, update={++update}";
                        FailIf(_pushBlocks.Active != (rom[0xd140] != 0) ||
                            _pushBlocks.LinkMovementDisabled != ((rom[0xcc8a] & 1) != 0) ||
                            _pushBlocks.RemainingPushFrames != rom[0xcc6a],context + ": reserved allocation/contact clock/Link lock differs.");
                        if (_pushBlocks.Active)
                            FailIf(_pushBlocks.BlockTopLeft+new Vector2(8,6) !=
                                new Vector2(rom.Word(0xd14c)/256f,rom.Word(0xd14a)/256f) ||
                                _pushBlocks.ActiveMoveFrames-(int)SomariaPrivate<float>(_pushBlocks,"_moveFrame") != rom[0xd146],
                                context + ": native fixed movement/counter differs.");
                        var falls = _entities.Entities<FallingDownHoleEffect>().ToArray();
                        int[] slots = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                            .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x0f).ToArray();
                        FailIf(falls.Length != slots.Length,context + ": checked hole allocation/lifetime differs.");
                        for (int i = 0; i < falls.Length; i++)
                        {
                            var fall = falls[i]; int slot = slots[i];
                            FailIf(_entities.InteractionSlot(fall) != (slot>>8)-0xd0 ||
                                fall.PrecisePosition != new Vector2(rom.Word(slot+0xc)/256f,rom.Word(slot+0xa)/256f) ||
                                fall.CurrentParameter != rom[slot+0x21] ||
                                SomariaPrivate<int>(fall,"_animationCounter") != rom[slot+0x20] ||
                                fall.Visible != ((rom[slot+0x1a] & 0x80) != 0),
                                context + $": fall$0f fixed XY/animation/visibility differs: runtime={fall.PrecisePosition}/{SomariaPrivate<int>(fall,"_animationCounter")}/${fall.CurrentParameter:x2}, native=${rom.Word(slot+0xc):x4},${rom.Word(slot+0xa):x4}/{rom[slot+0x20]}/${rom[slot+0x21]:x2}.");
                        }
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls-seed.Calls != rom.RandomCalls ||
                            !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": ordered cues/shared RNG differ.");
                    });
                Step();
                for (int wait = 0; !_pushBlocks.Active && wait < 60; wait++) Step(input:angle);
                FailIf(!_pushBlocks.Active,"Original block/grave must be reachable through its actual collision approach.");
                _dialogue.ShowGameplayMessage("Moving block pause",100); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                while (_pushBlocks.Active && rom[0xd146] > 1) Step();
                if (spec.Full)
                {
                    for (int index = 0; index < 14; index++)
                    {
                        var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
                        int slot = 0xd000+_entities.InteractionSlot(puff)*256+0x40;
                        rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80;
                        rom[slot+0xb] = rom[slot+0xd] = 24;
                    }
                }
                Step();
                FailIf(_pushBlocks.Active || _pushBlocks.LinkMovementDisabled || IsTransitioning ||
                    _currentRoom.GetMetatile(center) != (spec.Grave ? 0xdc : 0xa0) ||
                    _currentRoom.GetMetatile(center+direction*16) != (spec.Grave ? 2 : 0xf7),
                    "Source completion must clear the reserved block/lock, preserve the hole or place the grave, and retain Link outside stair bounds.");
                if (spec.Grave)
                    FailIf(
                        !RoomTransitionController.LinkWithinTileWarpBounds(_currentRoom,0x25,new(0x58,0x28)) ||
                        RoomTransitionController.LinkWithinTileWarpBounds(_currentRoom,0x25,new(0x53,0x28)) ||
                        !RoomTransitionController.LinkWithinTileWarpBounds(_currentRoom,0x25,new(0x54,0x20)) ||
                        !RoomTransitionController.LinkWithinTileWarpBounds(_currentRoom,0x25,new(0x5d,0x29)) ||
                        RoomTransitionController.LinkWithinTileWarpBounds(_currentRoom,0x25,new(0x5e,0x29)) ||
                        RoomTransitionController.LinkWithinTileWarpBounds(_currentRoom,0x25,new(0x58,0x2a)),
                        "Revealed room0:7c/$25 staircase must retain bounds $54-$5d/$20-$29.");
                if (!spec.Grave && !spec.Full)
                {
                    _dialogue.ShowGameplayMessage("Falling block continues",100); rom[0xcba0] = 1;
                    Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                }
                Step(36); Step(4,angle);
                FailIf(_entities.Entities<FallingDownHoleEffect>().Count != 0 || _pushBlocks.Active ||
                    sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 1 ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != (spec.Grave ? 1 : 0) ||
                    sounds.Requests.Count(cue => cue == SoundId.SndFallInHole) != (!spec.Grave && !spec.Full ? 1 : 0),
                    "Completed block/grave must retain source cue counts, finish its hole animation, and reject renewed contact.");
            }
        }
    }
}
