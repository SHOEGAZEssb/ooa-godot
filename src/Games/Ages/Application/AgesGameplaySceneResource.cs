using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Resource preparation is independent of gameplay initialization: no nodes,
// save bytes, room objects or RNG are advanced by loading the packed scene.
internal sealed class AgesGameplaySceneResource
{
    private PackedScene? _scene;
    private bool _requested;

    internal void BeginPreload()
    {
        if (_scene is not null || _requested) return;
        Error error = ResourceLoader.LoadThreadedRequest(AgesGameSceneGraph.ScenePath, "PackedScene");
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not preload gameplay scene {AgesGameSceneGraph.ScenePath}: {error}.");
        _requested = true;
    }

    internal PackedScene Load()
    {
        if (_scene is not null) return _scene;
        _scene = (_requested
            ? ResourceLoader.LoadThreadedGet(AgesGameSceneGraph.ScenePath) as PackedScene
            : ResourceLoader.Load<PackedScene>(AgesGameSceneGraph.ScenePath)) ??
            throw new InvalidOperationException($"Could not load gameplay scene {AgesGameSceneGraph.ScenePath}.");
        _requested = false;
        return _scene;
    }

    internal IEnumerable<bool> Prepare()
    {
        BeginPreload();
        while (_requested && ResourceLoader.LoadThreadedGetStatus(AgesGameSceneGraph.ScenePath) ==
            ResourceLoader.ThreadLoadStatus.InProgress)
            yield return false;
        _ = Load();
    }
}
