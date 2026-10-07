using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareToggleFloorQueueRom()
    {
        foreach (bool batch in new[] { false,true })
        foreach (int queued in new[] { 0,30,31 })
        foreach (int freeSlots in new[] { 0,1,2 })
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(4,0xa1); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,56.5f)); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,120,56);
            var menu = rom.CreateMenuView(); menu.LoadRoomTileset(); menu.LoadRoomMappings(); menu.EnableVramDmaTransfers();
            rom.Word(0xd00a,56*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 5; rom[0xc2ef] = 1;
            menu.CopyToggleRoom(_currentRoom);
            var toggle = _entities.FloorToggle!;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            int[] positions = [0x44,0x46];
            bool compareGraphics = false;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool toggling = rom[0xc2ef] == 2;
                if (toggling) menu.AdvanceToggleCutscene();
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics();
                if (!toggling) rom.SelectToggleCutscene();
                CompareSomariaMotionRom(rom,$"Toggle queue={queued}, free={freeSlots}, batch={batch}, update{++update}");
                if (compareGraphics)
                    foreach (int packed in positions)
                    for (int subtile = 0; subtile < 4; subtile++)
                    {
                        int x = (packed&15)*2+(subtile&1), y = (packed>>4)*2+(subtile>>1);
                        FailIf(_currentRoom.GetBackgroundSubtileForValidation(x,y) != rom.BackgroundTile(x,y) ||
                            _currentRoom.GetBackgroundAttributeForValidation(x,y) != rom.BackgroundAttribute(x,y),
                            $"Toggle update{update}, queue={queued}, cell${packed:x2}: displayed tile/attribute runtime={_currentRoom.GetBackgroundSubtileForValidation(x,y):x2}/{_currentRoom.GetBackgroundAttributeForValidation(x,y):x2}, ROM={rom.BackgroundTile(x,y):x2}/{rom.BackgroundAttribute(x,y):x2}.");
                    }
                FailIf(toggle.Active != (rom[0xc2ef] == 2) || toggle.Active &&
                    (toggle.State != rom[0xcc03] || toggle.State > 0 && toggle.Counter != rom[0xcbb4]) ||
                    _runtimeState.ReadWramByte(WramAddress.wLastToggleBlocksState) != rom[0xcd2c] ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) || !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Toggle update{update}: state={toggle.State}/{rom[0xc2ef]:x2}:{rom[0xcc03]}, counter={toggle.Counter}/{rom[0xcbb4]}, queue={_rooms.PendingTileGraphics}/{((rom[0xcce0]-rom[0xccdf])&31)}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                var debris = _entities.Entities<RockDebrisEffect>();
                int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot+1] == 6).ToArray();
                FailIf(debris.Count != native.Length,"Toggle debris allocations/lifetimes must match the native INTERACTION pool.");
                for (int index = 0; index < debris.Count; index++)
                    FailIf(_entities.InteractionSlot(debris[index]) != (native[index]>>8)-0xd0 ||
                        debris[index].Position != new Vector2(rom[native[index]+0xd],rom[native[index]+0xb]) ||
                        (debris[index].ElapsedUpdates > 0) != (rom[native[index]+4] != 0) ||
                        debris[index].CurrentParameter != rom[native[index]+0x21],
                        "Descending floor scan must allocate debris into ascending free slots with the source packed position.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Toggle floor scans and debris must preserve original RNG order.");
            });
            // Declare the orb publication; native cutscene01 selects the
            // cutscene after its actual object phases. Orb contact is separate.
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,1); rom[0xcdd2] = 1;
            Step(8); FailIf(toggle.Counter != 1,"Toggle queue fixture must stop immediately before wait zero.");
            foreach (int packed in positions)
            {
                _currentRoom.SetUnderlyingStorageMetatile(packed,0x29);
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),0x10,null,(long)_animationTicks);
                // Declare matching movable blocks and their initial mapping.
                rom.SetTile((byte)packed,0x10);
            }
            rom.AdvanceTileGraphics(); menu.CopyToggleRoom(_currentRoom);
            compareGraphics = true;
            for (int index = 0; index < 14-freeSlots; index++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                int slot = (0xd2+index)*256+0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80; rom[slot+0xb] = 120; rom[slot+0xd] = 200;
            }
            for (int index = 0; index < queued; index++)
            {
                FailIf(!_rooms.TrySetTile(0x11,0xa0),"Toggle fixture must accept its declared graphics-queue entries.");
                rom.SetTile(0x11,0xa0);
            }
            Step();
            FailIf(toggle.Active || _entities.Entities<RockDebrisEffect>().Count != freeSlots ||
                positions.Any(packed => _currentRoom.Layout[packed] != 0x0f || _currentRoom.GetUnderlyingStorageMetatile(packed) != 0x0f),
                "Wait zero must rewrite both floor buffers independently of queue/debris capacity.");
            Step(8); FailIf(_rooms.PendingTileGraphics != 0,"Toggle graphics queue must finish draining without retries.");
            Step(20); FailIf(_entities.Entities<RockDebrisEffect>().Count != 0 || !_entities.InteractionSlotAvailable,
                "Debris and silent filler puffs must release their native interaction slots.");
        }
    }
}
