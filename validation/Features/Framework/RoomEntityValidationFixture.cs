using Godot;
using System;

namespace oracleofages;

internal sealed class RoomEntityValidationOptions
{
    internal NpcDatabase? Npcs { get; init; }
    internal EnemyDatabase? Enemies { get; init; }
    internal ItemDropDatabase? ItemDrops { get; init; }
    internal TimePortalDatabase? TimePortals { get; init; }
    internal OracleRandom? Random { get; init; }
    internal OracleSaveData? SaveData { get; init; }
    internal OracleRuntimeState? RuntimeState { get; init; }
    internal InventoryState? Inventory { get; init; }
    internal Func<long>? AnimationTick { get; init; }
    internal TreasureDatabase? Treasures { get; init; }
    internal RoomSession? Rooms { get; init; }
}

internal sealed class RoomEntityValidationFixture : IDisposable
{
    private readonly bool _ownsRoot;
    private bool _disposed;

    internal Node Root { get; }
    internal RoomEntityManager Manager { get; }

    private RoomEntityValidationFixture(
        Node root,
        bool ownsRoot,
        RoomEntityValidationOptions options)
    {
        Root = root;
        _ownsRoot = ownsRoot;
        Manager = new RoomEntityManager(
            root,
            options.Npcs ?? new NpcDatabase(),
            options.Enemies ?? new EnemyDatabase(),
            options.ItemDrops ?? new ItemDropDatabase(),
            options.TimePortals ?? new TimePortalDatabase(),
            options.Random ?? new OracleRandom(),
            options.SaveData,
            options.RuntimeState,
            options.Inventory,
            options.AnimationTick,
            options.Treasures,
            options.Rooms);
    }

    internal static RoomEntityValidationFixture ForRoot(
        Node root,
        RoomEntityValidationOptions? options = null) =>
        new(root, false, options ?? new RoomEntityValidationOptions());

    internal static RoomEntityValidationFixture Attach(
        Node owner,
        string name,
        RoomEntityValidationOptions? options = null)
    {
        var root = new Node { Name = name };
        owner.AddChild(root);
        return new RoomEntityValidationFixture(
            root, true, options ?? new RoomEntityValidationOptions());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (GodotObject.IsInstanceValid(Root))
            Manager.Clear();
        Manager.Dispose();
        if (!_ownsRoot || !GodotObject.IsInstanceValid(Root))
            return;
        Root.GetParent()?.RemoveChild(Root);
        Root.Free();
    }
}
