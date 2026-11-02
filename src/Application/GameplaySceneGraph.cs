
namespace Oracles;

/// <summary>
/// Typed binding for the stable gameplay hierarchy stored in gameplay.tscn.
/// Original behavior remains in the owning runtime classes; the scene owns
/// only node lifecycle, parentage, draw order, and fixed presentation values.
/// </summary>
public partial class GameplaySceneGraph : Node2D
{
    [Export] internal OracleGame Game { get; set; } = OracleGame.Ages;
    private OracleGame? _boundGame;
    internal string AssetRoot => OracleAssetPack.ForGame(Game);
    public override void _EnterTree() => BindContent();

    private void BindContent()
    {
        if (_boundGame is OracleGame bound && bound != Game)
            throw new InvalidOperationException("A prepared gameplay hierarchy cannot change content packs.");
        string root = AssetRoot;
        Unique<Hud>("Hud").AssetRoot = root;
        Unique<DialogueBox>("Dialogue").AssetRoot = root;
        Unique<MapScreen>("MapScreen").AssetRoot = root;
        Unique<InventoryScreen>("InventoryScreen").AssetRoot = root;
        Unique<RingMenuScreen>("RingMenuScreen").AssetRoot = root;
        Unique<SaveQuitScreen>("SaveQuitScreen").AssetRoot = root;
        _boundGame = Game;
    }
    internal System.Collections.Generic.IEnumerable<bool> PreparePresentation()
    {
        BindContent();
        BindNodes();
        Unique<Hud>("Hud").PrepareResources();
        yield return false;
        Unique<DialogueBox>("Dialogue").PrepareResources(AssetRoot);
        yield return false;
        Unique<MapScreen>("MapScreen").PrepareResources();
        yield return false;
        Unique<InventoryScreen>("InventoryScreen").PrepareResources();
        yield return false;
        Unique<RingMenuScreen>("RingMenuScreen").PrepareResources();
        yield return false;
        foreach (bool step in Unique<SaveQuitScreen>("SaveQuitScreen").PrepareResources())
            yield return step;
        foreach (bool step in Unique<Player>("Link").PrepareInitialPresentation(AssetRoot))
            yield return step;
    }
    public const string ScenePath = "res://scenes/gameplay.tscn";

    internal void SetCameraOrigin(Vector2 origin, int fieldTop = OracleRoomData.GameplayScreenTop)
    {
        RoomCamera.Position = origin + new Vector2(OracleRoomData.ViewportWidth / 2,
            OracleRoomData.ScreenHeight / 2 - fieldTop);
        if (RoomCamera.IsInsideTree() && RoomCamera.Enabled)
        {
            if (!RoomCamera.IsCurrent()) RoomCamera.MakeCurrent();
            RoomCamera.ForceUpdateScroll();
        }
    }

    public Node2D WorldRoot { get; private set; } = null!;
    public CanvasLayer InterfaceLayer { get; private set; } = null!;
    public RoomView RoomView { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public Camera2D RoomCamera { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    public RoomLoadColumnRevealOverlay RoomLoadReveal { get; private set; } = null!;
    public ColorRect WarpFade { get; private set; } = null!;
    public DialogueBox Dialogue { get; private set; } = null!;
    public Label RoomDebug { get; private set; } = null!;
    public MapScreen MapScreen { get; private set; } = null!;
    public InventoryScreen InventoryScreen { get; private set; } = null!;
    public SaveQuitScreen SaveQuitScreen { get; private set; } = null!;
    public RingMenuScreen RingMenuScreen { get; private set; } = null!;
    public SecretListScreen SecretListScreen { get; private set; } = null!;
    public DebugFlagScreen DebugFlagScreen { get; private set; } = null!;
    public DebugObjectSpawnerScreen DebugObjectSpawnerScreen { get; private set; } = null!;
    public ColorRect MenuFade { get; private set; } = null!;

    public override void _Ready()
    {
        BindNodes();
        if (RoomCamera.Enabled) RoomCamera.MakeCurrent();
    }

    private void BindNodes()
    {
        WorldRoot = Unique<Node2D>("World");
        InterfaceLayer = Unique<CanvasLayer>("Interface");
        RoomView = Unique<RoomView>("RoomView");
        Player = Unique<Player>("Link");
        RoomCamera = Unique<Camera2D>("RoomCamera");
        Hud = Unique<Hud>("Hud");
        RoomLoadReveal = Unique<RoomLoadColumnRevealOverlay>("RoomLoadColumnReveal");
        WarpFade = Unique<ColorRect>("RoomWarpFade");
        Dialogue = Unique<DialogueBox>("Dialogue");
        RoomDebug = Unique<Label>("RoomDebug");
        MapScreen = Unique<MapScreen>("MapScreen");
        InventoryScreen = Unique<InventoryScreen>("InventoryScreen");
        SaveQuitScreen = Unique<SaveQuitScreen>("SaveQuitScreen");
        RingMenuScreen = Unique<RingMenuScreen>("RingMenuScreen");
        SecretListScreen = Unique<SecretListScreen>("SecretListScreen");
        DebugFlagScreen = Unique<DebugFlagScreen>("DebugFlagScreen");
        DebugObjectSpawnerScreen = Unique<DebugObjectSpawnerScreen>("DebugObjectSpawnerScreen");
        MenuFade = Unique<ColorRect>("MenuFade");

        if (WorldRoot.GetParent() != this || InterfaceLayer.GetParent() != this ||
            RoomView.GetParent() != WorldRoot || Player.GetParent() != WorldRoot ||
            RoomCamera.GetParent() != WorldRoot || Hud.GetParent() != InterfaceLayer ||
            RoomLoadReveal.GetParent() != InterfaceLayer ||
            WarpFade.GetParent() != InterfaceLayer || Dialogue.GetParent() != InterfaceLayer ||
            RoomDebug.GetParent() != InterfaceLayer || MapScreen.GetParent() != InterfaceLayer ||
            InventoryScreen.GetParent() != InterfaceLayer ||
            SaveQuitScreen.GetParent() != InterfaceLayer ||
            RingMenuScreen.GetParent() != InterfaceLayer ||
            SecretListScreen.GetParent() != InterfaceLayer ||
            DebugFlagScreen.GetParent() != InterfaceLayer ||
            DebugObjectSpawnerScreen.GetParent() != InterfaceLayer ||
            MenuFade.GetParent() != InterfaceLayer)
        {
            throw new InvalidOperationException(
                $"{ScenePath} does not match the required world/interface ownership hierarchy.");
        }
    }

    internal int ApplyHudPlacement(bool bottom, bool nativeRoomUsesFullScreen)
    {
        // Original full-screen presentations can contain a HUD aperture at y=0.
        // Keep their imported layouts intact; only rearrange the gameplay field.
        bottom &= !MapScreen.Visible && !InventoryScreen.Visible &&
            !SaveQuitScreen.Visible && !RingMenuScreen.Visible && !SecretListScreen.Visible;
        // Native sprite projection retains its $10 origin even when the BG
        // uses all 144 lines. Only the room BG loses the HUD-row displacement.
        int fieldTop = bottom && !nativeRoomUsesFullScreen ? 0 : OracleRoomData.GameplayScreenTop;
        RoomView.SetNativeFullScreenPresentation(nativeRoomUsesFullScreen);
        Hud.Position = new Vector2(0, bottom ? 128 : 0);
        // Native events can temporarily own this rectangle as a full-screen
        // fade. Its captured size/position belong to that owner until release.
        if (WarpFade.Size.Y == OracleRoomData.ViewportHeight)
            WarpFade.Position = new Vector2(0, fieldTop);
        RoomLoadReveal.Position = new Vector2(0, fieldTop);
        RoomDebug.Position = new Vector2(2, nativeRoomUsesFullScreen ? 0 : fieldTop);
        DebugObjectSpawnerScreen.Position = new Vector2(0, fieldTop);
        Dialogue.SetGameplayPresentationOffset(fieldTop - OracleRoomData.GameplayScreenTop);
        return fieldTop;
    }

    private T Unique<T>(string name) where T : Node
    {
        return GetNodeOrNull<T>($"%{name}") ?? throw new InvalidOperationException(
            $"{ScenePath} is missing required unique {typeof(T).Name} node %{name}.");
    }
}
