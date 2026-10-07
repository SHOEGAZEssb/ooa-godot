using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareToggleFloorScanRom()
    {
        // The final bare-floor loop excludes$00 and includes padding through$af.
        (int Position,byte Original,byte Expected,byte Collision)[] cells =
        [(0x00,0x0e,0x0e,0x1e),(0x01,0x0e,0x28,0),(0x02,0x0f,0x29,0),
         (0xae,0x28,0x0e,0x1e),(0xaf,0x29,0x0f,0x1e),(0x2f,0x28,0x0e,0x1e)];
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(4,0xa1); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,56.5f)); _player.Face(Vector2I.Up);
            foreach (var cell in cells)
            {
                _currentRoom.SetUnderlyingStorageMetatile(cell.Position,cell.Original);
                _currentRoom.SetStorageTileAndCollision(cell.Position,cell.Original,
                    cell.Original < 0x28 ? (byte)0x1e : (byte)0,(long)_animationTicks);
            }
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,120,56);
            var menu = rom.CreateMenuView(); menu.LoadRoomTileset();
            rom.Word(0xd00a,56*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 5; rom[0xc2ef] = 1;
            menu.CopyToggleRoom(_currentRoom);
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,1); rom[0xcdd2] = 1;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool toggling = rom[0xc2ef] == 2;
                if (toggling) menu.AdvanceToggleCutscene();
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics();
                if (!toggling) rom.SelectToggleCutscene();
                CompareSomariaMotionRom(rom,$"Toggle storage scan update{++update}, batch={batch}");
                foreach (var cell in cells)
                    FailIf(_currentRoom.Layout[cell.Position] != rom[0xcf00+cell.Position] ||
                        _currentRoom.GetUnderlyingStorageMetatile(cell.Position) != rom.Underlying(cell.Position),
                        $"Native floor scan differs at storage${cell.Position:x2}, including$00 and padding.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !System.Linq.Enumerable.SequenceEqual(sounds.Requests,rom.Sounds),
                    "Bare toggle scan must preserve native cue and RNG order without debris.");
            });
            Step(8);
            foreach (var cell in cells)
                FailIf(_currentRoom.Layout[cell.Position] != cell.Original,"Storage scan must wait through counter1.");
            Step();
            foreach (var cell in cells)
            {
                FailIf(_currentRoom.Layout[cell.Position] != cell.Expected ||
                    _currentRoom.GetUnderlyingStorageMetatile(cell.Position) != cell.Expected || rom[0xce00+cell.Position] != cell.Collision,
                    $"Source literal replacement/collision pair differs at${cell.Position:x2}.");
                if ((cell.Position&15) < 15)
                    FailIf(_currentRoom.GetTerrainInfo(new((cell.Position&15)*16+8,(cell.Position>>4)*16+8)).Collision != cell.Collision,
                        $"Floor collision differs at${cell.Position:x2}.");
            }
            Step(2);
            FailIf(_entities.Entities<RockDebrisEffect>().Count != 0,"Bare floor changes must not create block debris.");
        }
    }
}
