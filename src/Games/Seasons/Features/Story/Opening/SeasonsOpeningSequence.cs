using Godot;
using System;

namespace Oracles;

// code/seasons/cutscenes/introCutscenes.s: native controllers $06-$08.
internal sealed class SeasonsOpeningSequence(SeasonsOpeningSession session, SeasonsOpeningScreen screen)
{
    private int _kind, _state, _counter, _index, _scrollRemaining, _animationTicks, _shakeFrames, _shakePhase;
    private int _templeCounter, _templeScene, _windowY, _sinkTicks, _paletteCycle, _paletteTicks;
    private Color[,]? _paletteFrom, _paletteTo;
    private Color[,]? _basePalette;
    private int _paletteFrame;
    internal bool SkipObjects => _kind == 1 && _state is 2 or 3;
    internal void StartDance() { _kind = 1; _state = 0; session.SetPhase(SeasonsOpeningPhase.Dance); }
    internal void Shake(int frames) => _shakeFrames = frames;
    private bool Tick() { _counter--; return _counter == 0; }
    private void PaletteTo(int address)
    {
        _paletteFrom = session.Data.World.Palettes.Capture();
        _paletteTo = OracleGraphicsData.LoadPalette(SeasonsOpeningData.Root + $"opening/palette-{address:x4}.bin", 6, 2);
        _paletteFrame = 32;
    }
    internal void AdvanceFrame()
    {
        if (_kind == 0) return;
        if (_paletteFrame != 0 && --_paletteFrame != 0) MixPalette(_paletteFrame);
        if (_kind == 1) Dance(); else if (_kind == 2) Imprisonment(); else Temple();
        session.UpdateNativeCamera(screen.CameraX, screen.CameraY);
    }
    private void MixPalette(int remaining)
    {
        int from = (remaining >> 1) & 15, to = 16 - from;
        var colors = session.Data.World.Palettes.Capture();
        int first = (remaining & 1) == 0 ? 5 : 2;
        for (int palette = first; palette < first + 3; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            Color a = _paletteFrom![palette, shade], b = _paletteTo![palette, shade];
            float Mix(float x, float y) => ((Mathf.RoundToInt(x * 31) * from + Mathf.RoundToInt(y * 31) * to) >> 4) / 31f;
            colors[palette, shade] = new(Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B));
        }
        session.Data.World.Palettes.Restore(colors);
    }
    internal void AdvanceShake()
    {
        Vector2 offset = Vector2.Zero;
        if (_shakeFrames > 0)
        {
            int[] amounts = [-2,-1,1,2];
            int y = amounts[session.Random.Next().Value & 3], x = amounts[session.Random.Next().Value & 3];
            offset = new(x, y); _shakeFrames--;
        }
        session.SetRoomShake(offset);
    }
    private void Dance()
    {
        switch (_state)
        {
            case 0:
                session.Sound.PlaySound(SoundId.SndCloseMenu); session.Signal(0, 1);
                screen.ShowDance(); session.SetFullScreen(true); session.Fade(false, fast: true);
                _counter = 600; _scrollRemaining = 40; _state = 2; break;
            case 2:
                if (session.Fading) return;
                if ((session.FrameCounter & 7) == 0 && _scrollRemaining > 0) { _scrollRemaining--; screen.ScrollX++; screen.QueueRedraw(); }
                if (!Tick() && !(_counter < 240 && Input.IsActionJustPressed("attack"))) return;
                session.Sound.PlaySound(SoundId.SndCloseMenu); session.Fade(true, fast: true); _state = 3; break;
            case 3:
                if (session.Fading) return;
                screen.Hide(); session.SetFullScreen(false); session.Signal(0, 0); session.Fade(false); _state = 4; break;
            case 4:
                if (session.Signal(0) != 2) return;
                _index = 0; Strike(); PaletteTo(0x44e8);
                session.Sound.PlaySound(SoundId.SndCtrlStopMusic); _state = 5;
                session.SetPhase(SeasonsOpeningPhase.Capture); break;
            case 5:
                if (_paletteFrame != 0) return;
                session.Signal(0, 3); ApplyLightningTiles();
                if (!Tick()) return;
                if (++_index < 10) Strike(); else { _counter = 60; _state = 6; }
                break;
            case 6:
                if (!Tick()) return;
                session.ShowText(0x0c08); _state = 7; break;
            case 7:
                if (session.Dialogue.IsOpen) return;
                session.StrikeLightning(0x10, new(0x18, 0x70)); _state = 8; break;
            case 8:
                if ((session.Signal(2) & 0x10) == 0) return;
                session.Spawn(0x4e, 7, Vector2.Zero); _state = 9; break;
            case 9:
                if (session.Signal(0) != 5) return;
                session.ShowText(0x0c09); _state = 10; break;
            case 10:
                if (session.Dialogue.IsOpen) return;
                session.Signal(0, 6); _state = 11; break;
            case 11:
                if (session.Signal(0) != 8) return;
                session.ShowText(0x0c14); _state = 12; break;
            case 12:
                if (session.Dialogue.IsOpen) return;
                _counter = 30; _state = 13; break;
            case 13:
                if (!Tick()) return;
                session.Signal(0, 9); _state = 14; break;
            case 14:
                if (session.Signal(0) != 0x0b) return;
                _counter = 60; _state = 15; break;
            case 15:
                if (!Tick()) return;
                session.ClearActors(); session.ReleaseLink(); session.Link.Hide(); session.Save.SetGlobalFlag(0x30, false);
                _kind = 2; _state = 0; session.Fade(true);
                session.SetPhase(SeasonsOpeningPhase.Imprisonment); break;
        }
    }
    private void Strike()
    {
        int[][] data = [[60,0,0x50,0x20],[60,1,0x70,0x58],[40,0,0x40,0x80],[40,0,0x18,0x30],
            [30,2,0x10,0x80],[30,0,0x40,0x48],[20,0,0x20,0x70],[20,4,0x78,0x88],[20,8,0x70,0x70],[20,0,0x40,0x40]];
        int[] row = data[_index]; _counter = row[0]; session.StrikeLightning(row[1], new(row[3], row[2]));
    }
    private void ApplyLightningTiles()
    {
        int[][] positions = [[0x65,0x75],[0x07,0x08,0x18],[0x78],[0x66,0x67,0x76,0x77]];
        for (int bit = 3; bit >= 0; bit--)
        {
            if ((session.Signal(2) & (1 << bit)) == 0) continue;
            session.Signal(2, session.Signal(2) & ~(1 << bit));
            foreach (int position in positions[bit])
            {
                // cutsceneDinDancing_loadListOfTiles redraws the current room's wRoomLayout.
                byte tile = session.Room.GetPackedStorageMetatile((byte)position);
                session.Rooms.TrySetTile((byte)position, tile);
            }
        }
    }
    private void OutsideCastle(int registers)
    {
        screen.Load("GFXH_SCENE_OUTSIDE_ONOX_CASTLE", "PALH_TILESET_ONOX_CASTLE_OUTSIDE_WINTER", registers);
        session.SetFullScreen(true);
        session.Spawn(0x88, registers == 9 ? 0 : 1, Vector2.Zero);
    }
    private void Imprisonment()
    {
        if (_state is >= 3 and <= 7) screen.Animate(0x18, ++_animationTicks);
        switch (_state)
        {
            case 0:
                if (session.Fading) return;
                _state++; session.Signal(0, 9); _counter = 0x158;
                OutsideCastle(9); session.Sound.PlaySound(0x1c); session.Fade(false); break;
            case 1:
                if (session.Fading) return;
                if (Tick()) { screen.ScrollY = 0; _state++; session.Fade(true); }
                else if ((_counter & 1) == 0 && screen.ScrollY != 0) { screen.ScrollY--; screen.CameraY = screen.ScrollY; screen.QueueRedraw(); }
                break;
            case 2:
                if (session.Fading) return;
                _state++; session.Signal(0, 0x0a);
                screen.Load("GFXH_SCENE_INSIDE_ONOX_CASTLE", "PALH_SEASONS_97", 0x0d); _animationTicks = 0;
                session.Spawn(0x4f, 0, Vector2.Zero); session.Spawn(0x4f, 1, Vector2.Zero); session.Fade(false); break;
            case 3:
                if (session.Signal(0) != 0x0b) return;
                for (int index = 3; index >= 0; index--) session.Spawn(0x4f, 2, Vector2.Zero, index);
                _state++; break;
            case 4:
                if (session.Signal(0) != 0x0c) return;
                _state++; _counter = 0; break;
            case 5:
                _counter++;
                if (_counter < 17) { session.SetFlash(_counter is <= 2 or > 4 and <= 6 or > 8 and <= 12 or > 14 and <= 16); return; }
                session.SetFlash(false); screen.ClearCastleForCrystal();
                session.SetCrystalFlash(true);
                session.ShowText(0x1e05); session.Signal(0, 0x0d); _state++; break;
            case 6:
                if (session.Dialogue.IsOpen) return;
                session.SetCrystalFlash(false);
                screen.RestoreCastle(); session.Sound.PlaySound(SoundId.SndClink); session.Fade(false); _counter = 240; _state++; break;
            case 7:
                if (session.Fading || !Tick()) return;
                _state++; session.Fade(true); break;
            case 8:
                if (session.Fading) return;
                _state++; session.Signal(0, 0x0e); OutsideCastle(7); session.Fade(false); break;
            case 9:
                if (session.Fading || session.Signal(0) != 0x0f) return;
                session.ClearActors(); _kind = 3; _state = 0; session.Fade(true);
                session.SetPhase(SeasonsOpeningPhase.TempleSinking); break;
        }
    }
    private void Temple()
    {
        switch (_state)
        {
            case 0:
                if (session.Fading) return;
                screen.Load("GFXH_TEMPLEFALL_SCENE1", "PALH_SEASONS_98", 0x0e);
                session.Spawn(0x77, 1, Vector2.Zero); session.Spawn(0x77, 0, Vector2.Zero);
                _windowY = screen.WindowY; _counter = 60; session.Signal(3, 0); _shakePhase = 2;
                session.Sound.PlaySound(0x21); session.Fade(false); _state++; break;
            case 1:
                if (!session.Fading && Tick())
                {
                    for (int subid = 4; subid >= 0; subid--) session.Spawn(0x86, subid, Vector2.Zero);
                    _counter = 180; _sinkTicks = 0; _state = 2;
                }
                Earthquake(); break;
            case 2:
                if (Tick())
                {
                    _templeScene = 0; SetTempleScene(); session.Signal(3, 0x81); _state = 3; return;
                }
                Sink(); Earthquake(); break;
            case 3:
                Earthquake();
                if (!Tick()) return;
                if (++_templeScene != 3) { session.Signal(3, session.Signal(3) + 1); SetTempleScene(); return; }
                screen.Load("GFXH_TEMPLEFALL_SCENE1", "PALH_SEASONS_98", 0x0e);
                _counter = 120; _sinkTicks = 0; _templeScene = 0;
                session.Signal(3, (session.Signal(3) + 1) & 0x7f); _state = 4; break;
            case 4:
                if (!Tick()) { Sink(); Earthquake(); return; }
                _templeScene = 3; SetTempleScene(); session.Signal(3, 0xff);
                _paletteTicks = 2; BeginSeasonPalette(); _state = 5; break;
            case 5:
                Earthquake(); CycleSeasons();
                if (!Tick()) return;
                if (++_templeScene != 6) { SetTempleScene(); return; }
                _counter = 60; OutsideCastle(7); _state++; break;
            case 6:
                if (!Tick()) return;
                session.ShowText(0x1e04); _state++; break;
            case 7:
                if (session.Dialogue.IsOpen) return;
                _counter = 90; session.Fade(true, black: true); _state++; break;
            case 8:
                if (session.Fading || !Tick()) return;
                session.Save.ApplyDeathRespawnPreset(0xfe, [0,0xb6,3,2,0x48,0x78,0]);
                _kind = 0; session.BeginAftermath(); break;
        }
    }
    private void Sink()
    {
        if (++_sinkTicks != 7) return;
        _sinkTicks = 0; _windowY++; screen.WindowY = _windowY;
    }
    private void SetTempleScene()
    {
        if (_templeScene >= 3)
        {
            int[] rooms = [0xb8,0xc6,0xc8], seasons = [1,2,2];
            session.LoadRoom(rooms[_templeScene - 3], seasons[_templeScene - 3], spawn: false, preserveActors: true);
            Image graphics = session.Data.World.NativeRoomGraphics(rooms[_templeScene - 3], seasons[_templeScene - 3]);
            screen.UploadRoom(session.Room, graphics, _templeScene == 5 ? 0 : 0x60);
            _paletteCycle = new[] { 2,3,1 }[_templeScene - 3];
        }
        string palette = _templeScene < 3 ? "PALH_TILESET_SUBROSIA_TEMPLE_VARIANT_UNUSED" :
            new[] { "PALH_TILESET_OVERWORLD_SUMMER_A", "PALH_TILESET_OVERWORLD_WINTER_A", "PALH_TILESET_OVERWORLD_AUTUMN_A" }[_templeScene - 3];
        screen.Load($"GFXH_TEMPLEFALL_SCENE{_templeScene + 2}", palette, 4, preserveCamera: true);
        _basePalette = session.Data.World.Palettes.Capture();
        _counter = 90;
    }
    private void CycleSeasons()
    {
        if (--_paletteTicks != 0) return;
        _paletteTicks = 2;
        if (--_templeCounter == 0) BeginSeasonPalette(); else MixPalette(_templeCounter);
    }
    private void BeginSeasonPalette()
    {
        _templeCounter = 31; _paletteCycle = (_paletteCycle + 1) & 3;
        int address = new[] { 0x49b0,0x4a10,0x49e0,0x4a40 }[_paletteCycle];
        PaletteTo(address);
        _paletteFrame = 0; _paletteFrom = _basePalette;
        MixPalette(_templeCounter);
    }
    private void Earthquake()
    {
        if ((session.FrameCounter & 15) == 0) session.Sound.PlaySound(SoundId.SndRumble2);
        int[][] offsets = [[0,0,0,0,0,1,0,0],[0,0,1,0,0,0,-1,0],[-1,1,0,1,0,0,-1,0]];
        int[] values = offsets[_shakePhase];
        int dy = values[session.Random.Next().Value & 7];
        screen.ScrollY = (screen.CameraY + dy) & 255; screen.ScrollX = screen.CameraX;
        int wy = values[session.Random.Next().Value & 7];
        screen.WindowX = 0x4c - 7; screen.WindowY = _windowY + wy; screen.QueueRedraw();
    }
}
