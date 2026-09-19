using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGameplayScenePreload()
    {
        byte[] save = _saveData.Serialize();
        int rngCalls = _random.Calls;
        int children = GetChildCount();
        OracleRoomData room = _currentRoom;
        var resource = new GameplaySceneResource();
        resource.BeginPreload();
        resource.BeginPreload();
        PackedScene prepared = resource.Load();
        resource.BeginPreload();
        FailIf(prepared != resource.Load() || prepared.ResourcePath != GameSceneGraph.ScenePath,
            "Gameplay scene preload did not retain its resource across repeated requests.");
        FailIf(!save.SequenceEqual(_saveData.Serialize()) || _random.Calls != rngCalls ||
            _currentRoom != room || GetChildCount() != children,
            "Gameplay resource preparation changed save, RNG, room identity or live nodes before the intro handoff.");
        GameSceneGraph instance = prepared.Instantiate<GameSceneGraph>();
        try
        {
            FailIf(instance.IsInsideTree() || instance.GetNodeOrNull<Player>("World/Link") is null,
                "Prepared gameplay scene did not preserve its off-tree Link hierarchy.");
        }
        finally { instance.Free(); }
        var direct = new GameplaySceneResource();
        FailIf(direct.Load() != prepared,
            "Direct gameplay initialization did not reuse the same packed scene resource.");
        GD.Print("Validated gameplay scene background preload, repeated/direct loads, and side-effect-free preparation before intro completion.");
    }
}
