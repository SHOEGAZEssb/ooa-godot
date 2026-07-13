using Godot;

namespace oracleofages;

public sealed class GameSceneGraph
{
    public RoomView RoomView { get; }
    public Player Player { get; }
    public Camera2D RoomCamera { get; }
    public Hud Hud { get; }
    public ColorRect WarpFade { get; }
    public DialogueBox Dialogue { get; }
    public Label RoomDebug { get; }
    public MapScreen MapScreen { get; }
    public InventoryScreen InventoryScreen { get; }
    public DebugFlagScreen DebugFlagScreen { get; }
    public ColorRect MenuFade { get; }

    public GameSceneGraph(Node root)
    {
        RoomView = new RoomView { Name = "RoomView", ZIndex = 0 };
        root.AddChild(RoomView);

        Player = new Player { Name = "Link", ZIndex = 10 };
        root.AddChild(Player);

        RoomCamera = new Camera2D
        {
            Name = "RoomCamera",
            Enabled = true,
            PositionSmoothingEnabled = false
        };
        root.AddChild(RoomCamera);

        var interfaceLayer = new CanvasLayer { Name = "Interface", Layer = 10 };
        root.AddChild(interfaceLayer);

        Hud = new Hud { Name = "Hud", Position = new Vector2(0, 128), ZIndex = 20 };
        interfaceLayer.AddChild(Hud);

        WarpFade = new ColorRect
        {
            Name = "RoomWarpFade",
            Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight),
            Color = new Color(1, 1, 1, 0),
            ZIndex = 15,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        interfaceLayer.AddChild(WarpFade);

        Dialogue = new DialogueBox { Name = "Dialogue", ZIndex = 30, Visible = false };
        interfaceLayer.AddChild(Dialogue);

        MapScreen = new MapScreen { Name = "MapScreen", ZIndex = 40, Visible = false };
        interfaceLayer.AddChild(MapScreen);

        InventoryScreen = new InventoryScreen { Name = "InventoryScreen", ZIndex = 45, Visible = false };
        interfaceLayer.AddChild(InventoryScreen);

        MenuFade = new ColorRect
        {
            Name = "MenuFade",
            Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight),
            Color = new Color(1, 1, 1, 0),
            ZIndex = 50,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        interfaceLayer.AddChild(MenuFade);

        DebugFlagScreen = new DebugFlagScreen
        {
            Name = "DebugFlagScreen",
            ZIndex = 110,
            Visible = false
        };
        interfaceLayer.AddChild(DebugFlagScreen);

        RoomDebug = new Label
        {
            Name = "RoomDebug",
            Position = new Vector2(2, 0),
            ZIndex = 100,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        RoomDebug.AddThemeFontSizeOverride("font_size", 8);
        RoomDebug.AddThemeColorOverride("font_color", Color.Color8(255, 248, 207));
        RoomDebug.AddThemeColorOverride("font_outline_color", Color.Color8(20, 24, 20));
        RoomDebug.AddThemeConstantOverride("outline_size", 1);
        interfaceLayer.AddChild(RoomDebug);
    }
}
