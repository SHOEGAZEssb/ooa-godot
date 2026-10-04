using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateContinueInitializationRom()
    {
        int hostCase1 = 0;
        foreach (int companion in new[] { 0, 0x0b, 0x0c, 0x0d })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.SetDeathRespawnPoint(0, 0x45, 0, 2, 0x58, 0x50,
                new RememberedCompanion(companion, 1, 0xa7, 0x38, 0x48), 0xd0);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.ApplyDamage(_inventory.HealthQuarters);
            BeginGameOver();
            var application = new ApplicationValidationFixture(this);
            application.Step(11, Vector2.Zero, batched: batched);
            application.Step(1, Vector2.Zero, ["attack"], ["attack"], batched);
            application.Step(29, Vector2.Zero, batched: batched);
            var initialization = new FrontendRom();
            for (int address = 0xc5b0; address < 0xcb00; address++) initialization[address] = save.ReadWramByte(address);
            initialization.InitializeSavedGame();
            application.Step(1, Vector2.Zero, batched: batched);
            FailIf(_player.InvincibilityFrames != unchecked((sbyte)initialization[0xd02b]),
                $"Continue initial invincibility: runtime={_player.InvincibilityFrames}, ROM=${initialization[0xd02b]:x2}.");
            for (int address = 0xcc24; address <= 0xcc28; address++)
                FailIf(_runtimeState.ReadWramByte(address) != initialization[address],
                    $"Continue companion ${companion:x2}: restored ${address:x4} differs from native initializeGame.");
            var invincibility = new LinkCollisionRom();
            invincibility[0xd02b] = initialization[0xd02b];
            int update = 0;
            application.Step(119, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                invincibility.Call(LinkCollisionRom.Invincibility);
                FailIf(_player.InvincibilityFrames != unchecked((sbyte)invincibility[0xd02b]) ||
                    _player.ApplyEnemyContactDamage(_player.Position + new Vector2(8, 0), 1),
                    $"Continue companion ${companion:x2} update {++update}: invincibility expired early or differs from native counter.");
            });
            FailIf(_player.InvincibilityFrames != -1, "Continue initial protection did not retain its last update.");
            application.Step(1, Vector2.Zero, batched: batched);
            invincibility.Call(LinkCollisionRom.Invincibility);
            int health = _inventory.HealthQuarters;
            FailIf(invincibility[0xd02b] != 0 || _player.InvincibilityFrames != 0 ||
                !_player.ApplyEnemyContactDamage(_player.Position + new Vector2(8, 0), 1) ||
                _inventory.HealthQuarters != health || (_player.PendingContactDamageRaw & 0xff) != 0xfe,
                "Continue did not become vulnerable on original object update 120.");
            invincibility[0xc6aa] = (byte)health;
            invincibility[0xc6ab] = (byte)_inventory.MaxHealthQuarters;
            invincibility[0xc6cb] = (byte)_inventory.ActiveRing;
            invincibility[0xd029] = 1; invincibility[0xd025] = 0xfe;
            invincibility.Call(LinkCollisionRom.DamageRings, bank: 6);
            invincibility.Call(LinkCollisionRom.ApplyDamage, bank: 6);
            application.Step(1, Vector2.Zero, batched: batched);
            FailIf(_inventory.HealthQuarters != invincibility[0xc6aa] || _player.PendingContactDamageRaw != 0,
                "Continue's accepted contact did not commit original pending damage on the next Link update.");
            FailIf(_saveWriteRequests != 0, "Continue initialization/protection wrote a save.");
        }
        GD.Print("Validated clean-US Continue initializeGame remembered-companion bytes, $88 non-flashing protection, separate first object dispatch, all 120 counter updates and resumed damage in split/batched application updates.");
    }
}
