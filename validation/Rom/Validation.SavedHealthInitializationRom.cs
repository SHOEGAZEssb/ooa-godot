using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSavedHealthInitializationRom()
    {
        var rom = new FrontendRom();
        // All original quarter-heart health byte values, including negative
        // underflow, at every legal maximum from three through fourteen hearts.
        for (int maximum = 0x0c; maximum <= 0x38; maximum += 4)
        for (int health = 0; health <= 0xff; health++)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wLinkHealth, (byte)health);
            save.WriteWramByte(WramAddress.wLinkMaxHealth, (byte)maximum);
            for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = save.ReadWramByte(address);
            rom.InitializeSavedGame();
            save.ResetHealthIfDepleted();
            FailIf(!rom.InitializationHandoff || save.ReadWramByte(WramAddress.wLinkHealth) != rom[0xc6aa] ||
                rom[0xcbe4] != rom[0xc6aa],
                $"Saved health initial=${health:x2}, max=${maximum:x2}: runtime=${save.ReadWramByte(WramAddress.wLinkHealth):x2}, native=${rom[0xc6aa]:x2}/display=${rom[0xcbe4]:x2}.");
        }
        // Also exercise the real menu's stopped thread and completed Continue,
        // rather than establishing the application handoff by direct mutation.
        int hostCase1 = 0;
        foreach (int maximum in new[] { 0x0c, 0x14, 0x20, 0x38 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.SetDeathRespawnPoint(1, 0xa7, 0, 3, 0x28, 0x48);
            save.WriteWramByte(WramAddress.wLinkMaxHealth, (byte)maximum);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.ApplyDamage(_inventory.HealthQuarters);
            GameSceneGraph previousScene = _scene;
            BeginGameOver();
            var menu = _inventoryMenu;
            var application = new ApplicationValidationFixture(this);
            application.Step(11, Vector2.Zero, batched: batched);
            application.Step(1, Vector2.Zero, ["attack"], ["attack"], batched);
            application.Step(29, Vector2.Zero, batched: batched);
            FailIf(_inventory.HealthQuarters != 0 || previousScene.IsQueuedForDeletion() || !menu.IsActive,
                "Game Over Continue initialized health before its $1e delay completed.");
            for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = save.ReadWramByte(address);
            rom.InitializeSavedGame();
            application.Step(1, Vector2.Zero, batched: batched);
            FailIf(_inventory.HealthQuarters != rom[0xc6aa] || _hud.HealthQuarters != rom[0xcbe4] ||
                _rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30] ||
                _player.Position != new Vector2(rom[0xd00d], rom[0xd00b]) ||
                _player.FacingVector != Vector2I.Left || !previousScene.IsQueuedForDeletion() ||
                menu.IsActive || _gameplayPause.IsLeased || _saveWriteRequests != 0,
                $"Game Over Continue max=${maximum:x2} did not restore native health/display/checkpoint and release the previous scene without saving.");
        }
        GD.Print("Validated clean-US initializeGame health for all $100 health bytes at every supported maximum, half-health rounding/minimum, displayed health, and delayed Game Over Continue checkpoint/application handoff in split/batched updates.");
    }
}
