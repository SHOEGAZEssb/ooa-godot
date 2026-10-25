using Godot;
using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGameContentBoundaries()
    {
        FailIf(OracleAssetPack.RootFor("res://assets/oracle/ages/menu/a.tsv") != AgesGameContent.AssetRoot ||
            OracleAssetPack.RootFor("res://assets/oracle/seasons/preview/din_oam.tsv") != SeasonsPreviewContent.AssetRoot,
            "Generated asset lookup did not retain the game's manifest scope.");
        foreach (string invalid in new[] { "res://assets/oracle/unscoped.tsv", "res://assets/oracle/ages/../seasons/a.tsv" })
        {
            bool rejected = false;
            try { _ = OracleAssetPack.RootFor(invalid); }
            catch (ArgumentException) { rejected = true; }
            FailIf(!rejected, $"Generated asset boundary accepted '{invalid}'.");
        }

        string[] agesImages = OracleAssetCache.Preload(AgesGameContent.AssetRoot, CancellationToken.None);
        FailIf(agesImages.Length == 0 || agesImages.Any(path => !path.StartsWith(AgesGameContent.AssetRoot, StringComparison.Ordinal)),
            "Ages preload crossed into another game's image pack.");
        FailIf(GeneratedTableManifest.EntryCount(AgesGameContent.AssetRoot) == 0 ||
            GeneratedTableManifest.EntryCount(SeasonsPreviewContent.AssetRoot) != 1,
            "The Seasons preview must retain its own single OAM table manifest.");
        var oam = GeneratedTable.Load(SeasonsPreviewContent.PreviewRoot + "din_oam.tsv",
            new GeneratedTableSchema("Din source OAM", GeneratedTableKeySemantics.Ordered,
                ["y", "x", "tile", "flags"], headerRequired: true));
        // code/seasons/cutscenes/introCutscenes.s: seasonsOamData_03_7397.
        FailIf(oam.Rows.Count != 0x24, "Din's source close-up must retain $24 OAM sprites.");

        var dialogue = new DialogueBox();
        try
        {
            dialogue.PrepareResources(AgesGameContent.AssetRoot);
            bool rejected = false;
            try { dialogue.PrepareResources(SeasonsPreviewContent.AssetRoot); }
            catch (InvalidOperationException) { rejected = true; }
            FailIf(!rejected, "Prepared dialogue reused Ages textures for another game pack.");
        }
        finally { dialogue.Free(); }

        FailIf(OracleSaveStore.PathForSlot(0) != "user://oracle_of_ages.sav" ||
            OracleSaveStore.PathForSlot(1) != "user://oracle_of_ages_2.sav" ||
            OracleSaveStore.PathForSlot(2) != "user://oracle_of_ages_3.sav",
            "Separating Ages bindings changed existing save locations.");
        byte[] save = OracleSaveData.CreateStandardGame().Serialize();
        FailIf(save.Length != 0x550 || Encoding.ASCII.GetString(save, 2, 8) != "Z21216-0" ||
            !OracleSaveData.TryDeserialize(save, out _),
            "Separating Ages bindings changed the original save image/signature.");
        var empty = new OracleRuntimeState();
        var ages = AgesGameContent.CreateRuntimeState();
        FailIf(empty.ReadWramByte(0xcc4d) != 0 || empty.ReadWramByte(0xcc4e) != 0 ||
            ages.ReadWramByte(0xcc4d) != 0xf0 || ages.ReadWramByte(0xcc4e) != 0xff,
            "Ages seed-tree initialization leaked into the shared memory mechanism or lost its source bytes.");
    }
}
