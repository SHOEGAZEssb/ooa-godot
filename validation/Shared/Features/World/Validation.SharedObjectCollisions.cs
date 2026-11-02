
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedObjectCollisionDataRom()
    {
        int effects = 0, masks = 0;
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var data = ObjectCollisionDatabase.For(game == OracleGame.Ages ? AgesGameContent.AssetRoot : SeasonsGameContent.AssetRoot);
            var rom = ValidationRom.LoadCleanUs(game).Span;
            var native = new ObjectCollisionRom(game);
            int modes = game == OracleGame.Ages ? 0x7d : 0x73, parts = game == OracleGame.Ages ? 0x5a : 0x54;
            int table = game == OracleGame.Ages ? 0x6d0a : 0x6a90;
            int enemies = game == OracleGame.Ages ? 0x69a2 : 0x6740;
            int partMasks = game == OracleGame.Ages ? 0x6ba2 : 0x6940;
            int enemyData = 0x3f * 0x4000 + (game == OracleGame.Ages ? 0x1d4b : 0x1d71);
            int items = 7 * 0x4000 + (game == OracleGame.Ages ? 0x25b3 : 0x2351);
            FailIf(data.ModeCount != modes || data.PartCount != parts, $"{game}: native collision extents differ.");
            for (int id = 0; id < 0x80; id++)
                FailIf(data.EnemyMode(id) != (rom[enemyData + 4 * id + 1] & 0x7f), $"{game}: ENEMY ${id:x2} native initial collision mode.");
            for (int id = 0; id < 0x2c; id++)
                FailIf(data.ItemType(id) != (rom[items + 4 * id] & 0x7f), $"{game}: ITEM ${id:x2} native initial collision type.");
            FailIf(data.ItemType(0x03) != (game == OracleGame.Ages ? 0x18 : 0x17) ||
                data.ItemType(0x06) != (game == OracleGame.Ages ? 0x17 : 0x15),
                $"{game}: Bomb and Boomerang must use selected-game item columns.");
            for (int mode = 0; mode < modes; mode++)
            for (int item = 0; item < 0x20; item++)
            {
                int expected = native.Table(table + 32 * mode + item);
                FailIf(data.Effect(mode,item) != expected || data.Effect(mode | 0x80,item) != expected,
                    $"{game}: native collision mode ${mode:x2}, item ${item:x2} lost its effect or enabled-bit masking.");
                effects++;
            }
            foreach (bool part in new[] { false,true })
            for (int id = 0; id < (part ? parts : 0x80); id++)
            for (int item = 0; item < 0x20; item++)
            {
                bool expected = (native.Table((part ? partMasks : enemies) + id * 4 + item / 8) & (1 << (item & 7))) != 0;
                bool Enabled(int type) => part ? data.PartEnabled(type,item) : data.EnemyEnabled(type,item);
                FailIf(Enabled(id) != expected || Enabled(id | 0x80) != expected,
                    $"{game}: {(part ? "PART" : "ENEMY")} ${id:x2}, item ${item:x2} native mask.");
                // Execute the original scan, including type/enable, pending hit,
                // invincibility, item-mask and XY/Z gates; the native effect is $00.
                for (int gate = 0; gate < 5; gate++)
                {
                    native.ClearObjects();
                    int target = part ? 0xd0c0 : 0xd080;
                    native[target + 0x24] = (byte)(id | (gate == 1 ? 0 : 0x80));
                    native[target + 0x2a] = (byte)(gate == 2 ? 0x80 : 0);
                    native[target + 0x2b] = (byte)(gate == 3 ? 1 : gate == 4 ? 0xff : 0);
                    native[target + 0x0b] = native[0xd70b] = 64;
                    native[target + 0x0d] = native[0xd70d] = 80;
                    native[target + 0x26] = native[target + 0x27] = native[0xd726] = native[0xd727] = 6;
                    native[0xd724] = (byte)(0x80 | item);
                    native.Call(native.ScanAddress);
                    FailIf(native.Dispatches.Count != (gate == 0 && expected ? 1 : 0),
                        $"{game}: {(part ? "PART" : "ENEMY")} ${id:x2}, item ${item:x2}, gate {gate} native scan.");
                    masks++;
                }
            }
            bool missingMode = false, missingPart = false;
            try { data.Effect(modes,0); }
            catch (NotSupportedException error) { missingMode = error.Message.Contains($"${modes:x2}") && error.Message.Contains(data.AssetRoot); }
            try { data.PartEnabled(parts,0); }
            catch (NotSupportedException error) { missingPart = error.Message.Contains($"${parts:x2}") && error.Message.Contains(data.AssetRoot); }
            FailIf(!missingMode || !missingPart, $"{game}: absent native collision data must report its pack and hexadecimal identity.");
        }
        GD.Print($"Validated {effects} clean-US collision effects and {masks} executed target/item mask scans across both games.");
    }
}
