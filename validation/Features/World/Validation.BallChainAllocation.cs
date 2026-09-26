using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBallChainAllocation()
    {
        foreach (bool batched in new[] { false, true })
        for (int free = 0; free <= 4; free++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa0);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.Clear();
                var fillers = Enumerable.Range(0, 16 - free).Select(_ =>
                    _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, new(216, 152)))).ToArray();
                for (int offset = 0; offset < 0x40; offset++) _runtimeState.SetWramByte(0xc0c0 + offset, 0x66);
                FailIf(!_entities.TrySpawnEnemy(EnemyId.BallAndChainSoldier, 0, new(120, 88), "Isolated Crown PART allocation", out string error), error);
                var soldier = _entities.Entities<BallChainSoldierCharacter>().Single();
                int randomCalls = _entities.RandomCalls;
                Step();
                var parts = _entities.Entities<SpikedBallPart>();
                FailIf(soldier.State != 8 || !soldier.Visible || soldier.ReturnState != 8 ||
                    !parts.Select(p => p.SubId).SequenceEqual(Enumerable.Range(0, free)) ||
                    parts.Any(p => p.State != 1) || _entities.RandomCalls != randomCalls + 1,
                    $"With {free} PART slots, $4b must retain partial allocation, initialize in the same pass, and consume one state0 RNG byte.");
                for (int offset = 0; offset < 0x40; offset++)
                {
                    int expected = free == 4 ? 0x66 : offset switch
                    {
                        0 => 0x2a, 1 => 3, 0x16 => 0xc0,
                        0x17 => free == 0 ? 0xe0 : 0xd0 + 16 - free,
                        _ => 0x66
                    };
                    FailIf(_runtimeState.ReadWramByte(0xc0c0 + offset) != expected,
                        $"Full chain allocation must preserve clean-US echo write ${0xc0c0 + offset:x4}=${expected:x2} with {free} free parts.");
                }
                foreach (var filler in fillers) filler.ClearHealthAndCollision();
                Step(2);
                FailIf(_entities.Entities<SpikedBallPart>().Count != free || soldier.State == 0 ||
                    !_entities.PartSlotAvailable || _entities.RandomCalls != randomCalls + 1,
                    "Freeing PART slots after the failed chain calls must not retry initialization or consume its RNG again.");
                soldier.Health = 0;
                Step();
                FailIf(_entities.Entities<SpikedBallPart>().Any(),
                    "Every successfully allocated partial chain must release when its soldier dies.");
            }
        }
    }
}
