using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkDamageRom()
    {
        int cases = 0;
        var accumulator = typeof(Player).GetField("_damageAccumulator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (int ring = 0; ring < 64; ring++)
        foreach (int quarters in new[] { 1, 2, 3, 4, 5, 7, 8, 12, 16, 31, 32, 63, 64 })
        foreach (int initialHealth in new[] { 1, 80 })
        foreach (bool potion in new[] { false, true })
        {
            if (initialHealth == 80 && potion) continue;
            OracleSaveData save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wRingBoxLevel, 1);
            save.WriteWramByte(WramAddress.wRingBoxContents, (byte)ring);
            save.WriteWramByte(WramAddress.wLinkHealth, (byte)initialHealth);
            save.WriteWramByte(WramAddress.wLinkMaxHealth, 80);
            var inventory = new InventoryState(_treasures, save);
            if (potion) inventory.GiveTreasure(TreasureId.Potion, 0);
            FailIf(!inventory.EquipRingAt(0), "ROM damage fixture could not equip its ring.");
            var player = new Player();
            player.Initialize(new ValidationRingPlayerWorld(), inventory, new(80, 64), new OracleRandom());
            var rom = new LinkCollisionRom();
            rom[0xc6cb] = (byte)ring;
            rom[0xc6aa] = (byte)initialHealth;
            rom[0xc6ab] = 80;
            rom[0xc69f] = (byte)(potion ? 0x80 : 0);
            rom[0xd029] = 1; // Link state00 initializes the fractional damage accumulator.
            rom[0xd004] = 1;
            for (int hit = 0; hit < 4; hit++)
            {
                rom[0xd025] = unchecked((byte)(-2 * quarters));
                rom.Call(LinkCollisionRom.DamageRings, bank: 6);
                rom.Call(LinkCollisionRom.ApplyDamage, bank: 6);
                player.ApplyDamage(quarters);
                FailIf(player.HealthQuarters != rom[0xc6aa] || player.IsDying != (rom[0xcdd5] != 0) ||
                    (int)accumulator.GetValue(player)! != rom[0xd029] ||
                    inventory.HasTreasure(TreasureId.Potion) != ((rom[0xc69f] & 0x80) != 0),
                    $"ROM damage ring=${ring:x2}, quarters={quarters}, hit={hit}: ROM health={rom[0xc6aa]}, fraction=${rom[0xd029]:x2}; runtime health={player.HealthQuarters}, dying={player.IsDying}.");
                cases++;
                if (player.IsDying) break;
                // A refill changes real health, not the fractional damage byte.
                inventory.RefillHealth();
                rom[0xc6aa] = 80;
            }
            player.Free();
        }
        GD.Print($"Validated {cases} ROM shared damage/ring arithmetic and repeated-hit cases.");
        foreach (int boundary in new[] { 0, 1, 2, 3 })
        {
            var player = new Player();
            var inventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
            player.Initialize(new ValidationRingPlayerWorld(), inventory, new(80, 64), new OracleRandom());
            try
            {
                int health = player.HealthQuarters;
                FailIf(!player.ApplyEnemyContactDamage(new(88, 64), 2), "Reset fixture did not publish a contact.");
                var rom = new LinkCollisionRom();
                rom[0xd025] = 0xfc; rom[0xd02a] = 0x80;
                switch (boundary)
                {
                    case 0:
                        player.ResetPortalDamageState();
                        rom.Call(0x2ba9, bank: 0); // resetLinkInvincibility.
                        break;
                    case 1:
                        player.ResetDeathAnimationForRoomLoad();
                        rom.Call(0x35ba, bank: 0); // clearLinkObject; loadingRoom clears WRAM bank1.
                        break;
                    case 2:
                        player.WarpTo(new(88, 64));
                        rom.Call(0x35ba, bank: 0); // Full replacement, not coordinate-only scrolling.
                        break;
                    case 3:
                        player.BeginScrollingTransition(new(88, 64));
                        player.FinishScrollingTransition(new(96, 64));
                        break; // Native transition adjusts coordinate bytes; it does not clear Link.
                }
                FailIf((player.PendingContactDamageRaw & 0xff) != rom[0xd025] ||
                    player.NativeContactSignal != (rom[0xd02a] != 0) || player.HealthQuarters != health,
                    $"Contact reset boundary {boundary} differs from native pending damage/signal lifetime.");
            }
            finally { player.Free(); }
        }
    }
}
