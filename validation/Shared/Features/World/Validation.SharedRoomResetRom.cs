namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedRoomResetRom()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        foreach (bool reload in new[] { false, true })
        foreach (byte linkPage in new byte[] { 0xd0, 0xd1, 0xde })
        {
            var memory = OracleRuntimeState.CreateInitialized(game);
            var native = new LinkCollisionRom(game: game);
            for (int address = 0xcc00; address < 0xcd00; address++)
            {
                byte value = (byte)(0x80 | (address & 0x7f));
                memory.SetWramByte(address, value); native[address] = value;
            }
            bool ages = game == OracleGame.Ages;
            int index = ages ? 0xcc2c : 0xcc48;
            memory.SetWramByte(index, linkPage); native[index] = linkPage;
            if (reload) memory.ClearRoomReloadGlobals(); else memory.ClearScrollEntryGlobals();
            // Independently located clean-US bank1 clearMemoryOnScreenReload /
            // func_49c9. The native call also clears ITEM slots on full reload;
            // this comparison isolates globals and the Link/camera tail.
            native.Call(ages ? reload ? 0x49af : 0x49c9 : reload ? 0x493c : 0x4956, bank: 1);
            for (int address = 0xcc00; address < 0xcd00; address++)
                FailIf(memory.ReadWramByte(address) != native[address],
                    $"{game}, reload={reload}, Link=${linkPage:x2}: global byte${address:x4} differs from the original reset span/tail.");
            if (reload)
                foreach (int address in new[] { 0xcd17, 0xcd18 })
                    FailIf(memory.ReadWramByte(address) != native[address],
                        $"{game}: native full reload must focus the selected Link page and clear its camera type.");
        }
    }
}
