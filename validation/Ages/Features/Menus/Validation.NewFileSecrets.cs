using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNewFileSecretCodecRom()
    {
        var codec = new LinkedGameNpcDatabase();
        byte[] glyphs = new SecretEntryDatabase().Glyphs;
        int cases = 0;
        void Compare(byte[] symbols, string context)
        {
            var rom = new FrontendRom();
            rom.InitializeMenu();
            // Bounded fileSelectMode6 commit, with the destination SRAM empty.
            for (int index = 0; index < 32; index++)
                rom.SetBankByte(4, 0xd7c0 + index, index < symbols.Length && symbols[index] < 64 ? glyphs[symbols[index]] : (byte)0x20);
            rom[0xcbb3] = 6; rom[0xcbb4] = 2;
            rom.UpdateMenu(0, 0);
            bool accepted = codec.TryCreateGameFromSecret(symbols, out var save);
            FailIf(accepted != (rom[0xcbb3] == 1), $"Game secret {context}: acceptance differs from fileSelectMode6.");
            byte[]? payload = save?.Serialize();
            for (int index = 0; index < OracleSaveData.FileSize; index++)
            foreach (bool backup in new[] { false, true })
                FailIf((accepted ? payload![index] : 0) != rom.SavedByte(0, 0xc5b0 + index, backup),
                    $"Game secret {context}: {(backup ? "backup" : "primary")} byte ${0xc5b0 + index:x4} differs from initializeFile.");
            cases++;
        }
        foreach (int cipher in Enumerable.Range(0, 8))
        foreach (int variant in new[] { 0, 1, 2 })
        foreach (int child in new[] { 0, 1, 5, 6, 10, 11, 63 })
        {
            var source = OracleSaveData.CreateStandardGame();
            source.SetLinkName("LinkZ"); source.NameChild("Pip");
            source.WriteWramByte(0xc600, (byte)(8 + ((cipher + 6) & 7)));
            source.WriteWramByte(0xc601, 0x12);
            source.WriteWramByte(AgesWramAddress.wFileIsLinkedGame, (byte)(variant == 1 ? 1 : 0));
            source.WriteWramByte(AgesWramAddress.wFileIsHeroGame, (byte)(variant == 2 ? 1 : 0));
            source.WriteWramByte(AgesWramAddress.wWhichGame, (byte)(cipher & 1));
            source.WriteWramByte(AgesWramAddress.wChildStatus, (byte)child);
            source.WriteWramByte(AgesWramAddress.wAnimalCompanion, (byte)(0x0b + cipher % 3));
            source.WriteWramByte(0xc615, (byte)(cipher & 1));
            source.WriteWramByte(AgesWramAddress.wLinkName + 5, 3); // initializeFile must replace the secret's terminator.
            byte[] symbols = codec.GenerateGameTransferSecretValues(source);
            Compare(symbols, $"cipher ${cipher:x2}, variant {variant}, child ${child:x2}");
            if (child == 0)
            {
                byte[] damaged = (byte[])symbols.Clone(); damaged[^1] ^= 1;
                Compare(damaged, "invalid checksum");
                byte[] wrongType = (byte[])symbols.Clone();
                int offset = cipher * 4;
                for (int index = 0; index < 20; index++) wrongType[index] ^= (byte)(codec.XorCipher[offset + index] & (index == 0 ? 7 : 63));
                wrongType[0] ^= 4; // Type 1 has the same length and body as type 0.
                wrongType[^1] &= 0x30;
                wrongType[^1] |= (byte)(wrongType.Sum(value => value) & 15);
                for (int index = 0; index < 20; index++) wrongType[index] ^= (byte)(codec.XorCipher[offset + index] & (index == 0 ? 7 : 63));
                Compare(wrongType, "wrong secret type with valid checksum");
            }
        }
        Compare(Enumerable.Repeat((byte)0xff, 20).ToArray(), "empty input");
        FailIf(codec.TryCreateGameFromSecret(new byte[5], out _) || codec.TryCreateGameFromSecret(new byte[15], out _),
            "New-file secret accepted an NPC or ring secret length.");
        FailIf(cases != 217, $"Expected 217 native new-file secret cases, got {cases}.");
        GD.Print("Validated 217 clean-ROM game-secret accept/reject and complete primary/backup save images across eight ciphers, linked/hero variants and child personality boundaries.");
    }

    private void ValidateNewFileSecretMenuRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int slot in new[] { 0, 1, 2 })
        {
            var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                root.Initialize(persistSaveData: true);
                root.Step(241, batched); root.Step(1, batched, "inventory");
                root.Step(1, batched); root.Step(1, batched, "inventory"); root.Step(32, batched);
                var application = new ApplicationValidationFixture(root);
                var rom = new FrontendRom(); rom.InitializeMenu();
                var sounds = root._sound.AttachPlayRequestAudit(); rom.Sounds.Clear();
                var screen = root._mainMenuScreen!; var menu = root._mainMenu!;
                var keyboard = new SecretEntryDatabase();
                int update = 0;
                void Step(int count = 1, int keys = 0, int? held = null)
                {
                    int edge = keys;
                    application.Step(count, Vector2.Zero, MenuRomActions(held ?? keys), MenuRomActions(keys), batched, () =>
                    {
                        rom.UpdateMenu(edge, held ?? keys); edge = 0; update++;
                        Page expected = rom[0xcbb3] switch
                        {
                            1 => rom[0xcbb4] < 2 ? Page.FileSelect : Page.TextSpeed,
                            5 => Page.NewFileOptions, 6 => Page.SecretEntry,
                            _ => throw new InvalidOperationException($"Secret update {update}: unexpected mode ${rom[0xcbb3]:x2}.")
                        };
                        FailIf(menu.CurrentPage != expected, $"Secret update {update}: page {menu.CurrentPage} != ROM {expected}.");
                        if (expected == Page.SecretEntry && rom[0xcbb4] != 0)
                        {
                            FailIf(screen.NameCursor != rom[0xcbbc] || screen.NameEntryPosition != rom[0xcbbe],
                                $"Secret update {update}: keyboard/input cursor differs from native $cbbc/$cbbe.");
                            byte[] entered = screen.EnteredSecret;
                            for (int index = 0; index < 20; index++)
                            {
                                byte glyph = entered[index] == 0xff ? (byte)0x20 : keyboard.Glyphs[entered[index]];
                                FailIf(glyph != rom.BankByte(4, 0xd7c0 + index), $"Secret update {update}: input byte ${index:x2} differs from ROM.");
                            }
                        }
                        FailIf(!sounds.Requests.Take(rom.Sounds.Count).SequenceEqual(rom.Sounds) ||
                            !rom.GameplayHandoff && sounds.Requests.Count != rom.Sounds.Count,
                            $"Secret update {update}: menu sound order differs.");
                    });
                }
                void Open()
                {
                    for (int index = 0; index < slot; index++) Step(keys: 0x80);
                    Step(keys: 1); Step(); Step(keys: 0x80); Step(keys: 1);
                    Step(keys: 1); // Initialization consumes A.
                    FailIf(screen.EnteredSecret.Length != 20 || screen.SelectedSlot != slot, "New-file secret lost its twenty slots or destination file.");
                }
                Open();
                Step(keys: 8); Step(keys: 8); Step(); // Empty input: error, then $10-update wait.
                Step(16, keys: 1); Step(keys: 1); // Dismissal consumes A.
                Step(keys: 0x80); // OK row wraps to keyboard row zero.
                Step(6, keys: 1); // Only one A edge in a batched host frame.
                for (int index = 0; index < 6; index++) Step(keys: 1);
                FailIf(screen.NameEntryPosition <= 4, "Game-secret input remained clamped to five letters.");
                Step(keys: 2); // B clears current position before moving left.
                for (int index = 0; index < 5; index++) Step(keys: 0x80);
                // Navigate the actual lower row to BACK and confirm through A.
                while (screen.NameLowerChoice != 2) Step(keys: 0x10);
                Step(keys: 1); Step(); Step();
                FailIf(root.FileSlotExists(slot), "Cancelling a new-file secret wrote a save.");
                Open();
                var source = OracleSaveData.CreateStandardGame(); source.SetLinkName("LINK"); source.NameChild("PIP");
                source.WriteWramByte(AgesWramAddress.wWhichGame, 0);
                source.WriteWramByte(0xc600, 0x34); source.WriteWramByte(0xc601, 0x12);
                source.WriteWramByte(AgesWramAddress.wChildStatus, 6); source.WriteWramByte(AgesWramAddress.wAnimalCompanion, 0x0c);
                var codec = new LinkedGameNpcDatabase(); byte[] secret = codec.GenerateGameTransferSecretValues(source);
                foreach (byte symbol in secret)
                {
                    char target = (char)keyboard.Glyphs[symbol];
                    int cursor = Enumerable.Range(0, 5 * 13).Select(index => (index / 13) * 16 + index % 13)
                        .First(index => keyboard.KeyboardGlyph(index / 16, index % 16) == target);
                    while (screen.NameCursor / 16 != cursor / 16) Step(keys: 0x80);
                    while (screen.NameCursor % 16 != cursor % 16) Step(keys: 0x10);
                    Step(keys: 1);
                }
                FailIf(!screen.EnteredSecret.SequenceEqual(secret) || screen.NameEntryPosition != 19,
                    "Twenty-character keyboard entry did not reach the final slot.");
                Step(keys: 8); Step(keys: 8);
                FailIf(root.FileSlotExists(slot), "Secret confirmation saved before mode6 state 2.");
                Step(); Step();
                byte[] payload = root.FileSlotSnapshot(slot);
                for (int index = 0; index < payload.Length; index++)
                foreach (bool backup in new[] { false, true })
                    FailIf(payload[index] != rom.SavedByte(slot, 0xc5b0 + index, backup),
                        $"Secret file ${slot:x2}: saved byte ${0xc5b0 + index:x4} differs from ROM.");
                FailIf(!OracleSaveData.TryDeserialize(payload, out var saved) || saved!.LinkName != "LINK" ||
                    !saved.IsLinkedGame || saved.MaxHealthQuarters != 16 || saved.ReadWramByte(AgesWramAddress.wSwordLevel) != 1 ||
                    saved.ReadWramByte(AgesWramAddress.wChildStage) != 5 || saved.ReadWramByte(AgesWramAddress.wChildPersonality) != 1,
                    "New linked file did not persist its transferred identity, four hearts, sword or child state.");
                for (int index = 0; index < slot; index++) Step(keys: 0x80);
                Step(keys: 1); Step(keys: 8); Step(32);
                FailIf(root._mainMenu is not null || !rom.GameplayHandoff ||
                    !root._saveData.IsLinkedGame || root._saveData.LinkName != "LINK",
                    "The secret-created file did not launch through the normal saved-file fade and identity handoff.");
                root.CompleteScenePreload();
            }
            finally { root.Free(); }
        }
        ValidateNewFileSecretSaveFailure();
        GD.Print("Validated native new-file secret entry across all three slots with split/batched application input, twenty keyboard symbols, invalid retry, Back, reopening and complete saved bytes.");
    }

    private void ValidateNewFileSecretSaveFailure()
    {
        var screen = new MainMenuScreen(); AddChild(screen);
        try
        {
            OracleSaveData? stored = null;
            int attempts = 0;
            var menu = new MainMenuController(screen, (_, _) => throw new InvalidOperationException("Unexpected launch."),
                _ => stored, (_, save) =>
                {
                    if (++attempts == 1) return SaveResult.Failed("secret save failure");
                    stored = save; return SaveResult.Succeeded;
                }, startAtFileSelect: true);
            void Tick(params string[] keys)
            {
                Input.BeginOriginalUpdate(new ApplicationInputSnapshot(keys, keys, Vector2.Zero));
                try { menu.Update(1.0 / 60.0); }
                finally { Input.EndOriginalUpdate(); }
            }
            Tick("attack"); Tick(); Tick("move_down"); Tick("attack"); Tick();
            var source = OracleSaveData.CreateStandardGame(); source.SetLinkName("HERO");
            source.WriteWramByte(AgesWramAddress.wWhichGame, 0);
            byte[] symbols = new LinkedGameNpcDatabase().GenerateGameTransferSecretValues(source);
            byte[] glyphs = new SecretEntryDatabase().Glyphs;
            foreach (byte symbol in symbols) screen.AppendNameCharacter((char)glyphs[symbol]);
            Tick("inventory"); Tick("inventory"); Tick();
            FailIf(!screen.SaveErrorVisible || stored is not null || menu.CurrentPage != Page.SecretEntry,
                "Failed new-file secret save lost the input screen or committed a file.");
            Tick("attack"); // Dismiss the store error; retain the complete entry.
            FailIf(!screen.EnteredSecret.SequenceEqual(symbols), "Save failure discarded the entered secret.");
            Tick("inventory"); Tick(); Tick();
            FailIf(attempts != 2 || stored?.LinkName != "HERO" || menu.CurrentPage != Page.FileSelect,
                "New-file secret save could not be retried after an I/O failure.");
        }
        finally { screen.Free(); }
    }
}
