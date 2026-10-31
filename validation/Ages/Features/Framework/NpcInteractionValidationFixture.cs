using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class NpcInteractionValidationFixture : IDisposable
{
    private readonly Node _root;
    private readonly RoomEntityValidationFixture _entities;
    private bool _disposed;

    internal OracleSaveData Save { get; } = OracleSaveData.CreateStandardGame(OracleGame.Ages);
    internal RoomSession Rooms { get; }
    internal long Tick { get; private set; }
    internal TreasureDatabase Treasures { get; } = new(AgesGameContent.AssetRoot);
    internal InventoryState Inventory { get; }
    internal DialogueBox Dialogue { get; } = new() { AssetRoot = AgesGameContent.AssetRoot, Name = "Dialogue" };
    internal List<int> Sounds { get; } = [];
    internal RoomEntityManager Manager => _entities.Manager;
    internal InteractionController Interactions { get; }

    internal NpcInteractionValidationFixture(Node owner, string name, int group, int room)
    {
        _root = new Node { Name = name };
        var world = new Node { Name = "World" };
        var screen = new Node { Name = "Interface" };
        var view = new RoomView { Name = "RoomView" };
        _root.AddChild(world);
        _root.AddChild(screen);
        _root.AddChild(view);
        _root.AddChild(Dialogue);
        owner.AddChild(_root);

        Rooms = AgesRoomContent.CreateSession(group, room, () => Tick, () => Tick = 0, Save);
        Inventory = new InventoryState(Treasures, Save, () => Rooms.CurrentDungeonIndex);
        _entities = RoomEntityValidationFixture.ForRoot(world, new()
        {
            SaveData = Save, Inventory = Inventory, Treasures = Treasures, Rooms = Rooms
        });
        Manager.SoundRequested += Sounds.Add;
        Interactions = AgesInteractionValidation.Create(
            Rooms, Manager, new SignDatabase(AgesGameContent.AssetRoot), new ChestDatabase(AgesGameContent.AssetRoot),
            Treasures, Dialogue, world, view,
            static position => position, () => Tick, Inventory, screen, Sounds.Add);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Manager.SoundRequested -= Sounds.Add;
        _entities.Dispose();
        _root.GetParent()?.RemoveChild(_root);
        _root.Free();
    }
}
