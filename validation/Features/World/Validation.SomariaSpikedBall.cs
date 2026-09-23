using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaSpikedBall()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var random = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0xa0);
            _player.WarpTo(new(232, 144));
            void Step(int count = 1)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            Step();
            var soldier = _entities.Entities<BallChainSoldierCharacter>().Single();
            var ball = _entities.Entities<SpikedBallPart>().Single(part => part.SubId == 0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // Isolate the part response from the now-supported body hit.
                soldier.InvincibilityCounter = 127;
                // Isolated collision surface alongside the native orbit. Link
                // stays on the entrance floor; this does not test progression.
                Vector2 point = new SomariaPlacementDatabase().Align(soldier.Position + new Vector2(8, 0));
                _currentRoom.SetPositionTileAndCollision(point, 0x0c, 0, 0);
                FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Somaria/spiked-ball fixture must allocate ITEM$18.");
                var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
                Step(9);
                FailIf(block.CollisionEnabled || (block.Flags & 0x20) != 0,
                    "Phasing ITEM$18 must reject PART$2a until the ninth animation update enables collisions.");
                for (int count = 0; count < 80 && (block.Flags & 0x20) == 0; count++) Step();
                FailIf((block.Flags & 0x20) == 0 || block.Finished || block.Health != 9 || block.DamageToApply != 0 ||
                    ball.PendingCollision || ball.InvincibilityCounter != 0 || soldier.InvincibilityCounter <= 0 ||
                    _currentRoom.GetMetatile(point) != 0xda,
                    $"PART$2a effect$2d must mark the solid block without health, recoil, or part-status side effects (repeat {repeat}).");
                var textSource = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(2);
                    FailIf(block.Finished || _currentRoom.GetMetatile(point) != 0xda,
                        "A collision-marked block must retain its tile while its item handler is frozen.");
                }
                finally { _entities.TextActiveSource = textSource; }
                Step();
                FailIf(!block.Finished || _currentRoom.GetMetatile(point) != 0x0c || !_entities.DynamicItemSlotAvailable,
                    "The next eligible ITEM$18 update must restore its floor and release its native slot.");
                Step(2);
                FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any(), "The deleted block must not reappear after collision completion.");
            }
        }
        // Isolate the collision pass to make competing ITEM slot order and
        // rejection gates observable independently of the ball's orbit.
        foreach (bool seedFirst in new[] { false, true })
        {
            LoadValidationRoom(4, 0xa0);
            _player.WarpTo(new(232, 144));
            void Step(int count)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                scheduler.Advance(count / 60.0, update);
            }
            Step(1);
            var soldier = _entities.Entities<BallChainSoldierCharacter>().Single();
            var ball = _entities.Entities<SpikedBallPart>().Single(part => part.SubId == 0);
            var target = _entities.EntityAdapters<SpikedBallRoomEntity>().Single(part => part.Node == ball);
            Vector2 point = new(40, 38);
            _currentRoom.SetPositionTileAndCollision(point, 0x0c, 0, 0);
            BombEffect? filler = seedFirst ? _entities.Spawn<BombEffect>(new BombSpawn(
                _player, new BombDatabase().Data, 4, _ => { })) : null;
            FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Collision ordering fixture must allocate ITEM$18.");
            var blockAdapter = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single();
            var block = blockAdapter.Block;
            Step(10);
            filler?.Discard();
            FailIf(!new SeedSatchelDatabase().TryGet(0x21, out var record), "Missing source Scent Seed.");
            var seed = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                point, Vector2I.Down, record, 4, SeedLaunchKind.Shooter));
            Step(1);
            seed.Position = ball.Position = point;
            var seedAdapter = _entities.EntityAdapters<EmberSeedRoomEntity>().Single(item => item.Node == seed);
            FailIf((_entities.DynamicItemSlotOf(seedAdapter) < _entities.DynamicItemSlotOf(blockAdapter)) != seedFirst ||
                !block.CollisionEnabled || !seed.CollisionEnabled || seed.HasPendingNativeCollision,
                "Slot-order fixture must overlap live block/seed with the later-created seed reusing $d7 only in the seed-first case.");
            foreach (int invincibility in new[] { -1, 1 })
            {
                ball.InvincibilityCounter = invincibility;
                FailIf(target.ApplySomariaBlockCollision(block, []), "PART$2a must reject ITEM$18 for either sign of invincibility.");
            }
            ball.InvincibilityCounter = 0;
            ball.PublishCollision(0x15);
            FailIf(target.ApplySomariaBlockCollision(block, []), "PART$2a pending var2a must suppress the entire next collision check.");
            ball.UpdateFrame(_player.Position);
            ball.Position = point;
            foreach (int z in new[] { -8, 7 })
            {
                soldier.ZFixed = z << 8;
                ball.UpdateFrame(_player.Position);
                ball.Position = point;
                FailIf(target.ApplySomariaBlockCollision(block, []), $"PART$2a / ITEM$18 Z difference {z} is outside [-7,6].");
            }
            soldier.ZFixed = 0;
            ball.UpdateFrame(_player.Position);
            ball.Position = point;
            _entities.ResolvePostObjectCollisions(_player);
            FailIf(((block.Flags & 0x20) != 0) == seedFirst || seed.HasPendingNativeCollision != seedFirst,
                "Native slot order must select only the first overlapping item, independently of scene insertion order.");
            FailIf(ball.PendingCollision || ball.InvincibilityCounter != 0,
                "Neither spiked-ball seed consumption nor block destruction may publish a part hit or recoil.");
            foreach (int z in new[] { -7, 6 })
            {
                block.Flags &= ~0x20;
                soldier.ZFixed = z << 8;
                ball.UpdateFrame(_player.Position);
                ball.Position = point;
                FailIf(!target.ApplySomariaBlockCollision(block, []) || (block.Flags & 0x20) == 0,
                    $"PART$2a / ITEM$18 Z difference {z} must remain inside the source half-open interval.");
            }
        }
        GD.Print("Validated repeated live Somaria/spiked-ball destruction, phase-in/freeze restoration, native slot reuse, pending-hit and Z/invincibility gates.");
    }
}
