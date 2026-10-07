using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaSmasherRom()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool isBall in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xb4); _entities.Clear();
            var db = new EnemyDatabase(); var random = new OracleRandom();
            var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
            var world = new SmasherRoomEnvironment(_ => parent, () => { }, () => { },
                () => true, () => true, () => { }, () => { }, _ => { }, 1, true);
            SmasherRoomEntity Add(SmasherCharacter actor, int subid) => (SmasherRoomEntity)_entities.TryAllocateEnemy(slot =>
            {
                actor.InitializePending(db.ImportedEnemy(EnemyId.Smasher, subid), _currentRoom, new(120, 88), random, slot);
                return new SmasherRoomEntity(actor, world, subid == 0);
            })!;
            var ballAdapter = Add(ball, 0); var parentAdapter = Add(parent, 1);
            ball.UpdateInitializationFrame(1, () => { }, () => parent);
            parent.UpdateInitializationFrame(1, () => { }, () => null);
            ball.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { });
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.EquipA(TreasureId.CaneOfSomaria); _inventory.EquipB(0);
            _player.WarpTo(new(56, 72)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position), "Smasher contact fixture must cast from actual room $4:$b4 floor.");
            var actor = isBall ? ball : parent;
            var adapter = isBall ? ballAdapter : parentAdapter;
            Vector2 point = new(72, 70);
            _currentRoom.SetPositionTileAndCollision(point, 0x0c, 0, 0);
            var collision = new ObjectCollisionRom();
            SomariaBlock? observed = null;
            int contacts = 0;
            _entities.AddEntity(new CollisionRomObserver(() =>
            {
                collision.ClearObjects(); observed = _entities.Entities<SomariaBlock>().SingleOrDefault();
                if (observed is not { CollisionEnabled: true } block) return;
                collision[0xd080] = 1; collision[0xd081] = 0x74;
                collision[0xd08b] = (byte)actor.Position.Y; collision[0xd08d] = (byte)actor.Position.X;
                collision[0xd08f] = unchecked((byte)(actor.ZFixed >> 8));
                collision[0xd0a4] = actor.CollisionEnabled ? (byte)0xf4 : (byte)0x74;
                collision[0xd0a5] = (byte)actor.CollisionMode;
                collision[0xd0a6] = (byte)(actor.CollisionBounds.Size.Y / 2);
                collision[0xd0a7] = (byte)(actor.CollisionBounds.Size.X / 2);
                collision[0xd0a9] = (byte)actor.Health;
                collision[0xd0aa] = actor.PendingCollision ? (byte)0x80 : (byte)0;
                collision[0xd0ab] = unchecked((byte)actor.InvincibilityCounter);
                collision[0xd0ac] = (byte)actor.KnockbackAngle; collision[0xd0ad] = (byte)actor.KnockbackCounter;
                collision[0xd700] = 1; collision[0xd701] = 0x18;
                collision[0xd70b] = (byte)block.Position.Y; collision[0xd70d] = (byte)block.Position.X;
                collision[0xd70f] = unchecked((byte)block.ZHigh); collision[0xd724] = (byte)block.Collision;
                collision[0xd725] = unchecked((byte)block.DamageToApply);
                collision[0xd726] = (byte)block.Radius.Y; collision[0xd727] = (byte)block.Radius.X;
                collision[0xd729] = (byte)block.Health; collision[0xd72f] = (byte)block.Flags;
                collision.Call(ObjectCollisionRom.Scan);
                contacts += collision.Dispatches.Count(entry => entry.Effect == 0x2d);
            }));
            void Compare()
            {
                if (observed is not { CollisionEnabled: true } block) return;
                FailIf(block.Flags != collision[0xd72f] || block.Health != collision[0xd729] ||
                    (block.DamageToApply & 0xff) != collision[0xd725] || actor.Health != collision[0xd0a9] ||
                    actor.PendingCollision != ((collision[0xd0aa] & 0x80) != 0) ||
                    actor.InvincibilityCounter != unchecked((sbyte)collision[0xd0ab]) ||
                    actor.KnockbackAngle != collision[0xd0ac] || actor.KnockbackCounter != collision[0xd0ad],
                    $"Native ENEMY$74 mode${actor.CollisionMode:x2} Somaria collision publications differ.");
            }
            void Step(int count = 1, bool press = false) =>
                StepGameplayUpdates(count, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched: batch, afterUpdate: Compare);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                ball.CopyCarriedPosition(new(40, 104), 0);
                parent.CopyCarriedPosition(new(136, 104), 0);
                Step(1, true); Step(14);
                var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
                Step(8);
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
            FailIf(contacts != 2, "Native Smasher/ball effect$2d must destroy one block per repeated cast.");
        }
        GD.Print("Validated native Smasher/ball effect$2d against actual Cane casting and live contact, exact mark/health/recoil/mode publications, phase-in/Z/invincibility gates, text freeze, floor restoration and repeated split/batched use.");
    }
}
