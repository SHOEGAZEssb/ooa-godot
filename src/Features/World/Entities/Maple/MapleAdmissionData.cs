using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Oracles;

// roomInitialization.checkAndSpawnMaple; encounter presentation is a separate binding.
internal sealed class MapleAdmissionData
{
    private static readonly ConcurrentDictionary<string, Lazy<MapleAdmissionData>> Packs = new(StringComparer.Ordinal);
    internal static MapleAdmissionData For(string root) => Packs.GetOrAdd(root,
        static pack => new Lazy<MapleAdmissionData>(() => new(pack))).Value;
    private readonly OracleGame _game;
    private readonly int _pastGroup;
    private readonly byte _id, _y, _x, _ring;
    private readonly int _killThreshold;
    private readonly Dictionary<int, byte[]> _bitmaps = [];
    private readonly (int Target, int Bitmap, string Source)[] _selectors = new (int, int, string)[256];
    internal int LocationCount { get; }

    private MapleAdmissionData(string root)
    {
        _game = OracleAssetPack.GameFor(root);
        var policy = GeneratedTable.Load(root + "metadata/maple_admission.tsv", new("native Maple admission",
            GeneratedTableKeySemantics.Ordered,
            ["past-group", "zero-companion-is-ricky", "id", "y", "x", "ring", "kill-threshold", "source"], headerRequired: true)).SingleRow();
        _pastGroup = policy.Decimal(0, -1, 7);
        bool zero = policy.Boolean01(1);
        _id = (byte)policy.HexByte(2); _y = (byte)policy.HexByte(3); _x = (byte)policy.HexByte(4);
        _ring = (byte)policy.HexByte(5); _killThreshold = policy.UnsignedDecimal(6); _ = policy.RequiredString(7);
        if (_pastGroup is not (-1 or 1) || zero != (_pastGroup == 1) || _id != 0x0e || _y != 0x18 || _x != 0xb8 || _ring != 0x0f || _killThreshold != 30)
            throw policy.Invalid(0, "supported US checkAndSpawnMaple policy");
        foreach (var row in GeneratedTable.Load(root + "metadata/maple_location_bitmaps.tsv", new("native Maple bitmaps",
            GeneratedTableKeySemantics.Unique, ["index", "blocked-bits", "source"], ["index"], headerRequired: true)).Rows)
        {
            int index = row.Decimal(0, 0, _pastGroup < 0 ? 2 : 3);
            string hex = row.RequiredString(1); _ = row.RequiredString(2);
            if (hex.Length != 64 || !System.Linq.Enumerable.All(hex, Uri.IsHexDigit)) throw row.Invalid(1, "32 native bitmap bytes");
            byte[] bits = Convert.FromHexString(hex);
            _bitmaps.Add(index, bits);
            int locations = 0;
            for (int room = 0; room < 256; room++) if ((bits[room >> 3] & (1 << (room & 7))) == 0) locations++;
            if (_pastGroup == 1 && locations != (index == 3 ? 23 : 29))
                throw row.Invalid(1, "supported US Ages present/past bitmap extent");
            LocationCount += locations;
        }
        if (_bitmaps.Count != (_pastGroup < 0 ? 3 : 4)) throw new InvalidOperationException(root + ": incomplete native Maple bitmaps.");
        var selectors = GeneratedTable.Load(root + "metadata/maple_location_selectors.tsv", new("native Maple selectors",
            GeneratedTableKeySemantics.Unique, ["companion", "target", "bitmap-index", "source"], ["companion"], headerRequired: true));
        if (selectors.Rows.Count != 256) throw new InvalidOperationException(root + ": expected all $100 raw Maple companion selectors.");
        foreach (var row in selectors.Rows)
            _selectors[row.HexByte(0)] = (row.HexWord(1), row.Decimal(2, -1, _pastGroup < 0 ? 2 : 3), row.RequiredString(3));
    }

    internal bool IsEligibleLocation(int group, int room, int companion)
    {
        if (group != 0 && (_pastGroup < 0 || group != _pastGroup)) return false;
        if ((uint)room > 255 || (uint)companion > 255) throw new ArgumentOutOfRangeException(nameof(room), "Native room and companion inputs are bytes.");
        int bitmap = group == _pastGroup ? 3 : _selectors[companion].Bitmap;
        if (bitmap < 0)
        {
            var selector = _selectors[companion];
            throw new NotSupportedException($"{selector.Source}: raw companion ${companion:x2} selects unbound native bitmap ${selector.Target:x4}.");
        }
        return (_bitmaps[bitmap][room >> 3] & (1 << (room & 7))) == 0;
    }

    internal bool TryAdmit(int group, int room, OracleSaveData save, OracleRuntimeState memory)
    {
        if (save.Game != _game || memory.Game != _game)
            throw new ArgumentException("checkAndSpawnMaple requires one selected pack, save and runtime owner.");
        if ((uint)group > 7 || (uint)room > 255) throw new ArgumentOutOfRangeException(nameof(group), "Native room identity is group $00-$07 and one room byte.");
        memory.SetField("wIsMaplePresent", 0);
        if (memory.ReadField("wcc85") != 0) return false;
        if (group != _pastGroup && (group != 0 || CompanionRuntimeState.AnyActive(memory))) return false;
        if (!IsEligibleLocation(group, room, save.ReadField("wAnimalCompanion"))) return false;
        int threshold = save.ReadField("wActiveRing") == _ring ? _killThreshold >> 1 : _killThreshold;
        if (save.MapleKillCounter < threshold) return false;
        save.SetMapleKillCounter(0);
        memory.SetField("wcc85", 1);
        memory.SetField("wIsMaplePresent", 1);
        CompanionRuntimeState.BeginIdle(memory, _id, new(_x, _y));
        return true;
    }
}
