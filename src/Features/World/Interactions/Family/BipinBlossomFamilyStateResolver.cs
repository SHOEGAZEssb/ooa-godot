using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the live WRAM interpretation of INTERAC_BIPIN_BLOSSOM_FAMILY_SPAWNER
/// $ac. NpcDatabase remains a read-only view of the generated actor tables.
/// </summary>
internal sealed class BipinBlossomFamilyStateResolver
{
    private const int AgesRefillMask = 0x02;

    private readonly NpcDatabase _npcs;
    private readonly BipinBlossomFamilyInteractionDatabase _interactions = new();

    internal RunningBipinRecord RunningBipin => _interactions.RunningBipin;

    internal BipinBlossomFamilyStateResolver(NpcDatabase npcs)
    {
        _npcs = npcs;
    }

    /// <summary>
    /// Executes the family spawner at its room-object slot: conditionally
    /// advances the child, selects and resolves the spawned actor list, then
    /// clears Ages seed-tree refill bit 1.
    /// </summary>
    internal IReadOnlyList<NpcRecord> ResolveRoomNpcs(
        int group,
        int room,
        OracleSaveData? save,
        OracleRuntimeState runtime)
    {
        IReadOnlyList<NpcRecord> placed = _npcs.GetRoomNpcs(group, room);
        if (!_npcs.TryGetFamilyRoomNpcs(
                group, room, out IReadOnlyList<FamilyNpcRecord> family))
        {
            return placed;
        }
        if (save is not null &&
            save.HasGlobalFlag(GlobalFlag.FinishedGame))
        {
            return placed;
        }

        if (save is not null)
            AdvanceStage(save, runtime);
        int stage = save is null
            ? 0
            : Math.Clamp(
                (int)save.ReadWramByte(WramAddress.wChildStage),
                0,
                9);
        int personality = stage < 4 || save is null
            ? -1
            : save.ReadWramByte(WramAddress.wChildPersonality);

        var result = new List<NpcRecord>(placed.Count + family.Count);
        result.AddRange(placed);
        foreach (FamilyNpcRecord candidate in family)
        {
            if (candidate.Stage != stage ||
                candidate.Personality != personality)
            {
                continue;
            }
            // Keep NpcCharacter.BaseRecord on the imported row. The manager
            // applies ResolveRecord through the same resolver after all
            // spawned actors have been installed and on every later save
            // refresh.
            result.Add(candidate.Record);
        }
        return result;
    }

    internal bool TryResolveDialogue(
        NpcRecord baseRecord,
        OracleSaveData save,
        out Dialogue dialogue)
    {
        dialogue = default;
        if (!_npcs.TryGetFamilyRoomNpcs(
                baseRecord.Group,
                baseRecord.Room,
                out IReadOnlyList<FamilyNpcRecord> family) ||
            !ContainsRecord(family, baseRecord))
        {
            return false;
        }

        NpcRecord resolved = ResolveRecord(baseRecord, save);
        dialogue = new Dialogue(resolved.TextId, resolved.Message);
        return true;
    }

    internal Dialogue Text(
        int textId,
        OracleSaveData save,
        string? childNameOverride = null) =>
        _interactions.Text(textId, save, childNameOverride);

    private NpcRecord ResolveRecord(
        NpcRecord record,
        OracleSaveData save)
    {
        int textId = record.TextId;
        if (save.ChildNamed &&
            save.ReadWramByte(WramAddress.wChildStage) == 0)
        {
            textId = record switch
            {
                { Id: InteractionId.Bipin, SubId: 0x00 } => 0x4301,
                { Id: InteractionId.Blossom, SubId: 0x00 } => 0x4409,
                _ => textId
            };
        }

        Dialogue dialogue = textId == record.TextId
            ? new Dialogue(
                textId,
                BipinBlossomFamilyInteractionDatabase.SubstituteChildName(
                    record.Message,
                    save))
            : _interactions.Text(textId, save);
        return record with
        {
            TextId = dialogue.TextId,
            Message = dialogue.Message
        };
    }

    private static bool ContainsRecord(
        IReadOnlyList<FamilyNpcRecord> family,
        NpcRecord record)
    {
        foreach (FamilyNpcRecord candidate in family)
        {
            if (candidate.Record == record)
                return true;
        }
        return false;
    }

    private static void AdvanceStage(
        OracleSaveData save,
        OracleRuntimeState runtime)
    {
        int refillBits = runtime.ReadWramByte(
            WramAddress.wSeedTreeRefilledBitset);
        if ((refillBits & AgesRefillMask) == 0)
            return;

        int nextStage = save.ReadWramByte(
            WramAddress.wNextChildStage);
        int requiredEssences = nextStage switch
        {
            1 or 7 => 2,
            3 or 8 => 4,
            4 or 9 => 6,
            _ => 0
        };
        int essenceCount = CountBits(
            save.ReadWramByte(WramAddress.wEssencesObtained));
        bool saveChanged = false;
        if (nextStage is >= 0 and <= 9 &&
            essenceCount >= requiredEssences)
        {
            saveChanged |= save.WriteWramByte(
                WramAddress.wChildStage,
                (byte)nextStage);
            int personality = nextStage switch
            {
                4 => DecideInitialPersonality(
                    save.ReadWramByte(WramAddress.wChildStatus)),
                7 => DecideFinalPersonality(
                    save.ReadWramByte(
                        WramAddress.wChildPersonality),
                    save.ReadWramByte(WramAddress.wChildStatus)),
                _ => -1
            };
            if (personality >= 0)
            {
                saveChanged |= save.WriteWramByte(
                    WramAddress.wChildPersonality,
                    (byte)personality);
            }
        }

        runtime.SetWramByte(
            WramAddress.wSeedTreeRefilledBitset,
            (byte)(refillBits & ~AgesRefillMask));
        if (saveChanged)
            save.CommitInventoryChange();
    }

    private static int DecideInitialPersonality(int status) =>
        status switch
        {
            >= 0x0b => 0,
            >= 0x06 => 1,
            _ => 2
        };

    private static int DecideFinalPersonality(
        int initialPersonality,
        int status) =>
        initialPersonality switch
        {
            0 when status >= 0x1a => 2,
            0 when status >= 0x15 => 1,
            0 => 0,
            1 when status >= 0x13 => 2,
            1 when status >= 0x0f => 0,
            1 => 3,
            2 when status >= 0x0e => 1,
            2 when status >= 0x0a => 0,
            2 => 3,
            _ => 0
        };

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }
}
