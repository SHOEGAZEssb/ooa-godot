using System;

namespace Oracles;

public sealed class MapPresentationState
{
    private readonly OracleSaveData _save;
    private readonly InventoryState _inventory;
    private readonly GlobalFlagDatabase _flags;

    public MapPresentationState(OracleSaveData save, InventoryState inventory)
    {
        if (OracleAssetPack.GameFor(inventory.AssetRoot) != save.Game)
            throw new ArgumentException("Map presentation and inventory must belong to the same game.");
        _save = save;
        _inventory = inventory;
        _flags = new(inventory.AssetRoot);
    }

    public int AnimalCompanion => _inventory.AnimalCompanion;
    internal OracleGame Game => _save.Game;
    public int AnimalCompanionAreaTextId =>
        0x0300 | unchecked((byte)((_save.Game == OracleGame.Ages ? 0x2d : 0x08) + AnimalCompanion - SpecialObjectId.Ricky));

    public bool TryGetTimePortalRoom(int group, out int room)
    {
        if (group is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(group));
        room = _save.Game == OracleGame.Ages ? _save.TimePortalRoom : 0;
        return _save.Game == OracleGame.Ages && _save.TimePortalGroup == group;
    }

    public int ResolveMakuAdviceTextId(int group)
    {
        if (group is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(group));
        if (_save.Game == OracleGame.Seasons)
            return _save.HasGlobalFlag(_flags.GetId("GLOBALFLAG_GNARLED_KEY_GIVEN"))
                ? 0x1700 | _save.MakuMapTextPresent : 0x0317;
        bool past = group == 1;
        int metFlag = _flags.GetId(past ? "GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PAST_MAP" : "GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PRESENT_MAP");
        if (!_save.HasGlobalFlag(metFlag)) return past ? 0x0324 : 0x0323;
        return 0x0500 | (past ? _save.MakuMapTextPast : _save.MakuMapTextPresent);
    }
}
