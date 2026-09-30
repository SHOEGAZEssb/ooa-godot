using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwordContactHandoffRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int subid in new[] { 0, 1 })
        foreach (bool heldContact in new[] { false, true })
        {
            SwordRom rom = PrepareSwordGameplayRom(1);
            // Isolate the weapon handoff from Link's independently validated
            // damage owner using its real collision/input eligibility mask.
            _player.SetBraceletLiftCollisionsDisabled(true);
            SmogProjectilePart? target = null;
            var data = new SmogProjectileDatabase();
            int update = 0, accepted = 0, contacts = 0;
            bool pressed = true;
            // Capture target inputs after its own dispatch. The ROM executes
            // the sword parent/child/post pass and the real collision scan;
            // it retains its weapon contact signal across subsequent updates.
            var observer = new CollisionRomObserver(() =>
            {
                rom[0xd00b] = (byte)_player.Position.Y;
                rom[0xd00d] = (byte)_player.Position.X;
                rom.Update(true, pressed);
                pressed = false;
                if (target is null) return;
                rom[0xd0e4] = (byte)(target.CollisionEnabled ? 0xca : 0x4a);
                rom[0xd0e5] = (byte)target.CollisionMode;
                // common/parts/commonCode.s:partCommon_standardUpdate writes
                // $08 after native state-zero graphics/properties loading.
                rom[0xd0fe] = 0x08;
                rom[0xd0e8] = (byte)data.RawDamage;
                rom[0xd0e9] = (byte)data.Health;
                rom[0xd0ea] = (byte)target.ContactFlags;
                rom[0xd0eb] = unchecked((byte)target.InvincibilityCounter);
                rom[0xd0cb] = (byte)target.Position.Y;
                rom[0xd0cd] = (byte)target.Position.X;
                rom[0xd0e6] = (byte)data.Radius.Y;
                rom[0xd0e7] = (byte)data.Radius.X;
                // Mirror Link's actual body eligibility, rather than placing
                // him inside a target hitbox to manufacture a weapon contact.
                rom[0xd024] = 0;
                rom.Call(7, ObjectCollisionRom.Scan);
                if (rom[0xd62a] != 0) accepted++;
                if ((rom[0xd0ea] & 0x80) != 0) contacts++;
            });
            _entities.AddEntity(observer);
            void Compare()
            {
                CompareSwordRom(_player, rom, $"Sword PART $4a:${subid:x2} contact, held={heldContact}, batch={batched}, update={update++}");
                if (target is null) return;
                FailIf(target.ContactFlags != rom[0xd0ea] || target.InvincibilityCounter != unchecked((sbyte)rom[0xd0eb]),
                    $"Sword PART $4a:${subid:x2} contact flags/invincibility differ at update={update}.");
            }
            if (heldContact) StepGameplayUpdates(20, Vector2.Zero, ["attack"], ["attack"], batched, afterUpdate: Compare);
            target = _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(120, 96), subid));
            StepGameplayUpdates(35, Vector2.Zero, ["attack"], heldContact ? [] : ["attack"], batched, afterUpdate: Compare);
            bool reportsContact = subid == 0 && !heldContact;
            FailIf(reportsContact ? accepted == 0 : accepted != 0,
                $"PART $4a must report swung-sword contact for subid $00, preserve the $01 no-op and reject held collision $09: subid={subid}, held={heldContact}, accepted={accepted}, contacts={contacts}.");
            _entities.Clear();
            _player.SetBraceletLiftCollisionsDisabled(false);
            StepGameplayUpdates(1, Vector2.Zero);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_player.IsAttacking, "Sword must be usable again after the contact target is retired.");
        }
        GD.Print("Validated executed-ROM sword contact flags, real/no-op effect priority and retained parent handoff through individual/batched gameplay updates on reachable $4:$91 floor.");
    }
}
