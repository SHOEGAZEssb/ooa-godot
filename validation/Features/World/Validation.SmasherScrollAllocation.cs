using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmasherScrollAllocation()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool expire in new[] { false, true })
        {
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            _player.WarpTo(new(72, 40));
            var database = new EnemyDatabase();
            for (int i = 0; i < 15; i++)
            {
                _entities.TryAllocateEnemy(slot =>
                {
                    var actor = new SmasherCharacter();
                    actor.InitializePending(database.ImportedEnemy(EnemyId.Smasher, 0), _currentRoom,
                        new(120, 88), new OracleRandom(), slot);
                    return new SmasherSlotValidationEntity(actor, (_, _) => { });
                });
            }
            _saveData.SetRoomFlag(4, 0xb4, 0x80, false);
            _entities.BeginScreenTransition(4, _world.LoadRoom(4, 0xb4), new(240, 0), _player);
            var ball = _entities.Entities<SmasherCharacter>().Single();
            FailIf(ball.State != 0 || ball.NativeSlot != 15,
                "Only slot$0f must remain for the incoming ball; its parent allocation must fail.");
            int expiration = ball.ExpirationCounter;
            int rng = _entities.RandomCalls;
            void Objects(int count)
            {
                if (batch) _entities.Update(count / 60.0, _player);
                else for (int i = 0; i < count; i++) _entities.Update(1.0 / 60, _player);
            }
            // smasher_ball_updateRespawnTimer expires at 180 even updates.
            // Keep allocation blocked close to that boundary: failed state$00
            // retries must retain var30, not restart the lifetime each time.
            Objects(354);
            FailIf(ball.State != 0 || ball.ExpirationCounter != expiration + 177 || _entities.RandomCalls != rng + 354 ||
                _entities.OutgoingEntities<SmasherCharacter>().Count != 15,
                "Scrolling state0 must retry each update, consuming property RNG and the even-frame timer while retaining outgoing slots.");
            if (expire)
            {
                Objects((180 - ball.ExpirationCounter) * 2);
                FailIf(ball.State != 14 || ball.Counter1 != 60 || ball.Visible || ball.CollisionEnabled ||
                    _entities.Entities<PuzzlePuffEffect>().Count != 1,
                    "Unlinked state0 expiry during scrolling must allocate one puff and disappear in the same update.");
                Objects(2);
                FailIf(ball.Counter1 != 60, "Initialized unlinked disappearance must freeze during the remaining scroll.");
                _entities.FinishScreenTransition();
                Objects(1);
                FailIf(ball.Counter1 != 59 || _entities.Entities<SmasherCharacter>().Count != 1,
                    "Unlinked disappearance must resume without allocating a late parent.");
                Objects(59);
                FailIf(ball.State != 15 || !ball.Visible || ball.ZFixed >> 8 != -32,
                    "Unlinked ball must respawn after its full60 eligible hidden updates.");
                continue;
            }
            _entities.FinishScreenTransition();
            Objects(1);
            FailIf(_entities.Entities<SmasherCharacter>().Count != 2 || ball.State != 8 || ball.IsBall ||
                _entities.OutgoingEntities<SmasherCharacter>().Count != 0,
                "After bulk clear, retry must reuse a lower parent slot and swap native ball/parent roles.");
            Objects(1);
            FailIf(_entities.Entities<SmasherCharacter>().Any(actor => actor.State < 8),
                "The lower linked slot must initialize on the following native enemy pass.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
