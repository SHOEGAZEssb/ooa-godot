using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleQueue()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int queued in new[] { 0, 30, 31 })
        foreach (int freeSlots in new[] { 0, 1, 2 })
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _inventory.RefillHealth();
            _player.WarpTo(new(120, 56));
            // Isolate the cutscene consumer; real orb input is covered separately.
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 1);
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(_entities.FloorToggle!.Counter != 1, "Toggle queue fixture must reach the final delayed update.");
            int[] positions = [0x44, 0x46];
            byte[] Graphics(int p) => Enumerable.Range(0, 4).SelectMany(i => new[] {
                _currentRoom.GetBackgroundSubtileForValidation((p & 15) * 2 + i % 2, (p >> 4) * 2 + i / 2),
                _currentRoom.GetBackgroundAttributeForValidation((p & 15) * 2 + i % 2, (p >> 4) * 2 + i / 2)
            }).ToArray();
            foreach (int p in positions)
            {
                _currentRoom.SetUnderlyingStorageMetatile(p, 0x29);
                _currentRoom.SetPositionTileAndCollision(new((p & 15) * 16 + 8, (p >> 4) * 16 + 8),
                    0x10, null, (long)_animationTicks);
            }
            var before = positions.Select(Graphics).ToArray();
            // getFreeInteractionSlot searches $d2..$df. Leave the final
            // zero, one or two slots free immediately before completion.
            for (int i = 0; i < 14 - freeSlots; i++)
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 120), 0));
            for (int i = 0; i < queued; i++)
                FailIf(!_rooms.TrySetTile(0x11, 0xa0), "Toggle fixture must fill the requested queue entries.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.FloorToggle.Active || positions.Any(p => _currentRoom.Layout[p] != 0x0f ||
                _currentRoom.GetUnderlyingStorageMetatile(p) != 0x0f ||
                _currentRoom.GetTerrainInfo(new((p & 15) * 16 + 8, (p >> 4) * 16 + 8)).Collision != 0x1e),
                "Toggle collision and both buffers must change even when setTile is rejected.");
            var debris = _entities.Entities<RockDebrisEffect>();
            FailIf(debris.Count != freeSlots || debris.Any(d => d.ElapsedUpdates != 1),
                "Debris allocations must honor the interaction pool independently of graphics queue capacity.");
            for (int i = 0; i < debris.Count; i++)
                FailIf(debris[i].Position != new Vector2(i == 0 ? 104 : 72, 72) ||
                    _entities.InteractionSlot(debris[i]) != 16 - freeSlots + i,
                    "Descending floor scan must allocate $46 before $44 into ascending free interaction slots.");
            if (queued != 0)
                FailIf(positions.Where((p, i) => !Graphics(p).SequenceEqual(before[i])).Any(),
                    "Pending or rejected toggle writes must retain the displayed block mapping.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            for (int i = 0; i < positions.Length; i++)
            {
                bool accepted = queued == 0 || queued == 30 && positions[i] == 0x46;
                FailIf(Graphics(positions[i]).SequenceEqual(before[i]) == accepted,
                    $"Toggle queue must accept descending $46 first and never retry rejected ${positions[i]:x2}.");
            }
            FailIf(_rooms.PendingTileGraphics != 0, "Toggle graphics queue must finish draining.");
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<RockDebrisEffect>().Count != 0 || !_entities.InteractionSlotAvailable,
                "Completed debris and filler puffs must release their native interaction slots.");
        }
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(0, 0x60);
    }
}
