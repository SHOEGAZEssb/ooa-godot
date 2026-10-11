using Godot;
using System;

namespace oracleofages;

// CUTSCENE_CLEAN_SEAS $1e: miscCutscenes.s:func_03_7619, before updateAllObjects.
internal sealed class CleanSeasEvent(RoomEventContext context) : IRoomEvent
{
    private readonly RoomEventResources _resources = new(context, new object());
    private readonly SeaFairyDatabase _data = new();
    private bool _ownsFade;
    internal bool NativeActive { get; private set; }
    public bool HasState => _ownsFade;
    public bool BlocksGameplay => HasState;
    public bool OwnsGameLogic => NativeActive;
    public bool DisablesLink => NativeActive;
    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => HasState;
    public bool OwnsUpdatesOf(IRoomEvent other) => other is SeaFairyEvent;
    internal int RoomIndex { get; private set; } = -1;
    internal int Phase { get; private set; }
    internal int Counter { get; private set; }
    private byte Signal { get => context.Entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0);
        set => context.Entities.RuntimeState.SetWramByte(WramAddress.wTmpcfc0, value); }
    internal void BeginFade()
    {
        _ownsFade = true; _resources.CaptureFullScreenFade();
        context.Transitions.BeginNativeWhiteFade(true);
    }
    internal void Begin() { NativeActive = true; RoomIndex = -1; Phase = 0; }
    internal void AdvanceBeforeObjects()
    {
        if (!_ownsFade) return;
        context.Transitions.AdvanceNativeWhiteFade();
        if (!NativeActive) return;
        if (RoomIndex < 0)
        {
            if (context.Transitions.NativePaletteFadeActive) return;
            for (int address = 0xcbb3; address < 0xcbc3; address++) context.Entities.RuntimeState.SetWramByte(address, 0);
            context.Player.ClearNativeItemParents(); context.InterruptBracelet(false);
            context.Sound.PlaySound(SoundId.MusFairyFountain);
            LoadNextRoom(); return;
        }
        if (RoomIndex == 4 && Phase is > 0 and < 4 && ((context.Entities.FrameCounter + 1) & 31) == 0)
            context.Player.Face(context.Player.FacingVector == Vector2I.Down || context.Player.FacingVector == Vector2I.Left
                ? Vector2I.Right : Vector2I.Left);
        switch (Phase)
        {
            case 0:
                if (context.Transitions.NativePaletteFadeActive) return;
                Counter = _data.Timing[4]; Phase = 1; return;
            case 1:
                int frame = (context.Entities.FrameCounter + 1) & 255;
                if ((frame & 7) == 0 && context.Entities.PartSlotAvailable)
                {
                    int packed = context.Entities.NextRandomValue() & 0x7f;
                    context.Entities.Spawn(new SeaPowderSpawn(_data, new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8)));
                }
                if ((frame & 31) == 0) context.Sound.PlaySound(SoundId.SndMagicPowder);
                if (--Counter != 0) return;
                Counter = _data.Timing[5]; Phase = 2;
                context.Transitions.BeginNativeWhiteFade(true, 4);
                context.Sound.PlaySound(SoundId.SndFadeOut); return;
            case 2:
                if (context.Transitions.NativePaletteFadeActive) return;
                bool found = false;
                // func_782a is called four times even after an early miss.
                for (int i = 0; i < 4; i++) found = ReplacePollutedTile();
                if (found) return;
                context.Transitions.BeginNativeWhiteFade(false, 4);
                context.Sound.PlaySound(SoundId.SndFairyCutscene); Phase = 3; return;
            case 3:
                if (context.Transitions.NativePaletteFadeActive) return;
                if (RoomIndex < 2)
                {
                    if (--Counter != 0) return;
                    LoadNextRoom(); return;
                }
                if ((Signal & 0x80) == 0) return;
                if (RoomIndex == 2) Signal &= 0x7f;
                if (RoomIndex < 4) { LoadNextRoom(); return; }
                Counter = _data.Timing[6]; Phase = 4;
                context.Sound.PlaySound(SoundId.SndSolvePuzzle2); return;
            case 4:
                if (--Counter != 0) return;
                Complete(); return;
        }
    }
    private bool ReplacePollutedTile()
    {
        var room = context.Rooms.CurrentRoom;
        // bank0.s:findTileInRoom searches backwards from $cfbf.
        for (int y = room.HeightInTiles - 1; y >= 0; y--)
        for (int x = room.WidthInTiles - 1; x >= 0; x--)
        {
            if (room.GetPackedStorageMetatile((byte)(y * 16 + x)) != 0xeb) continue;
            if (!context.Rooms.TrySetTile((byte)(y * 16 + x), (byte)((room.TilesetFlags & 0x40) == 0 ? 0xfc : 0x3a)))
                throw new InvalidOperationException("miscCutscenes.s:func_782a exhausted the four-entry setTile queue.");
            return true;
        }
        return false;
    }
    private void LoadNextRoom()
    {
        RoomIndex++; Phase = 0;
        var location = _data.Rooms[RoomIndex];
        var room = context.Rooms.LoadCutsceneRoom(location.Group, location.Room);
        context.Entities.LoadCutsceneRoom(location.Group, room, includeTimePortals: false);
        context.RoomView.SetRoom(room.Texture); context.Transitions.ResetCamera();
        context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 1);
        context.Player.Visible = RoomIndex == 4;
        if (RoomIndex == 2)
        {
            context.Entities.Spawn(new CleanSeasZoraSpawn(_data, new(0x28, 0x28), 0x0a));
            context.Entities.Spawn(new CleanSeasZoraSpawn(_data, new(0x68, 0x48), 0x0b));
        }
        if (RoomIndex == 3)
        {
            context.Entities.Spawn(new CleanSeasZoraSpawn(_data, new(0x28, 0x28), 0x0c));
            context.Entities.Spawn(new CleanSeasZoraSpawn(_data, new(0x78, 0x58), 0x0d));
        }
        if (RoomIndex == 4)
        {
            context.Player.WarpTo(new(0x68, 0x38), recordSafe: false);
            context.Player.Face(Vector2I.Down);
            context.Player.RequestState08Control(this);
        }
        // @func_764a loads PALH_0f and the tileset palettes with LCD off;
        // it does not start a palette thread for the newly viewed room.
        context.Fade.Color = new Color(1, 1, 1, 0);
    }
    private void Complete()
    {
        context.Rooms.SaveData.SetGlobalFlag(GlobalFlag.WaterPollutionFixed);
        context.Rooms.SaveData.SetDeathRespawnPoint(1, 0xa3, 0, 2, 0x38, 0x68,
            CompanionRuntimeState.ReadRemembered(context.Entities.RuntimeState), 0xd0);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0);
        context.Player.EndCutsceneControl(this);
        context.Player.Face(Vector2I.Down);
        _ownsFade = false; NativeActive = false; _resources.ReleaseFullScreenFade();
        context.Sound.PlayRoomMusic(1, 0xa3, context.Rooms.SaveData);
        context.Transitions.ApplyWarp(context.Player, new Warp(1, 0xa3, 0, 0, 0, 1, 0xa3, 0x36, 0, 0));
    }
    public void UpdateFrame() { }
    public void Cancel()
    {
        if (!_ownsFade) return;
        _ownsFade = false; NativeActive = false; RoomIndex = -1;
        context.Transitions.CancelNativeWhiteFade(); _resources.ReleaseFullScreenFade();
        context.Player.Visible = true; context.Player.EndCutsceneControl(this);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0);
    }
}
