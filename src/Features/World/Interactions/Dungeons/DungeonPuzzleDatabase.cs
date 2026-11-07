
namespace Oracles;

// Shared native push, shutter, button and staircase parameters.
internal class DungeonPuzzleDatabase
{
    private readonly string _root;
    private IReadOnlyList<CutsceneCommand>? _enemyClearChestCommands;
    internal IReadOnlyList<CutsceneCommand> EnemyClearChestCommands => _enemyClearChestCommands ??=
        CutsceneCommandCatalog.Load(_root+"cutscenes/enemy_clear_chest_commands.tsv");
    private readonly Dictionary<string,int> _puzzleConstants = new(StringComparer.Ordinal);
    internal int PushableBlock => PuzzleConstant("pushable-block");
    internal int PushDelay => PuzzleConstant("push-delay");
    internal int SolveWait => PuzzleConstant("solve-wait");
    internal int ChestPuffWait => PuzzleConstant("chest-puff-wait");
    internal int SolveSound => PuzzleConstant("solve-sound");
    internal int DoorFrameWait => PuzzleConstant("door-frame-wait");
    internal int OpenTile => PuzzleConstant("open-tile");
    internal int DoorSound => PuzzleConstant("door-sound");
    internal int DoorTorchWait => PuzzleConstant("door-torch-wait");
    internal int DoorEntryScratchMask => PuzzleConstant("door-entry-scratch-mask");
    internal int ButtonTile => PuzzleConstant("button-tile");
    internal int PressedButtonTile => PuzzleConstant("pressed-button-tile");
    internal int ButtonRadiusY => PuzzleConstant("button-radius-y");
    internal int ButtonRadiusX => PuzzleConstant("button-radius-x");
    internal int ButtonObjectReleaseDelay => PuzzleConstant("button-object-release-delay");
    internal int ButtonSound => PuzzleConstant("button-sound");
    internal int EnemyStairTile(int sourceTile) => PuzzleConstant($"enemy-stair-tile-{sourceTile-0x40}");
    internal int DoorAngle(int subId) => subId switch
    {
        >=0x04 and <=0x0b => 0x10+(subId&3)*2,
        >=0x10 and <=0x17 => PuzzleConstant($"door-angle-{subId:x2}"),
        _ => throw new ArgumentOutOfRangeException(nameof(subId),$"Unsupported INTERAC$1e shutter subid${subId:x2}.")
    };
    internal int DoorRadiusY(int subId) => subId>=0x10 ? PuzzleConstant($"door-y-{subId:x2}") : (subId&1)==0 ? 10 : 8;
    internal int DoorRadiusX(int subId) => subId>=0x10 ? PuzzleConstant($"door-x-{subId:x2}") : (subId&1)==0 ? 8 : 10;
    internal int DoorTorchCount(int subId) => PuzzleConstant($"door-speed-{subId:x2}");
    internal int ClosedTile(int subId) => ((DoorAngle(subId)>>1)&3) switch
    {
        0 => PuzzleConstant("closed-up"), 1 => PuzzleConstant("closed-right"),
        2 => PuzzleConstant("closed-down"), _ => PuzzleConstant("closed-left")
    };

    internal DungeonPuzzleDatabase(string root)
    {
        _root=root;
        var table=GeneratedTable.Load(root+"objects/dungeon_puzzle_constants.tsv",
            new("native dungeon puzzle parameters",GeneratedTableKeySemantics.Unique,
                ["key","value"],["key"],headerRequired:true));
        foreach(var row in table.Rows)_puzzleConstants.Add(row.RequiredString(0),row.Decimal(1));
        if(_puzzleConstants.Count!=56)throw new InvalidOperationException($"{table.Path}: expected 56 native dungeon puzzle parameters.");
    }
    protected int PuzzleConstant(string key) => _puzzleConstants.TryGetValue(key,out int value)?value:
        throw new KeyNotFoundException($"Native dungeon puzzle parameter '{key}' was not imported.");
}
