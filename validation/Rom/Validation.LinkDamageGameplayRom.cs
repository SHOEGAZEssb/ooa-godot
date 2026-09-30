using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkInvincibilityRom()
    {
        var world = new ValidationRingPlayerWorld();
        var player = new Player();
        player.Initialize(world, new InventoryState(_treasures), new(80, 64), new OracleRandom());
        var counter = typeof(Player).GetField("_enemyInvincibilityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rom = new LinkCollisionRom();
        rom[0xd01b] = 8;
        for (int value = 0; value < 256; value++)
        for (int phase = 0; phase < 8; phase++)
        {
            counter.SetValue(player, (float)unchecked((sbyte)value));
            rom[0xd02b] = (byte)value;
            world.FrameCounter = phase;
            rom[0xcc00] = (byte)phase;
            rom[0xd01c] = 8;
            rom.Call(LinkCollisionRom.Invincibility);
            player._Process(1.0 / 60.0);
            FailIf(player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                player.DamagePaletteActive != (rom[0xd01c] == 0x0d),
                $"ROM invincibility counter=${value:x2}, phase={phase}: ROM=${rom[0xd02b]:x2}/${rom[0xd01c]:x2}, runtime={player.InvincibilityFrames}/{player.DamagePaletteActive}.");
        }
        player.Free();
        GD.Print("Validated all 256 signed invincibility bytes and eight flashing phases against the ROM.");
    }

    private void ValidateLinkDamageGameplayRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int ring in new[] { 0, (int)RingId.ArmorL3, (int)RingId.Steadfast })
        foreach (int outcome in new[] { 0, 1, 2 }) // survive, lethal, potion
        foreach (int angle in new[] { 0, 4, 8, 12, 16, 20, 24, 28 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0), "Could not equip ROM recoil fixture.");
            if (outcome != 0) _inventory.ApplyDamage(_inventory.HealthQuarters - 1);
            if (outcome == 2) _inventory.GiveTreasure(TreasureId.Potion, 0);
            var room = _rooms.CurrentRoom;
            var rom = new LinkCollisionRom();
            rom[0xcc33] = (byte)room.ActiveCollisions;
            rom[0xc6cb] = (byte)ring;
            rom[0xc6aa] = (byte)_inventory.HealthQuarters;
            rom[0xc6ab] = (byte)_inventory.MaxHealthQuarters;
            rom[0xc69f] = (byte)(outcome == 2 ? 0x80 : 0);
            rom[0xd004] = 1;
            rom[0xd024] = 0x80;
            rom[0xd029] = 1;
            rom[0xd01b] = 8;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                // A solid perimeter exercises recoil into walls and corner adjustment.
                byte collision = (byte)(x is 3 or 6 || y is 2 or 5 ? 0x0f : 0);
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, collision, 0);
                rom[0xcf00 + y * 16 + x] = 0x2c;
                rom[0xce00 + y * 16 + x] = collision;
            }
            Vector2 start = new(80.5f, 64.25f);
            _player.WarpTo(start);
            rom.Word(0xd00a, (int)(start.Y * 256));
            rom.Word(0xd00c, (int)(start.X * 256));
            void Hit()
            {
                FailIf(!_player.ApplyEnemyContactDamage(_player.Position - OracleObjectMovement.Shared.Direction(angle) * 32, 4),
                    "Shared contact hit was unexpectedly rejected.");
                rom[0xd025] = 0xf8;
                rom[0xd02a] = 0x80;
                rom[0xd02b] = 0x22;
                rom[0xd02c] = (byte)angle;
                rom[0xd02d] = 0x0f;
                rom.Call(LinkCollisionRom.DamageRings, bank: 6);
                rom.Call(LinkCollisionRom.ApplyDamage, bank: 6);
                rom[0xd02a] = 0;
            }
            Hit();
            int update = 0;
            void Compare()
            {
                if (rom[0xcdd5] != 0)
                {
                    rom[0xd004] = 3;
                    rom.Call(LinkCollisionRom.Dying);
                }
                else
                {
                    rom.Call(LinkCollisionRom.Probe);
                    rom.Call(LinkCollisionRom.Knockback);
                }
                rom[0xcc00] = (byte)_entities.FrameCounter;
                rom.Call(LinkCollisionRom.Invincibility);
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected || _player.HealthQuarters != rom[0xc6aa] ||
                    _player.KnockbackFrames != rom[0xd02d] || _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                    _inventory.HasTreasure(TreasureId.Potion) != ((rom[0xc69f] & 0x80) != 0) ||
                    _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] != 0),
                    $"ROM shared hit ring=${ring:x2}, angle=${angle:x2}, outcome={outcome}, update={update}, batch={batched}: ROM XY={expected}, HP={rom[0xc6aa]}, recoil={rom[0xd02d]}, inv={rom[0xd02b]}, state/sub={rom[0xd004]}/{rom[0xd005]}; runtime XY={_player.PrecisePosition}, HP={_player.HealthQuarters}, recoil={_player.KnockbackFrames}, inv={_player.InvincibilityFrames}, death={_player.DeathAnimationActive}.");
                rom.Call(LinkCollisionRom.Vulnerable);
                bool vulnerable = (rom[0xc201] & 0x10) != 0;
                FailIf(_player.NativeObjectVulnerable != vulnerable, $"ROM hit eligibility differs at update {update}.");
                if (!vulnerable)
                    FailIf(_player.ApplyEnemyContactDamage(start, 2), "An ineligible repeated contact was accepted.");
                update++;
            }
            // Stop lethal traces just after the spin handoff; game-over UI is separate.
            StepGameplayUpdates(outcome == 1 ? 18 : 35, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (outcome != 1)
            {
                Hit();
                StepGameplayUpdates(8, Vector2.Zero, batched: batched, afterUpdate: Compare);
            }
        }
        GD.Print("Validated shared ROM hit eligibility, ring recoil, wall collisions, potion consumption and lethal spin handoff in individual/batched gameplay updates.");
    }
}
