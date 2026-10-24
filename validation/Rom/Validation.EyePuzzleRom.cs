using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEyePuzzleRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0xf3);
            _player.WarpTo(new(0x78, 0x88));
            var spawner = _entities.EntityAdapters<StatueEyeballSpawnerRoomEntity>().Single();
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x78, 0x88);
            rom.InitializeLinkGameplay();
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            int parent = 0xd040 + 0x100 * _entities.NativeInteractionSlot(spawner);
            rom[parent] = 1; rom[parent + 1] = 0xe2; rom[parent + 2] = 3;
            rom[0xcc00] = (byte)_entities.FrameCounter;
            int calls = _random.Calls, updates = 0;
            StepGameplayUpdates(66, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0, 0xff, (rom[0xcc00] + 1) & 0xff);
                updates++;
                var eyes = _entities.EntityAdapters<EyePuzzleStatueRoomEntity>().ToArray();
                int nativeEyes = Enumerable.Range(0, 16).Count(slot =>
                    rom[0xd040 + slot * 256] != 0 && rom[0xd041 + slot * 256] == 0xe2);
                FailIf(eyes.Length != nativeEyes || eyes.Length == 0 ||
                    _entities.FrameCounter != rom[0xcc00] ||
                    _entities.RuntimeState.ReadWramByte(0xcca5) != rom[0xcca5] ||
                    _random.CaptureState().Rng1 != rom[0xff94] || _random.CaptureState().Rng2 != rom[0xff95] ||
                    _random.Calls - calls != rom.RandomCalls,
                    $"Eye maze $5:$f3 update{updates}: allocation, global frame counter, selected direction or RNG differs from native.");
                foreach (var eye in eyes)
                {
                    int slot = 0xd040 + 0x100 * _entities.NativeInteractionSlot(eye);
                    FailIf(eye.Node.Position != new Vector2(rom[slot + 13], rom[slot + 11]) ||
                        eye.Node.Visible != ((rom[slot + 0x1a] & 0x80) != 0),
                        $"Eye maze $e2:$04 slot${slot:x4} update{updates}: native position or visibility differs.");
                    if(updates==2||updates==66)
                    {
                        var sprite=(StatueEyeball)eye.Node;
                        var animation=SomariaPrivate<EnemyAnimationPlayer>(sprite,"_animation");
                        CompareInteractionSpritePixelsRom(rom,animation.CurrentTexture,animation.CurrentOffset,slot,1,"Eye maze pupil");
                    }
                }
            });
            var stopped = _entities.EntityAdapters<EyePuzzleStatueRoomEntity>().Select(eye => eye.Node.Position).ToArray();
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(!_entities.EntityAdapters<EyePuzzleStatueRoomEntity>().Select(eye => eye.Node.Position).SequenceEqual(stopped),
                "Eye maze stopped eyes resumed spinning after the sixty-update boundary.");
        }

        // Native branch matrix is bounded to the transition handler; no maze
        // playthrough or large inventory/input product is needed.
        var native = new FrontendRom();
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf3);
        for(int repeat=0;repeat<2;repeat++)
        {
            StepGameplayUpdates(66,Vector2.Zero);
            int selected=EyePuzzleDatabase.Shared.CorrectDirection(_runtimeState);
            Vector2I exit=directions[selected];
            _player.WarpTo(selected switch{0=>new(0x78,24),1=>new(_currentRoom.Width-24,0x58),_=>new(24,0x58)});
            int walked=0;
            while(!_transitions.IsTransitioning&&walked++<48)StepGameplayUpdates(1,exit);
            FailIf(!_transitions.IsTransitioning||_runtimeState.ReadWramByte(0xcc37)!=repeat+1,
                $"Eye maze normal movement failed exit{selected}, repeat{repeat}, position{_player.Position},count{_runtimeState.ReadWramByte(0xcc37)}.");
            int ticks=0;while(_transitions.IsTransitioning&&ticks++<180)StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_transitions.IsTransitioning||_currentRoom.Id!=0xf3,"Eye maze normal movement failed self-scroll activation.");
        }
        // Enter through a real scroll as in normal play, rather than relying
        // solely on direct room loads or calls to BeginScroll.
        ReinitializeGameplayForValidation();_saveData.SetRoomFlag(5,0xf4,0x40);LoadValidationRoom(5,0xf4);
        _player.WarpTo(new(0x78,24));
        int entryWalk=0;while(!_transitions.IsTransitioning&&entryWalk++<48)StepGameplayUpdates(1,Vector2I.Up);
        FailIf(!_transitions.IsTransitioning,"Eye maze entrance north approach could not start scrolling.");
        int entryScroll=0;while(_transitions.IsTransitioning&&entryScroll++<180)StepGameplayUpdates(1,Vector2.Zero);
        StepGameplayUpdates(66,Vector2.Zero);
        var arrivedEyes=_entities.Entities<StatueEyeball>();
        FailIf(_currentRoom.Id!=0xf3||arrivedEyes.Count!=10||arrivedEyes.Any(eye=>(eye.Direction&1)!=0)||
            arrivedEyes.Select(eye=>eye.Direction/2).Distinct().Count()!=3||
            arrivedEyes.Any(eye=>eye.Direction/2==EyePuzzleDatabase.Shared.CorrectDirection(_runtimeState)),
            "Scrolling from 5:f4 must produce ten stopped pupils showing precisely the three wrong directions.");
        for (int count = 0; count <= 6; count++)
        for (int correct = 0; correct < 4; correct++)
        for (int direction = 0; direction < 4; direction++)
        {
            var memory = new OracleRuntimeState();
            memory.SetWramByte(0xcc37, (byte)count); memory.SetWramByte(0xcc38, 0x55);
            memory.SetWramByte(0xcca5, (byte)correct);
            native[0xcc37] = (byte)count; native[0xcc38] = 0x55;
            native[0xcca5] = (byte)correct; native[0xcd02] = (byte)direction;
            native[0xcc30] = 0xf3; native[0xcc2d] = 5; native[0xcc2e] = 1;
            var puzzle = EyePuzzleDatabase.Shared;
            bool loops = puzzle.TryResolve(memory, 5, 0xf3, directions[direction], out int target);
            FailIf(memory.ReadWramByte(0xcc37) != count, "Eye maze preview query mutated its progress byte.");
            puzzle.CommitTransition(memory, 5, 0xf3, directions[direction]);
            native.Call(0x5feb, 1); // bank1.s:screenTransitionEyePuzzle (clean US).
            FailIf(memory.ReadWramByte(0xcc37) != native[0xcc37] || memory.ReadWramByte(0xcc38) != native[0xcc38] ||
                loops != (native[0xcc30] == 0xf3) || loops && target != 0xf3,
                $"Eye maze count{count}/correct{correct}/exit{direction}: transition branch or counter differs from native.");
        }
        ReinitializeGameplayForValidation();
        LoadValidationRoom(5, 0xf3);
        _player.WarpTo(new(0x78, 0x88));
        StepGameplayUpdates(66, Vector2.Zero);
        _entities.RuntimeState.SetWramByte(0xcc37, 3);
        _entities.RuntimeState.SetWramByte(0xcca5, 1);
        for (int probe = 0; probe < 3; probe++)
            FailIf(!_transitions.TryGetScreenTransitionDestinationForValidation(Vector2I.Right, out int destination) ||
                destination != 0xf3 || _entities.RuntimeState.ReadWramByte(0xcc37) != 3,
                "Eye maze repeated collision probes changed progress or lost the self-scroll destination.");
        _transitions.BeginScroll(_player, Vector2I.Right, 0xf3);
        FailIf(_entities.RuntimeState.ReadWramByte(0xcc37) != 4,
            "Eye maze actual self-scroll did not commit exactly one successful direction.");
        int scrollUpdates = 0;
        while (_transitions.IsTransitioning && scrollUpdates++ < 180) StepGameplayUpdates(1, Vector2.Zero);
        FailIf(_transitions.IsTransitioning || _currentRoom.Id != 0xf3 ||
            _entities.RuntimeState.ReadWramByte(0xcc37) != 4,
            "Eye maze self-scroll lost its shared progress during destination activation.");
        StepGameplayUpdates(66, Vector2.Zero);
        _entities.RuntimeState.SetWramByte(0xcc37, 5);
        _entities.RuntimeState.SetWramByte(0xcca5, 0);
        FailIf(!_transitions.TryGetScreenTransitionDestinationForValidation(Vector2I.Up, out int finalRoom) || finalRoom != 0xf2,
            "Eye maze completed north exit did not resolve through imported dungeon $0a layout.");
        int solveRequests = _sound.PlayRequestsFor(SoundId.SndSolvePuzzle);
        _player.WarpTo(new(0x78,24));
        int finalWalk=0;while(!_transitions.IsTransitioning&&finalWalk++<48)StepGameplayUpdates(1,Vector2I.Up);
        FailIf(!_transitions.IsTransitioning,"Eye maze sixth correct exit did not start through normal movement.");
        FailIf(_entities.RuntimeState.ReadWramByte(0xcc37) != 0 || _entities.RuntimeState.ReadWramByte(0xcc38) != 0,
            "Eye maze standard north exit did not clear both shared lost-woods counter aliases.");
        scrollUpdates = 0;
        while (_transitions.IsTransitioning && scrollUpdates++ < 180) StepGameplayUpdates(1, Vector2.Zero);
        FailIf(_transitions.IsTransitioning || _currentRoom.Id != 0xf2,
            "Eye maze completed north exit failed actual destination activation.");
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != solveRequests + 1,
            "Eye maze north-exit cue did not run once after destination scrolling finished.");
        GD.Print("Validated Room of Rites eye maze ordered allocation, sixty-update spin, RNG/global-frame writes and settled poses in split/batched gameplay; 112 native transition branches and read-only previews.");
    }
}
