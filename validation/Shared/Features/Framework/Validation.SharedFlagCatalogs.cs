
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedFlagCatalogs()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var flags = new GlobalFlagDatabase(OracleAssetPack.ForGame(game));
            // constants/common/globalFlags.s: shared prefix, then distinct
            // native enum branches. Names select semantics, never an Ages ID.
            int moblin = game == OracleGame.Ages ? 0x1a : 0x16;
            int finished = game == OracleGame.Ages ? 0x14 : 0x28;
            FailIf(flags.GetId("GLOBALFLAG_INTRO_DONE") != 0x0a ||
                flags.GetId("GLOBALFLAG_MOBLINS_KEEP_DESTROYED") != moblin ||
                flags.GetId("GLOBALFLAG_FINISHEDGAME") != finished ||
                flags.GetName(moblin) != "GLOBALFLAG_MOBLINS_KEEP_DESTROYED" ||
                flags.GetShortName(0x0a) != "INTRO_DONE" || flags.GetName(0x7f) != "GLOBALFLAG_7f",
                $"{game}: shared flag tools and native name bindings must select the correct source enum.");
            if (game == OracleGame.Seasons)
                FailIf(flags.GetId("GLOBALFLAG_GNARLED_KEY_GIVEN") != 0x18 ||
                    flags.GetId("GLOBALFLAG_PIRATE_SHIP_DOCKED") != 0x17 ||
                    flags.GetId("GLOBALFLAG_PREGAME_INTRO_DONE") != 0x21,
                    "Seasons map and intro bindings must retain its native global flag names and indices.");
            bool rejected = false;
            try { flags.GetId(game == OracleGame.Ages ? "GLOBALFLAG_GNARLED_KEY_GIVEN" : "GLOBALFLAG_SAVED_NAYRU"); }
            catch (NotSupportedException) { rejected = true; }
            FailIf(!rejected, "A flag name absent from the selected game must fail instead of aliasing another game's flag.");
        }
    }
}
