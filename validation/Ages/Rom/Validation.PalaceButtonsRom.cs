using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceButtonsRom()
    {
        // Source placements and gate directions, independent of the import.
        int[] rooms = [0x15, 0x17, 0x35, 0x37];
        Vector2[] positions = [new(0x48, 0x38), new(0x88, 0x18), new(0x88, 0x58), new(0x78, 0x58)];
        int[] gates = [0x33, 0x34, 0x35, 0x36];
        for (int index = 0; index < 4; index++)
        foreach (bool batch in RomHostSchedules(index))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(0x0b); _saveData.SetGlobalFlag(0x32);
            _saveData.SetRoomFlag(4, 0xfc, 0x80);
            LoadValidationRoom(1, rooms[index]); _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 approach = new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left }
                .First(direction => Enumerable.Range(0, 25).All(distance => !_collision.Collides(positions[index] - direction * distance)));
            Vector2 start = positions[index] - approach * 24;
            int direction = approach == Vector2.Up ? 0 : approach == Vector2.Right ? 1 : approach == Vector2.Down ? 2 : 3;
            _player.WarpTo(start); _player.Face(new Vector2I((int)approach.X, (int)approach.Y));
            FailIf(_collision.Collides(start), $"Palace button${index:x2}: approach starts in solid geometry at {start}.");
            var button = _entities.EntityAdapters<PalaceButtonRoomEntity>().Single();
            var eventOwner = _roomEvents.Get<PalacePassageEvent>();
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, direction, (int)start.X, (int)start.Y)
            { HostilePartsEnabled = true, CompanionDispatchEnabled = true };
            const int slot = 0xd240;
            rom[slot] = 1; rom[slot+1] = 0xbe; rom[slot+2] = (byte)index;
            rom[slot+11] = (byte)positions[index].Y; rom[slot+13] = (byte)positions[index].X;
            int updates = 0; bool movedGate = false, returned = false;
            void Compare(int held, int angle)
            {
                updates++;
                rom.AdvanceWarpPalette();
                if (rom[0xc2ef] == 3) rom.UpdateFadeOutWarp();
                else
                {
                    if (rom[0xc2ef] == 0x1c) rom.AdvancePalacePassage();
                    rom.UpdateGameplay(0, held, angle, updates);
                    if (movedGate && rom[0xc2ef] == 1 && rom[0xcc30] == rooms[index]) rom.AdvanceArrivalRoomControl();
                    rom.SelectTriggeredCutscene();
                }
                FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30],
                    $"Palace button${index:x2}, update={updates}, batch={batch}: room handoff port=${_rooms.ActiveGroup:x}:${_rooms.CurrentRoom.Id:x2}, ROM=${rom[0xcc2d]:x}:${rom[0xcc30]:x2}, cutscene=${rom[0xc2ef]:x2}.");
                if (_rooms.CurrentRoom.Id == rooms[index] && !eventOwner.HasState && !IsTransitioning && !movedGate)
                    FailIf(button.State != rom[slot+4] || button.Counter != rom[slot+6],
                        $"Palace button${index:x2}: press/countdown differs at update {updates}, port={button.State}/{button.Counter}, ROM={rom[slot+4]}/{rom[slot+6]}.");
                if (_rooms.CurrentRoom.Id == 0x27 && eventOwner.State == 2)
                {
                    movedGate = true;
                    int portTile = _rooms.CurrentRoom.GetMetatile(new Vector2((gates[index]&15)*16+8,(gates[index]>>4)*16+8));
                    int romTile = rom[0xcf00 + gates[index]];
                    FailIf(portTile != romTile,
                        $"Palace button${index:x2}: gate source ${gates[index]:x2} tile port=${portTile:x2}, ROM=${romTile:x2} at update={updates}; mode={_rooms.CurrentRoom.ActiveCollisions}/{rom[0xcc49]}, initial=${_rooms.CurrentRoom.GetOriginalMetatile(new Vector2((gates[index]&15)*16+8,(gates[index]>>4)*16+8)):x2}.");
                }
                returned |= movedGate && _rooms.CurrentRoom.Id == rooms[index] && !IsTransitioning;
            }
            StepGameplayUpdates(24, approach, [$"move_{new[] { "up", "right", "down", "left" }[direction]}"], batched: batch,
                afterUpdate: () => Compare(new[] { 0x40, 0x10, 0x80, 0x20 }[direction], direction * 8));
            FailIf(button.State != 2, $"Palace button${index:x2} is unreachable through its real floor approach.");
            StepGameplayUpdates(330, Vector2.Zero, batched: batch, afterUpdate: () => Compare(0, 0xff));
            FailIf(!movedGate || !returned || !_saveData.HasRoomFlag(1, rooms[index], 0x80) || eventOwner.HasState,
                $"Palace button${index:x2} must open its gate, persist source flag$80 and return to the button.");
            FailIf(_entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=rom[0xcc8a] ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)!=rom[0xcbca] || _player.CutsceneControlled,
                $"Palace button${index:x2} must release its original input/menu masks on return.");
            LoadValidationRoom(1, rooms[index]);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(_entities.EntityAdapters<PalaceButtonRoomEntity>().Any(button => !button.Finished),
                $"Palace button${index:x2} must remain pressed on re-entry.");
        }
        ReinitializeGameplayForValidation();LoadValidationRoom(1,0x15);
        var passage=_roomEvents.Get<PalacePassageEvent>();
        _entities.RuntimeState.SetWramByte(0xcbbb,0x15);_entities.RuntimeState.SetWramByte(0xcbbc,0x34);
        _entities.RuntimeState.SetWramByte(0xcbbd,0);passage.Begin();StepGameplayUpdates(40,Vector2.Zero);
        LoadValidationRoom(1,0x36);StepGameplayUpdates(2,Vector2.Zero);
        FailIf(passage.HasState || !_player.Visible || _player.CutsceneControlled ||
            _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0 || _saveData.HasRoomFlag(1,0x15,0x80),
            "Cancelled palace passage must release its fade/player ownership without committing the pressed-room flag.");
    }
}
