using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockAllocation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            LoadValidationRoom(4, 0x9b);
            _player.WarpTo(new(88, 40));
            for (int i = 0; _pushBlocks.RemainingPushFrames != 1 && i < 80; i++)
            {
                StepGameplayUpdates(1, Vector2.Down);
                FailIf(_currentRoom.IsSolid(_player.Position), "Blue-block allocation approach must stay on floor.");
            }
            FailIf(_pushBlocks.RemainingPushFrames != 1 || _pushBlocks.Active,
                "Allocation fixture must stop one input update before pushing source $35.");
            var fillers = new List<PuzzlePuffEffect>();
            while (slots.Count < 14 - free)
                fillers.Add(_entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 120), 0)));
            StepGameplayUpdates(1, Vector2.Down);
            var first = _entities.Entities<PushBlockController>();
            FailIf(!_pushBlocks.Active || first.Count != free ||
                free == 1 && (first[0].BlockTopLeft != new Vector2(96, 80.5f) || _entities.InteractionSlot(first[0]) != 15),
                "A single free slot must go to descending source $56 before $43; a full pool must skip both.");
            StepGameplayUpdates(18, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<PushBlockController>().Count != free || fillers.Any(p => p.Finished),
                "Repeated synchronizer scans must not allocate through occupied puff slots.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.Entities<PushBlockController>().Count != free || fillers.Any(p => !p.Finished),
                "Puff deletion on update20 must not retroactively satisfy the earlier synchronizer dispatch.");
            StepGameplayUpdates(1, Vector2.Zero);
            var children = _entities.Entities<PushBlockController>().OrderBy(b => _entities.InteractionSlot(b)).ToArray();
            FailIf(children.Length != 2 || children[0].BlockTopLeft != (free == 0 ? new Vector2(96, 80.5f) : new Vector2(48, 64.5f)),
                "The next state1 scan must retry skipped partners in source order and initialize them in later slots.");
            StepGameplayUpdates(11, Vector2.Zero, batched: batched);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != (free == 0 ? 2 : 1),
                "Primary update32 must finish only blocks that started with it.");
            StepGameplayUpdates(19, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<PushBlockController>().Count != (free == 0 ? 2 : 1),
                "Retried blocks must retain their independent movement counter through update31.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.Entities<PushBlockController>().Count != 0 ||
                new[] { 0x45, 0x53, 0x66 }.Any(p => _currentRoom.Layout[p] != 0x2e),
                "Retried blocks must finish their own update32 after the primary reservation is gone.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
