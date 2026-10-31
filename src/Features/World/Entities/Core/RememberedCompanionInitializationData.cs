using Godot;
using System;
using System.Collections.Concurrent;

namespace Oracles;

internal sealed class RememberedCompanionInitializationData
{
    private static readonly ConcurrentDictionary<string, Lazy<RememberedCompanionInitializationData>> Packs = new(StringComparer.Ordinal);
    internal static RememberedCompanionInitializationData For(string root) => Packs.GetOrAdd(root,
        static pack => new Lazy<RememberedCompanionInitializationData>(() => new(pack))).Value;
    private readonly OracleGame _game;
    private readonly bool _copyMountPoint;
    private readonly int _interactionCompanionId, _interactionId, _interactionSubId, _interactionTilesetMask;

    private RememberedCompanionInitializationData(string root)
    {
        _game = OracleAssetPack.GameFor(root);
        var row = GeneratedTable.Load(root + "metadata/remembered_companion_initialization.tsv", new("native remembered companion initialization",
            GeneratedTableKeySemantics.Ordered,
            ["copy-mount-point", "interaction-companion-id", "interaction-id", "interaction-subid", "interaction-tileset-mask", "source"], headerRequired: true)).SingleRow();
        _copyMountPoint = row.Boolean01(0); _interactionCompanionId = row.RequiredString(1) == "-1" ? -1 : row.HexByte(1);
        _interactionId = row.HexByte(2); _interactionSubId = row.HexByte(3); _interactionTilesetMask = row.HexByte(4); _ = row.RequiredString(5);
        if (_copyMountPoint ? _interactionCompanionId != -1 || _interactionId != 0 || _interactionSubId != 0 || _interactionTilesetMask != 0 :
            _interactionCompanionId != 0x13 || _interactionId != 0xe6 || _interactionSubId != 2 || _interactionTilesetMask != 0x80)
            throw row.Invalid(0, "supported US remembered-companion dispatch policy");
    }

    internal bool TryInitialize(int group, int room, int tilesetFlags, OracleSaveData save, OracleRuntimeState memory,
        out RememberedCompanionAdmission admission)
    {
        if (save.Game != _game || memory.Game != _game)
            throw new ArgumentException("loadRememberedCompanion requires one selected pack, save and runtime owner.");
        if ((uint)group > 7 || (uint)room > 255 || (uint)tilesetFlags > 255)
            throw new ArgumentOutOfRangeException(nameof(group), "Native room identity and tileset flags must come from a supported room.");
        var remembered = CompanionRuntimeState.ReadRemembered(memory);
        admission = default;
        if (remembered.Id == 0 || remembered.Group != group || remembered.Room != room || CompanionRuntimeState.AnyActive(memory)) return false;
        Vector2 position = new(remembered.X, remembered.Y);
        if (remembered.Id == _interactionCompanionId)
        {
            if ((tilesetFlags & _interactionTilesetMask) == 0) return false;
            admission = new(remembered.Id, _interactionId, _interactionSubId, position);
            return true;
        }
        // The native loader leaves state, fractions and every other slot byte intact.
        memory.SetWramByte(0xd101, (byte)remembered.Id);
        memory.SetWramByte(0xd100, 1);
        memory.SetWramByte(0xd10b, (byte)remembered.Y);
        memory.SetWramByte(0xd10d, (byte)remembered.X);
        if (_copyMountPoint) save.SetLastAnimalMountPoint((byte)remembered.Y, (byte)remembered.X);
        admission = new(remembered.Id, -1, 0, position);
        return true;
    }
}

internal readonly record struct RememberedCompanionAdmission(int SpecialObjectId, int InteractionId, int InteractionSubId, Vector2 Position)
{
    internal bool IsInteraction => InteractionId >= 0;
    internal string Source => IsInteraction ? $"code/roomInitialization.s:loadRememberedCompanion: INTERAC ${InteractionId:x2}:${InteractionSubId:x2}" :
        $"code/roomInitialization.s:loadRememberedCompanion: SPECIALOBJECT ${SpecialObjectId:x2}";
}
