using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyTiming()
    {
        foreach (bool batch in new[] { false, true })
        foreach (string mode in new[] { "normal", "text", "freeze", "scroll" })
        {
            LoadValidationRoom(4, 0xbc);
            _entities.Clear();
            _player.WarpTo(new(24, 24));
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            DungeonKeyUseEffect Spawn() => _entities.Spawn<DungeonKeyUseEffect>(
                new DungeonKeyUseSpawn(new(24, 24), _treasures.GetObjectVisual(0x42)));
            var key = Spawn();
            var outgoing = key;
            var freeze = new CrownEntranceFreeze { FreezesRoomEntities = mode == "freeze" };
            typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_entities, [freeze]);
            _sound.ClearPlayRequestAudit();
            try
            {
                if (mode == "text") _dialogue.ShowMessage("Key timing.", 120);
                if (mode == "scroll")
                {
                    _entities.BeginScreenTransition(4, _currentRoom, new(240, 0), _player);
                    key = Spawn();
                }
                FailIf(key.Visible || key.Initialized || _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 0,
                    "INTERAC$17 allocation must not run state0 or play its sound.");
                Step(1);
                FailIf(!key.Visible || !key.Initialized || key.Counter != 8 || key.Z != -4 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != (mode == "scroll" ? 2 : 1),
                    $"INTERAC$17 state0 must initialize without consuming a timer update during {mode}.");
                if (mode != "normal")
                {
                    Step(20);
                    FailIf(key.Counter != 8 || key.Phase != 0 || !key.Visible ||
                        mode == "scroll" && (!outgoing.Initialized || outgoing.Counter != 8),
                        $"INTERAC$17 must stop after state0 during {mode}.");
                }
                _dialogue.Close();
                freeze.FreezesRoomEntities = false;
                if (mode == "scroll") _entities.FinishScreenTransition();
                Step(7);
                FailIf(key.Counter != 1 || key.Phase != 0 || key.Z != -4,
                    "INTERAC$17 must retain Z=$fc through seven state1 updates.");
                Step(1);
                FailIf(key.Counter != 20 || key.Phase != 1 || key.Z != -8,
                    "INTERAC$17 state1 update8 must enter the 20-update Z=$f8 phase.");
                // Check that text also holds the second timer, then resumes it.
                _dialogue.ShowMessage("Key timer hold.", 120);
                Step(3);
                FailIf(key.Counter != 20, "Key sprite state2 must remain frozen during text.");
                _dialogue.Close();
                Step(19);
                FailIf(key.Finished || key.Counter != 1 || !key.Visible,
                    "INTERAC$17 must survive through state2 update19.");
                Step(1);
                FailIf(_entities.Entities<DungeonKeyUseEffect>().Count != 0,
                    "INTERAC$17 must delete and release its native slot on state2 update20.");
            }
            finally
            {
                _dialogue.Close();
                freeze.FreezesRoomEntities = false;
                if (mode == "scroll") _entities.FinishScreenTransition();
                LoadValidationRoom(0, 0x60);
            }
        }
    }
}
