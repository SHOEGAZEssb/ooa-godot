using System;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateSaveIntegrityRom()
    {
        byte[] first = OracleSaveData.CreateStandardGame().Serialize();
        var otherSave = OracleSaveData.CreateStandardGame();
        otherSave.SetLinkName("OTHER");
        byte[] second = otherSave.Serialize();
        var rom = new FrontendRom();
        void Copies(int slot, byte[] primary, byte[] backup)
        {
            for (int offset = 0; offset < OracleSaveData.FileSize; offset++)
            {
                rom.SetSavedByte(slot, 0xc5b0 + offset, primary[offset]);
                rom.SetSavedByte(slot, 0xc5b0 + offset, backup[offset], backup: true);
            }
        }
        void CheckNativeCopy(int slot, bool backup, byte[] expected, string context)
        {
            for (int offset = 0; offset < expected.Length; offset++)
                FailIf(rom.SavedByte(slot, 0xc5b0 + offset, backup) != expected[offset],
                    context + $": repaired {(backup ? "backup" : "primary")} ${0xc5b0 + offset:x4} differs.");
        }
        void CheckNativeLive(byte[] expected, string context)
        {
            for (int offset = 0; offset < expected.Length; offset++)
                FailIf(rom[0xc5b0 + offset] != expected[offset],
                    context + $": loaded WRAM ${0xc5b0 + offset:x4} differs.");
        }
        // The source checksum adds little-endian words, excludes its own two
        // bytes, and retains a 16-bit sum. Independently re-sign bad signatures
        // so they reach the signature gate rather than failing the checksum.
        static void Sign(byte[] bytes)
        {
            int checksum = 0;
            for (int offset = 2; offset < bytes.Length; offset += 2)
                checksum = (checksum + bytes[offset] + (bytes[offset + 1] << 8)) & 0xffff;
            bytes[0] = (byte)checksum;
            bytes[1] = (byte)(checksum >> 8);
        }
        foreach (int slot in new[] { 0, 1, 2 })
        {
            // Every byte is part of the native verification contract. Rotate
            // bit masks to include both word halves and signature/checksum bytes.
            for (int offset = 0; offset < OracleSaveData.FileSize; offset++)
            {
                byte[] broken = (byte[])first.Clone();
                broken[offset] ^= (byte)(1 << (offset & 7));
                FailIf(OracleSaveData.TryDeserialize(broken, out _),
                    $"Save integrity slot ${slot:x2}: mutation ${0xc5b0 + offset:x4} was accepted.");
                Copies(slot, broken, second);
                rom.LoadFile(slot);
                CheckNativeLive(second, $"Corrupt primary slot ${slot:x2} offset ${offset:x3}");
                CheckNativeCopy(slot, false, second, "Native primary recovery");
            }
            for (int character = 0; character < 8; character++)
            {
                byte[] brokenSignature = (byte[])first.Clone();
                brokenSignature[2 + character] ^= 0x80;
                Sign(brokenSignature);
                FailIf(OracleSaveData.TryDeserialize(brokenSignature, out _),
                    $"Save signature slot ${slot:x2} character ${character} was accepted despite a valid checksum.");
                Copies(slot, brokenSignature, second);
                rom.LoadFile(slot);
                CheckNativeLive(second, "Native signature rejection");
            }
            // A checksum collision remains valid in the original; validation
            // must not invent semantic rejection for an otherwise intact image.
            byte[] collision = (byte[])first.Clone();
            collision[0x54c]++;
            collision[0x54e]--;
            collision[0x54f]--; // Borrow: $0000 - 1 is $ffff, not $00ff.
            FailIf(!OracleSaveData.TryDeserialize(collision, out _), "The native additive checksum collision was rejected.");
            Copies(slot, collision, second);
            rom.LoadFile(slot);
            CheckNativeLive(collision, "Valid checksum collision");
            CheckNativeCopy(slot, true, second, "Both valid copies must retain the backup");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"ooa-save-rom-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            byte[] corrupt = (byte[])first.Clone();
            corrupt[^1] ^= 0x80;
            foreach (int slot in new[] { 0, 1, 2 })
            foreach (int mask in new[] { 0, 1, 2, 3 })
            {
                bool primaryValid = (mask & 1) != 0;
                bool backupValid = (mask & 2) != 0;
                byte[] primary = primaryValid ? first : corrupt;
                byte[] backup = backupValid ? second : corrupt;
                Copies(slot, primary, backup);
                rom.LoadFile(slot);
                byte[] nativeResult = primaryValid ? first : backupValid ? second : new byte[OracleSaveData.FileSize];
                CheckNativeLive(nativeResult, $"Recovery mask ${mask:x2}");
                CheckNativeCopy(slot, false, nativeResult, "Recovery primary");
                CheckNativeCopy(slot, true, primaryValid && backupValid ? second : nativeResult, "Recovery backup");

                string path = Path.Combine(directory, $"slot-{slot}.sav");
                File.WriteAllBytes(path, primary);
                File.WriteAllBytes(path + ".bak", backup);
                OracleSaveData recovered = OracleSaveStore.LoadOrCreate(path);
                if (primaryValid || backupValid)
                    FailIf(!recovered.Serialize().SequenceEqual(nativeResult),
                        $"Disk recovery slot ${slot:x2} mask ${mask:x2} selected a different native image.");
                else
                {
                    FailIf(OracleSaveData.TryDeserialize(nativeResult, out _) ||
                        !recovered.Serialize().SequenceEqual(first),
                        "Both invalid copies did not yield native rejection and the documented port new-save fallback.");
                }
                // The documented filesystem policy keeps generations and repairs
                // only on explicit saves; native SRAM verification repairs now.
                FailIf(!File.ReadAllBytes(path).SequenceEqual(primary) ||
                    !File.ReadAllBytes(path + ".bak").SequenceEqual(backup),
                    "Reading a disk generation unexpectedly persisted SRAM-style repairs.");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
        Console.WriteLine("Validated clean-US save integrity at every $550-byte offset in all three slots, correctly signed invalid signatures, checksum collisions, four copy-validity combinations, native repair bytes and disk recovery selection; filesystem generation/repair policy remains the documented port contract.");
    }
}
