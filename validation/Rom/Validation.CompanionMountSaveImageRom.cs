using System;
using System.IO;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCompanionMountSaveImageRom(CompanionRom companion)
    {
        // Execute fileManagement.saveFile independently after the actual mount
        // loop. Only its shared coordinate bytes are compared here; save menu
        // decisions and saved-game spawn initialization have separate scenarios.
        var native = new FrontendRom();
        for (int address = 0xc5b0; address < 0xcb00; address++)
            native[address] = _saveData.ReadWramByte(address);
        native[0xc638] = companion[0xc638];
        native[0xc639] = companion[0xc639];
        native[0xff9a] = 0;
        native.Call(0x09d8, 0);
        string directory = Path.Combine(Path.GetTempPath(), $"ooa-mount-save-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "mount.sav");
        Directory.CreateDirectory(directory);
        try
        {
            SaveResult result = OracleSaveStore.Save(_saveData, path);
            FailIf(!result.Success, $"Explicit companion fixture save failed: {result.ErrorMessage}");
            foreach (bool backup in new[] { false, true })
            {
                byte[] image = File.ReadAllBytes(backup ? path + ".bak" : path);
                FailIf(!OracleSaveData.TryDeserialize(image, out OracleSaveData? loaded),
                    "Companion mount image must pass the native signature/checksum contract.");
                foreach (int address in new[] { 0xc638, 0xc639 })
                    FailIf(loaded!.ReadWramByte(address) != native.SavedByte(0, address, backup),
                        $"Explicit {(backup ? "backup" : "primary")} save lost shared mount byte ${address:x4}.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
