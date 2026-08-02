using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Ports the clean-US runIntro dispatch: Nintendo/Capcom, all three cinematic
/// sections, title idle replay, Start skip, sound requests, and shared RNG.
/// One call to <see cref="AdvanceOneOriginalUpdate"/> is one original update.
/// </summary>
internal sealed class FrontendIntroController
{
    private readonly FrontendIntroDatabase _data = FrontendIntroDatabase.Shared;
    private readonly FrontendIntroScreen _screen;
    private readonly MainMenuScreen _titleScreen;
    private readonly OracleRandom _random;
    private readonly Action _restartSound;
    private readonly Action<int> _playSound;
    private readonly Action _openFileSelect;
    private readonly List<FrontendBirdState> _birds = [];
    private FrontendSequenceValue[] _templeInput = [];
    private int _templeInputIndex;
    private int _templeInputRemaining;
    private bool _templeInputDone;
    private int _fadeUpdate;
    private int _fadeDuration;
    private bool _fadeToWhite;
    private int _restartStep;
    private int _groundStepCounter;
    private int _spritePaletteCounter;
    private int _castleScrollCounter;
    private int _castleHorseFrameCounter;
    private int _castleHorseTerminalEventsRemaining;
    private int _treeScrollCounter;
    private int _titleSoundCounter;
    private int _titleRevealIndex;
    private int _flashCounter;
    private int _triforceCounter;
    private bool _energySoundPlayed;
    private ushort _horseFrontYFixed;
    private ushort _horseFrontXFixed;
    private ushort _horseBirdXFixed;
    private ushort _castleActorXFixed;
    private ushort _cloudOffsetYFixed;
    private bool _cloudsCreated;

    internal FrontendIntroStage Stage { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal byte FrameCounter { get; private set; }
    internal bool InputsEnabled { get; private set; }
    internal bool IsActive { get; private set; } = true;
    internal int HorseScrollY { get; private set; }
    internal int HorseGroundScrollX { get; private set; }
    internal int HorseCloudScrollX { get; private set; }
    internal int HorseMountainScrollX { get; private set; }
    internal int HorseFaceScrollX { get; private set; }
    internal int HorseFaceTopBarLastY { get; private set; }
    internal int HorseFaceBottomBarY { get; private set; }
    internal int HorseFaceSparkleClock { get; private set; }
    internal bool HorseFaceSparkleVisible { get; private set; }
    internal int BlackBarPixels { get; private set; }
    internal int HorseSpritePalette { get; private set; }
    internal int HorseAnimationClock { get; private set; }
    internal int HorseFrontY => _horseFrontYFixed >> 8;
    internal int HorseFrontX => _horseFrontXFixed >> 8;
    internal int HorseBirdX => _horseBirdXFixed >> 8;
    internal int CastleScrollX { get; private set; }
    internal int CastleActorY { get; private set; }
    internal int CastleActorX => _castleActorXFixed >> 8;
    internal int CastleHorseAnimation { get; private set; }
    internal int CastleStaticAnimation { get; private set; }
    internal int CastleHorseAnimationFrame { get; private set; }
    internal int CastleHorseTerminalEventsRemaining =>
        _castleHorseTerminalEventsRemaining;
    internal bool CastleHorseAnimationStopped { get; private set; }
    internal int TempleCameraY { get; private set; }
    internal int TempleLinkY { get; private set; }
    internal int TempleLinkAnimation { get; private set; }
    internal int TempleLinkAnimationClock { get; private set; }
    internal bool TempleLinkBlinking { get; private set; }
    internal int TempleAnimationClock { get; private set; }
    internal int TempleBackgroundAnimationGroup { get; private set; }
    internal long TempleBackgroundAnimationTick { get; private set; }
    internal int TriforceMotionClock { get; private set; }
    internal int TempleWaveClock { get; private set; }
    internal int TriforceState { get; private set; }
    internal bool TempleLinkVisible { get; private set; }
    internal bool TempleWaveActive { get; private set; }
    internal bool FlashWhite { get; private set; }
    internal int TreeScrollY { get; private set; }
    internal int TreeMapPhase { get; private set; }
    internal int TreeAnimationClock { get; private set; }
    internal int TitleRevealPixels { get; private set; }
    internal bool TreeBranchesVisible { get; private set; }
    internal bool CloudsVisible { get; private set; }
    internal int CloudOffsetY => _cloudOffsetYFixed >> 8;
    internal IReadOnlyList<FrontendBirdState> Birds => _birds;
    internal int RandomCallsAtConstruction { get; }

    internal FrontendIntroController(
        FrontendIntroScreen screen,
        MainMenuScreen titleScreen,
        OracleRandom random,
        Action restartSound,
        Action<int> playSound,
        Action openFileSelect,
        bool startAtTitle = false)
    {
        _screen = screen;
        _titleScreen = titleScreen;
        _random = random;
        _restartSound = restartSound;
        _playSound = playSound;
        _openFileSelect = openFileSelect;
        RandomCallsAtConstruction = random.Calls;
        _screen.Attach(this);
        if (startAtTitle)
        {
            InputsEnabled = true;
            EnterTitle();
        }
        else
        {
            Stage = FrontendIntroStage.Boot;
            ShowIntro();
            SetWhiteFade(1.0f);
        }
    }

    internal void Update(double delta)
    {
        if (!IsActive)
            return;
        // ApplicationFixedUpdateScheduler owns accumulation. This controller
        // deliberately consumes one discrete original update per call.
        _ = delta;
        AdvanceOneOriginalUpdate(Input.IsActionJustPressed("inventory"));
    }

    internal void AdvanceOneOriginalUpdate(bool startPressed = false)
    {
        if (!IsActive)
            return;

        FrameCounter = unchecked((byte)(FrameCounter + 1));
        if (startPressed && InputsEnabled && Stage != FrontendIntroStage.Title)
        {
            EnterTitle();
            RunTitle(startPressed: false);
            QueueRedraw();
            return;
        }

        switch (Stage)
        {
            case FrontendIntroStage.Boot:
                // The clean US build skips the Japanese-only stage without
                // falling through to the Capcom state in this update.
                Stage = FrontendIntroStage.Capcom;
                State = 0;
                break;
            case FrontendIntroStage.Capcom:
                RunCapcom();
                break;
            case FrontendIntroStage.Horse:
                RunHorse();
                break;
            case FrontendIntroStage.Temple:
                RunTemple();
                break;
            case FrontendIntroStage.PreTitle:
                RunPreTitle();
                break;
            case FrontendIntroStage.Title:
                RunTitle(startPressed);
                break;
            case FrontendIntroStage.Restart:
                RunRestart();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported frontend stage {Stage}.");
        }
        QueueRedraw();
    }

    private void RunCapcom()
    {
        switch (State)
        {
            case 0:
                _restartSound();
                ShowIntro();
                _screen.Scene = FrontendIntroScene.Capcom;
                Counter = _data.Timing("capcom-hold");
                BeginFade(toWhite: false);
                State = 1;
                break;
            case 1:
                AdvanceFade();
                if (--Counter != 0)
                    break;
                BeginFade(toWhite: true);
                State = 2;
                break;
            case 2:
                if (!AdvanceFade())
                    break;
                InputsEnabled = true;
                Stage = FrontendIntroStage.Horse;
                State = 0;
                break;
            default:
                throw InvalidState();
        }
    }

    private void RunHorse()
    {
        HorseAnimationClock++;
        if (HorseFaceSparkleVisible)
            HorseFaceSparkleClock++;
        switch (State)
        {
            case 0:
                _screen.Scene = FrontendIntroScene.HorseFar;
                HorseAnimationClock = 0;
                Counter = _data.Timing("horse-sunset");
                BlackBarPixels = 0;
                HorseScrollY = 0;
                HorseGroundScrollX = 0;
                HorseCloudScrollX = 0;
                HorseMountainScrollX = 0;
                HorseSpritePalette = 0;
                _horseBirdXFixed = 0xc800;
                _playSound(OracleSoundEngine.MusIntro1);
                BeginFade(
                    toWhite: false,
                    _data.Timing("horse-fade-divisor"));
                State = 1;
                break;
            case 1:
                BlackBarPixels = Math.Min(24, BlackBarPixels + 2);
                AdvanceFade();
                if (--Counter != 0)
                    break;
                _fadeDuration = 0;
                SetWhiteFade(0.0f);
                _groundStepCounter = _data.Timing("horse-ground-step");
                State = 2;
                break;
            case 2:
                AdvanceHorseGround();
                _groundStepCounter--;
                if (_groundStepCounter != 0)
                    break;
                _groundStepCounter = _data.Timing("horse-ground-step");
                HorseScrollY++;
                if (HorseScrollY != _data.Timing("horse-ground-target"))
                    break;
                Counter = _data.Timing("horse-pause");
                State = 3;
                break;
            case 3:
                AdvanceHorseGround();
                if (--Counter != 0)
                    break;
                _screen.Scene = FrontendIntroScene.HorseFront;
                HorseAnimationClock = 0;
                Counter = _data.Timing("horse-front");
                _spritePaletteCounter = 60;
                _groundStepCounter = 0x0d;
                _horseFrontYFixed = 0x4c00;
                _horseFrontXFixed = 0x6c00;
                State = 4;
                break;
            case 4:
                AdvanceHorseFront();
                if (--Counter != 0)
                    break;
                _screen.Scene = FrontendIntroScene.HorseFace;
                HorseAnimationClock = 0;
                HorseFaceScrollX = 0;
                FrontendSequenceValue initialFaceBars =
                    _data.Sequence("horse-face-bars")[0];
                HorseFaceTopBarLastY = initialFaceBars.A;
                HorseFaceBottomBarY = initialFaceBars.B;
                HorseFaceSparkleClock = 0;
                HorseFaceSparkleVisible = false;
                BlackBarPixels = 0;
                State = 5;
                break;
            case 5:
                AdvanceHorseFacePan();
                if (HorseFaceScrollX !=
                    _data.Sequence("horse-face-motion")[1].A)
                    break;
                Counter = _data.Timing("horse-face-linger");
                HorseFaceSparkleClock = 0;
                HorseFaceSparkleVisible = true;
                State = 6;
                break;
            case 6:
                if (--Counter != 0)
                    break;
                _screen.Scene = FrontendIntroScene.HorseCloseup;
                HorseScrollY = _data.Timing("horse-closeup-scroll");
                HorseFaceSparkleVisible = false;
                State = 7;
                break;
            case 7:
                HorseScrollY--;
                if (HorseScrollY != 0)
                    break;
                Counter = _data.Timing("horse-closeup-linger");
                State = 8;
                break;
            case 8:
                if (--Counter != 0)
                    break;
                _screen.Scene = FrontendIntroScene.Castle;
                HorseAnimationClock = 0;
                CastleScrollX = 0x20;
                _castleScrollCounter = _data.Timing("castle-scroll");
                Counter = _data.Timing("castle-hold");
                InitializeCastleActors();
                State = 9;
                break;
            case 9:
                if (_castleScrollCounter > 0)
                {
                    _castleScrollCounter--;
                    if (_castleScrollCounter % 5 == 0 && CastleScrollX > 0)
                        CastleScrollX--;
                }
                AdvanceCastleActors();
                if (--Counter != 0)
                    break;
                BeginFade(toWhite: true);
                State = 10;
                break;
            case 10:
                if (!AdvanceFade())
                    break;
                Stage = FrontendIntroStage.Temple;
                State = 0;
                break;
            default:
                throw InvalidState();
        }
    }

    private void InitializeCastleActors()
    {
        FrontendSequenceValue position =
            _data.Sequence("castle-actor-position")[0];
        FrontendSequenceValue[] animations =
            _data.Sequence("castle-animation");
        CastleActorY = position.A;
        _castleActorXFixed = (ushort)(position.B << 8);
        CastleHorseAnimation = animations[0].A;
        CastleStaticAnimation = animations[1].A;
        _castleHorseTerminalEventsRemaining = animations[0].B;
        CastleHorseAnimationFrame = 0;
        CastleHorseAnimationStopped = false;
        FrontendAnimation animation = _data.Animation(
            $"horse-{CastleHorseAnimation}");
        _castleHorseFrameCounter = animation.Frames[0].Duration;
    }

    private void AdvanceCastleActors()
    {
        if (!CastleHorseAnimationStopped)
        {
            FrontendAnimation animation = _data.Animation(
                $"horse-{CastleHorseAnimation}");
            _castleHorseFrameCounter--;
            if (_castleHorseFrameCounter == 0)
            {
                CastleHorseAnimationFrame++;
                if (CastleHorseAnimationFrame >= animation.Frames.Length)
                    CastleHorseAnimationFrame = animation.LoopStart;
                FrontendAnimationFrame frame =
                    animation.Frames[CastleHorseAnimationFrame];
                _castleHorseFrameCounter = frame.Duration;
                if (frame.Parameter != 0)
                {
                    _castleHorseTerminalEventsRemaining--;
                    if (_castleHorseTerminalEventsRemaining == 0)
                    {
                        // @runSubid1 calls interactionSetAnimation $04 and then
                        // stops calling interactionAnimate once substate is set.
                        CastleHorseAnimationFrame = 0;
                        _castleHorseFrameCounter = animation.Frames[0].Duration;
                        CastleHorseAnimationStopped = true;
                    }
                }
            }
        }

        // State 9 decrements wIntro.cbb6 before interactions run. On the
        // update where it reaches zero, both subids stop applying SPEED_20.
        if (_castleScrollCounter == 0)
            return;
        FrontendSequenceValue motion =
            _data.Sequence("castle-actor-motion")[0];
        OracleObjectVelocity velocity =
            OracleObjectSpeedTable.Shared.Get(motion.A, motion.B);
        _castleActorXFixed = unchecked(
            (ushort)(_castleActorXFixed + velocity.XFixed));
    }

    private void AdvanceHorseGround()
    {
        if ((FrameCounter & 1) == 0)
            HorseGroundScrollX = unchecked((byte)(HorseGroundScrollX - 1));
        AdvanceInitialHorseObjects();
    }

    private void AdvanceHorseFacePan()
    {
        FrontendSequenceValue targetBars =
            _data.Sequence("horse-face-bars")[1];
        FrontendSequenceValue motion =
            _data.Sequence("horse-face-motion")[0];
        int scrollTarget = _data.Sequence("horse-face-motion")[1].A;

        // introCinematic_moveBlackBarsOut updates LYC and WINY before state
        // 5 advances only wGfxRegs2.SCX. LYC is the last cleared-map line;
        // WINY is the first line covered by the cleared priority window.
        HorseFaceTopBarLastY = Math.Max(
            targetBars.A,
            HorseFaceTopBarLastY - motion.A);
        HorseFaceBottomBarY = Math.Min(
            targetBars.B,
            HorseFaceBottomBarY + motion.A);
        HorseFaceScrollX = Math.Min(
            scrollTarget,
            HorseFaceScrollX + motion.B);
    }

    private void AdvanceInitialHorseObjects()
    {
        // Subid $06 moves only after the slow palette thread is cleared.
        if (_fadeDuration != 0)
            return;
        OracleObjectVelocity velocity = OracleObjectSpeedTable.Shared.Get(0x0f, 0x1a);
        _horseBirdXFixed = unchecked((ushort)(_horseBirdXFixed + velocity.XFixed));
    }

    private void AdvanceHorseFront()
    {
        if ((FrameCounter & 0x1f) == 0)
            HorseCloudScrollX = unchecked((byte)(HorseCloudScrollX - 1));
        if (--_groundStepCounter == 0)
        {
            _groundStepCounter = 0x0d;
            HorseMountainScrollX = unchecked((byte)(HorseMountainScrollX - 1));
        }
        if (--_spritePaletteCounter == 0)
        {
            _spritePaletteCounter = 60;
            HorseSpritePalette = Math.Min(3, HorseSpritePalette + 1);
        }
        if ((FrameCounter & 3) == 0)
        {
            OracleObjectVelocity velocity = OracleObjectSpeedTable.Shared.Get(0x0a, 0x19);
            _horseFrontYFixed = unchecked((ushort)(_horseFrontYFixed + velocity.YFixed));
            _horseFrontXFixed = unchecked((ushort)(_horseFrontXFixed + velocity.XFixed));
        }
    }

    private void RunTemple()
    {
        TempleAnimationClock++;
        if (TempleLinkVisible)
        {
            // linkCutscene0 resets animation $00 instead of advancing it while
            // wLinkAngle has its stopped value. That includes the fade before
            // simulated input begins, not only explicit $00 input records.
            if (TempleLinkAnimation == 0 && !TempleLinkIsWalking())
                TempleLinkAnimationClock = 0;
            else
                TempleLinkAnimationClock++;
        }
        switch (State)
        {
            case 0:
                _screen.Scene = FrontendIntroScene.Temple;
                TempleAnimationClock = 0;
                FrontendSequenceValue backgroundAnimation =
                    _data.Sequence("temple-background-animation")[0];
                TempleBackgroundAnimationGroup = backgroundAnimation.A;
                TempleBackgroundAnimationTick = 0;
                TriforceMotionClock = 0;
                TempleWaveClock = 0;
                TempleCameraY = 0x70;
                TempleLinkY = 0xd0;
                TempleLinkVisible = true;
                TempleLinkAnimation = 0;
                TempleLinkAnimationClock = 0;
                TempleLinkBlinking = false;
                TempleWaveActive = false;
                TriforceState = 0;
                _templeInput = _data.Sequence("temple-input");
                _templeInputIndex = 0;
                _templeInputRemaining = 0;
                _templeInputDone = false;
                Counter = _data.Timing("temple-fade-input-block");
                BeginFade(toWhite: false);
                State = 1;
                break;
            case 1:
                AdvanceFade();
                if (Counter > 0)
                {
                    Counter--;
                    break;
                }
                if (_templeInputDone)
                {
                    State = 2;
                    break;
                }
                AdvanceTempleInput();
                break;
            case 2:
                AdvanceTriforceSequence();
                if (TriforceState != 3)
                    break;
                BeginFade(toWhite: true);
                State = 3;
                break;
            case 3:
                if (!AdvanceFade())
                    break;
                TempleWaveActive = true;
                BeginFade(toWhite: false);
                State = 4;
                break;
            case 4:
                if (!AdvanceFade())
                    break;
                Counter = _data.Timing("temple-wave-hold");
                State = 5;
                break;
            case 5:
                if (--Counter != 0)
                    break;
                _flashCounter = 1;
                FlashWhite = true;
                State = 6;
                break;
            case 6:
                AdvanceTempleFlash();
                break;
            case 7:
                TempleLinkBlinking = true;
                if (--Counter != 0)
                    break;
                TempleLinkVisible = false;
                TempleLinkBlinking = false;
                Counter = _data.Timing("temple-wait");
                State = 8;
                break;
            case 8:
                if (--Counter != 0)
                    break;
                Counter = _data.Timing("temple-wait");
                State = 9;
                break;
            case 9:
                if (--Counter != 0)
                    break;
                _playSound(OracleSoundEngine.SndFadeOut);
                BeginFade(toWhite: true);
                State = 10;
                break;
            case 10:
                if (!AdvanceFade())
                    break;
                Stage = FrontendIntroStage.PreTitle;
                State = 0;
                break;
            default:
                throw InvalidState();
        }
        if (TriforceState != 0)
            TriforceMotionClock++;
        if (TempleWaveActive)
            TempleWaveClock++;
        // intro_cinematic calls updateAnimations after the stage handler.
        // State 0 has already performed the direct loadAnimationData call, so
        // its trailing animation update is the first one counted here.
        TempleBackgroundAnimationTick++;
    }

    private void AdvanceTempleInput()
    {
        if (_templeInputRemaining == 0)
        {
            if (_templeInputIndex >= _templeInput.Length)
            {
                _templeInputDone = true;
                return;
            }
            _templeInputRemaining = _templeInput[_templeInputIndex].A;
        }

        bool up = _templeInput[_templeInputIndex].B != 0;
        if (up)
        {
            TempleLinkY = unchecked((byte)(TempleLinkY - 1));
            if (TempleLinkY < 0x40 && TriforceState == 0)
            {
                TriforceState = 1;
                _triforceCounter = _data.Timing("triforce-converge");
                _energySoundPlayed = false;
                _playSound(OracleSoundEngine.SndDropEssence);
            }
        }
        else
        {
            // linkCutscene0 resets animation $00 whenever wLinkAngle retains
            // its stopped value with bit 7 set.
            TempleLinkAnimationClock = 0;
        }
        UpdateTempleCamera();

        _templeInputRemaining--;
        if (_templeInputRemaining == 0)
        {
            _templeInputIndex++;
            if (_templeInputIndex == _templeInput.Length)
                _templeInputDone = true;
        }
    }

    private bool TempleLinkIsWalking() =>
        State == 1 &&
        Counter == 0 &&
        !_templeInputDone &&
        _templeInputIndex < _templeInput.Length &&
        _templeInput[_templeInputIndex].B != 0;

    private void UpdateTempleCamera()
    {
        int screenY = unchecked((byte)(TempleLinkY - TempleCameraY - 0x40));
        int next = unchecked((byte)(TempleCameraY + screenY));
        if (next < 0x70)
            TempleCameraY = next;
    }

    private void AdvanceTriforceSequence()
    {
        if (TriforceState is not (1 or 2))
            return;
        if (TriforceState == 1 && !_energySoundPlayed &&
            _triforceCounter == _data.Timing("triforce-converge") - 300)
        {
            _energySoundPlayed = true;
            _playSound(OracleSoundEngine.SndEnergyThing);
        }
        if (--_triforceCounter != 0)
            return;
        if (TriforceState == 1)
        {
            TriforceState = 2;
            _triforceCounter = _data.Timing("triforce-link-rise");
            TempleLinkAnimation = 4;
            TempleLinkAnimationClock = 0;
            _playSound(OracleSoundEngine.SndAquamentusHover);
        }
        else
        {
            TriforceState = 3;
        }
    }

    private void AdvanceTempleFlash()
    {
        // screenFlashingData@data0: $02,$04,$06,$0c,$0e,$ff.
        _flashCounter++;
        FlashWhite = _flashCounter switch
        {
            < 2 => true,
            < 4 => false,
            < 6 => true,
            < 12 => false,
            < 14 => true,
            _ => false
        };
        if (_flashCounter < _data.Timing("temple-flash"))
            return;
        FlashWhite = false;
        _playSound(OracleSoundEngine.SndFairyCutscene);
        Counter = _data.Timing("temple-link-fall");
        TempleLinkAnimation = 5;
        TempleLinkAnimationClock = 0;
        TempleLinkBlinking = false;
        State = 7;
    }

    private void RunPreTitle()
    {
        TreeAnimationClock++;
        switch (State)
        {
            case 0:
                _screen.Scene = FrontendIntroScene.Tree;
                TreeScrollY = 0x70;
                TreeMapPhase = 0;
                TitleRevealPixels = 0;
                TreeBranchesVisible = true;
                CloudsVisible = false;
                _cloudsCreated = false;
                _cloudOffsetYFixed = 0;
                InitializeBirds();
                _treeScrollCounter = _data.Timing("tree-scroll-step");
                BeginFade(toWhite: false);
                _playSound(OracleSoundEngine.MusIntro2);
                State = 1;
                break;
            case 1:
                AdvanceFade();
                _treeScrollCounter--;
                if (_treeScrollCounter == 0)
                {
                    _treeScrollCounter = _data.Timing("tree-scroll-step");
                    TreeScrollY = unchecked((byte)(TreeScrollY - 1));
                    if (TreeScrollY == 0)
                        TreeBranchesVisible = false;
                    else if (TreeScrollY == 0xb0)
                        TreeMapPhase = 2;
                    else if (TreeScrollY == 0x10)
                    {
                        TreeMapPhase = 1;
                        _cloudsCreated = true;
                    }
                }
                UpdateBirds();
                UpdateClouds();
                if (TreeScrollY != 0x88)
                    break;
                _titleRevealIndex = 0;
                TitleRevealPixels = _data.Sequence("title-size")[0].A * 2;
                _titleSoundCounter = _data.Timing("title-sound-wait");
                State = 2;
                break;
            case 2:
                UpdateBirds();
                if (_titleSoundCounter > 0 && --_titleSoundCounter == 0)
                    _playSound(OracleSoundEngine.SndSwordObtained);
                if ((FrameCounter & 1) != 0)
                    break;
                if (_titleRevealIndex < 7)
                {
                    _titleRevealIndex++;
                    TitleRevealPixels =
                        _data.Sequence("title-size")[_titleRevealIndex].A * 2;
                    break;
                }
                if (_titleSoundCounter != 0)
                    break;
                _flashCounter = 0;
                State = 3;
                break;
            case 3:
                UpdateBirds();
                _flashCounter++;
                FlashWhite = _flashCounter is 1 or 2 or 5 or 6 or 9 or 10;
                if (_flashCounter < 14)
                    break;
                FlashWhite = false;
                EnterTitle();
                RunTitle(startPressed: false);
                break;
            default:
                throw InvalidState();
        }
    }

    private void InitializeBirds()
    {
        _birds.Clear();
        FrontendSequenceValue[] positions = _data.Sequence("bird-position");
        // The spawn loop decrements B before assigning the subid, so the
        // interaction slots—and therefore updates and RNG calls—are 7..0.
        for (int subid = positions.Length - 1; subid >= 0; subid--)
        {
            FrontendSequenceValue record = positions[subid];
            int x = record.B & 0xff;
            int delay = record.B >> 8;
            _birds.Add(new FrontendBirdState(
                subid,
                record.A,
                x,
                delay,
                subid < 4 ? 0x1a : 0x06));
        }
    }

    private void UpdateBirds()
    {
        OracleObjectVelocity left = OracleObjectSpeedTable.Shared.Get(0x32, 0x1a);
        OracleObjectVelocity right = OracleObjectSpeedTable.Shared.Get(0x32, 0x06);
        foreach (FrontendBirdState bird in _birds)
        {
            bird.RawY = unchecked((byte)((bird.YFixed >> 8) - TreeScrollY));
            switch (bird.Substate)
            {
                case 0:
                    if (TreeScrollY == 0x10)
                        bird.Substate = 1;
                    break;
                case 1:
                    bird.Counter1 = unchecked((byte)(bird.Counter1 - 1));
                    if (bird.Counter1 == 0)
                    {
                        bird.Substate = 2;
                        bird.Visible = true;
                    }
                    break;
                case 2:
                    bird.AnimationClock++;
                    if (bird.Counter2 > 0)
                        bird.Counter2--;
                    OracleObjectVelocity velocity = bird.Angle == 0x1a ? left : right;
                    bird.YFixed = unchecked(
                        (ushort)(bird.YFixed + velocity.YFixed));
                    bird.XFixed = unchecked((ushort)(bird.XFixed + velocity.XFixed));
                    if ((bird.XFixed >> 8) < 0xb0)
                        break;
                    if (bird.Counter2 == 0)
                    {
                        bird.Deleted = true;
                        bird.Visible = false;
                        break;
                    }
                    bird.Substate = 1;
                    bird.ResetPosition();
                    bird.Counter1 = (byte)(_random.Next().Value & 0x0f);
                    bird.Visible = false;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported intro bird substate {bird.Substate}.");
            }
        }
    }

    private void UpdateClouds()
    {
        if (!_cloudsCreated)
            return;
        if (!CloudsVisible)
        {
            if (TreeScrollY == 0xe0)
                CloudsVisible = true;
            return;
        }
        if (TreeScrollY == 0x88)
            return;
        OracleObjectVelocity velocity =
            OracleObjectSpeedTable.Shared.Get(0x05, 0x10);
        _cloudOffsetYFixed = unchecked(
            (ushort)(_cloudOffsetYFixed + velocity.YFixed));
    }

    private void RunTitle(bool startPressed)
    {
        // intro_titlescreen calls getRandomNumber_noPreserveVars before every
        // state dispatch, including initialization and both fade states.
        _random.Next();
        switch (State)
        {
            case 0:
                _restartSound();
                ShowTitle();
                Counter = _data.Timing("title-idle");
                _titleScreen.SetTitleBlink(true);
                _playSound(OracleSoundEngine.MusTitlescreen);
                State = 1;
                break;
            case 1:
                if (startPressed)
                {
                    _playSound(OracleSoundEngine.SndSelectItem);
                    _playSound(OracleSoundEngine.SndCtrlFastFadeOut);
                    BeginFade(toWhite: true);
                    State = 3;
                    break;
                }
                Counter--;
                _titleScreen.SetTitleBlink((Counter & 0x20) == 0);
                if (Counter != 0)
                    break;
                _playSound(OracleSoundEngine.SndCtrlFastFadeOut);
                BeginFade(toWhite: true);
                State = 2;
                break;
            case 2:
                if (!AdvanceFade())
                    break;
                Stage = FrontendIntroStage.Restart;
                State = 0;
                _restartStep = 0;
                break;
            case 3:
                if (!AdvanceFade())
                    break;
                IsActive = false;
                _openFileSelect();
                break;
            default:
                throw InvalidState();
        }
    }

    private void EnterTitle()
    {
        Stage = FrontendIntroStage.Title;
        State = 0;
        FlashWhite = false;
        SetWhiteFade(0.0f);
        ShowTitle();
    }

    private void RunRestart()
    {
        // intro_titlescreen_state2 -> stage $04, intro_restart -> stage $00,
        // then the clean-US Japanese stage advances to Capcom on the following
        // update. Keep those dispatch boundaries observable.
        if (_restartStep++ == 0)
        {
            Stage = FrontendIntroStage.Boot;
            State = 0;
            ShowIntro();
            SetWhiteFade(1.0f);
        }
    }

    private void BeginFade(bool toWhite, int divisor = 1)
    {
        _fadeToWhite = toWhite;
        _fadeUpdate = 0;
        _fadeDuration = _data.Timing("palette-fade") * divisor;
        SetWhiteFade(toWhite ? 0.0f : 1.0f);
    }

    private bool AdvanceFade()
    {
        if (_fadeDuration == 0)
            return true;
        _fadeUpdate = Math.Min(_fadeDuration, _fadeUpdate + 1);
        float progress = _fadeUpdate / (float)_fadeDuration;
        SetWhiteFade(_fadeToWhite ? progress : 1.0f - progress);
        if (_fadeUpdate != _fadeDuration)
            return false;
        _fadeDuration = 0;
        return true;
    }

    private void SetWhiteFade(float progress)
    {
        _screen.SetWhiteFade(progress);
        _titleScreen.SetWhiteFade(progress);
    }

    private void ShowIntro()
    {
        _screen.Visible = true;
        _titleScreen.Visible = false;
    }

    private void ShowTitle()
    {
        _screen.Visible = false;
        _titleScreen.Visible = true;
        _titleScreen.ShowTitle();
    }

    private void QueueRedraw()
    {
        if (_screen.Visible)
            _screen.QueueRedraw();
    }

    private InvalidOperationException InvalidState() =>
        new($"Unsupported frontend {Stage} state {State}.");
}

internal enum FrontendIntroStage
{
    Boot,
    Capcom,
    Horse,
    Temple,
    PreTitle,
    Title,
    Restart
}

internal enum FrontendIntroScene
{
    Capcom,
    HorseFar,
    HorseFront,
    HorseFace,
    HorseCloseup,
    Castle,
    Temple,
    Tree
}

internal sealed class FrontendBirdState
{
    private readonly int _initialX;
    private readonly int _initialDelay;

    internal int Subid { get; }
    internal int BaseY { get; }
    internal int Angle { get; }
    internal ushort XFixed { get; set; }
    internal ushort YFixed { get; set; }
    internal int RawY { get; set; }
    internal byte Counter1 { get; set; }
    internal byte Counter2 { get; set; } = 45;
    internal int Substate { get; set; }
    internal int AnimationClock { get; set; }
    internal bool Visible { get; set; }
    internal bool Deleted { get; set; }

    internal FrontendBirdState(
        int subid,
        int y,
        int x,
        int appearanceDelay,
        int angle)
    {
        Subid = subid;
        BaseY = y;
        _initialX = x;
        _initialDelay = appearanceDelay;
        Angle = angle;
        ResetPosition();
    }

    internal void ResetPosition()
    {
        XFixed = (ushort)((_initialX << 8) | (XFixed & 0xff));
        YFixed = (ushort)((BaseY << 8) | (YFixed & 0xff));
        Counter1 = (byte)_initialDelay;
    }
}
