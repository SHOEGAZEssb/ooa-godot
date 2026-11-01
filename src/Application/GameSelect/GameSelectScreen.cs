using Godot;
using System;

namespace Oracles;

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
    private OracleGame? _optionsConfirmation;
    internal GameSelectOptions Options { get; private set; } = null!;
    internal OracleGame SelectedGame { get; private set; }
    internal bool OptionsSelected { get; private set; }
    internal bool IsActive => _window is not null;
    internal event Action? AgesSelected;
    internal event Action? SeasonsSelected;
    internal event Action<int>? SoundRequested;
    internal GameSelectPreview Preview(OracleGame game) => game == OracleGame.Ages ? _ages : _seasons;

    public override void _Ready()
    {
        _ages = GetNode<GameSelectPreview>("Ages");
        Options = GetNode<GameSelectOptions>("SaveOptions");
        _seasons = GetNode<GameSelectPreview>("Seasons");
        _agesOptions = _ages.GetNode<TextureButton>("Options");
        _seasonsOptions = _seasons.GetNode<TextureButton>("Options");
        _agesCursor = _agesOptions.GetNode<TextureRect>("Acorn");
        _seasonsCursor = _seasonsOptions.GetNode<TextureRect>("Acorn");
        _ages.SelectionRequested += RequestPointerSelection;
        _seasons.SelectionRequested += RequestPointerSelection;
        _agesOptions.MouseEntered += () => RequestOptionsSelection(OracleGame.Ages);
        _seasonsOptions.MouseEntered += () => RequestOptionsSelection(OracleGame.Seasons);
        _agesOptions.Pressed += () => RequestOptionsConfirmation(OracleGame.Ages);
        _seasonsOptions.Pressed += () => RequestOptionsConfirmation(OracleGame.Seasons);
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
        _optionsConfirmation = null;
        if (_agesOptions.TextureNormal is null)
        {
            foreach (var (game, button, marker) in new[] {
                (OracleGame.Ages, _agesOptions, _agesCursor),
                (OracleGame.Seasons, _seasonsOptions, _seasonsCursor) })
            {
                string root = OracleAssetPack.ForGame(game);
                button.TextureNormal = new NativeMenuPresentation(root).BuildOptionsButton();
                MenuOamPart acorn = FileMenuPresentation.LoadOam(root)["acorn-cursor"][0];
                marker.Texture = OracleTileRenderer.GetOamCellTexture(
                    OracleGraphicsCache.LoadImage(root + "menu/spr_fileselect_decorations.png"),
                    acorn.Tile - 0x20, (byte)acorn.Attributes,
                    OracleGraphicsData.LoadPalette(root + "menu/palette_file_sprites.bin"),
                    sourceGrayscaleInverted: false);
            }
        }
        _ages.Prepare();
        _seasons.Prepare();
        Select(OracleGame.Ages, playSound: false);
        Visible = true;
        ResizeToViewport();
    }

    internal void End()
    {
        Visible = false;
        Options.Close();
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
        if (!IsActive || Options.Visible) return;
        _pointerSelection = (game, false);
        if (confirm) _pointerConfirmation = game;
    }

    internal void RequestOptionsSelection(OracleGame game)
    {
        if (IsActive && !Options.Visible) _pointerSelection = (game, true);
    }

    private void RequestOptionsConfirmation(OracleGame game)
    {
        if (!IsActive || Options.Visible) return;
        RequestOptionsSelection(game);
        _optionsConfirmation = game;
    }

    private void Select(OracleGame game, bool options = false, bool playSound = true)
    {
        bool changed = SelectedGame != game || OptionsSelected != options;
        SelectedGame = game;
        OptionsSelected = options;
        _ages.SetSelected(game == OracleGame.Ages);
        _seasons.SetSelected(game == OracleGame.Seasons);
        _agesCursor.Visible = options && game == OracleGame.Ages;
        _seasonsCursor.Visible = options && game == OracleGame.Seasons;
        if (changed && playSound) SoundRequested?.Invoke(SoundId.SndMenuMove);
    }

    internal void AdvanceFrame()
    {
        if (!IsActive) return;
        if (Options.Visible) { Options.AdvanceFrame(); return; }
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
        bool confirm = (_pointerConfirmation == SelectedGame && !OptionsSelected) ||
            (_optionsConfirmation == SelectedGame && OptionsSelected) ||
            Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory");
        _pointerConfirmation = null;
        _optionsConfirmation = null;
        if (confirm && !leavingOptions)
        {
            SoundRequested?.Invoke(SoundId.SndSelectItem);
            if (OptionsSelected) Options.Open(SelectedGame);
            else if (SelectedGame == OracleGame.Ages) AgesSelected?.Invoke();
            else SeasonsSelected?.Invoke();
        }
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= ResizeToViewport;
        End();
    }
}

internal enum OracleGame { Ages, Seasons }
