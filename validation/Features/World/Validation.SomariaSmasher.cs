using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaSmasher()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        foreach (bool batch in new[] { false, true })
        foreach (bool isBall in new[] { false, true })
        {
            LoadValidationRoom(4, 0xb4); _entities.Clear();
            var db = new EnemyDatabase(); var random = new OracleRandom();
            var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
            var world = new SmasherRoomEnvironment(_ => parent, () => { }, () => { },
                () => true, () => true, () => { }, () => { }, _ => { }, 1, true);
            SmasherRoomEntity Add(SmasherCharacter actor, int subid) => (SmasherRoomEntity)_entities.TryAllocateEnemy(slot =>
            {
                actor.InitializePending(db.ImportedEnemy(0x74, subid), _currentRoom, new(120, 88), random, slot);
                return new SmasherRoomEntity(actor, world, subid == 0);
            })!;
            var ballAdapter = Add(ball, 0); var parentAdapter = Add(parent, 1);
            ball.UpdateInitializationFrame(1, () => { }, () => parent);
            parent.UpdateInitializationFrame(1, () => { }, () => null);
            ball.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { });
            _player.WarpTo(new(24, 24));
            void Step(int count = 1)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            var actor = isBall ? ball : parent;
            var adapter = isBall ? ballAdapter : parentAdapter;
            Vector2 point = new(72, 70);
            _currentRoom.SetPositionTileAndCollision(point, 0x0c, 0, 0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                ball.CopyCarriedPosition(new(40, 104), 0);
                parent.CopyCarriedPosition(new(136, 104), 0);
                FailIf(!_entities.TryCreateSomariaBlock(_player, 4, point, 0), "Smasher fixture must allocate ITEM$18.");
                var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
                Step(9);
                actor.CopyCarriedPosition(point, 0);
                FailIf(adapter.ApplySomariaBlockCollision(block, []), "Smasher cannot hit ITEM$18 before its phase-in enables collision.");
                actor.CopyCarriedPosition(isBall ? new(40, 104) : new(136, 104), 0);
                Step();
                foreach (int z in new[] { -8, 7 })
                {
                    actor.CopyCarriedPosition(point, z);
                    FailIf(adapter.ApplySomariaBlockCollision(block, []), $"Smasher/Somaria Z difference {z} must reject contact.");
                }
                actor.CopyCarriedPosition(point, 0);
                foreach (int invincibility in new[] { -1, 1 })
                {
                    actor.InvincibilityCounter = invincibility;
                    FailIf(adapter.ApplySomariaBlockCollision(block, []), "Smasher invincibility of either sign must reject ITEM$18.");
                }
                actor.InvincibilityCounter = 0;
                int mode = actor.CollisionMode;
                Step();
                FailIf((block.Flags & 0x20) == 0 || block.Finished || block.Health != 9 || block.DamageToApply != 0 ||
                    actor.Health != 5 || actor.PendingCollision || actor.InvincibilityCounter != 0 || actor.KnockbackCounter != 0 ||
                    mode != (isBall ? 0x63 : 0x45) || actor.CollisionMode != mode,
                    $"Smasher mode${mode:x2} must mark Somaria for deletion without boss damage, pending status, recoil or mode change.");
                var textSource = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(2);
                    FailIf(block.Finished || _currentRoom.GetMetatile(point) != 0xda, "Frozen Somaria retains its marked solid tile.");
                }
                finally { _entities.TextActiveSource = textSource; }
                Step();
                FailIf(!block.Finished || _currentRoom.GetMetatile(point) != 0x0c || !_entities.DynamicItemSlotAvailable,
                    "Smasher-marked Somaria must restore its floor on the next eligible item update.");
            }
        }
        GD.Print("Validated Smasher and ball Somaria destruction, repeated live updates, phase-in/Z/invincibility gates, freeze and floor restoration.");
    }
}
