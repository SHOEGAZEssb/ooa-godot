using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedRememberedCompanions()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var state = OracleRuntimeState.CreateInitialized(game);
            var save = OracleSaveData.CreateStandardGame(game);
            // include/wram.s: the five live bytes move from $cc24 to $cc40;
            // DeathRespawnStruct's saved bytes remain $c631-$c633/$c636-$c637.
            int first = game == OracleGame.Ages ? 0xcc24 : 0xcc40;
            int other = game == OracleGame.Ages ? 0xcc40 : 0xcc24;
            byte[] sentinel = [0xa0, 0xa1, 0xa2, 0xa3, 0xa4];
            for (int i = 0; i < sentinel.Length; i++) state.SetWramByte(other + i, sentinel[i]);
            CompanionRuntimeState.Remember(state, 0x0b, 0, 0x97, new(0x58 + 0.75f, 0x38 + 0.25f));
            FailIf(!Enumerable.Range(0, 5).Select(i => state.ReadWramByte(first + i))
                    .SequenceEqual(new byte[] { 0x0b, 0, 0x97, 0x38, 0x58 }) ||
                !CompanionRuntimeState.TryGetRemembered(state, 0x0b, 0, 0x97, out var position) ||
                position != new Vector2(0x58, 0x38) ||
                CompanionRuntimeState.TryGetRemembered(state, 0x0b, 1, 0x97, out _),
                $"{game}: remembered companion identity/position must use its native live field addresses.");
            CompanionRuntimeState.ForgetRemembered(state);
            FailIf(state.ReadWramByte(first) != 0 || state.ReadWramByte(first + 2) != 0x97 ||
                state.ReadWramByte(first + 3) != 0x38 || state.ReadWramByte(first + 4) != 0x58,
                $"{game}: forgetting a companion must clear only its native ID byte.");
            save.WriteWramByte(0xc635, 0x6e);
            save.SetDeathRespawnPoint(2, 0x2d, 3, 1, 0x48, 0x68,
                new RememberedCompanion(0x0c, 1, 0x2d, 0x58, 0x78), 0xd1);
            CompanionRuntimeState.RestoreRememberedFromDeathRespawn(state, save);
            FailIf(!Enumerable.Range(0, 5).Select(i => state.ReadWramByte(first + i))
                    .SequenceEqual(new byte[] { 0x0c, 1, 0x2d, 0x58, 0x78 }) ||
                save.ReadWramByte(0xc634) != 0xd1 || save.ReadWramByte(0xc635) != 0x6e ||
                CompanionRuntimeState.ReadRemembered(state) != new RememberedCompanion(0x0c, 1, 0x2d, 0x58, 0x78) ||
                !Enumerable.Range(0, 5).Select(i => state.ReadWramByte(other + i)).SequenceEqual(sentinel),
                $"{game}: checkpoint restore must copy the native remembered bytes, preserving skipped saved and unrelated live bytes.");
            byte[] original = state.CaptureState().Wram;
            var wrongGame = OracleSaveData.CreateStandardGame(game == OracleGame.Ages ? OracleGame.Seasons : OracleGame.Ages);
            foreach (Action mutation in new Action[] {
                () => CompanionRuntimeState.RestoreRememberedFromDeathRespawn(state, wrongGame),
                () => CompanionRuntimeState.InstallPreset(state, wrongGame, Vector2.Zero),
                () => CompanionRuntimeState.BeginAnimalRide(state, wrongGame, 0x0b, Vector2.Zero, 0) })
            {
                bool rejected = false;
                try { mutation(); }
                catch (ArgumentException) { rejected = true; }
                FailIf(!rejected || !state.CaptureState().Wram.SequenceEqual(original),
                    "Cross-game companion/checkpoint mutation must fail before changing live bytes.");
            }
        }
    }
}
