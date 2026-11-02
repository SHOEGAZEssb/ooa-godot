
namespace Oracles;

internal sealed class ScreenTransitionPaletteDatabase
{
    public string AssetRoot { get; }
    private readonly OracleGame _game;
    private readonly Dictionary<(int Group, int Room, int Direction, int StateModifier),
        (byte[] Source, byte[] Destination, int DisabledFlag)> _routes = new();

    internal ScreenTransitionPaletteDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        _game = OracleAssetPack.GameFor(assetRoot);

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "metadata/screen_transition_palettes.tsv",
            new GeneratedTableSchema("smooth screen palettes", GeneratedTableKeySemantics.Unique,
                ["group", "room", "direction", "state-modifier", "disabled-flag", "source-rgb5", "destination-rgb5", "source"],
                ["group", "room", "direction", "state-modifier"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            byte[] from = Convert.FromHexString(row.RequiredString(5));
            byte[] to = Convert.FromHexString(row.RequiredString(6));
            if (from.Length != 72 || to.Length != 72 || Array.Exists(from, b => b > 31) || Array.Exists(to, b => b > 31))
                throw new InvalidOperationException($"Invalid RGB5 palette {row.RequiredString(7)}.");
            int stateModifier = row.Decimal(3, -1, 3);
            int disabled = row.HexByteOrSentinel(4, "-", -1);
            if (_game == OracleGame.Ages ? stateModifier != -1 : stateModifier < 0 || disabled != -1)
                throw row.Invalid(3, "the native game's season selection and flag gate");
            _routes.Add((row.Decimal(0, 0, _game == OracleGame.Ages ? 2 : 1), row.HexByte(1), row.Decimal(2, 0, 3), stateModifier), (from, to, disabled));
        }
        int expected = _game == OracleGame.Ages ? 21 : 32;
        if (_routes.Count != expected) throw new InvalidOperationException($"{table.Path}: expected {expected} native palette-transition records.");
    }

    internal bool TryGet(int group, int room, Vector2I direction, OracleSaveData save, int stateModifier,
        out (byte[] Source, byte[] Destination) palettes)
    {
        palettes = default;
        if (save.Game != _game) throw new ArgumentException("Screen palette routes and save state must belong to the same game.", nameof(save));
        if (_game == OracleGame.Seasons && group is not (0 or 1)) return false;
        int index = direction == Vector2I.Up ? 0 : direction == Vector2I.Right ? 1 : direction == Vector2I.Down ? 2 :
            direction == Vector2I.Left ? 3 : throw new ArgumentOutOfRangeException(nameof(direction));
        if (_game == OracleGame.Seasons && stateModifier is < 0 or > 3)
        {
            if (!_routes.ContainsKey((group, room, index, 0))) return false;
            throw new NotSupportedException($"{AssetRoot}: paletteTransitionSeasonData cannot select season ${stateModifier:x2}.");
        }
        if (!_routes.TryGetValue((group, room, index, _game == OracleGame.Ages ? -1 : stateModifier), out var route) ||
            route.DisabledFlag >= 0 && save.HasGlobalFlag(route.DisabledFlag)) return false;
        palettes = (route.Source, route.Destination);
        return true;
    }
}
