using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Typed binding for the stable gameplay hierarchy stored in gameplay.tscn.
/// Original behavior remains in the owning runtime classes; the scene owns
/// only node lifecycle, parentage, draw order, and fixed presentation values.
/// </summary>
public partial class GameSceneGraph : Node2D
{
    public const string ScenePath = "res://scenes/gameplay.tscn";

    public Node2D WorldRoot { get; private set; } = null!;
    public CanvasLayer InterfaceLayer { get; private set; } = null!;
    public RoomView RoomView { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public Camera2D RoomCamera { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    public ColorRect WarpFade { get; private set; } = null!;
    public DialogueBox Dialogue { get; private set; } = null!;
    public Label RoomDebug { get; private set; } = null!;
    public MapScreen MapScreen { get; private set; } = null!;
    public InventoryScreen InventoryScreen { get; private set; } = null!;
    public SaveQuitScreen SaveQuitScreen { get; private set; } = null!;
    public DebugFlagScreen DebugFlagScreen { get; private set; } = null!;
    public ColorRect MenuFade { get; private set; } = null!;

    public override void _Ready()
    {
        WorldRoot = Unique<Node2D>("World");
        InterfaceLayer = Unique<CanvasLayer>("Interface");
        RoomView = Unique<RoomView>("RoomView");
        Player = Unique<Player>("Link");
        RoomCamera = Unique<Camera2D>("RoomCamera");
        Hud = Unique<Hud>("Hud");
        WarpFade = Unique<ColorRect>("RoomWarpFade");
        Dialogue = Unique<DialogueBox>("Dialogue");
        RoomDebug = Unique<Label>("RoomDebug");
        MapScreen = Unique<MapScreen>("MapScreen");
        InventoryScreen = Unique<InventoryScreen>("InventoryScreen");
        SaveQuitScreen = Unique<SaveQuitScreen>("SaveQuitScreen");
        DebugFlagScreen = Unique<DebugFlagScreen>("DebugFlagScreen");
        MenuFade = Unique<ColorRect>("MenuFade");

        if (WorldRoot.GetParent() != this || InterfaceLayer.GetParent() != this ||
            RoomView.GetParent() != WorldRoot || Player.GetParent() != WorldRoot ||
            RoomCamera.GetParent() != WorldRoot || Hud.GetParent() != InterfaceLayer ||
            WarpFade.GetParent() != InterfaceLayer || Dialogue.GetParent() != InterfaceLayer ||
            RoomDebug.GetParent() != InterfaceLayer || MapScreen.GetParent() != InterfaceLayer ||
            InventoryScreen.GetParent() != InterfaceLayer ||
            SaveQuitScreen.GetParent() != InterfaceLayer ||
            DebugFlagScreen.GetParent() != InterfaceLayer ||
            MenuFade.GetParent() != InterfaceLayer)
        {
            throw new InvalidOperationException(
                $"{ScenePath} does not match the required world/interface ownership hierarchy.");
        }
    }

    private T Unique<T>(string name) where T : Node
    {
        return GetNodeOrNull<T>($"%{name}") ?? throw new InvalidOperationException(
            $"{ScenePath} is missing required unique {typeof(T).Name} node %{name}.");
    }
}
