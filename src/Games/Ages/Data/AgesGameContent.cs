namespace Oracles;

// Default bindings for the supported clean-US Ages content; shared algorithms
// receive its records without owning another copy of game state.
internal static class AgesGameContent
{
    internal const string AssetRoot = "res://assets/oracle/ages/";
    internal const string SavePath = "user://oracle_of_ages.sav";
    internal const string PresentationPath = "user://presentation.cfg";

    internal const int MamamuDogLocationAddress = 0xcde2;
    internal const int ArmosTriggerAddress = AgesWramAddress.wcca2;

    internal static string SaveSlotPath(int slot) =>
        slot == 0 ? SavePath : $"user://oracle_of_ages_{slot + 1}.sav";

    internal static OracleRuntimeState CreateRuntimeState()
    {
        // initializeSeedTreeRefillData: locations $04-$0f begin refilled.
        var state = new OracleRuntimeState();
        state.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0xf0);
        state.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset + 1, 0xff);
        return state;
    }

    // include/wram.s: wToggleBlocksState
    internal const int ToggleBlocksStateAddress = 0xcdd2;
    // include/wram.s: wSwitchState
    internal const int SwitchStateAddress = 0xcdd3;
    // include/wram.s: wSpinnerState
    internal const int SpinnerStateAddress = 0xcdd4;
}
