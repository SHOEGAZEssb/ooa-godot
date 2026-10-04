using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShopCheckpointInitializationRom()
    {
        var rom = new FrontendRom();
        foreach (int hardware in new[] { 0, 1, 0x7f, 0x80, 0x81, 0xff })
        foreach (int group in new[] { 0, 1, 2, 3, 4, 7 })
        foreach (int room in new[] { 0x57, 0x58, 0xfd, 0xfe, 0xff })
        foreach (int x in group == 3 && room == 0xfe || group == 1 && room == 0x58
            ? Enumerable.Range(0, 256) : new[] { 0, 0x3f, 0x40, 0x41, 0x58, 0xff })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetDeathRespawnPoint(group, room, 0xa5, 1, 0x38, x,
                new RememberedCompanion(0x0c, 1, 0xa7, 0x28, 0x48), 0xd1);
            for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = save.ReadWramByte(address);
            rom[0xff96] = (byte)hardware;
            rom.InitializeSavedGame();
            int notifications = 0;
            save.Changed += () => notifications++;
            DeathRespawnInitialization.Apply(save, (byte)hardware);
            for (int address = 0xc62b; address <= 0xc637; address++)
                FailIf(save.ReadWramByte(address) != rom[address],
                    $"Shop checkpoint hardware=${hardware:x2}, room=${group:x2}:${room:x2}, X=${x:x2}: ${address:x4} differs from native masked repair.");
            bool repaired = hardware < 0x80 && (group == 3 && room == 0xfe || group == 1 && room == 0x58 && x >= 0x40);
            FailIf(save.RespawnGroup != (repaired ? 1 : group) || save.RespawnRoom != (repaired ? 0x58 : room) ||
                save.RespawnY != (repaired ? 0x48 : 0x38) || save.RespawnX != (repaired ? 0x58 : x) ||
                save.ReadWramByte(0xc62d) != 0xa5 || save.ReadWramByte(0xc631) != 0x0c,
                "Shop correction lost its independently traced bounds, destination or preserved modifier/companion fields.");
            FailIf(notifications != (repaired ? 1 : 0), "Checkpoint repair did not publish exactly one completed save mutation.");
            DeathRespawnInitialization.Apply(save, (byte)hardware);
            FailIf(notifications != (repaired ? 1 : 0), "Repeated checkpoint initialization republished unchanged state.");
        }
        int hostCase1 = 0;
        foreach (bool interior in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link"); save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.SetDeathRespawnPoint(interior ? 3 : 1, interior ? 0xfe : 0x58, 0, 1, 0x38, 0x40);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.ApplyDamage(_inventory.HealthQuarters);
            BeginGameOver();
            var application = new ApplicationValidationFixture(this);
            application.Step(11, Vector2.Zero, batched: batched);
            application.Step(1, Vector2.Zero, ["attack"], ["attack"], batched);
            application.Step(29, Vector2.Zero, batched: batched);
            for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = save.ReadWramByte(address);
            rom[0xff96] = 1; rom.InitializeSavedGame();
            application.Step(1, Vector2.Zero, batched: batched);
            FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30] ||
                _player.Position != new Vector2(rom[0xd00d], rom[0xd00b]) ||
                _player.FacingVector != Vector2I.Down || _saveWriteRequests != 0,
                "Game Over Continue did not apply the native CGB shop checkpoint repair before loading its room, without committing a save.");
        }
        GD.Print("Validated clean-US shop checkpoint correction across hardware-bit gates, source-room aliases and X=$3f/$40 boundary, exact masked save bytes/preservation, plus split/batched Game Over Continue room initialization.");
    }
}
