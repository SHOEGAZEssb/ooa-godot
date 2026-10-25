using Godot;
using System;

namespace oracleofages;

internal partial class GameSelectScreen : Control
{
    private Window? _window;
    private Vector2I _previousSize;
    private Window.ContentScaleAspectEnum _previousAspect;
    private GameSelectPreview _ages = null!;
    private GameSelectPreview _seasons = null!;
    private TextureButton _agesOptions = null!;
    private TextureButton _seasonsOptions = null!;
    private TextureRect _agesCursor = null!;
    private TextureRect _seasonsCursor = null!;
    private (OracleGame Game, bool Options)? _pointerSelection;
    private OracleGame? _pointerConfirmation;
    internal OracleGame SelectedGame { get; private set; }
    internal bool OptionsSelected { get; private set; }
    internal bool IsActive => _window is not null;
    internal event Action? AgesSelected;
    internal GameSelectPreview Preview(OracleGame game) => game == OracleGame.Ages ? _ages : _seasons;

    public override void _Ready()
    {
        _ages = GetNode<GameSelectPreview>("Ages");
        _seasons = GetNode<GameSelectPreview>("Seasons");
        _agesOptions = _ages.GetNode<TextureButton>("Options");
        _seasonsOptions = _seasons.GetNode<TextureButton>("Options");
        _agesCursor = _agesOptions.GetNode<TextureRect>("Acorn");
        _seasonsCursor = _seasonsOptions.GetNode<TextureRect>("Acorn");
        _ages.SelectionRequested += RequestPointerSelection;
        _seasons.SelectionRequested += RequestPointerSelection;
        _agesOptions.MouseEntered += () => RequestOptionsSelection(OracleGame.Ages);
        _seasonsOptions.MouseEntered += () => RequestOptionsSelection(OracleGame.Seasons);
        _agesOptions.Pressed += () => RequestOptionsSelection(OracleGame.Ages);
        _seasonsOptions.Pressed += () => RequestOptionsSelection(OracleGame.Seasons);
        GetViewport().SizeChanged += ResizeToViewport;
    }

    internal void Begin()
    {
        if (IsActive) throw new InvalidOperationException("Game selection is already active.");
        _window = GetWindow();
        _previousSize = _window.ContentScaleSize;
        _previousAspect = _window.ContentScaleAspect;
        _window.ContentScaleSize = new Vector2I(480, 270);
        _window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        _pointerSelection = null;
        _pointerConfirmation = null;
        if (_agesOptions.TextureNormal is null)
        {
            Texture2D texture = SaveOptionsPresentation.BuildOptionsButton();
            _agesOptions.TextureNormal = texture;
            _seasonsOptions.TextureNormal = texture;
            MenuOamPart acorn = MenuPresentationDatabase.Shared.FileOam("acorn-cursor")[0];
            Texture2D cursor = OracleTileRenderer.GetOamCellTexture(
                OracleGraphicsCache.LoadImage("res://assets/oracle/menu/spr_fileselect_decorations.png"),
                acorn.Tile - 0x20, (byte)acorn.Attributes,
                OracleGraphicsData.LoadPalette("res://assets/oracle/menu/palette_file_sprites.bin"),
                sourceGrayscaleInverted: false);
            _agesCursor.Texture = cursor;
            _seasonsCursor.Texture = cursor;
        }
        _ages.Prepare();
        _seasons.Prepare();
        Select(OracleGame.Ages);
        Visible = true;
        ResizeToViewport();
    }

    internal void End()
    {
        Visible = false;
        if (_window is null) return;
        _window.ContentScaleSize = _previousSize;
        _window.ContentScaleAspect = _previousAspect;
        _window = null;
    }

    private void ResizeToViewport()
    {
        if (!IsActive) return;
        Size = GetViewportRect().Size;
    }

    internal void RequestPointerSelection(OracleGame game, bool confirm)
    {
        if (!IsActive) return;
        _pointerSelection = (game, false);
        if (confirm) _pointerConfirmation = game;
    }

    internal void RequestOptionsSelection(OracleGame game)
    {
        if (IsActive) _pointerSelection = (game, true);
    }

    private void Select(OracleGame game, bool options = false)
    {
        SelectedGame = game;
        OptionsSelected = options;
        _ages.SetSelected(game == OracleGame.Ages);
        _seasons.SetSelected(game == OracleGame.Seasons);
        _agesCursor.Visible = options && game == OracleGame.Ages;
        _seasonsCursor.Visible = options && game == OracleGame.Seasons;
    }

    internal void AdvanceFrame()
    {
        if (!IsActive) return;
        if (_pointerSelection is { } pointer) Select(pointer.Game, pointer.Options);
        _pointerSelection = null;
        bool up = Input.IsActionJustPressed("move_up");
        bool down = Input.IsActionJustPressed("move_down");
        if (up != down) Select(up ? OracleGame.Ages : OracleGame.Seasons, OptionsSelected);
        bool left = Input.IsActionJustPressed("move_left");
        bool right = Input.IsActionJustPressed("move_right");
        bool cancel = Input.IsActionJustPressed("item");
        bool leavingOptions = OptionsSelected && (left || cancel);
        if (left != right) Select(SelectedGame, right);
        if (cancel) Select(SelectedGame);
        Preview(SelectedGame).AdvanceFrame();
        bool confirm = _pointerConfirmation == SelectedGame ||
            Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory");
        _pointerConfirmation = null;
        if (confirm && !OptionsSelected && !leavingOptions && SelectedGame == OracleGame.Ages) AgesSelected?.Invoke();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= ResizeToViewport;
        End();
    }
}

internal enum OracleGame { Ages, Seasons }
