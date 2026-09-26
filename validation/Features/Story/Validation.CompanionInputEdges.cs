using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCompanionInputEdges()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        {
            PrepareCompanionFidelityRoom();
            IRoomEntity actor = id switch
            {
                0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(new(72,56), 1, 0, 0x2a, Riding: true)),
                0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(new(72,56), 1, 0, 0x2a, Riding: true)),
                _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(new(72,56), 1, 0, 0x2a, Riding: true))
            };
            CompanionRuntimeState.Begin(_runtimeState, id, 0x2a, actor.Node.Position, 1);
            string Phase() => actor switch
            {
                RickyCompanionRoomEntity r => r.Phase.ToString(),
                DimitriCompanionRoomEntity d => d.Phase.ToString(),
                MooshCompanionRoomEntity m => m.Phase.ToString(),
                _ => throw new System.InvalidOperationException()
            };
            void Step(int count = 1, string[]? held = null, string[]? pressed = null) =>
                StepGameplayUpdates(count, Vector2.Zero, held, pressed, batched);
            Step();
            FailIf(Phase() != "Riding", $"Companion ${id:x2} input fixture must begin riding.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // All three native handlers read wGameKeysJustPressed. A
                // button consumed by text must not become a later actor edge.
                _dialogue.ShowGameplayMessage("Companion input", 100);
                Step(1, ["attack", "item"], ["attack", "item"]);
                Step(3, ["attack", "item"]);
                FailIf(Phase() != "Riding", $"Companion ${id:x2} acted during dialogue.");
                _dialogue.Close();
                Step(4, ["attack", "item"]);
                FailIf(Phase() != "Riding", $"Companion ${id:x2} reconstructed a consumed dialogue edge.");
                Step();
                Step(1, ["attack"], ["attack"]);
                string expected = id switch { 0x0b => "Punching", 0x0c => "Eating", _ => "Airborne" };
                FailIf(Phase() != expected, $"Companion ${id:x2} fresh attack must enter {expected}, got {Phase()}.");
                Step(100);
                FailIf(Phase() != "Riding", $"Companion ${id:x2} must finish the attack before repeat {repeat}.");
            }
            Step(1, ["item"], ["item"]);
            FailIf(Phase() != "Dismounting", $"Companion ${id:x2} fresh item edge must dismount.");
            Step(60);
            FailIf(((IPlayerRideableRoomEntity)actor).LinkRiding,
                $"Companion ${id:x2} dismount must complete without replaying input.");
            LoadValidationRoom(0, 0x60);
            Step(2);
        }
    }
}
