
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomInitializationPreambleRom()
    {
        ReinitializeGameplayForValidation();
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
        foreach (byte flags in new byte[] { 1, 5, 9, 13 })
        foreach (int remembered in new[] { 0, 0x0b, 0x0c, 0x0d, -1, -2 })
        foreach (byte previousMaple in new byte[] { 0, 0x5a })
        foreach (int kills in new[] { 29, 30 })
        {
            _entities.Clear();
            for (int address = 0xd100; address < 0xd140; address++) _runtimeState.SetWramByte(address, 0);
            _runtimeState.SetWramByte(0xd10a, 0x40);
            _runtimeState.SetWramByte(0xd10c, 0xc0);
            _runtimeState.SetWramByte(0xd10e, 0x20);
            if (remembered == -2)
            {
                _runtimeState.SetWramByte(0xd100, 1);
                _runtimeState.SetWramByte(0xd101, 0x0b);
                _runtimeState.SetWramByte(0xd10b, 48);
                _runtimeState.SetWramByte(0xd10d, 104);
            }
            CompanionRuntimeState.Remember(_runtimeState, remembered < 0 ? 0x0b : remembered,
                0, remembered < 0 ? 2 : 1, new Vector2(72, 56));
            _runtimeState.SetField("wcc05", flags);
            _runtimeState.SetField("wcc85", 0);
            _runtimeState.SetField("wIsMaplePresent", previousMaple);
            _saveData.SetMapleKillCounter(kills);
            var room = _rooms.Load(0, 1);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, room, 2, 80, 80);
            native[0xcc2e] = 0;
            native[0xcc05] = flags; native[0xcdda] = previousMaple;
            for (int address = 0xd100; address < 0xd140; address++) native[address] = _runtimeState.ReadWramByte(address);
            for (int address = 0xcc24; address <= 0xcc28; address++) native[address] = _runtimeState.ReadWramByte(address);
            native.InitializeRoomInteractions(); // Original initializeRoom, including its native gate branches and parseObjectData.
            _entities.LoadRoom(0, room);
            bool companion = remembered == -2 || (flags & 4) != 0 && remembered > 0;
            bool maple = (flags & 8) != 0 && !companion && kills == 30;
            string context = $"initializeRoom flags${flags:x2}/remembered{remembered:x2}/previousMaple${previousMaple:x2}/kills{kills}";
            for (int address = 0xd100; address < 0xd140; address++)
                FailIf(_runtimeState.ReadWramByte(address) != native[address],
                    $"{context}: pre-state-zero SPECIALOBJECT byte ${address:x4} differs: port=${_runtimeState.ReadWramByte(address):x2}, ROM=${native[address]:x2}.");
            int expectedMaple = (flags & 8) == 0 ? previousMaple : maple ? 1 : 0;
            FailIf(_runtimeState.ReadField("wIsMaplePresent") != expectedMaple || native[0xcdda] != expectedMaple ||
                _runtimeState.ReadField("wcc85") != native[0xcc85] || native[0xcc85] != (maple ? 1 : 0) ||
                _saveData.MapleKillCounter != native[0xc641] || _saveData.MapleKillCounter != (maple ? 0 : kills) ||
                _entities.MaplePresent() != maple ||
                companion && (_runtimeState.ReadWramByte(0xd100) != 1 || _runtimeState.ReadWramByte(0xd101) != (remembered == -2 ? 0x0b : remembered)),
                context + ": remembered companion must precede Maple, and a disabled check must preserve its prior flag and kill counter.");
            var parsed = _random.CaptureState();
            FailIf(parsed.Calls - seed.Calls != 256 || native.RandomCalls != 256 ||
                parsed.Rng1 != native[0xff94] || parsed.Rng2 != native[0xff95] ||
                !Enumerable.Range(0, 256).All(index => parsed.PlacementBuffer[index] == native.FloorPermutation(index)),
                context + ": initialization must not consume state-zero RNG before parseObjectData generates its exact shared placement buffer.");
        }
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var policy = NativeObjectStreamDatabase.For(OracleAssetPack.ForGame(game));
            var memory = OracleRuntimeState.CreateInitialized(game);
            for (int flags = 0; flags <= 0xff; flags++)
            {
                if (game == OracleGame.Ages) memory.SetField("wcc05", (byte)flags);
                FailIf(policy.RememberedCompanionAllowed(memory) != (game == OracleGame.Seasons || (flags & 4) != 0) ||
                    policy.MapleAllowed(memory) != (game == OracleGame.Seasons || (flags & 8) != 0) ||
                    policy.RootAllowed(memory) != (game == OracleGame.Seasons || (flags & 1) != 0),
                    $"{game}: initializeRoom must use its source dispatch masks for flags${flags:x2}.");
            }
        }
        GD.Print("Validated native initializeRoom companion/Maple gates, preserved disabled-check flags, native SPECIALOBJECT bytes before state zero, and exact parse RNG/permutation; both packs retain their source gate policies.");
    }
}
