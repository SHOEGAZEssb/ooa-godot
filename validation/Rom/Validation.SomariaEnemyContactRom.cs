using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaEnemyContactRom()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            var target = _entities.Entities<KeeseCharacter>().First();
            var targetAdapter = _entities.EntityAdapters<KeeseRoomEntity>().First();
            var collision = new ObjectCollisionRom();
            SomariaBlock? observedBlock = null;
            int nativeContacts = 0;
            var observer = new CollisionRomObserver(() =>
            {
                // The mobile room target's post-AI position and eligibility
                // are declared inputs. The original scan owns all contact
                // priority, damage, recoil and invincibility publications.
                collision.ClearObjects();
                observedBlock = _entities.Entities<SomariaBlock>().SingleOrDefault();
                if (observedBlock is not { CollisionEnabled: true } child) return;
                const int enemy = 0xd080, item = 0xd700;
                collision[enemy] = 1; collision[enemy + 1] = 0x32;
                collision[enemy + 0xb] = (byte)target.Position.Y;
                collision[enemy + 0xd] = (byte)target.Position.X;
                collision[enemy + 0xf] = unchecked((byte)target.SpriteHeight);
                collision[enemy + 0x24] = (byte)(target.CollisionEnabled ? 0xb2 : 0x32);
                collision[enemy + 0x25] = (byte)targetAdapter.DimitriCollisionMode;
                collision[enemy + 0x26] = (byte)target.Record.CollisionRadiusY;
                collision[enemy + 0x27] = (byte)target.Record.CollisionRadiusX;
                collision[enemy + 0x28] = unchecked((byte)target.Record.RawDamage);
                collision[enemy + 0x29] = (byte)target.Health;
                collision[enemy + 0x2a] = target.NativeHitPending ? (byte)0x80 : (byte)0;
                collision[enemy + 0x2b] = unchecked((byte)target.InvincibilityCounter);
                collision[enemy + 0x2c] = (byte)target.KnockbackAngle;
                collision[enemy + 0x2d] = (byte)target.KnockbackCounter;
                collision[enemy + 0x3e] = 1; // Common enemy initialization.
                collision[item] = 1; collision[item + 1] = 0x18;
                collision[item + 0xb] = (byte)child.Position.Y; collision[item + 0xd] = (byte)child.Position.X;
                collision[item + 0xf] = unchecked((byte)child.ZHigh);
                collision[item + 0x24] = (byte)child.Collision;
                collision[item + 0x25] = unchecked((byte)child.DamageToApply);
                collision[item + 0x26] = (byte)child.Radius.Y; collision[item + 0x27] = (byte)child.Radius.X;
                collision[item + 0x28] = unchecked((byte)child.Damage); collision[item + 0x29] = (byte)child.Health;
                collision[item + 0x2c] = (byte)child.KnockbackAngle; collision[item + 0x2f] = (byte)child.Flags;
                collision[item + 0x2a] = (byte)child.ContactFlags;
                collision.Call(ObjectCollisionRom.Scan);
                nativeContacts += collision.Dispatches.Count(entry => entry.Effect == 0x2f);
            });
            _entities.AddEntity(observer);
            void CompareContact()
            {
                if (observedBlock is not { CollisionEnabled: true } child) return;
                FailIf(target.Health != collision[0xd0a9] || target.NativeHitPending != ((collision[0xd0aa] & 0x80) != 0) ||
                    target.InvincibilityCounter != unchecked((sbyte)collision[0xd0ab]) ||
                    target.KnockbackAngle != collision[0xd0ac] || target.KnockbackCounter != collision[0xd0ad] ||
                    (child.DamageToApply & 0xff) != collision[0xd725] || child.Health != collision[0xd729] ||
                    child.KnockbackAngle != collision[0xd72c] || child.Flags != collision[0xd72f] || child.ContactFlags != collision[0xd72a],
                    $"Native Somaria/ENEMY$32 contact differs: enemy health={target.Health}/{collision[0xd0a9]}, pending={target.NativeHitPending}/${collision[0xd0aa]:x2}, inv={target.InvincibilityCounter}/{collision[0xd0ab]}, angle={target.KnockbackAngle}/{collision[0xd0ac]}, recoil={target.KnockbackCounter}/{collision[0xd0ad]}; child damage={child.DamageToApply}/{unchecked((sbyte)collision[0xd725])}, health={child.Health}/{collision[0xd729]}, angle={child.KnockbackAngle}/{collision[0xd72c]}, flags=${child.Flags:x2}/${collision[0xd72f]:x2}, contact=${child.ContactFlags:x2}/${collision[0xd72a]:x2}; dispatches={string.Join(',', collision.Dispatches)}.");
            }
            foreach (var enemy in _entities.Entities<KeeseCharacter>())
            {
                enemy.Position = new(24, 24);
                enemy.InvincibilityCounter = 127;
            }
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.GiveTreasure(TreasureId.Bracelet, 1);
            _inventory.EquipA(TreasureId.CaneOfSomaria);
            _player.WarpTo(new(120, 56));
            _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Throw fixture must start on actual Crown floor.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(24, Vector2.Zero, batched: batched);
            var block = _entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            _inventory.EquipA(TreasureId.Bracelet);
            StepGameplayUpdates(8, Vector2.Up, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(30, Vector2.Zero, ["attack"], batched: batched);
            FailIf(!block.IsHeld || !_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                "Normal Cane/Bracelet inputs must finish lifting the block.");
            _player.Face(Vector2I.Down);
            StepGameplayUpdates(1, Vector2.Zero);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(block.IsHeld || block.State != 2 || block.Substate != 2 || (block.Collision & 0x7f) != 0x15,
                "Throwing ITEM$18 must retain Somaria collision$15 rather than generic thrown-object$16.");
            target.Health = 9;
            int updates = 0;
            while (!block.Finished && target.Health == 9 && updates++ < 80)
            {
                // Stage the room's mobile Keese on the descending block's
                // path, after it is low enough for the native Z overlap.
                if (block.ZHigh >= -7)
                {
                    target.Position = block.Position + Vector2.Down * 2;
                    target.InvincibilityCounter = 0;
                }
                StepGameplayUpdates(1, Vector2.Zero, afterUpdate: CompareContact);
            }
            FailIf(block.Finished || target.Health != 5 || target.InvincibilityCounter != 21 ||
                target.KnockbackCounter != 11 || block.Health != 9 || block.DamageToApply != -4,
                "A normal Somaria throw must use effect$2f and leave raw incoming damage queued in flight.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: CompareContact);
                FailIf(block.Finished || block.Health != 9 || block.DamageToApply != -4,
                    "Text freeze must retain the thrown block and pending damage.");
            }
            finally { _entities.TextActiveSource = text; }
            for (int i = 0; !block.Finished && i < 80; i++)
            {
                StepGameplayUpdates(1, Vector2.Zero, afterUpdate: CompareContact);
                FailIf(block.Health != 9 || block.DamageToApply != -4 || target.Health != 5,
                    "Throw substates omit itemUpdateDamageToApply: queued damage must remain unconsumed through landing.");
            }
            FailIf(!block.Finished, "The thrown block must finish its normal landing lifecycle.");
            FailIf(nativeContacts != 1, "The original collision scan must accept exactly one effect$2f contact before landing.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated original Somaria/Keese collision scan against reachable Cane/Bracelet release, Z contact, exact health/recoil/invincibility, raw pending damage through text and first landing, and repeat use with split/batched updates.");
    }
}
