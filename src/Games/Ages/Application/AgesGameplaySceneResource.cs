
namespace Oracles;

// Resource preparation is independent of gameplay initialization: no nodes,
// save bytes, room objects or RNG are advanced by loading the packed scene.
internal sealed class AgesGameplaySceneResource
{
    internal const string ScenePath = "res://scenes/ages/gameplay.tscn";
    private PackedScene? _scene;
    private bool _requested;

    internal void BeginPreload()
    {
        if (_scene is not null || _requested) return;
        Error error = ResourceLoader.LoadThreadedRequest(AgesGameplaySceneResource.ScenePath, "PackedScene");
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not preload gameplay scene {AgesGameplaySceneResource.ScenePath}: {error}.");
        _requested = true;
    }

    internal PackedScene Load()
    {
        if (_scene is not null) return _scene;
        _scene = (_requested
            ? ResourceLoader.LoadThreadedGet(AgesGameplaySceneResource.ScenePath) as PackedScene
            : ResourceLoader.Load<PackedScene>(AgesGameplaySceneResource.ScenePath)) ??
            throw new InvalidOperationException($"Could not load gameplay scene {AgesGameplaySceneResource.ScenePath}.");
        _requested = false;
        return _scene;
    }

    internal IEnumerable<bool> Prepare()
    {
        BeginPreload();
        while (_requested && ResourceLoader.LoadThreadedGetStatus(AgesGameplaySceneResource.ScenePath) ==
            ResourceLoader.ThreadLoadStatus.InProgress)
            yield return false;
        _ = Load();
    }
}
