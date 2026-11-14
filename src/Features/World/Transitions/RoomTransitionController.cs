
namespace Oracles;

public class RoomTransitionController : IDisposable
{
	private bool _disposed;
	private protected string AssetRoot => _rooms.AssetRoot;

	public event Action<Vector2I>? ScrollingTransitionFinished;
	internal event Action? WarpDestinationLoading;
	internal event Action? CommonGraphicsReloadRequested;

	public const float WarpFadeFrames = 32.0f;
	// fadeoutToWhite advances wPaletteThread_fadeOffset from $00 through $1f.
	// The 32nd handler update only stops the thread; the visible palette is
	// already completely white on update 31.
	public const float WarpFadeMaximumOffset = 31.0f;
	// fadeinFromWhite(ToRoom) starts at $20, displays $1f through
	// $00 on 32 updates, then stops on the 33rd subtraction's borrow.
	public const float WarpFadeInFrames = 33.0f;
	// applyWarpTransition2 bit 7 calls fadeoutToWhiteWithDelay(4). The palette
	// offset advances on updates 1,5,...121 and stops when it reaches $20 on 125.
	public const float DelayedWarpFadeFrames = 125.0f;
	public const float WarpLeaveFrames = 16.0f;
	public const float WarpEnterFrames = 28.0f;
	public const int RoomLoadRevealInitializationFrames = 3;
	public const int RoomLoadRevealColumnUpdates = 32;
	public const int RoomLoadRevealCompletionFrame =
		RoomLoadRevealInitializationFrames + RoomLoadRevealColumnUpdates + 1;
	public const int RoomLoadRevealVisibleColumns = 20;
	public const int RoomLoadRevealColumnWidth = 8;
	public const float FastPaletteFadeFrames = 11.0f;
	private protected readonly RoomSession _rooms;
	private protected readonly WarpDatabase _warps;
	private protected readonly RoomView _roomView;
	private readonly RoomLoadColumnRevealOverlay _roomLoadReveal;
	private protected readonly Player _player;
	private protected readonly Camera2D _camera;
	private int _gameplayScreenTop = OracleRoomData.GameplayScreenTop;
	private readonly ColorRect _warpFade;
	private protected readonly Hud _hud;
	private protected readonly DialogueBox _dialogue;
	private protected readonly RoomEntityManager _entities;
	private protected readonly DeathRespawnPointController _deathRespawnPoints;
	private protected readonly OracleSoundEngine _sound;

	private readonly RoomScrollController _scroll = new();
	private Vector2I _scrollDirection;
	private Vector2 _scrollIncomingStartOffset;
	private double _scrollTickAccumulator;
	private readonly ScreenBoundaryController _screenBoundary;
	private IPlayerScreenTransitionRoomEntity? _scrollPlayerOwner;

	private protected bool _warpActive;
	private bool _tileWarpCheckRequested;
	private Vector2? _roomPackArrival;
	private protected bool _suppressDestinationMusic;
	private WarpPhase _warpPhase;
	private protected Warp _pendingWarp;
	private bool _nativeWarpRequested;
	private protected float _warpFrame;
	private double _warpTickAccumulator;
	private float _warpFadeOutFrames = WarpFadeFrames;
	private bool _roomLoadColumnReveal;
	private int _roomLoadRevealLoadedColumns;
	private Vector2 _warpWalkStart;
	private Vector2 _warpWalkEnd;
	private protected bool _destinationWalk;
	private bool _destinationFall;
	private bool _destinationFallInitializationPending;
	private bool _standingArrivalPending;
	public bool IsTransitioning => !_disposed && (_warpActive || _scroll.Active || _roomView.IsTransitioning);
	// cutscene00 becomes cutscene01 after the first destination object
	// pass. cutscene03's func_131f already loads the complete tilemap and
	// sets screenTransitionState2/$01, so falling arrivals keep normal
	// menu dispatch during the remaining palette and collapse updates.
	internal bool GameplayMenusAvailable => !IsTransitioning ||
		_warpActive && _warpPhase == WarpPhase.FadeIn && _destinationFall &&
		!_destinationFallInitializationPending;
	internal bool AwaitingLinkWarpState => _warpActive && (_warpPhase == WarpPhase.AwaitingLinkState || _player.SubrosiaWarpPending);
	internal bool LinkArrivalActive => _warpActive && !ExtendedSequenceActive &&
		_warpPhase is WarpPhase.FadeIn or WarpPhase.RoomLoadColumnReveal or WarpPhase.EnterScreen;
	internal bool DeathUpdatesSuspendedByWarp => _warpActive && !AwaitingLinkWarpState;
	internal bool SuppressesDestinationMusic => _warpActive && _suppressDestinationMusic;
	public bool ScrollActive => _scroll.Active;
	internal bool RoomExitPending => !_disposed && !IsTransitioning &&
		(_entities.RuntimeState.ReadField("wScrollMode") & 4) != 0;
	internal bool RoomPackTransitionActive => _roomPackArrival is not null;
	internal bool UsesOwners(RoomSession rooms, RoomEntityManager entities) =>
		!_disposed && ReferenceEquals(_rooms,rooms) && ReferenceEquals(_entities,entities);
	public Vector2I ScrollDirection => _scrollDirection;
	internal Vector2I ScreenEntryDirection =>
		_scroll.Active || _roomPackArrival is not null ? _scrollDirection : Vector2I.Zero;
	internal Vector2 ScrollLinkPositionInDestination =>
		_scroll.LinkPosition + _scroll.FinishOffset;
	public float ScrollDistance => _scroll.Distance;
	public int ScrollFrames => _scroll.Frames;
	internal int ScrollTotalFrames => _scroll.TotalFrames;
	internal int ScreenTransitionDelay => _screenBoundary.Delay;
	internal int ActiveWarpDestinationPosition =>
		_warpActive ? _pendingWarp.DestinationPosition : -1;
	internal Func<bool> ScreenTransitionsDisabledSource { get; set; } =
		static () => false;
	internal Func<bool> AllScreenTransitionsDisabledSource { get; set; } =
		static () => false;
	// Palette-thread lifetime, including its terminal update. A held black
	// screen or a landing animation after fade-in is not an active fade.
	private int _nativePaletteDirection, _nativePaletteOffset, _nativePaletteDelay = 1, _nativePaletteCounter = 1;
	private Color _nativePaletteColor = Colors.White;
	private int _nativePaletteSpeed = 1;
	private Color[,]? _nativeBackgroundFadeSource;
	private OracleRoomData? _nativeBackgroundFadeRoom;
	private OracleRoomData? _nativeDarkeningRoom;
	private int _nativeDarkeningOffset;
	private int _nativeRoomColorTarget;
	private Color[,]? _nativeMaskedBackground;
	private IReadOnlyDictionary<int,Color[]>? _nativeMaskedSprites;
	private int _nativeBackgroundMask,_nativeSpriteMask;
	internal void BeginNativeMaskedFade(Color color,bool fadeOut,int backgroundMask,int spriteMask,
		Color[,] background,IReadOnlyDictionary<int,Color[]> sprites,int delay=1,int speed=1)
	{
		BeginNativePaletteFade(color,fadeOut,delay,speed);
		_nativeMaskedBackground=background;_nativeMaskedSprites=sprites;
		_nativeBackgroundMask=backgroundMask;_nativeSpriteMask=spriteMask;
		_warpFade.Color=new Color(color,0);
	}
	private void PublishNativeMaskedFade()
	{
		if(_nativeMaskedBackground is null)return;
		int offset=Math.Clamp(_nativePaletteOffset,0,32)*(_nativePaletteColor==Colors.Black?-1:1);
		Color Blend(Color c)=>new(Math.Clamp(Mathf.RoundToInt(c.R*31)+offset,0,31)/31f,
			Math.Clamp(Mathf.RoundToInt(c.G*31)+offset,0,31)/31f,Math.Clamp(Mathf.RoundToInt(c.B*31)+offset,0,31)/31f,c.A);
		var bg=_rooms.BackgroundPalettes.Capture();
		for(int palette=0;palette<8;palette++)if((_nativeBackgroundMask&(1<<palette))!=0)
			for(int shade=0;shade<4;shade++)bg[palette,shade]=Blend(_nativeMaskedBackground[palette,shade]);
		_rooms.BackgroundPalettes.Restore(bg);
		var sprites=new Dictionary<int,Color[]>(_entities.NativeObjectPalettes??new Dictionary<int,Color[]>());
		foreach(var entry in _nativeMaskedSprites!)if((_nativeSpriteMask&(1<<entry.Key))!=0)
		{var colors=new Color[4];for(int shade=0;shade<4;shade++)colors[shade]=Blend(entry.Value[shade]);sprites[entry.Key]=colors;}
		_entities.SetNativeObjectPalette(sprites);
	}
	internal Vector2? NativeCameraFocus { get; set; }
	internal bool CutsceneCameraFrozen {get;set;}
	// Native graphics states with the status window off-screen render the
	// room BG at display y=0 while retaining the original OBJ draw origin.
	internal bool NativeRoomUsesFullScreen {get;set;}
	internal bool NativePaletteFadeActive => _nativePaletteDirection != 0 || _nativeDarkeningRoom is not null;
	internal int NativePaletteThreadMode=>_nativeDarkeningRoom is not null ? (_nativeRoomColorTarget == 0 ? 6 : 5) : _nativePaletteDirection==0?0:
		(_nativePaletteColor==Colors.Black?(_nativePaletteDirection>0?3:4):(_nativePaletteDirection>0?1:2))|(_nativePaletteDelay>1?8:0);
	internal void BeginNativeWhiteFade(bool fadeOut, int delay = 1, int speed = 1)
		=> BeginNativePaletteFade(Colors.White, fadeOut, delay, speed);
	internal void BeginNativeLightRoomDarkening()
		=> BeginNativeRoomDarkening(-9); // darkenRoomLightly: target $f7.
	internal void BeginNativeRoomDarkening(int offset)
	{
		if(offset is < -31 or >= 0)throw new ArgumentOutOfRangeException(nameof(offset));
		// bank0._darkenRoomHelper: BG 2-7 only, speed $01.
		_nativePaletteDirection = 0;
		_nativeDarkeningRoom = _rooms.CurrentRoom;
		_nativeDarkeningOffset = 0;
		_nativeRoomColorTarget = offset;
		_nativeDarkeningRoom.SetTemporaryBackgroundPaletteOffset(0);
	}
	internal void BeginNativeRoomBrightening()
	{
		_nativePaletteDirection = 0;
		_nativeDarkeningRoom = _rooms.CurrentRoom;
		_nativeRoomColorTarget = 0;
	}
	internal void AdvanceNativeRoomColorFade(OracleRoomData room)
	{ if (ReferenceEquals(room, _nativeDarkeningRoom)) AdvanceNativeWhiteFade(); }
	internal void CancelNativeRoomColorFade(OracleRoomData room)
	{ if (ReferenceEquals(room, _nativeDarkeningRoom)) _nativeDarkeningRoom = null; }
	internal void BeginNativeBlackFade(bool fadeOut, int speed = 1,int delay=1)
		=> BeginNativePaletteFade(Colors.Black, fadeOut,delay:delay,speed: speed);
	internal void BeginNativeBackgroundBlackFade(int delay)
	{
		BeginNativeBlackFade(true,delay:delay);
		// ganonRevivalCutscene_controller clears the sprite-source mask.
		// Keep bright enemy OAM while the shared palette thread darkens BG.
		_nativeBackgroundFadeRoom=_rooms.CurrentRoom;
		_nativeBackgroundFadeSource=_nativeBackgroundFadeRoom.BackgroundPalettes.Capture();
	}
	private void RestoreNativeBackgroundFade()
	{
		if(_nativeBackgroundFadeSource is not null)_nativeBackgroundFadeRoom!.BackgroundPalettes.Restore(_nativeBackgroundFadeSource);
		_nativeBackgroundFadeSource=null;_nativeBackgroundFadeRoom=null;
	}
	private void BeginNativePaletteFade(Color color, bool fadeOut, int delay = 1, int speed = 1)
	{
		if (delay < 1) throw new ArgumentOutOfRangeException(nameof(delay));
		if (speed < 1) throw new ArgumentOutOfRangeException(nameof(speed));
		RestoreNativeBackgroundFade();_nativeDarkeningRoom = null;
		_nativeMaskedBackground=null;_nativeMaskedSprites=null;
		_nativePaletteColor = color;
		_nativePaletteSpeed = speed;
		_nativePaletteDirection = fadeOut ? 1 : -1;
		_nativePaletteOffset = fadeOut ? 0 : 32;
		// setPaletteThreadDelay starts at one, then refills with the delay.
		_nativePaletteDelay = delay; _nativePaletteCounter = 1;
	}
	internal void AdvanceNativeWhiteFade()
	{
		if (_nativeDarkeningRoom is { } room)
		{
			if (_nativeDarkeningOffset == _nativeRoomColorTarget) { _nativeDarkeningRoom = null; return; }
			_nativeDarkeningOffset += Math.Sign(_nativeRoomColorTarget - _nativeDarkeningOffset);
			// bank1.paletteFadeHandler06 stops at the target in Ages;
			// Seasons writes the target and stops on the following update.
			if (_nativeRoomColorTarget == 0 && _nativeDarkeningOffset == 0 && _rooms.SaveData.Game == OracleGame.Ages)
				_nativeDarkeningRoom = null;
			room.SetTemporaryBackgroundPaletteOffset(_nativeDarkeningOffset);
			return;
		}
		if (_nativePaletteDirection == 0) return;
		if (--_nativePaletteCounter != 0) return;
		_nativePaletteCounter = _nativePaletteDelay;
		_nativePaletteOffset += _nativePaletteDirection * _nativePaletteSpeed;
		if(_nativeBackgroundFadeSource is not null)
		{
			var colors=(Color[,])_nativeBackgroundFadeSource.Clone();
			float offset=Math.Clamp(_nativePaletteOffset,0,32)/31f;
			for(int palette=0;palette<8;palette++)for(int shade=0;shade<4;shade++)
			{var c=colors[palette,shade];colors[palette,shade]=new(Math.Max(0,c.R-offset),Math.Max(0,c.G-offset),Math.Max(0,c.B-offset),c.A);}
			_nativeBackgroundFadeRoom!.BackgroundPalettes.Restore(colors);
		}
		// Black fades use the native negative offset ($e0..$ff). Fade-in
		// stops on carry to $00; white fade-in stops below zero instead.
		bool finished = _nativePaletteColor == Colors.Black
			? _nativePaletteOffset > 32 || _nativePaletteOffset <= 0
			: _nativePaletteOffset >= 32 || _nativePaletteOffset < 0;
		if (finished)
		{
			_nativePaletteDirection = 0;
			return;
		}
		PublishNativeMaskedFade();
		if(_nativeBackgroundFadeSource is null&&_nativeMaskedBackground is null)_warpFade.Color = new Color(_nativePaletteColor, Math.Min(_nativePaletteOffset, 31) / 31f);
	}
	internal void CancelNativeWhiteFade(bool clearPresentation = false)
	{
		RestoreNativeBackgroundFade();
		_nativePaletteDirection = 0;
		_nativeDarkeningRoom = null;
		_nativeMaskedBackground=null;_nativeMaskedSprites=null;
		if (clearPresentation) SetFade(0);
	}
	internal virtual bool PaletteFadeActive => NativePaletteFadeActive || _warpActive && (_warpPhase switch
	{
		WarpPhase.FadeOut => _warpFrame < _warpFadeOutFrames,
		WarpPhase.FadeIn => _warpFrame < WarpFadeInFrames,
		_ => false
	});
	internal bool RoomLoadColumnRevealActive => _roomLoadColumnReveal;
	internal int RoomLoadRevealLoadedColumns => _roomLoadRevealLoadedColumns;
	public RoomTransitionController(
		RoomSession rooms,
		WarpDatabase warps,
		RoomView roomView,
		RoomLoadColumnRevealOverlay roomLoadReveal,
		Player player,
		Camera2D camera,
		ColorRect warpFade,
		Hud hud,
		DialogueBox dialogue,
		RoomEntityManager entities,
		DeathRespawnPointController deathRespawnPoints,
		OracleSoundEngine sound)
	{
		if (rooms.AssetRoot != warps.AssetRoot || rooms.AssetRoot != entities.AssetRoot ||
			!ReferenceEquals(rooms.RuntimeState,entities.RuntimeState) || !ReferenceEquals(rooms.SaveData,entities.SaveData))
			throw new ArgumentException("Transitions require the selected pack and authoritative room, save and runtime owners.");
		_rooms = rooms;
		_warps = warps;
		_roomView = roomView;
		_roomLoadReveal = roomLoadReveal;
		_player = player;
		_camera = camera;
		_warpFade = warpFade;
		_hud = hud;
		_dialogue = dialogue;
		_entities = entities;
		_screenBoundary = new(entities.RuntimeState,
			() => AllScreenTransitionsDisabledSource() || ScreenTransitionsDisabledSource());
		_entities.PaletteFadeActiveSource = () => PaletteFadeActive;
		_deathRespawnPoints = deathRespawnPoints;
		_sound = sound;
		ResetLoadedScrollGraphics();
		_rooms.RoomChanged += RoomChanged;

	}
	private void RoomChanged(int group, OracleRoomData room)
	{
		if (_disposed) return;
		if (!_scroll.Active) ResetLoadedScrollGraphics();
	}

	public virtual void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_rooms.RoomChanged -= RoomChanged;
		_scroll.Cancel();
		_player.CancelHoleWarp();
		if (_player.SubrosiaWarpActive)
		{
			_player.CancelSubrosiaWarp(); _roomView.ClearHorizontalWave();
			CancelNativeWhiteFade(clearPresentation: true);
		}
		_warpActive = false;
		_roomPackArrival = null;
		_nativeWarpRequested = false;
		_tileWarpCheckRequested = false;
		_standingArrivalPending = false;
		_nativePaletteDirection = 0;
		_nativeDarkeningRoom = null;
		_nativeBackgroundFadeRoom = null;
		_nativeBackgroundFadeSource = null;
		_nativeMaskedBackground = null;
		_nativeMaskedSprites = null;
		ScrollingTransitionFinished = null;
		WarpDestinationLoading = null;
		CommonGraphicsReloadRequested = null;
	}
	private protected void RequireActive() => ObjectDisposedException.ThrowIf(_disposed,this);

	private protected virtual bool ExtendedSequenceActive => false;
	private protected virtual bool RetainsDestinationLinkPosition => _roomPackArrival is not null;
	internal string PhaseName => _warpPhase.ToString();
	private protected virtual void UpdateContinuingEffects() { }
	private protected virtual void OnOrdinaryWarpStarting() { }
	private protected virtual void BeforeDestinationLoad() { }
	private protected virtual void BeforeDestinationLayout(Warp warp) { }
	private protected virtual OracleRoomData LoadDestinationRoom(Warp warp) =>
		_rooms.Load(warp.DestinationGroup,warp.DestinationRoom);
	private protected virtual void InitializeDestinationEntities(OracleRoomData room, Warp warp)
	{
		// bank4.applyWarpDest_b04 installs the unadjusted packed position
		// before initializeRoom. Link's warp handler changes facing and
		// applies entrance/fall/$0e offsets only after room code has run.
		if (RetainsDestinationLinkPosition)
			_deathRespawnPoints.RecordRoomInitialization();
		else
			_deathRespawnPoints.RecordRoomInitialization(
				new Vector2((warp.DestinationPosition & 0x0f) * 16 + 8,
					(warp.DestinationPosition >> 4) * 16 + 8), facing: 0);
		// bank1.loadingRoom leaves bit $02 set through destination state0.
		_entities.RuntimeState.SetField("wScrollMode", 2);
		_entities.LoadRoom(
			_rooms.ActiveGroup, room,
			EnemyPlacementContext.FromWarpDestination(warp.DestinationPosition),
			retainReservedInteractions: _roomPackArrival is not null);
	}
	private protected virtual void BeginDestinationPresentation(Vector2 spawn)
	{
		_warpPhase = _roomLoadColumnReveal ? WarpPhase.RoomLoadColumnReveal : WarpPhase.FadeIn;
	}
	private protected virtual void OnWarpFinished() { }
	private protected virtual bool TryGetInteractionTileWarp(Player player, Vector2 point, out Warp warp)
	{ warp = default; return false; }
	private protected virtual bool TryResolveTileWarp(OracleRoomData room, int position, byte tile, out Warp warp, out bool staircaseCue)
	{
		staircaseCue = false;
		return _warps.TryGetTileWarp(_rooms.ActiveGroup,room,position,tile,out warp);
	}
	private protected virtual bool TryGetSpecialHoleWarp(int packedPosition, out Warp warp)
	{ warp = default; return false; }
	internal virtual bool TryGetScreenTransitionDestination(Vector2I direction, out int targetId) =>
		_rooms.TryGetNeighbor(direction,out targetId);
	private protected virtual void CommitScreenTransition(Vector2I direction) { }
	private protected virtual void PrepareRoomPackTransition(int targetId) { }

	public void Update(double delta)
	{
		UpdateWarpAndEffects(delta);
		UpdateScroll(delta);
	}

	internal void UpdateWarpAndEffects(double delta)
	{
		UpdateContinuingEffects();
		UpdateWarp(delta);
		// updateAllObjects calls updateCamera once after Link and the other
		// objects. Ordinary gameplay receives that sample from GameRoot's
		// post-object pass; active warps do not reach that pass, so retain the
		// same single sample here while their Link state is moving.
		if (_warpActive)
			UpdateCamera();
	}

	internal void CompleteHoleWarpUpdate()
	{
		if(_disposed || !_warpActive || _warpPhase!=WarpPhase.HoleFall ||
			_entities.RuntimeState.ReadField("wWarpTransition2")==0)return;
		// cutscene01 consumes Link's request after this update's object pass.
		_entities.RuntimeState.SetField("wWarpTransition2",0);
		_warpPhase=WarpPhase.FadeOut;_warpFrame=0;
		SetFade(0);
	}

	internal bool AdvanceFadeOutWarpBeforeObjects(double delta)
	{
		// CUTSCENE_03 returns while the palette thread is active. Its
		// terminal update loads/initializes the destination and returns too;
		// cutscene00 owns the first destination object pass on the next tick.
		if (!_warpActive || ExtendedSequenceActive || _warpPhase != WarpPhase.FadeOut)
			return false;
		UpdateWarpAndEffects(delta);
		return true;
	}

	internal bool AdvanceStandingArrivalPaletteBeforeObjects(double delta)
	{
		// The palette thread precedes cutscene01/updateAllObjects. Basic
		// arrivals initialize Link during the fade; its terminal borrow
		// therefore permits the first normal Link update on this same tick.
		if (!_warpActive || _warpPhase != WarpPhase.FadeIn || _destinationWalk ||
			_destinationFall || _roomPackArrival is not null || ExtendedSequenceActive ||
			_warps.HandlerFor(_pendingWarp.DestinationTransition) is not (WarpTransitionHandler.Standing or
				WarpTransitionHandler.SetRespawn or WarpTransitionHandler.Instant or WarpTransitionHandler.KeepFacing or
				WarpTransitionHandler.ParameterFacing or WarpTransitionHandler.Shifted or WarpTransitionHandler.Walk))
			return false;
		UpdateWarpAndEffects(delta);
		return true;
	}

	internal void HandOffArrivalPaletteToMenu()
	{
		if (!_warpActive || _warpPhase != WarpPhase.FadeIn || !_destinationFall)
			return;
		// openMenu replaces the single native palette thread. Its closing
		// fastFadeinFromWhiteToRoom finishes that replacement; the interrupted
		// ordinary arrival fade is never resumed. Link's fall remains intact.
		_warpFrame = Mathf.Max(_warpFrame, WarpFadeInFrames);
		SetFade(0.0f);
	}

	internal void BeginLinkUpdate()
	{
		_tileWarpCheckRequested = false;
		if (!_standingArrivalPending) return;
		_standingArrivalPending = false;
		WarpTransitionHandler handler = _warps.HandlerFor(_pendingWarp.DestinationTransition);
		if (handler == WarpTransitionHandler.Shifted)
			_player.SetScriptedPosition(_player.PrecisePosition - new Vector2(8,0));
		if (handler == WarpTransitionHandler.ParameterFacing)
			_player.Face((Vector2I)OracleObjectMath.StrictCardinalVector((_entities.RuntimeState.ReadField("wcc50") & 3) * 8));
		else if (handler is not (WarpTransitionHandler.KeepFacing or WarpTransitionHandler.Invisible))
			_player.Face(_warps.FacingDirection(_rooms.CurrentRoom.ActiveCollisions,_rooms.CurrentRoom.GetMetatile(_player.PrecisePosition)));
		if (handler is WarpTransitionHandler.SetRespawn or WarpTransitionHandler.Shifted)
			_deathRespawnPoints.RecordWarpDestination(_pendingWarp.DestinationTransition);
	}

	internal void RequestTileWarpCheck() => _tileWarpCheckRequested = true;

	internal bool CheckRequestedTileWarp(Player player)
	{
		bool requested = _tileWarpCheckRequested;
		_tileWarpCheckRequested = false;
		return requested && CheckTileWarp(player);
	}

	public bool CheckTileWarp(Player player)
	{
		if (_warpActive || _scroll.Active) return false;
		if (_entities.NativeWarpTilesDisabled) return false;
		OracleRoomData room = _rooms.CurrentRoom;
		Vector2 linkPosition = OracleObjectMath.ToPixelPosition(player.Position);
		bool hasInteractionWarp = TryGetInteractionTileWarp(player,linkPosition,out Warp interactionWarp);
		if (!hasInteractionWarp && (player.TopDownAirborne || _dialogue.IsOpen ||
			_entities.RuntimeState.ReadField("wWarpsDisabled") != 0))
			return false;
		Vector2 standingPoint = linkPosition + new Vector2(0, 4);
		int position = room.GetPackedPosition(standingPoint);
		byte tile = room.GetMetatile(standingPoint);
		int deactivatedWarp = _entities.RuntimeState.ReadField("wEnteredWarpPosition");
		if (deactivatedWarp != 0xff)
		{
			if (deactivatedWarp == position ||
				(deactivatedWarp - 1 == position &&
					_warps.IsWarpTile(room.ActiveCollisions, tile)))
			{
				return false;
			}
		}

		if (deactivatedWarp != 0xff)
			ClearDeactivatedWarp();

		// Interaction-owned travel remains independent of tile-source lookup.
		if (hasInteractionWarp)
		{
			ApplyWarp(player, interactionWarp);
			return true;
		}

		// Unlike func_60e9's earlier wLinkInAir check, checkTileWarps rejects
		// nonzero Link.zh and wLinkGrabState after updating the entered-warp
		// marker. A later hook/item pass can
		// change Z after Link's air-state update. Screen edges are separate.
		if (player.TopDownAirZ != 0 || _entities.MenuDisablesWarpTiles)
			return FinishUnmatchedTileWarpCheck();

		bool chimney = _warps.TryGetWarpTileParameter(room.ActiveCollisions,tile,out byte parameter) && parameter == 1;
		if (chimney)
		{
			// checkTileWarps@chimney bypasses grab/centering after clearing the native parents and damage block.
			player.ClearNativeItemParents();
			player.DropHeldItemsForScript();
			player.ResetLinkInvincibility();
		}
		else if (player.GrabStateActive) return FinishUnmatchedTileWarpCheck();

		if (!TryResolveTileWarp(room,position,tile,out Warp warp,out bool staircaseCue))
			return FinishUnmatchedTileWarpCheck();
		if (!chimney && !LinkWithinTileWarpBounds(
				room, position, linkPosition))
		{
			return FinishUnmatchedTileWarpCheck();
		}
		if (staircaseCue)
			_sound.PlaySound(SoundId.SndEnterCave);
		ApplyWarp(player, warp);
		return true;
	}

	private bool FinishUnmatchedTileWarpCheck()
	{
		// checkWarpsTopDown falls through to checkScreenEdgeWarps even when
		// checkTileWarps rejects height/menu/grab or finds no tile source.
		// Its $ff write precedes findScreenEdgeWarpSource's scroll-mode gate.
		// An actual boundary match is resolved by CheckRoomExit below.
		_entities.RuntimeState.SetField("wTmpcec0", 0xff);
		return false;
	}

	private bool TrySelectScreenEdgeWarp(
		OracleRoomData room, Vector2I direction, Vector2 position, out Warp warp)
	{
		_entities.RuntimeState.SetField("wTmpcec0", 0xff);
		if (!_warps.TryGetEdgeWarp(_rooms.ActiveGroup, room.Id, direction,
				position, out warp))
			return false;
		// bank4.findScreenEdgeWarpSource @foundWarpSource clears the byte.
		_entities.RuntimeState.SetField("wTmpcec0", 0);
		return true;
	}

	/// <summary>
	/// Mirrors wEnteredWarpPosition. Hazard respawns write Link's object
	/// position before the invisible delay so a safe anchor on a stair or
	/// doorway cannot immediately initiate another warp.
	/// </summary>
	public void DeactivateWarpAtPlayerPosition(Player player)
	{
		OracleRoomData room = _rooms.CurrentRoom;
		Vector2 linkPosition =
			OracleObjectMath.ToPixelPosition(player.Position);
		_entities.RuntimeState.SetField("wEnteredWarpPosition", (byte)room.GetPackedPosition(linkPosition));
	}

	/// <summary>
	/// Mirrors checkLinkCloseEnoughToWarpTileCenter and its horizontally
	/// adjacent multi-tile-door branch. The original tests Link's high-byte
	/// coordinates after selecting the standing tile at yh+4.
	/// </summary>
	internal static bool LinkWithinTileWarpBounds(
		OracleRoomData room,
		int packedPosition,
		Vector2 linkPosition)
	{
		byte collision = room.GetTerrainInfo(
			PackedPositionCenter(packedPosition)).Collision;
		int yOffset = collision == 0 ? 4 : 2;
		if (!WarpAxisWithinCenter(linkPosition.Y, yOffset))
			return false;

		bool multiTileDoor =
			WarpDatabase.For(room.AssetRoot).IsWarpTile(
				room.ActiveCollisions,
				MetatileAtPackedPosition(room, packedPosition + 1)) ||
			WarpDatabase.For(room.AssetRoot).IsWarpTile(
				room.ActiveCollisions,
				MetatileAtPackedPosition(room, packedPosition - 1));
		return collision != 0 || multiTileDoor ||
			WarpAxisWithinCenter(linkPosition.X, 0);
	}

	private static bool WarpAxisWithinCenter(float coordinate, int offset)
	{
		int withinTile = (Mathf.FloorToInt(coordinate) + offset) & 0x0f;
		return withinTile is >= 4 and <= 13;
	}

	private static byte MetatileAtPackedPosition(
		OracleRoomData room,
		int packedPosition)
	{
		int x = packedPosition & 0x0f;
		int y = packedPosition >> 4;
		if (x < 0 || x >= room.WidthInTiles ||
			y < 0 || y >= room.HeightInTiles)
		{
			return 0xff;
		}
		return room.GetMetatile(PackedPositionCenter(packedPosition));
	}

	private static Vector2 PackedPositionCenter(int packedPosition) => new(
		(packedPosition & 0x0f) * OracleRoomData.MetatileSize +
			OracleRoomData.MetatileSize / 2.0f,
		(packedPosition >> 4) * OracleRoomData.MetatileSize +
			OracleRoomData.MetatileSize / 2.0f);

	public void CheckRoomExit(Player player)
	{
		SampleRoomExit(player);
		CommitRoomExit(player);
	}

	internal void CommitRoomExit(Player player)
	{
		if (_disposed || IsTransitioning)
			return;
		if (_entities.RuntimeState.ReadField("wScrollMode") == 0)
		{
			// bank1.func_4000 omits the boundary pass. cutscene01 still
			// checks screen-edge warp sources, whose scroll gate rejects it.
			FinishUnmatchedTileWarpCheck();
			return;
		}

		OracleRoomData room = _rooms.CurrentRoom;
		Vector2 pixel = OracleObjectMath.ToPixelPosition(
			_entities.PlayerScreenTransitionOwner?.ScreenTransitionPosition ?? player.PrecisePosition);
		Vector2I vertical = pixel.Y <= 6 ? Vector2I.Up
			: pixel.Y >= room.Height - 7 ? Vector2I.Down : Vector2I.Zero;
		// cutscene01 checks edge warps after updateAllObjects' boundary pass,
		// but before consuming getNextActiveRoom's retained direction.
		if (!AllScreenTransitionsDisabledSource() && vertical != Vector2I.Zero &&
			TrySelectScreenEdgeWarp(room, vertical, pixel, out Warp warp))
		{
			_screenBoundary.ClearDelay();
			ApplyWarp(player, warp);
			return;
		}
		Vector2I direction = RoomExitPending ? _screenBoundary.PendingDirection : Vector2I.Zero;
		if (direction == Vector2I.Zero ||
			!TryGetScreenTransitionDestination(direction, out int targetId) ||
			!_rooms.HasRoom(_rooms.ActiveGroup, targetId))
			return;
		BeginScroll(player, direction, targetId);
		_hud.Refresh();
	}

	// bank0.updateAllObjects calls bank1.screenTransitionState2 before
	// cutscene01 selects a toggle. Its clamp and state3 request survive
	// cutscene02; only the later getNextActiveRoom starts destination loading.
	internal void SampleRoomExit(Player player)
	{
		if (_disposed || IsTransitioning || RoomExitPending) return;
		_screenBoundary.Sample(player, _rooms.CurrentRoom,
			_entities.PlayerScreenTransitionOwner);
	}

	public bool HasNeighborFor(Vector2 point)
	{
		OracleRoomData room = _rooms.CurrentRoom;
		Vector2I direction = point.X < 0 ? Vector2I.Left
			: point.X >= room.Width ? Vector2I.Right
			: point.Y < 0 ? Vector2I.Up
			: Vector2I.Down;
		bool hasEdgeWarp = _warps.TryGetEdgeWarp(
			_rooms.ActiveGroup,
			room.Id,
			direction,
			point,
			out _);
		return hasEdgeWarp ||
			(TryGetScreenTransitionDestination(direction, out int id) &&
				_rooms.HasRoom(_rooms.ActiveGroup, id));
	}

	internal void BeginLedgeScroll(Player player)
	{
		RequireActive();
		const int sourceDirection = 0x82;
		if (IsTransitioning)
		{
			throw new InvalidOperationException(
				"LINK_STATE_JUMPING_DOWN_LEDGE tried to start source " +
				$"transition ${sourceDirection:x2} during another transition.");
		}
		// linkState12 publishes after its landing calculation. The later
		// object-tail boundary pass consumes bit 7 before room loading.
		_entities.RuntimeState.SetField("wScreenTransitionDirection",sourceDirection);
	}



	public void BeginScroll(Player player, Vector2I direction, int targetId)
	{
		RequireActive();
		_screenBoundary.Request(direction);
		CommitScreenTransition(direction);
		using var diagnostics = OracleRoomLoadDiagnostics.Begin("scroll", _rooms.ActiveGroup, targetId);
		OracleRoomData source = _rooms.CurrentRoom;
		if (_rooms.RequiresRoomPackFade(_rooms.ActiveGroup, source.Id, targetId))
		{
			PrepareRoomPackTransition(targetId);
			// cutscene01 -> checkRoomPack -> CUTSCENE_05 performs a full
			// reload. func_4493 adds byte offsets to the clamped coordinates,
			// preserving the orthogonal coordinate and both subpixels.
			Vector2 arrival = player.PrecisePosition;
			if (direction.X != 0)
				arrival.X = (direction.X > 0 ? 10 : 150) + arrival.X - Mathf.Floor(arrival.X);
			else
				arrival.Y = (direction.Y > 0 ? 9 : 118) + arrival.Y - Mathf.Floor(arrival.Y);
			var warp = new Warp(_rooms.ActiveGroup, source.Id, -1, 0, 0,
				_rooms.ActiveGroup, targetId,
				((Mathf.FloorToInt(arrival.Y) >> 4) << 4) | (Mathf.FloorToInt(arrival.X) >> 4),
				0, WarpDestinationTransition.Basic);
			BeginWarp(player, warp, false, forceFadeOut: true);
			_roomPackArrival = arrival;
			_scrollDirection = direction;
			return;
		}
		// Coalesce only the synchronous setup's texture publications; the
		// screenTransitionState3-5 graphics schedule remains in UpdateScroll.
		// palette/VRAM writes, native initialization and RNG keep their order.
		using var texturePublication = _rooms.DeferTexturePublication();
		Image sourceGraphics = source.CaptureLiveGraphics();
		Color[,] sourceColors = source.BackgroundPalettes.Capture();
		diagnostics?.Mark("source-snapshot");
		OracleRoomData target = _rooms.GetRoom(_rooms.ActiveGroup, targetId);
		diagnostics?.Mark("destination-data");
		IPlayerScreenTransitionRoomEntity? transitionOwner =
			_entities.PlayerScreenTransitionOwner;
		UpdateCamera();
		Vector2 sourceCameraOrigin = CurrentCameraOrigin;
		Vector2 start =
			transitionOwner?.ScreenTransitionPosition ??
			player.PrecisePosition;
		_scrollDirection = direction;
		_scrollTickAccumulator = 0;
		_scroll.Begin(start, direction, source, target);

		Vector2 transitionEnd = _scroll.DestinationPosition;
		Vector2 destinationCameraOrigin = GetCameraOrigin(target, transitionEnd);
		_scrollIncomingStartOffset = sourceCameraOrigin - destinationCameraOrigin +
			(Vector2)direction * _scroll.Distance;
		_entities.ReservedKeyDoor?.BeginScreenTransition();
		_entities.ReservedPushBlock?.BeginScreenTransition();
		_entities.RuntimeState.SetField("wScrollMode",8);
		_rooms.SetLoadedRoom(_rooms.ActiveGroup, target, updateMinimap: false);
		// screenTransitionState2 writes the edge-clamped high coordinate
		// before destination object initialization observes Link.
		if (transitionOwner is null)
			player.SetScriptedPosition(start);
		_deathRespawnPoints.RecordRoomInitialization();
		_roomView.StartScreenTransition(
			target.Texture, direction, _scroll.Distance, sourceCameraOrigin, destinationCameraOrigin);
		_entities.BeginScreenTransition(
			_rooms.ActiveGroup, target, _scrollIncomingStartOffset, direction,
			target.GetPackedPosition(transitionEnd), player);
		diagnostics?.Mark("destination-objects");
		_scroll.BindGraphics(source, target, sourceGraphics, sourceColors, _rooms.SaveData,
			_entities.RuntimeState.ReadField("wRoomStateModifier"));
		diagnostics?.Mark("transition-graphics");
		_scrollPlayerOwner = transitionOwner;
		_scrollPlayerOwner?.BeginScreenTransition(target);
		// State0 may change either coordinate (platform contact), not only
		// the orthogonal one (soldierSubid05 writes w1Link.xh=$50).
		// transitionUpdateScrollAndLinkPosition adds to the resulting live
		// coordinates; it does not restore the pre-initialization position.
		if (_scrollPlayerOwner is null)
			start = player.PrecisePosition;
		_scroll.SetStart(start);
		if (_scrollPlayerOwner is null)
		{
			player.BeginScrollingTransition(start);
		}
		else
		{
			_scrollPlayerOwner.SetScreenTransitionPosition(
				start, Vector2.Zero, player);
		}
	}

	public void UpdateScroll(double delta)
	{
		if (_disposed) return;
		_scrollTickAccumulator += delta * 60.0;
		while (_scroll.Active && _scrollTickAccumulator + 0.000001 >= 1.0)
		{
			_scrollTickAccumulator -= 1.0;
			AdvanceScrollUpdate();
		}
		if (!_scroll.Active)
			_scrollTickAccumulator = 0;
	}

	private void ResetLoadedScrollGraphics()
	{
		_scroll.ResetGraphics(_rooms.CurrentRoom);
	}

	private void AdvanceScrollUpdate()
	{
		if (!_scroll.Active)
			return;
		bool positionChanged = _scroll.Advance();
		var phase = _scroll.NativePhase;
		_entities.RuntimeState.SetField("wScreenTransitionState",phase.State);
		_entities.RuntimeState.SetField("wScreenTransitionState2",phase.Substate);
		_entities.RuntimeState.SetField("wScreenTransitionState3",phase.Step);
		if (!_scroll.Active) _entities.RuntimeState.SetField("wScrollMode",1);
		if (!positionChanged) return;
		Vector2 linkPosition = _scroll.LinkPosition;
		float scrollPixels = _scroll.Pixels;
		Vector2 screenScroll = new(_scrollDirection.X * scrollPixels, _scrollDirection.Y * scrollPixels);
		_roomView.SetTransitionFrame(_scroll.Frame);
		_entities.SetScreenTransitionOffsets(-screenScroll, _scrollIncomingStartOffset - screenScroll);
		if (_scrollPlayerOwner is null)
		{
			_player.SetScrollingTransitionPosition(
				linkPosition, screenScroll);
		}
		else
		{
			_scrollPlayerOwner.SetScreenTransitionPosition(
				linkPosition, -screenScroll, _player);
		}
		if (_scroll.Active)
			return;

		_scroll.FinishGraphics();
		_player.DisableInstruments(8);
		_roomView.FinishTransition();
		Vector2 destinationPosition =
			linkPosition + _scroll.FinishOffset;
		if (_scrollPlayerOwner is null)
		{
			_player.FinishScrollingTransition(destinationPosition);
		}
		else
		{
			_scrollPlayerOwner.FinishScreenTransition(
				destinationPosition, _player);
		}
		ScrollingTransitionFinished?.Invoke(_scrollDirection);
		_entities.FinishScreenTransition();
		_rooms.UpdateMinimapLocation();
		_scrollPlayerOwner = null;
		ResetCamera();
	}

	public virtual void ApplyWarp(Player player, Warp warp)
	{
		if (warp.NativeFadeDelay is not (1 or 4))
			throw new InvalidOperationException($"Unsupported native warp fade delay {warp.NativeFadeDelay}.");
		BeginWarp(player, warp, warp.NativeFadeDelay==4);
	}

	// setWarpDestVariables installs bytes only. A native cutscene may return
	// to CUTSCENE_01, which consumes them after the following object pass.
	internal void RequestNativeFadeOutWarp(Warp warp)
	{
		RequireActive();
		if (_nativeWarpRequested || IsTransitioning)
			throw new InvalidOperationException("setWarpDestVariables: a warp is already pending.");
		_pendingWarp = warp;
		_nativeWarpRequested = true;
	}

	internal bool BeginRequestedNativeWarp(Player player)
	{
		if (!_nativeWarpRequested) return false;
		_nativeWarpRequested = false;
		BeginWarp(player, _pendingWarp, false, forceFadeOut: true);
		return true;
	}

	internal void CancelRequestedNativeWarp() => _nativeWarpRequested = false;

	/// <summary>
	/// Starts cutscene $03's ordinary white fade even when the source warp
	/// byte is $00. Some scripted interactions install the warp and select
	/// the cutscene independently instead of encoding a fade in that byte.
	/// </summary>
	public void ApplyWarpWithFadeOut(Player player, Warp warp)
	{
		BeginWarp(player, warp, false, forceFadeOut: true);
	}

	// A native cutscene can select CUTSCENE_03 after its own palette has
	// already reached white. Keep that palette; load on the next update.
	internal void ApplyWarpAfterNativeFadeOut(Player player, Warp warp)
	{
		BeginWarp(player, warp, false, forceFadeOut: true);
		_warpFrame = _warpFadeOutFrames;
		SetFade(1.0f);
	}

	internal void CancelPendingFadeOutWarp(int destinationGroup, int destinationRoom)
	{
		// A cancelled room event may still have a source-side fade request.
		// The terminal load invokes room-entry cancellation too; that committed
		// load must retain its arrival transition.
		if (!_warpActive || ExtendedSequenceActive || _warpPhase != WarpPhase.FadeOut ||
			_warpFrame >= _warpFadeOutFrames ||
			_pendingWarp.DestinationIsIndex ||
			_pendingWarp.DestinationGroup != destinationGroup || _pendingWarp.DestinationRoom != destinationRoom)
			return;
		_warpActive = false;
		_warpPhase = WarpPhase.None;
		_warpFrame = 0;
		_warpTickAccumulator = 0;
		_roomPackArrival = null;
		ClearRoomLoadColumnReveal();
		SetFade(0);
	}

	public void ApplyWarpWithDelayedFadeOut(
		Player player, Warp warp, bool suppressDestinationMusic = false)
	{
		BeginWarp(player, warp, true,
			suppressDestinationMusic: suppressDestinationMusic);
	}

	/// <summary>
	/// Ports initiateFallDownHoleWarp: decrement the active dungeon floor,
	/// retain Link's packed position, fade through cutscene $03, and arrive
	/// with TRANSITION_DEST_FALL ($05).
	/// </summary>
	public void ApplyDungeonHoleWarp(Player player, int packedPosition)
	{
		if (_warpActive)
		{
			throw new InvalidOperationException(
				"A dungeon warphole descent cannot start during another transition.");
		}
		if (TryGetSpecialHoleWarp(packedPosition,out Warp keepWarp))
		{
			BeginWarp(player, keepWarp, delayedFadeOut: false);
			return;
		}
		int dungeon = _rooms.CurrentDungeonIndex;
		if (dungeon < 0)
		{
			throw new InvalidOperationException(
				$"TILETYPE_WARPHOLE in room {_rooms.ActiveGroup:x1}:" +
				$"{_rooms.CurrentRoom.Id:x2} cannot resolve a dungeon floor.");
		}

		DungeonCell destination = _rooms.DungeonMaps.DungeonHoleDestination(
			dungeon,
			_rooms.CurrentRoom.Id);
		BeginWarp(
			player,
			new Warp(
				_rooms.ActiveGroup,
				_rooms.CurrentRoom.Id,
				packedPosition,
				EdgeMask: 0,
				SourceTransition: WarpSourceTransition.FadeOut,
				DestinationGroup: _rooms.ActiveGroup,
				DestinationRoom: destination.Room,
				DestinationPosition: packedPosition,
				DestinationParameter: 0,
				DestinationTransition: WarpDestinationTransition.Fall,
				DirectFadeOut: true),
			delayedFadeOut: false);
	}

	private protected void BeginWarp(
		Player player,
		Warp warp,
		bool delayedFadeOut,
		bool forceFadeOut = false,
		bool suppressDestinationMusic = false)
	{
		RequireActive();
		if (_warpActive) return;
		if (!CanBeginWarp(player, warp, delayedFadeOut || forceFadeOut || warp.DirectFadeOut)) return;
		if (warp.DestinationIsIndex ? !Enumerable.Range(0,256).Any(room => _rooms.HasRoom(warp.DestinationGroup,room)) :
			!_rooms.HasRoom(warp.DestinationGroup,warp.DestinationRoom))
			throw new NotSupportedException($"bank4.applyWarpDest_b04: {AssetRoot} warp from ${warp.SourceGroup:x1}:${warp.SourceRoom:x2} " +
				$"to ${warp.DestinationGroup:x1}:${warp.DestinationRoom:x2} {(warp.DestinationIsIndex ? "(native index)" : "room")} has no imported room content.");
		RequireOrdinaryHandlers(warp,delayedFadeOut || forceFadeOut || warp.DirectFadeOut);
		_dialogue.Close();
		_pendingWarp = warp;
		_roomPackArrival = null;
		_suppressDestinationMusic = suppressDestinationMusic;
		OnOrdinaryWarpStarting();
		_warpActive = true;
		_warpFrame = 0.0f;
		_warpFadeOutFrames = delayedFadeOut ? DelayedWarpFadeFrames : WarpFadeFrames;
		_warpTickAccumulator = 0;
		ClearRoomLoadColumnReveal();
		_roomLoadColumnReveal = false;
		_destinationWalk = false;
		_destinationFall = false;
		_destinationFallInitializationPending = false;
		_standingArrivalPending = false;
		// initiateWarp requests wLinkForceState=$0a; it does not dispatch
		// warpTransition2 itself. State03 never calls checkLinkForceState,
		// so lethal recoil can select a stair but cannot start its fade.
		// Keep the selected warp while Link continues dying. Direct
		// cutscene-owned fades bypass this request.
		if (player.IsDying && !delayedFadeOut && !forceFadeOut && !warp.DirectFadeOut)
		{
			_warpPhase = WarpPhase.AwaitingLinkState;
			return;
		}
		if(_warps.HandlerFor(warp.SourceTransition)!=WarpTransitionHandler.Hole || delayedFadeOut || forceFadeOut || warp.DirectFadeOut)
			player.BeginRoomWarpTransition();
		if (delayedFadeOut || forceFadeOut || warp.DirectFadeOut)
		{
			_warpPhase = WarpPhase.FadeOut;
			SetFade(0.0f);
			return;
		}
		switch (_warps.HandlerFor(warp.SourceTransition))
		{
			case WarpTransitionHandler.Hole:
				_warpPhase=WarpPhase.HoleFall;
				_entities.RuntimeState.SetField("wScrollMode",0);
				_entities.RuntimeState.SetField("wDisabledObjects",0x1e);
				_entities.RuntimeState.SetField("wWarpTransition",(byte)(warp.SourceParameter<<4|warp.SourceTransition));
				player.RequestHoleWarp();SetFade(0);
				break;
			case WarpTransitionHandler.FadeOut:
				// link.s warpTransition2: request $6e once on source
				// initialization, before the fade or destination load.
				_sound.PlaySound(SoundId.SndEnterCave);
				_warpPhase = WarpPhase.FadeOut;
				SetFade(0.0f);
				break;
			case WarpTransitionHandler.Walk:
				Vector2I leaveDirection = (warp.SourceParameter & 4) != 0
					? Vector2I.Down : Vector2I.Up;
				_sound.PlaySound(SoundId.SndEnterCave);
				_warpPhase = WarpPhase.LeaveScreen;
				_warpWalkStart = player.Position;
				_warpWalkEnd = player.Position + (Vector2)leaveDirection * (WarpLeaveFrames - 1);
				player.BeginRoomWarpWalk(player.Position, leaveDirection);
				break;
			case WarpTransitionHandler.Instant:
				// warpTransition4's source branch; destination $84 is silent.
				_sound.PlaySound(SoundId.SndEnterCave);
				goto default;
			default:
				SetFade(1.0f);
				LoadWarpDestination();
				break;
		}
	}

	private protected virtual bool CanBeginWarp(Player player, Warp warp, bool sourceAlreadyHandled) => true;

	private protected void RequireOrdinaryHandlers(Warp warp, bool sourceAlreadyHandled)
	{
		WarpTransitionHandler source = _warps.HandlerFor(warp.SourceTransition);
		// A direct CUTSCENE_03 load has already bypassed Link's source handler.
		if (!sourceAlreadyHandled && warp.SourceTransition != 0 && source is not
			(WarpTransitionHandler.FadeOut or WarpTransitionHandler.Walk or WarpTransitionHandler.Instant or WarpTransitionHandler.Hole))
			throw new NotSupportedException($"{AssetRoot}{_warps.HandlerSource(warp.SourceTransition)}: source mode 0x{warp.SourceTransition:x2} " +
				$"({source}) requires its native sequence before an ordinary room load.");
		if (warp.DestinationIsIndex) return;
		WarpTransitionHandler arrival = _warps.HandlerFor(warp.DestinationTransition);
		if (arrival is not (WarpTransitionHandler.Standing or WarpTransitionHandler.SetRespawn or
			WarpTransitionHandler.Walk or WarpTransitionHandler.Instant or WarpTransitionHandler.Fall or
			WarpTransitionHandler.KeepFacing or WarpTransitionHandler.ParameterFacing or WarpTransitionHandler.Shifted or WarpTransitionHandler.Invisible))
			throw new NotSupportedException($"{AssetRoot}{_warps.HandlerSource(warp.DestinationTransition)}: destination mode 0x{warp.DestinationTransition:x2} " +
				$"({arrival}) is not bound to the ordinary arrival lifecycle.");
	}

	public virtual void UpdateWarp(double delta)
	{
		if (_disposed || !_warpActive)
			return;
		_warpTickAccumulator += delta * 60.0;
		while (_warpActive && _warpTickAccumulator + 0.000001 >= 1.0)
		{
			_warpTickAccumulator -= 1.0;
			AdvanceWarpUpdate();
		}
		if (!_warpActive) _warpTickAccumulator = 0;
	}

	private void AdvanceWarpUpdate()
	{
		_warpFrame++;
		switch (_warpPhase)
		{
			case WarpPhase.AwaitingLinkState:
				break;
			case WarpPhase.FadeOut:
				SetFade(_warpFadeOutFrames == WarpFadeFrames
					? _warpFrame / WarpFadeMaximumOffset
					: (1 + Mathf.Floor((_warpFrame - 1) / 4)) /
						WarpFadeMaximumOffset);
				if (_warpFrame >= _warpFadeOutFrames)
				{
					SetFade(1.0f);
					LoadWarpDestination();
				}
				break;
			case WarpPhase.LeaveScreen:
				// warpTransition3 decrements $10 before moving; its zero
				// update requests the load without a sixteenth displacement.
				float leaveFrame = Mathf.Min(_warpFrame, WarpLeaveFrames - 1);
				_player.SetRoomWarpWalkPosition(
					_warpWalkStart.Lerp(_warpWalkEnd, leaveFrame / (WarpLeaveFrames - 1)));
				if (_warpFrame >= WarpLeaveFrames)
				{
					LoadWarpDestination();
				}
				break;
			case WarpPhase.FadeIn:
				bool destinationReady =
					!_destinationFall || AdvanceDestinationFall();
				if (_destinationWalk)
				{
					float enterFrame = Mathf.Min(_warpFrame, WarpEnterFrames);
					_player.SetRoomWarpWalkPosition(
						_warpWalkStart.Lerp(_warpWalkEnd, enterFrame / WarpEnterFrames));
				}
				SetFade((WarpFadeFrames - _warpFrame) / WarpFadeMaximumOffset);
				if (_warpFrame >= WarpFadeInFrames && destinationReady)
					FinishWarp();
				break;
			case WarpPhase.RoomLoadColumnReveal:
				int loadedColumns = Mathf.Clamp(
					Mathf.FloorToInt(_warpFrame) -
						RoomLoadRevealInitializationFrames,
					0,
					RoomLoadRevealColumnUpdates);
				SetFade(0.0f);
				SetRoomLoadColumnReveal(true, loadedColumns);
				if (_warpFrame >= RoomLoadRevealCompletionFrame)
				{
					ClearRoomLoadColumnReveal();
					_warpFrame = 0.0f;
					if (_destinationWalk)
						_warpPhase = WarpPhase.EnterScreen;
					else
						FinishWarp();
				}
				break;
			case WarpPhase.EnterScreen:
				if (_destinationWalk)
				{
					float enterFrame = Mathf.Min(_warpFrame, WarpEnterFrames);
					_player.SetRoomWarpWalkPosition(
						_warpWalkStart.Lerp(_warpWalkEnd, enterFrame / WarpEnterFrames));
				}
				if (_warpFrame >= WarpEnterFrames)
					FinishWarp();
				break;
		}
	}

	private protected void LoadWarpDestination()
	{
		Warp warp = _warps.ResolveDestination(_pendingWarp,_entities.RuntimeState);
		if (!_rooms.HasRoom(warp.DestinationGroup,warp.DestinationRoom))
			throw new NotSupportedException($"bank4.applyWarpDest_b04: {warp.NativeSource ?? AssetRoot} resolved unavailable room 0x{warp.DestinationGroup:x1}:0x{warp.DestinationRoom:x2}.");
		if (!ExtendedSequenceActive) RequireOrdinaryHandlers(warp,sourceAlreadyHandled:true);
		_pendingWarp = warp;
		_roomLoadColumnReveal = _warpFadeOutFrames != DelayedWarpFadeFrames && UsesRoomLoadColumnReveal(warp);
		using var publication = _rooms.DeferTexturePublication();
		_roomView.ClearHorizontalWave();
		BeforeDestinationLoad();

		// loadingRoom clears the source room's interaction memory before
		// applying and initializing the destination. Terrain-owned transient
		// interactions such as INTERAC_SPLASH follow that same full-load
		// boundary instead of surviving under the destination tilemap.
		WarpDestinationLoading?.Invoke();
		_player.ResetDeathAnimationForRoomLoad();
		// cutscene03 clears w1Link before applyWarpDest places its coordinates.
		if (_roomPackArrival is null && !ExtendedSequenceActive) _player.Face(Vector2I.Up);
		WarpTransitionHandler arrivalHandler = _warps.HandlerFor(warp.DestinationTransition);
		BeforeDestinationLayout(warp);
		// Full room loading retains the mounted animal slot. Update its room
		// and packed destination before the factory reconstitutes that slot.
		if (_entities.PlayerRidingObject && CompanionRuntimeState.AnyActive(_entities.RuntimeState))
		{
			ActiveCompanion companion = CompanionRuntimeState.Read(_entities.RuntimeState);
			if (companion.Id is CompanionRuntimeState.RickyId or CompanionRuntimeState.DimitriId or CompanionRuntimeState.MooshId)
			{
				Vector2 destination = new((warp.DestinationPosition & 15) * 16 + 8,
					(warp.DestinationPosition >> 4) * 16 + 8);
				if (arrivalHandler == WarpTransitionHandler.Shifted) destination.X -= 8;
				CompanionRuntimeState.Update(_entities.RuntimeState, companion.Id,
					destination, companion.Direction);
				CompanionRuntimeState.SetLastAnimalMountPosition(_rooms.SaveData, destination);
			}
		}
		OracleRoomData room = LoadDestinationRoom(warp);
		_roomView.SetRoom(room.Texture);
		if (_roomLoadColumnReveal)
		{
			_roomView.SetRoomLoadColumnReveal(
				room.ClearedTilemapTexture,
				0);
			_roomLoadReveal.SetReveal(
				room.ClearedTilemapTexture,
				0);
		}
		// Room-palette darkening belongs to the source room. Time-warp already
		// clears it above; ordinary/delayed warps must not carry it into the
		// destination either (notably after an Essence get sequence).
		_roomView.ClearBackgroundFade();
		// Standard warp loading clears wEnemiesKilledList only when the
		// destination's wDungeonIndex is $ff. Dungeon-to-dungeon warps retain
		// the same transient last-eight-room suppression as scrolling.
		if (_rooms.CurrentDungeonIndex < 0 && _roomPackArrival is null)
			_entities.ClearRecentEnemyDefeats();
		// CUTSCENE_05 calls func_4493 before initializeRoom, so destination
		// actors observe the adjusted Link position during their state 0.
		if (_roomPackArrival is { } arrival)
		{
			_player.WarpTo(arrival);
			DeactivateWarpAtPlayerPosition(_player);
		}
		else
			// cutscene03: applyWarpDest places Link before setEnteredWarpPosition,
			// including normal packed destinations of warpTransition3.
			_entities.RuntimeState.SetField("wEnteredWarpPosition", (byte)warp.DestinationPosition);
		InitializeDestinationEntities(room,warp);
		CheckDisplayEraInfoAfterFullRoomLoad();

		Vector2 spawn;
		if (_roomPackArrival is not null)
		{
			spawn = _player.PrecisePosition;
		}
		else if (arrivalHandler == WarpTransitionHandler.Walk)
		{
			Vector2I direction = (warp.DestinationParameter & 0x04) != 0 ? Vector2I.Down : Vector2I.Up;
			bool entersFromScreen = true;
			if (warp.DestinationPosition == 0xff)
			{
				float middleX = _rooms.ActiveGroup >= 4 ? 0x78 : 0x50;
				spawn = new Vector2(middleX, direction == Vector2I.Up ? room.Height : -16.0f);
			}
			else if ((warp.DestinationPosition & 0xf0) == 0xf0)
			{
				float x = (warp.DestinationPosition & 0x0f) * OracleRoomData.MetatileSize;
				if (_rooms.ActiveGroup >= 4) x += 8.0f;
				spawn = new Vector2(x, direction == Vector2I.Up ? room.Height : -16.0f);
			}
			else
			{
				int tileX = warp.DestinationPosition & 0x0f;
				int tileY = (warp.DestinationPosition >> 4) & 0x0f;
				spawn = new Vector2(tileX * OracleRoomData.MetatileSize + 8, tileY * OracleRoomData.MetatileSize + 8);
				entersFromScreen = false;
			}
			if (entersFromScreen)
			{
				_warpWalkStart = spawn;
				_warpWalkEnd = spawn + (Vector2)direction * WarpEnterFrames;
				_destinationWalk = true;
				_player.BeginRoomWarpWalk(spawn, direction);
			}
			else
			{
				// warpTransition3:@destInit only runs the 28-update entrance
				// walk for destination positions $ff or $f0-$ff. A normal
				// packed position such as the Maku cutscene's $45 is placed
				// directly and returned to standing.
				_destinationWalk = false;
				_player.WarpTo(spawn);
				_player.Face(direction);
			}
		}
		else if (arrivalHandler == WarpTransitionHandler.Fall)
		{
			int tileX = warp.DestinationPosition & 0x0f;
			int tileY = (warp.DestinationPosition >> 4) & 0x0f;
			Vector2 shortPosition = new(
				tileX * OracleRoomData.MetatileSize + 8,
				tileY * OracleRoomData.MetatileSize + 8);
			spawn = shortPosition;
			_destinationFall = true;
			// Full loading leaves LINK_STATE_00 and a forced $0a request.
			// linkSetState dispatches warpTransition5_00 immediately when
			// the first object update consumes that forced state request.
			_destinationFallInitializationPending = true;
			_player.WarpTo(spawn);
			_player.Face(Vector2I.Up); // clearScreenVariablesAndWramBank1.
			// The cleared Link object stays hidden until linkState00 consumes
			// the forced warp state and initializes its airborne presentation.
			_player.Visible = false;
		}
		else
		{
			int tileX = warp.DestinationPosition & 0x0f;
			int tileY = (warp.DestinationPosition >> 4) & 0x0f;
			spawn = new Vector2(tileX * OracleRoomData.MetatileSize + 8, tileY * OracleRoomData.MetatileSize + 8);
			// Link's first destination dispatch precedes initialized actors;
			// loading the room itself does not run warpTransition0/1/A/C/E/F.
			_standingArrivalPending = !ExtendedSequenceActive;
			PlaceStandingWarpDestination(spawn);
		}

		if (warp.DestinationTransition == WarpDestinationTransition.Invisible)
			_player.Visible = false;
		BeginDestinationPresentation(spawn);
		_warpFrame = 0.0f;
		ResetCamera();
		// Ages cutscene03 clears wDontUpdateStatusBar after initializeRoom.
		// Full loading replaces any cutscene BG occupying the status strip.
		if (_rooms.SaveData.Game == OracleGame.Ages) _hud.ShowStatusBar();
		else _hud.RestoreStatusBarPresentation();
		CommonGraphicsReloadRequested?.Invoke();
		_hud.Refresh();
		// cutscene03 starts fadeinFromWhiteToRoom after initializeRoom.
		// Outgoing event cleanup can restore its old fade color during that
		// load; the new arrival owns the final white endpoint.
		if (!ExtendedSequenceActive)
			SetFade(_roomLoadColumnReveal ? 0.0f : 1.0f);
	}
	private protected virtual void PlaceStandingWarpDestination(Vector2 spawn) => _player.WarpTo(spawn);

	/// <summary>
	/// Called after full room loads; ordinary scrolling retains banner state.
	/// </summary>
	internal bool CheckDisplayEraInfoAfterFullRoomLoad()
	{
		return EraOrSeasonInfoDatabase.For(AssetRoot).TryDisplay(_rooms,_entities);
	}

	private bool AdvanceDestinationFall()
	{
		if (!_destinationFallInitializationPending)
			return _player.AdvanceRoomWarpFall();
		_destinationFallInitializationPending = false;
		// objectGetZAboveScreen samples the packed destination before
		// warpTransition5_00 subtracts $04 from Link's Y.
		int screenY = Mathf.FloorToInt(WorldToGameplayScreen(_player.PrecisePosition).Y);
		_player.SetScriptedPosition(_player.PrecisePosition + new Vector2(0, -4));
		_player.BeginRoomWarpFall(Player.RoomWarpFallInitialZ(screenY));
		return false;
	}


	internal bool TryHandOffStationaryArrivalFade()
	{
		// Native destination interactions can replace fadeinFromWhiteToRoom
		// with their own palette thread. Wait until the first object update
		// after loading, with Link already placed and no entrance motion.
		if (!_warpActive || _warpPhase != WarpPhase.FadeIn || _warpFrame == 0 ||
			_destinationWalk || _destinationFall || ExtendedSequenceActive)
			return false;
		FinishWarp();
		return true;
	}

	private protected void FinishWarp()
	{
		_entities.RuntimeState.SetField("wScrollMode", 1);
		_player.FinishRoomWarpTransition(_destinationWalk ? _warpWalkEnd :
			_roomPackArrival is not null ? _player.PrecisePosition : _player.Position,
			freshlyLoadedBasicRoom: !ExtendedSequenceActive && !_destinationWalk && !_destinationFall &&
				_roomPackArrival is null && _pendingWarp.DestinationTransition == WarpDestinationTransition.Basic);
		if (_warps.HandlerFor(_pendingWarp.DestinationTransition) is not (WarpTransitionHandler.SetRespawn or WarpTransitionHandler.Shifted))
			_deathRespawnPoints.RecordWarpDestination(_pendingWarp.DestinationTransition);
		OnWarpFinished();
		_destinationWalk = false;
		_destinationFall = false;
		_warpActive = false;
		_roomPackArrival = null;
		_warpPhase = WarpPhase.None;
		// link.s warpTransitionF keeps objectSetInvisible after arrival;
		// FinishRoomWarpTransition/WarpTo must not publish Link again.
		_player.Visible = _pendingWarp.DestinationTransition != WarpDestinationTransition.Invisible;
		ClearRoomLoadColumnReveal();
		SetFade(0.0f);
	}

	public void ClearDeactivatedWarp()
	{
		_entities.RuntimeState.SetField("wEnteredWarpPosition", 0xff);
	}

	public void UpdateCamera()
	{
		if (_disposed) return;
		if (_scroll.Active || CutsceneCameraFrozen || _entities.CameraUpdatesDisabledSource())
			return;
		Vector2 focusPosition =
			NativeCameraFocus ??
			_entities.SwitchHook?.CameraFocus ??
			_entities.PlayerScreenTransitionOwner?.ScreenTransitionPosition ??
			_player.Position;
		Vector2 target = GetCameraOrigin(
			_rooms.CurrentRoom, focusPosition);
		if (_rooms.CurrentRoom.Width <= OracleRoomData.ViewportWidth &&
			_rooms.CurrentRoom.Height <= OracleRoomData.ViewportHeight)
		{
			SetCameraOrigin(target);
			return;
		}
		// updateCameraPosition leaves both components unchanged while a
		// textbox is active. Camera catch-up resumes when it closes.
		if (_dialogue.IsOpen)
			return;

		Vector2 current = CurrentCameraOrigin;
		SetCameraOrigin(new Vector2(
			MoveCameraComponent(current.X, target.X),
			MoveCameraComponent(current.Y, target.Y)));
	}

	/// <summary>
	/// Mirrors calculateCameraPosition, used only at room-load boundaries
	/// where the original places hCameraY/X at their targets immediately.
	/// </summary>
	public void ResetCamera()
	{
		Vector2 focusPosition =
			NativeCameraFocus ??
			_entities.SwitchHook?.CameraFocus ??
			_entities.PlayerScreenTransitionOwner?.ScreenTransitionPosition ??
			_player.Position;
		SetCameraOrigin(GetCameraOrigin(_rooms.CurrentRoom, focusPosition));
	}

	private void SetCameraOrigin(Vector2 origin)
	{
		_camera.Position = origin + GameplayCameraOffset;
		if (!_camera.IsInsideTree() || !_camera.Enabled || !_camera.IsCurrent()) return;
		_camera.ForceUpdateScroll();
	}
	// Native fullscreen cutscenes write hCameraY/X independently of SCY/SCX.
	internal void SetCutsceneCameraOrigin(Vector2 origin)=>SetCameraOrigin(origin);

	private static float MoveCameraComponent(float current, float target)
	{
		int currentPixel = Mathf.FloorToInt(current);
		int targetPixel = Mathf.FloorToInt(target);
		return currentPixel == targetPixel
			? targetPixel
			: currentPixel + Math.Sign(targetPixel - currentPixel);
	}

	/// <summary>Returns physical 160x144 display coordinates below the HUD.</summary>
	public Vector2 WorldToScreen(Vector2 worldPosition) =>
		WorldToGameplayScreen(worldPosition) +
		new Vector2(0, OracleRoomData.GameplayScreenTop);

	/// <summary>
	/// Returns the original engine's 160x128 gameplay-field coordinates.
	/// Object bounds and textbox-side selection use this coordinate space.
	/// </summary>
	internal Vector2 WorldToGameplayScreen(Vector2 worldPosition) =>
		worldPosition - CurrentCameraOrigin;

	private Vector2 CurrentCameraOrigin => _camera.Position -
		GameplayCameraOffset;

	internal void SetGameplayScreenTop(int top)
	{
		if (_gameplayScreenTop == top)
			return;
		Vector2 origin = CurrentCameraOrigin;
		_gameplayScreenTop = top;
		SetCameraOrigin(origin);
	}

	private Vector2 GameplayCameraOffset => new(
		OracleRoomData.ViewportWidth / 2.0f,
		OracleRoomData.ScreenHeight / 2.0f - _gameplayScreenTop);

	private static Vector2 GetCameraOrigin(OracleRoomData room, Vector2 playerPosition)
	{
		float maxX = Mathf.Max(0.0f, room.Width - OracleRoomData.ViewportWidth);
		float maxY = Mathf.Max(0.0f, room.Height - OracleRoomData.ViewportHeight);
		Vector2 highBytes = OracleObjectMath.ToPixelPosition(playerPosition);
		return new Vector2(
			Mathf.Clamp(highBytes.X - OracleRoomData.ViewportWidth / 2.0f, 0.0f, maxX),
			Mathf.Clamp(highBytes.Y - OracleRoomData.ViewportHeight / 2.0f, 0.0f, maxY));
	}

	private protected void SetFade(float alpha) =>
		SetFadeColor(Colors.White, alpha);

	private void SetFadeColor(Color color, float alpha) =>
		_warpFade.Color = new Color(color.R, color.G, color.B, Mathf.Clamp(alpha, 0.0f, 1.0f));

	private void SetRoomLoadColumnReveal(bool active, int loadedColumns)
	{
		_roomLoadColumnReveal = active;
		_roomLoadRevealLoadedColumns = Mathf.Clamp(
			loadedColumns, 0, RoomLoadRevealColumnUpdates);
		if (active)
		{
			_roomView.SetRoomLoadRevealLoadedColumns(
				_roomLoadRevealLoadedColumns);
			_roomLoadReveal.SetLoadedColumns(
				_roomLoadRevealLoadedColumns);
		}
		else
		{
			_roomView.ClearRoomLoadColumnReveal();
			_roomLoadReveal.ClearReveal();
		}
	}

	private protected void ClearRoomLoadColumnReveal()
	{
		_roomLoadColumnReveal = false;
		_roomLoadRevealLoadedColumns = 0;
		_roomView.ClearRoomLoadColumnReveal();
		_roomLoadReveal.ClearReveal();
	}

	private static bool UsesRoomLoadColumnReveal(Warp warp) =>
		warp.EdgeMask != 0 &&
		warp.SourceTransition == WarpSourceTransition.LeaveScreen &&
		warp.DestinationTransition == WarpDestinationTransition.EnterScreen &&
		warp.DestinationPosition == 0xff;

	internal static (int Left, int Right) RoomLoadRevealBounds(
		int loadedColumns)
	{
		int clamped = Mathf.Clamp(
			loadedColumns, 0, RoomLoadRevealColumnUpdates);
		int loadedLeft = (clamped + 1) / 2;
		int loadedRight = clamped / 2;
		int center = RoomLoadRevealVisibleColumns / 2;
		return (
			Math.Max(0, (center - loadedLeft) * RoomLoadRevealColumnWidth),
			Math.Min(
				OracleRoomData.ViewportWidth,
				(center + loadedRight) * RoomLoadRevealColumnWidth));
	}

	internal static bool RoomLoadRevealIsClearedAtPixel(
		int x,
		int loadedColumns)
	{
		(int left, int right) =
			RoomLoadRevealBounds(loadedColumns);
		int pixel = Mathf.Clamp(x, 0, OracleRoomData.ViewportWidth - 1);
		return pixel < left || pixel >= right;
	}


}


internal enum WarpPhase
{
	None,
	AwaitingLinkState,
	HoleFall,
	FadeOut,
	LeaveScreen,
	FadeIn,
	RoomLoadColumnReveal,
	EnterScreen
}
