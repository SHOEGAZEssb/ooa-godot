using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPushInitialization()
    {
        int fixture = 0;
        foreach (int gate in new[] { 0,1,2 }) // text, full queue plus text, scrolling.
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9e); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,120.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.Layout[0x57] != 0x2a,
                "Pending push must approach original Crown statue$57:$2a through room4:9e floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,120,120);
            rom.Word(0xd00a,120*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit();
            bool runtimeInjected = false, nativeInjected = false;
            var observer = new ItemPhaseValidationEntity(() => {
                if (runtimeInjected || !_pushBlocks.Active) return;
                FailIf(_pushBlocks.NativeInitialized || _pushBlocks.Visible ||
                    _currentRoom.Layout[0x57] != 0x2a || _rooms.PendingTileGraphics != 0 ||
                    _rooms.BlockPushAngle != 0 || sounds.Requests.Contains(SoundId.SndMoveBlock),
                    $"Player allocation must retain pending$14: initialized={_pushBlocks.NativeInitialized}, visible={_pushBlocks.Visible}, tile=${_currentRoom.Layout[0x57]:x2}, queue={_rooms.PendingTileGraphics}, angle=${_rooms.BlockPushAngle:x2}, cue={sounds.Requests.Contains(SoundId.SndMoveBlock)}.");
                runtimeInjected = true;
                if (gate == 1)
                    for (int i = 0; i < 31; i++)
                        FailIf(!_rooms.TrySetTile(0x11,0xa0),"Pending state0 queue fixture must accept31 writes.");
                // Declare only this owner's outgoing lifetime. Destination
                // parsing/preload is covered separately; no incoming puzzle
                // controller may run inside the current native object pass.
                if (gate == 2) _pushBlocks.BeginScreenTransition();
                else _dialogue.ShowGameplayMessage("Pending push initialization",100);
            });
            _entities.AddEntity(observer);
            _entities.RegisterEnemySlot(observer,0);
            int update = 0;
            void Step(int count = 1,int angle = 0xff)
            {
                StepGameplayUpdates(count,angle == 0 ? Vector2.Up : Vector2.Zero,batched:batch,afterUpdate:() => {
                    rom.UpdateGameplay(0,angle == 0 ? 0x40 : 0,angle,_entities.FrameCounter-1,() => {
                        if (nativeInjected || rom[0xd140] == 0) return;
                        FailIf(rom[0xd144] != 0 || rom[0xd14a] != 0 || rom[0xd14b] != 86 ||
                            rom[0xd14d] != 120 || rom[0xcf57] != 0x2a || rom[0xcca6] != 0 ||
                            rom.Sounds.Contains(SoundId.SndMoveBlock),
                            "Native player allocation must retain the original pending reserved$14 state.");
                        nativeInjected = true;
                        if (gate == 1) for (int i = 0; i < 31; i++) rom.SetTile(0x11,0xa0);
                        if (gate == 2) { rom[0xcd00] = 8; rom[0xd140] = 2; }
                        else rom[0xcba0] = 1;
                    });
                    // The scroll case declares only the outgoing block's
                    // update gate; destination graphics/preload are outside it.
                    if (gate != 2) rom.AdvanceTileGraphics();
                    string context = $"Pending$14 gate{gate}, batch={batch}, update{++update}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(runtimeInjected != nativeInjected || _pushBlocks.Active != (rom[0xd140] != 0) ||
                        _rooms.BlockPushAngle != rom[0xcca6] ||
                        gate != 2 && _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf]) & 31),
                        context+": allocation/shared angle/queue count differs.");
                    if (_pushBlocks.Active)
                        FailIf(!_pushBlocks.NativeInitialized || !_pushBlocks.Visible ||
                            _pushBlocks.ActiveTile != rom[0xd171] ||
                            _pushBlocks.BlockTopLeft+new Vector2(8,6) !=
                                new Vector2(rom.Word(0xd14c)/256f,rom.Word(0xd14a)/256f) ||
                            _pushBlocks.ActiveMoveFrames-(int)SomariaPrivate<float>(_pushBlocks,"_moveFrame") != rom[0xd146],
                            context+": state0/fixed XY/movement clock differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context+": cues/shared RNG differ.");
                });
            }
            Step();
            for (int wait = 0; !runtimeInjected && wait < 64; wait++) Step(angle:0);
            FailIf(!runtimeInjected || _pushBlocks.BlockTopLeft != new Vector2(112,79.5f),
                "Original statue must allocate and move once on its pending state0 dispatch.");
            Step(3);
            FailIf(rom[0xd146] != 31,"Initialized$14 must freeze after its first motion update.");
            if (gate == 2)
            {
                _pushBlocks.FinishScreenTransition();
                rom.ClearOutgoingInteractions(); rom[0xcd00] = 1;
                FailIf(_pushBlocks.Active,"Scroll completion must release the outgoing reserved block.");
                Step(3);
            }
            else
            {
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(31);
                FailIf(_pushBlocks.Active || _currentRoom.Layout[0x47] != 0x2a ||
                    _currentRoom.Layout[0x57] != (gate == 1 ? 0x2a : 0xa0) ||
                    sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 1,
                    "Native push must complete after32 eligible updates without retrying a rejected source write.");
                Step(3);
            }
            LoadValidationRoom(4,0x9e);
            FailIf(_pushBlocks.Active || _currentRoom.Layout[0x57] != 0x2a,
                "Completion/cancellation re-entry must restore the original statue.");
        }
    }
}
