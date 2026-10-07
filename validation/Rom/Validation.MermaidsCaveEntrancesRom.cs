using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidsCaveEntrances()
    {
        CompareMermaidKeyholesRom();
        ReinitializeGameplayForValidation();
        var entrance = _roomEvents.Get<MermaidsCaveEntranceEvent>();
        foreach (var (group, room, key, destination) in new[]
        {
            (3, 0x0f, 0x44, 0x44),
            (1, 0x0e, 0x45, 0x26)
        })
        {
            string source = $"Mermaid's Cave {group:x}:{room:x2} / $90:$12";
            Vector2 doorPosition = new(0x68, 0x18);
            byte Door() => _currentRoom.GetMetatile(doorPosition);
            void Arrange()
            {
                _saveData.SetRoomFlag(group, room, 0x80, false);
                LoadValidationRoom(group, room);
                _player.WarpTo(new(0x68, 0x38), recordSafe:false);
                StepGameplayUpdates(2, Vector2.Zero);
                FailIf(!entrance.HasState || entrance.BlocksGameplay || Door() != 0xae ||
                    _currentRoom.IsSolid(_player.Position) || !_currentRoom.IsSolid(doorPosition),
                    $"{source} lost the original walkable approach and solid keyhole $ae.");
            }
            void Open()
            {
                for (int i = 0; i < 50 && !entrance.BlocksGameplay; i++)
                    StepGameplayUpdates(1, Vector2.Up);
                FailIf(!entrance.BlocksGameplay, $"{source} failed to acquire keyhole control.");
            }
            void FinishEntry()
            {
                for (int i = 0; i < 180 && _transitions.IsTransitioning; i++)
                    StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 5 || _currentRoom.Id != destination,
                    $"{source} doorway did not enter 5:{destination:x2}: actual {_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}.");
            }

            // Native helper covers contact, keys, clocks, cues and release.
            // Keep distinct source goldens for import visuals and room loading.
            FailIf(!_keyholes.Database.TryGet(group,room,out var record) || record.Treasure != key ||
                record.SubId != (group == 3 ? 2 : 3) || record.TileBase != 0x12 ||
                record.Palette != (group == 3 ? 5 : 4),
                $"{source} lost named key ${key:x2} or the source $18 sprite visual.");
            _inventory.GiveTreasure(key,1);
            Arrange(); Open();
            StepGameplayUpdates(120,Vector2.Zero);
            StepGameplayUpdates(60,Vector2.Up);
            FinishEntry();
            LoadValidationRoom(group,room);
            FailIf(entrance.HasState || Door() != 0xaf || !_inventory.HasTreasure(key),
                $"{source} failed persistent singleTileChanges.s $80/$16/$af substitution.");
            _player.WarpTo(new(0x68,0x38),recordSafe:false);
            StepGameplayUpdates(60,Vector2.Up); FinishEntry();

            foreach (int elapsed in new[] { 10,65 })
            {
                Arrange(); Open(); StepGameplayUpdates(elapsed,Vector2.Zero);
                LoadValidationRoom(0,0x11);
                FailIf(entrance.HasState || _player.CutsceneControlled || _roomEvents.MenusDisabled,
                    $"{source} cancellation leaked input/menu ownership.");
                LoadValidationRoom(group,room);
                FailIf(entrance.HasState || Door() != 0xaf || !_inventory.HasTreasure(key),
                    $"{source} lost its unlock flag or key on cancellation/re-entry.");
            }
        }
        GD.Print("Validated clean-US Mermaid keyholes: actual collision approach, retained era keys, event/key clocks, text, input handoff, cues/RNG and repeat; source room-entry/cancellation goldens retained.");
    }
}
