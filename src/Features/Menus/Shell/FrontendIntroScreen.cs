using Godot;
using System;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>
/// Screen-space renderer for the retail boot/attract sequence. Backgrounds are
/// assembled from the original split VRAM sources and 32-byte-stride maps.
/// </summary>
internal partial class FrontendIntroScreen : Node2D
{
    private const int MapBytes = 32 * 32;
    private const int HorseFrontCloudLastY = 0x40;
    private const int HorseFrontWindowY = 0x68;
    private readonly FrontendIntroDatabase _data = FrontendIntroDatabase.Shared;
    private FrontendIntroController _controller = null!;
    private ShaderMaterial _fadeMaterial = null!;
    private Texture2D _capcom = null!;
    private Texture2D _horseFar = null!;
    private Texture2D _horseFront = null!;
    private Texture2D _horseFace = null!;
    private Texture2D _horseCloseup = null!;
    private Texture2D _castle = null!;
    private Texture2D _temple = null!;
    private Texture2D _animatedTemple = null!;
    private Texture2D[] _tree = null!;
    private int[] _templeWave = null!;
    private Image _horseFarSprites = null!;
    private Image _horseFrontSprites = null!;
    private Image _horseSparkleSprites = null!;
    private Image _closeupSprites = null!;
    private Image _castleSprites = null!;
    private Image _triforceSprites = null!;
    private Image _triforceGlowSprites = null!;
    private Image _treeSprites = null!;
    private Image _cloudSprites = null!;
    private Image _linkSprites = null!;
    private Color[,] _horseSpritePalette = null!;
    private Color[][,] _horseSpritePalettes = null!;
    private Color[,] _faceSpritePalette = null!;
    private Color[,] _closeupSpritePalette = null!;
    private Color[,] _castleSpritePalette = null!;
    private Color[,] _templePalette = null!;
    private Color[,] _templeSpritePalette = null!;
    private Color[,] _treeSpritePalette = null!;
    private OracleAnimationData _templeAnimations = null!;
    private int _templeAnimationSignature = int.MinValue;

    internal FrontendIntroScene Scene { get; set; }

    public override void _Ready()
    {
        _fadeMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    uniform float fade_offset = 0.0;
                    void fragment() {
                        vec4 pixel = texture(TEXTURE, UV) * COLOR;
                        pixel.rgb = sqrt(pixel.rgb);
                        pixel.rgb = min(pixel.rgb + vec3(fade_offset / 31.0), vec3(1.0));
                        COLOR = pixel;
                    }
                    """
            }
        };
        Material = _fadeMaterial;

        Color[,] capcomPalette = LoadPalette(
            "res://assets/oracle/intro/palette_capcom_bg.bin");
        Color[,] horsePalette = LoadPalette(
            "res://assets/oracle/intro/palette_horse_bg.bin");
        Color[,] horseFrontPalette = LoadPalette(
            "res://assets/oracle/intro/palette_horse_front_bg.bin");
        Color[,] facePalette = LoadPalette(
            "res://assets/oracle/intro/palette_face_bg.bin");
        Color[,] closeupPalette = LoadPalette(
            "res://assets/oracle/intro/palette_closeup_bg.bin");
        Color[,] castlePalette = LoadPalette(
            "res://assets/oracle/intro/palette_castle_bg.bin");
        _templePalette = LoadPalette(
            "res://assets/oracle/intro/palette_temple_bg.bin");
        Color[,] treePalette = LoadPalette(
            "res://assets/oracle/intro/palette_tree_bg.bin");

        _horseSpritePalette = LoadPalette(
            "res://assets/oracle/intro/palette_horse_sprites_0.bin");
        _horseSpritePalettes = new Color[4][,];
        for (int index = 0; index < _horseSpritePalettes.Length; index++)
        {
            _horseSpritePalettes[index] = LoadPalette(
                $"res://assets/oracle/intro/palette_horse_sprites_{index}.bin");
        }
        _faceSpritePalette = LoadPalette(
            "res://assets/oracle/intro/palette_face_sprites.bin");
        _closeupSpritePalette = LoadPalette(
            "res://assets/oracle/intro/palette_closeup_sprites.bin");
        _castleSpritePalette = LoadPalette(
            "res://assets/oracle/intro/palette_castle_sprites.bin");
        _templeSpritePalette = LoadPalette(
            "res://assets/oracle/intro/palette_temple_sprites.bin");
        _treeSpritePalette = LoadPalette(
            "res://assets/oracle/intro/palette_tree_sprites.bin");

        _horseFarSprites = LoadPng(
            "res://assets/oracle/intro/spr_link_on_horse_far.png");
        _horseFrontSprites = LoadPng(
            "res://assets/oracle/intro/spr_link_on_horse_front.png");
        _horseSparkleSprites = LoadPng(
            "res://assets/oracle/intro/spr_link_face_shot_sparkle.png");
        _closeupSprites = LoadPng(
            "res://assets/oracle/intro/spr_link_on_horse_closeup.png");
        _castleSprites = LoadPng(
            "res://assets/oracle/intro/spr_outside_castle.png");
        _triforceSprites = LoadPng(
            "res://assets/oracle/intro/spr_triforce.png");
        _triforceGlowSprites = LoadPng(
            "res://assets/oracle/intro/spr_triforce_glow.png");
        _treeSprites = LoadPng(
            "res://assets/oracle/intro/spr_tree_and_birds.png");
        _cloudSprites = LoadPng(
            "res://assets/oracle/intro/spr_clouds.png");
        _linkSprites = LoadPng("res://assets/oracle/gfx/spr_link.png");

        _capcom = BuildCapcom(capcomPalette);
        _horseFar = BuildHorseFar(horsePalette);
        _horseFront = BuildHorseFront(horseFrontPalette);
        _horseFace = BuildHorseFar(facePalette);
        _horseCloseup = BuildHorseCloseup(closeupPalette);
        _castle = BuildCastle(castlePalette);
        _templeAnimations = new OracleAnimationData();
        _temple = BuildTemple(_templePalette);
        _animatedTemple = ImageTexture.CreateFromImage(_temple.GetImage());
        _templeAnimationSignature = GetAnimationSignature(Array.Empty<int>());
        _tree = BuildTree(treePalette);
        _templeWave = BuildTempleWave();
        QueueRedraw();
    }

    internal void Attach(FrontendIntroController controller)
    {
        _controller = controller;
        QueueRedraw();
    }

    internal void SetWhiteFade(float progress)
    {
        float offset = Math.Min(
            31.0f,
            MathF.Floor(Math.Clamp(progress, 0.0f, 1.0f) * 32.0f));
        _fadeMaterial.SetShaderParameter("fade_offset", offset);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_controller is null)
            return;
        DrawRect(new Rect2(0, 0, 160, 144), Scene == FrontendIntroScene.Capcom
            ? Colors.White
            : Colors.Black);
        switch (Scene)
        {
            case FrontendIntroScene.Capcom:
                DrawScrolling(_capcom, 0, 0);
                break;
            case FrontendIntroScene.HorseFar:
                DrawHorseFar();
                break;
            case FrontendIntroScene.HorseFront:
                DrawHorseFront();
                break;
            case FrontendIntroScene.HorseFace:
                DrawHorseFace();
                break;
            case FrontendIntroScene.HorseCloseup:
                DrawHorseCloseup();
                break;
            case FrontendIntroScene.Castle:
                DrawCastle();
                break;
            case FrontendIntroScene.Temple:
                DrawTemple();
                break;
            case FrontendIntroScene.Tree:
                DrawTree();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported frontend scene {Scene}.");
        }
        if (_controller.BlackBarPixels > 0)
        {
            DrawRect(
                new Rect2(0, 0, 160, _controller.BlackBarPixels),
                Colors.Black);
            DrawRect(
                new Rect2(
                    0,
                    144 - _controller.BlackBarPixels,
                    160,
                    _controller.BlackBarPixels),
                Colors.Black);
        }
        if (_controller.FlashWhite)
            DrawRect(new Rect2(0, 0, 160, 144), Colors.White);
    }

    private void DrawHorseFar()
    {
        DrawScrolling(_horseFar, 0, _controller.HorseScrollY);
        int groundStart = 0xa8 - _controller.HorseScrollY;
        if (groundStart < 0x78)
        {
            for (int y = Math.Max(0, groundStart); y < 144; y++)
            {
                DrawScrollingScanline(
                    _horseFar,
                    y,
                    _controller.HorseGroundScrollX,
                    _controller.HorseScrollY);
            }
        }
        DrawAnimated(
            "horse-0", _horseFarSprites, 0, _horseSpritePalette,
            0xa0 - _controller.HorseScrollY, 0x70,
            _controller.HorseAnimationClock);
        DrawAnimated(
            "horse-3", _horseFarSprites, 0, _horseSpritePalette,
            0x90 - _controller.HorseScrollY, 0x50,
            _controller.HorseAnimationClock);
        DrawAnimated(
            "horse-6", _horseFarSprites, 0, _horseSpritePalette,
            0x80 - _controller.HorseScrollY, _controller.HorseBirdX,
            _controller.HorseAnimationClock);
    }

    private void DrawHorseFront()
    {
        // Gfx state $18 starts with wGfxRegs1.SCX for the clouds, switches
        // to wGfxRegs2.SCX after the LYC $40 hblank, and starts the
        // SCX-independent priority window at WY $68.
        for (int y = 0; y < HorseFrontWindowY; y++)
        {
            DrawScrollingScanline(
                _horseFront,
                y,
                HorseFrontBackgroundScrollX(
                    y,
                    _controller.HorseCloudScrollX,
                    _controller.HorseMountainScrollX),
                0x98);
        }
        DrawAnimated(
            "horse-1", _horseFrontSprites, 0,
            _horseSpritePalettes[_controller.HorseSpritePalette],
            _controller.HorseFrontY,
            _controller.HorseFrontX,
            _controller.HorseAnimationClock);
        DrawAbsoluteOam(
            _data.StaticOam("front-facing-link"),
            _horseFrontSprites,
            0,
            _horseSpritePalettes[_controller.HorseSpritePalette]);

        // UNCMP_GFXH_AGES_38 copies the staged ground into the window map.
        // Every imported ground attribute has CGB BG-priority set, so the
        // window covers the horse and the two fixed rock touch-up cells.
        for (int y = HorseFrontWindowY; y < 144; y++)
            DrawScrollingScanline(_horseFront, y, 0, 0x98);
    }

    internal static int HorseFrontBackgroundScrollX(
        int screenY,
        int cloudScrollX,
        int mountainScrollX)
    {
        if (screenY < 0 || screenY >= HorseFrontWindowY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(screenY),
                screenY,
                $"Horse-front background scanline must be $00-${HorseFrontWindowY - 1:x2}.");
        }
        return screenY <= HorseFrontCloudLastY
            ? cloudScrollX
            : mountainScrollX;
    }

    private void DrawHorseFace()
    {
        // Gfx state $19 begins on the cleared $9c00 map, switches after
        // wGfxRegs1.LYC to the $9800 face map at SCY $98, then starts the
        // cleared $9c00 priority window at wGfxRegs2.WINY. Only the narrow
        // interval between those two boundaries contains the moving face.
        FrontendSequenceValue faceRegisters =
            _data.Sequence("horse-face-registers")[1];
        for (int y = _controller.HorseFaceTopBarLastY + 1;
             y < _controller.HorseFaceBottomBarY;
             y++)
        {
            DrawScrollingScanline(
                _horseFace,
                y,
                _controller.HorseFaceScrollX,
                faceRegisters.A);
        }

        if (!_controller.HorseFaceSparkleVisible)
            return;
        FrontendSequenceValue[] sparkle =
            _data.Sequence("horse-face-sparkle");
        DrawAnimated(
            $"horse-{sparkle[1].A}",
            _horseSparkleSprites,
            0x80,
            _faceSpritePalette,
            sparkle[0].A,
            sparkle[0].B,
            _controller.HorseFaceSparkleClock);
    }

    private void DrawHorseCloseup()
    {
        DrawScrolling(_horseCloseup, 0, _controller.HorseScrollY);
        DrawAbsoluteOam(
            _data.StaticOam("closeup-touchup"),
            _closeupSprites,
            0,
            _closeupSpritePalette,
            -_controller.HorseScrollY,
            0);
    }

    private void DrawCastle()
    {
        DrawScrolling(_castle, _controller.CastleScrollX, 0);
        DrawAbsoluteOam(
            _data.StaticOam("castle-touchup"),
            _castleSprites,
            0,
            _castleSpritePalette,
            0,
            -_controller.CastleScrollX);
        DrawAnimatedFrame(
            $"horse-{_controller.CastleHorseAnimation}",
            _castleSprites,
            0,
            _castleSpritePalette,
            _controller.CastleActorY,
            _controller.CastleActorX,
            _controller.CastleHorseAnimationFrame);
        DrawAnimatedFrame(
            $"horse-{_controller.CastleStaticAnimation}",
            _castleSprites,
            0,
            _castleSpritePalette,
            _controller.CastleActorY,
            _controller.CastleActorX,
            0);
    }

    private void DrawTemple()
    {
        Texture2D temple = ResolveTempleBackground();
        if (_controller.TempleWaveActive)
        {
            for (int y = 0; y < 144; y++)
            {
                int phase = (_controller.TempleWaveClock + y * 2) & 0x7f;
                int scrollX = _templeWave[phase];
                DrawScrollingScanline(
                    temple, y, scrollX, _controller.TempleCameraY);
            }
        }
        else
        {
            DrawScrolling(temple, 0, _controller.TempleCameraY);
        }

        // introSpritesState1 only deletes the pieces for triforceState $04.
        // The temple sequence advances from $03 to $06, so they remain alive
        // while Link falls and while the wave distortion is active.
        if (_controller.TriforceState != 4)
        {
            FrontendSequenceValue[] positions =
                _data.Sequence("triforce-position");
            FrontendSequenceValue[] motions =
                _data.Sequence("triforce-motion");
            int glowCounter = 3 - (_controller.TempleAnimationClock % 3);
            for (int subid = 0; subid < positions.Length; subid++)
            {
                int moveUpdates = Math.Clamp(
                    _controller.TriforceMotionClock - motions[subid].A + 1,
                    0,
                    motions[subid].B >> 8);
                OracleObjectVelocity velocity = OracleObjectSpeedTable.Shared.Get(
                    0x05, motions[subid].B & 0xff);
                int yFixed = (positions[subid].A << 8) +
                    velocity.YFixed * moveUpdates;
                int xFixed = (positions[subid].B << 8) +
                    velocity.XFixed * moveUpdates;
                int y = unchecked((byte)((yFixed >> 8) -
                    _controller.TempleCameraY));
                int x = unchecked((byte)(xFixed >> 8));
                if (subid + 1 == glowCounter)
                {
                    DrawAnimated(
                        "triforce-glow", _triforceGlowSprites, 0,
                        _templeSpritePalette, y, x,
                        _controller.TempleAnimationClock);
                }
                DrawAnimated(
                    "triforce", _triforceSprites, 0, _templeSpritePalette,
                    y, x, _controller.TempleAnimationClock);
            }
        }

        if (_controller.TempleLinkVisible &&
            (!_controller.TempleLinkBlinking ||
                (_controller.FrameCounter & 1) != 0))
        {
            DrawTempleLink();
        }
    }

    private int[] BuildTempleWave()
    {
        FrontendSequenceValue[] sine = _data.Sequence("temple-wave-sine");
        var wave = new int[128];
        for (int index = 0; index < sine.Length; index++)
        {
            // multiplyAByC with C=$20 retains the high byte of the product.
            wave[index] = sine[index].A * 0x20 >> 8;
        }
        for (int index = 0; index < 32; index++)
            wave[32 + index] = wave[31 - index];
        for (int index = 0; index < 64; index++)
            wave[64 + index] = -wave[63 - index];
        return wave;
    }

    private void DrawTempleLink()
    {
        int rawY = unchecked((byte)(_controller.TempleLinkY -
            _controller.TempleCameraY));
        string animation = _controller.TempleLinkAnimation switch
        {
            0 => "temple-link-walk",
            4 => "temple-link-rise",
            5 => "temple-link-fall",
            _ => throw new InvalidOperationException(
                $"Unsupported Temple Link animation " +
                $"{_controller.TempleLinkAnimation:x2}.")
        };
        DrawAnimated(
            animation,
            _linkSprites,
            0,
            _templeSpritePalette,
            rawY,
            0x50,
            _controller.TempleLinkAnimationClock);
    }

    private void DrawTree()
    {
        Texture2D background =
            _tree[Math.Clamp(_controller.TreeMapPhase, 0, 2)];
        if (_controller.TitleRevealPixels == 0)
        {
            DrawScrolling(background, 0, _controller.TreeScrollY);
        }
        else
        {
            byte[] scanlineScroll = new byte[144];
            Array.Fill(scanlineScroll, (byte)0x88);
            int halfHeight = _controller.TitleRevealPixels / 2;
            int shrink = 0x18 / halfHeight;
            int source = 0;
            int line = 0x38 - halfHeight;
            for (int count = 0; count < halfHeight; count++)
            {
                scanlineScroll[line] = unchecked((byte)(source - line + 0x58));
                source += shrink;
                line++;
            }
            line = 0x37 + halfHeight;
            source = 0x2f;
            for (int count = 0; count < halfHeight; count++)
            {
                scanlineScroll[line] = unchecked((byte)(source - line + 0x58));
                source -= shrink;
                line--;
            }
            for (int y = 0; y < scanlineScroll.Length; y++)
                DrawScrollingScanline(background, y, 0, scanlineScroll[y]);
        }

        if (_controller.TreeBranchesVisible)
        {
            DrawAnimated(
                "tree-branches", _treeSprites, 0, _treeSpritePalette,
                unchecked((byte)(0x60 - _controller.TreeScrollY)),
                0x3d,
                _controller.TreeAnimationClock);
        }
        foreach (FrontendBirdState bird in _controller.Birds)
        {
            if (!bird.Visible || bird.Deleted)
                continue;
            DrawAnimated(
                bird.Subid < 4 ? "bird-0" : "bird-1",
                _treeSprites,
                0,
                _treeSpritePalette,
                bird.RawY,
                bird.XFixed >> 8,
                bird.AnimationClock);
        }
        if (_controller.CloudsVisible)
        {
            FrontendSequenceValue[] clouds = _data.Sequence("cloud-position");
            for (int index = 0; index < clouds.Length; index++)
            {
                DrawAnimated(
                    $"cloud-{index}", _cloudSprites, 0, _treeSpritePalette,
                    unchecked((byte)(clouds[index].A +
                        _controller.CloudOffsetY - _controller.TreeScrollY)),
                    clouds[index].B,
                    _controller.TreeAnimationClock);
            }
        }
    }

    private void DrawAnimated(
        string kind,
        Image source,
        int tileBase,
        Color[,] palettes,
        int objectY,
        int objectX,
        int clock)
    {
        FrontendAnimation animation = _data.Animation(kind);
        FrontendAnimationFrame frame = SelectFrame(animation, clock);
        DrawAnimationFrame(
            frame,
            source,
            tileBase,
            palettes,
            objectY,
            objectX);
    }

    private void DrawAnimatedFrame(
        string kind,
        Image source,
        int tileBase,
        Color[,] palettes,
        int objectY,
        int objectX,
        int frameIndex)
    {
        FrontendAnimation animation = _data.Animation(kind);
        if ((uint)frameIndex >= animation.Frames.Length)
        {
            throw new InvalidOperationException(
                $"Frontend animation {kind} has no frame {frameIndex}.");
        }
        DrawAnimationFrame(
            animation.Frames[frameIndex],
            source,
            tileBase,
            palettes,
            objectY,
            objectX);
    }

    private void DrawAnimationFrame(
        FrontendAnimationFrame frame,
        Image source,
        int tileBase,
        Color[,] palettes,
        int objectY,
        int objectX)
    {
        for (int index = frame.Parts.Length - 1; index >= 0; index--)
        {
            FrontendOamPart part = frame.Parts[index];
            int rawY = ObjectPartRawY(objectY, part.Y);
            int rawX = (objectX + part.X) & 0xff;
            int palette = (frame.BasePalette ^ part.Flags) & 7;
            DrawOamCell(
                source,
                tileBase,
                part.Tile,
                palettes,
                (part.Flags & 0xf8) | palette,
                rawY,
                rawX,
                frame.SourceTileOffset,
                frame.SourceGrayscaleInverted);
        }
    }

    internal static int ObjectPartRawY(int objectY, int partY)
    {
        // bank0._getObjectPositionOnScreen adds $10 before the object's OAM
        // offsets. The hardware then subtracts 16 from the raw OAM Y, so an
        // object's yh remains its screen-space origin when hCameraY is zero.
        return (objectY + 0x10 + partY) & 0xff;
    }

    private void DrawAbsoluteOam(
        FrontendOamPart[] parts,
        Image source,
        int tileBase,
        Color[,] palettes,
        int yOffset = 0,
        int xOffset = 0)
    {
        for (int index = parts.Length - 1; index >= 0; index--)
        {
            FrontendOamPart part = parts[index];
            DrawOamCell(
                source,
                tileBase,
                part.Tile,
                palettes,
                part.Flags,
                (part.Y + yOffset) & 0xff,
                (part.X + xOffset) & 0xff);
        }
    }

    private void DrawOamCell(
        Image source,
        int tileBase,
        int tile,
        Color[,] palettes,
        int flags,
        int rawY,
        int rawX,
        int sourceTileOffset = 0,
        bool sourceGrayscaleInverted = true)
    {
        if (rawY >= 0xa0 || rawX >= 0xa8)
            return;
        int sourceTile = tile - tileBase + sourceTileOffset;
        if (sourceTile < 0 || sourceTile / 2 >=
            source.GetWidth() / 8 * (source.GetHeight() / 16))
        {
            return;
        }
        Texture2D cell = OracleTileRenderer.GetOamCellTexture(
            source,
            sourceTile,
            (byte)flags,
            palettes,
            sourceGrayscaleInverted);
        DrawTexture(cell, new Vector2(rawX - 8, rawY - 16));
    }

    private static FrontendAnimationFrame SelectFrame(
        FrontendAnimation animation,
        int clock)
    {
        int remaining = Math.Max(0, clock);
        int index = 0;
        while (remaining >= animation.Frames[index].Duration)
        {
            remaining -= animation.Frames[index].Duration;
            index++;
            if (index >= animation.Frames.Length)
                index = animation.LoopStart;
        }
        return animation.Frames[index];
    }

    private void DrawScrolling(Texture2D texture, int scrollX, int scrollY)
    {
        int x = -(scrollX & 0xff);
        int y = -(scrollY & 0xff);
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < 2; column++)
        {
            DrawTexture(texture, new Vector2(x + column * 256, y + row * 256));
        }
    }

    private void DrawScrollingScanline(
        Texture2D texture,
        int screenY,
        int scrollX,
        int scrollY)
    {
        int sourceX = scrollX & 0xff;
        int sourceY = (scrollY + screenY) & 0xff;
        int firstWidth = Math.Min(160, 256 - sourceX);
        DrawTextureRectRegion(
            texture,
            new Rect2(0, screenY, firstWidth, 1),
            new Rect2(sourceX, sourceY, firstWidth, 1));
        if (firstWidth == 160)
            return;
        DrawTextureRectRegion(
            texture,
            new Rect2(firstWidth, screenY, 160 - firstWidth, 1),
            new Rect2(0, sourceY, 160 - firstWidth, 1));
    }

    private static Texture2D BuildCapcom(Color[,] palette)
    {
        byte[] map = new byte[MapBytes];
        byte[] flags = new byte[MapBytes];
        Overlay(map, ReadBytes(
            "res://assets/oracle/intro/map_capcom_nintendo.bin", 320), 0x80);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/intro/flags_capcom_nintendo.bin", 320), 0x80);
        return BuildBackground(
            map, flags, palette,
            ("res://assets/oracle/intro/gfx_capcom_nintendo.png", 0x8800, 0));
    }

    private static Texture2D BuildHorseFar(Color[,] palette)
    {
        byte[] map = new byte[MapBytes];
        byte[] flags = new byte[MapBytes];
        Overlay(map, ReadBytes(
            "res://assets/oracle/intro/map_link_on_horse_far.bin", 672), 0x60);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/intro/flags_link_on_horse_far.bin", 672), 0x60);
        Overlay(map, ReadBytes(
            "res://assets/oracle/intro/map_link_face_shot.bin", 192), 0x320);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/intro/flags_link_face_shot.bin", 192), 0x320);
        return BuildBackground(
            map, flags, palette,
            ("res://assets/oracle/intro/gfx_link_on_horse_front_bg.png", 0x8800, 0),
            ("res://assets/oracle/intro/gfx_link_face_shot.png", 0x9000, 0),
            ("res://assets/oracle/intro/gfx_link_on_horse_far_bg_1.png", 0x8c00, 1),
            ("res://assets/oracle/intro/gfx_link_on_horse_far_bg_2.png", 0x9000, 1));
    }

    private static Texture2D BuildHorseFront(Color[,] palette)
    {
        byte[] map = new byte[MapBytes];
        byte[] flags = new byte[MapBytes];
        Overlay(map, ReadBytes(
            "res://assets/oracle/intro/map_link_on_horse_front_ground.bin", 160), 0);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/intro/flags_link_on_horse_front_ground.bin", 160), 0);
        Overlay(map, ReadBytes(
            "res://assets/oracle/intro/map_bar.bin", 96), 0x260);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/intro/flags_bar.bin", 96), 0x260);
        Overlay(map, ReadBytes(
            "res://assets/oracle/intro/map_link_on_horse_front_bg.bin", 320), 0x2c0);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/intro/flags_link_on_horse_front_bg.bin", 320), 0x2c0);
        return BuildBackground(
            map, flags, palette,
            ("res://assets/oracle/intro/gfx_link_on_horse_front_bg.png", 0x8800, 0),
            ("res://assets/oracle/intro/gfx_link_face_shot.png", 0x9000, 0));
    }

    private static Texture2D BuildHorseCloseup(Color[,] palette) =>
        BuildBackground(
            ReadBytes("res://assets/oracle/intro/map_link_on_horse_closeup.bin", MapBytes),
            ReadBytes("res://assets/oracle/intro/flags_link_on_horse_closeup.bin", MapBytes),
            palette,
            ("res://assets/oracle/intro/gfx_link_on_horse_closeup_1.png", 0x8800, 0),
            ("res://assets/oracle/intro/gfx_link_on_horse_closeup_2.png", 0x9000, 0),
            ("res://assets/oracle/intro/gfx_link_on_horse_closeup_3.png", 0x8800, 1),
            ("res://assets/oracle/intro/gfx_link_on_horse_closeup_4.png", 0x9000, 1));

    private static Texture2D BuildCastle(Color[,] palette) =>
        BuildBackground(
            ReadBytes("res://assets/oracle/intro/map_outside_castle.bin", 576),
            ReadBytes("res://assets/oracle/intro/flags_outside_castle.bin", 576),
            palette,
            ("res://assets/oracle/intro/gfx_outside_castle_1.png", 0x8800, 0),
            ("res://assets/oracle/intro/gfx_outside_castle_2.png", 0x9000, 0),
            ("res://assets/oracle/intro/gfx_outside_castle_3.png", 0x8800, 1));

    private static Texture2D BuildTemple(Color[,] palette) =>
        BuildTemple(palette, animations: null, Array.Empty<int>());

    private static Texture2D BuildTemple(
        Color[,] palette,
        OracleAnimationData? animations,
        int[] activeHeaders) =>
        BuildBackground(
            ReadBytes("res://assets/oracle/intro/map_triforce_room.bin", MapBytes),
            ReadBytes("res://assets/oracle/intro/flags_triforce_room.bin", MapBytes),
            palette,
            animations,
            activeHeaders,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0),
            ("res://assets/oracle/intro/gfx_temple_1.png", 0x8800, 1),
            ("res://assets/oracle/intro/gfx_temple_2.png", 0x9000, 1),
            ("res://assets/oracle/intro/gfx_temple_3.png", 0x9400, 1));

    private static Texture2D[] BuildTree(Color[,] palette)
    {
        byte[] map0 = ReadBytes(
            "res://assets/oracle/intro/map_titlescreen_scroll_1.bin", MapBytes);
        byte[] flags0 = ReadBytes(
            "res://assets/oracle/intro/flags_titlescreen_scroll_1.bin", MapBytes);
        byte[] scrollMap = ReadBytes(
            "res://assets/oracle/intro/map_titlescreen_scroll_2.bin", 672);
        byte[] scrollFlags = ReadBytes(
            "res://assets/oracle/intro/flags_titlescreen_scroll_2.bin", 672);
        byte[] map1 = (byte[])map0.Clone();
        byte[] flags1 = (byte[])flags0.Clone();
        Array.Copy(scrollMap, 0x140, map1, 0x2a0, 0x160);
        Array.Copy(scrollFlags, 0x140, flags1, 0x2a0, 0x160);
        byte[] map2 = (byte[])map1.Clone();
        byte[] flags2 = (byte[])flags1.Clone();
        Array.Copy(scrollMap, 0, map2, 0x160, 0x160);
        Array.Copy(scrollFlags, 0, flags2, 0x160, 0x160);
        return
        [
            BuildTreeBackground(map0, flags0, palette),
            BuildTreeBackground(map1, flags1, palette),
            BuildTreeBackground(map2, flags2, palette)
        ];
    }

    private static Texture2D BuildTreeBackground(
        byte[] map,
        byte[] flags,
        Color[,] palette) =>
        BuildBackground(
            map, flags, palette,
            ("res://assets/oracle/menu/gfx_titlescreen_5.png", 0x8800, 0),
            ("res://assets/oracle/menu/gfx_titlescreen_2.png", 0x8d00, 0),
            ("res://assets/oracle/intro/gfx_titlescreen_7.png", 0x9300, 0),
            ("res://assets/oracle/menu/gfx_titlescreen_4.png", 0x9400, 0),
            ("res://assets/oracle/intro/gfx_titlescreen_tree_1.png", 0x8800, 1),
            ("res://assets/oracle/intro/gfx_titlescreen_tree_2.png", 0x9000, 1));

    private static Texture2D BuildBackground(
        byte[] map,
        byte[] flags,
        Color[,] palette,
        params (string Path, int Destination, int Bank)[] sources)
        => BuildBackground(
            map, flags, palette, animations: null, Array.Empty<int>(), sources);

    private static Texture2D BuildBackground(
        byte[] map,
        byte[] flags,
        Color[,] palette,
        OracleAnimationData? animations,
        int[] activeHeaders,
        params (string Path, int Destination, int Bank)[] sources)
    {
        if (map.Length != MapBytes || flags.Length != MapBytes)
        {
            byte[] expandedMap = new byte[MapBytes];
            byte[] expandedFlags = new byte[MapBytes];
            Overlay(expandedMap, map, 0);
            Overlay(expandedFlags, flags, 0);
            map = expandedMap;
            flags = expandedFlags;
        }
        var tiles = new OracleVramTileMap();
        foreach ((string path, int destination, int bank) in sources)
            tiles.Map(LoadPng(path), destination, bank);
        if (animations is not null)
        {
            for (int destination = 0; destination < 256; destination++)
            {
                if (!animations.TryGetOverride(
                    activeHeaders, destination,
                    out Image source, out int sourceTile))
                {
                    continue;
                }

                // Imported animation destinations use the signed tile-source
                // index ($8800-$97ff -> $00-$ff). Convert back to the tile ID
                // stored in the BG map; m_GfxHeaderAnim writes bank 1.
                int tileId = destination < 0x80
                    ? destination + 0x80
                    : destination - 0x80;
                tiles.MapTile(source, sourceTile, tileId, bank: 1);
            }
        }
        return OracleTileRenderer.BuildTileMapTexture(
            map, flags, tiles, palette, 32, 32);
    }

    private Texture2D ResolveTempleBackground()
    {
        int[] activeHeaders = _templeAnimations.GetActiveHeadersAfterDirectLoad(
            _controller.TempleBackgroundAnimationGroup,
            _controller.TempleBackgroundAnimationTick);
        int signature = GetAnimationSignature(activeHeaders);
        if (signature == _templeAnimationSignature)
            return _animatedTemple;

        Texture2D updated = BuildTemple(
            _templePalette, _templeAnimations, activeHeaders);
        Image updatedImage = updated.GetImage();
        updated.Dispose();
        ((ImageTexture)_animatedTemple).SetImage(updatedImage);
        _templeAnimationSignature = signature;
        return _animatedTemple;
    }

    private static int GetAnimationSignature(int[] headers)
    {
        int signature = 17;
        foreach (int header in headers)
            signature = signature * 31 + header;
        return signature;
    }

    internal ulong TempleAnimationPixelHashForValidation(int group, long tick)
    {
        int[] activeHeaders =
            _templeAnimations.GetActiveHeadersAfterDirectLoad(group, tick);
        Texture2D texture =
            BuildTemple(_templePalette, _templeAnimations, activeHeaders);
        ulong hash = OracleGraphicsCache.PixelHash(texture.GetImage());
        texture.Dispose();
        return hash;
    }

    internal ulong[] AssetPixelHashesForValidation()
    {
        Texture2D[] textures =
        [
            _capcom, _horseFar, _horseFront, _horseFace, _horseCloseup,
            _castle, _temple, _tree[0], _tree[1], _tree[2]
        ];
        return Array.ConvertAll(
            textures,
            texture => OracleGraphicsCache.PixelHash(texture.GetImage()));
    }
}
