using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// bank1.cutscene18 -> bank3Cutscenes: palette/body before updateAllObjects.
// Loading each background clears WRAM1; the fight resumes with NEW twins.
internal sealed partial class RoomOfRitesBattleEvent(RoomEventContext context) : IRoomEvent, IUpdatesDuringDialogueRoomEvent
{
    private readonly RoomOfRitesBattleDatabase _data = new();
    private readonly RoomEventResources _resources = new(context, new object());
    private readonly List<RoomOfRitesZeldaActor> _zeldas = [];
    private readonly List<TwinrovaFlameRoomEntity> _flames = [];
    private readonly List<IRoomEntity> _clearedFlames = [];
    private bool _ownsFade, _linkCleared, _nativeObjectPass;
    internal int Cutscene { get; private set; }
    internal int State { get; private set; }
    internal bool NativeActive => Cutscene != 0;
    public bool HasState => NativeActive || _ownsFade || _ganonHudHidden;
    public bool BlocksGameplay => false; // The native handler still updates normal Link until WRAM1 is cleared.
    public bool OwnsGameLogic => NativeActive;
    public bool MenusDisabled => NativeActive;
    public bool AllScreenTransitionsDisabled => NativeActive;
    public bool SuppressesStatusBar => NativeActive || _ganonHudHidden;
    public bool SuppressesNormalMenuDispatch => NativeActive;
    public bool DisablesLink => NativeActive && _linkCleared;
    private int Read(int address) => context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    internal void Begin(int cutscene)
    {
        if (cutscene is not(0x18 or 0x19)) throw new NotSupportedException($"bank3Cutscenes.s: CUTSCENE ${cutscene:x2} continuation is not implemented.");
        if (context.Rooms.ActiveGroup != 5) throw new NotSupportedException($"bank3Cutscenes.s: CUTSCENE $18 preserves active group${context.Rooms.ActiveGroup:x2}; only its original group$05 room set is imported.");
        if (NativeActive) return; // Both twins can publish the same trigger in one enemy pass.
        Cutscene = cutscene; State = 0; _linkCleared = false;
    }
    internal void Fade(bool fadeOut, int delay, int speed=1)
    {
        _resources.CaptureFullScreenFade(); _ownsFade = true;
        context.Transitions.BeginNativeWhiteFade(fadeOut, delay, speed);
    }
    internal void ReloadArena(int mode)
    {
        Write(TwinrovaArenaDatabase.ModeAddress,mode);
        var room=context.Rooms.ReloadCurrentRoomBackground();
        context.Entities.RebindCutsceneBackground(room);
        context.RoomView.SetRoom(room.Texture);
    }
    internal void AdvanceBeforeObjects()
    {
        _nativeObjectPass = NativeActive;
        if (_ownsFade)
        {
            context.Transitions.AdvanceNativeWhiteFade();
            if (!NativeActive && !context.Transitions.NativePaletteFadeActive)
            { _resources.ReleaseFullScreenFade(restoreColor: false); _ownsFade = false; }
        }
        if (!NativeActive) return;
        if(Cutscene==0x19&&State>=4){AdvanceSacrifice();return;}
        switch (State)
        {
            case 0:
                Fade(true, 4);
                for (int address = 0xcbb3; address < 0xcbc3; address++) Write(address, 0);
                State = 1; return;
            case 1:
                if (context.Transitions.NativePaletteFadeActive) return;
                State = 2; Load(0xf1, new(0x78, 0x38));
                Place("calm"); context.Entities.LoadNativeObjectPaletteSlots(6, _data.Ritual.Palette);
                Fade(false, 4); return;
            case 2:
                if (context.Transitions.NativePaletteFadeActive) return;
                Write(0xcbb4, 1); State = 3; return;
            case 3:
                Write(0xcbb4, Read(0xcbb4) - 1); if (Read(0xcbb4) != 0) return;
                Write(0xcbb4, 180);
                // CP $a9/CALL Z selects only flames. Its clear starts at ID,
                // leaving enabled=$81; the old slots become INTERAC $00
                // at (0,0), and new flames allocate above them. The 40-byte
                // clear also reaches the corresponding enemy's enabled byte.
                foreach (var flame in context.Entities.EntityAdapters<TwinrovaFlameRoomEntity>().ToArray())
                {
                    int slot = context.Entities.NativeInteractionSlot(flame);
                    if (context.Entities.ResolveEnemySlot(slot) is not null)
                        throw new NotSupportedException($"twinrovaCutscene_deleteAllInteractionsExceptFlames: clear at $d{slot:x1}41 aliases a live enemy enabled byte.");
                    _clearedFlames.Add(context.Entities.ReplaceInteraction(flame, new GrassDebrisSpawn(Vector2.Zero)));
                }
                _flames.Clear();
                context.Entities.LoadNativeObjectPaletteSlots(7, _data.Ritual.AngryPalette); Place("angry");
                context.Sound.PlayNativeSound(SoundId.SndOpening); State = 4; return;
            case 4:
                context.Entities.BeginScreenShake(255);
                if (((context.Entities.FrameCounter + 1) & 63) == 0) context.Sound.PlayNativeSound(SoundId.SndOpening);
                Write(0xcbb4, Read(0xcbb4) - 1); if (Read(0xcbb4) != 0) return;
                Fade(true, 4); State = 5; return;
            case 5:
                context.Entities.BeginScreenShake(255);
                if (context.Transitions.NativePaletteFadeActive) return;
                Load(0xf5, new(0x78, 0x78));
                context.Player.RequestNativeLinkInitialization(); _linkCleared = false;
                var actor = context.Entities.TryCreateTwinrova(new(0, 0), 0x80);
                if (actor is null) throw new InvalidOperationException("cutscene18_state5: ENEMY $03 allocation failed after clearWramBank1.");
                Cutscene = 0; Fade(false, 2); return;
            default: throw new NotSupportedException($"bank3Cutscenes.s: CUTSCENE ${Cutscene:x2} state${State:x2}.");
        }
    }
    private void Load(int roomId, Vector2 link)
    {
        RetireSceneActors();
        byte enemyCount = (byte)context.Entities.RoomEnemyCount;
        var room = context.Rooms.LoadCutsceneRoom(5, roomId);
        context.Entities.LoadCutsceneRoom(5, room, includeTimePortals: false);
        // clearScreenVariablesAndWramBank1 does not clear wNumEnemies $cdd1.
        // parseGivenObjectData also bypasses parseObjectData's zero write.
        context.Entities.SetNativeRoomEnemyCount(enemyCount);
        context.RoomView.SetRoom(room.Texture);
        context.Sound.SelectRoomMusic(5, roomId, context.Rooms.SaveData);
        context.Player.ClearNativeItemParents(); context.Player.WarpTo(link, recordSafe: false); context.Player.Visible = false;
        context.Player.Face(Vector2I.Up); _linkCleared = true;
        Write(AgesWramAddress.wScrollMode, 1); context.Transitions.ResetCamera();
    }
    private void Place(string scene)
    {
        foreach (var record in _data.Actors(scene))
        {
            if (!context.Entities.InteractionSlotAvailable) throw new InvalidOperationException($"{record.Source}: cutscene stream exhausted interaction slots after native clear.");
            if (record.Id == 0xa9)
            {
                var flame = new TwinrovaFlameRoomEntity(context.Rooms.SaveData, 5, 0xf1, record.Subid, _data.Ritual.Flame(record.Subid));
                _flames.Add(flame); context.Entities.AddEntity(flame);
            }
            else if (record.Id == 0xad && record.Subid == 0)
            {
                var source = NpcDatabase.Shared.AllRecords.Single(n => n.Group == 5 && n.Room == 0xf1 && n.Id == 0xad && n.SubId == 0);
                var zelda = new RoomOfRitesZeldaActor(context, source with { X = (int)record.Position.X, Y = (int)record.Position.Y });
                _zeldas.Add(zelda); context.Entities.AddEntity(zelda);
            }
            else throw new NotSupportedException($"{record.Source}: cutscene INTERAC ${record.Id:x2}:${record.Subid:x2}.");
        }
    }
    // updateAllObjects includes camera even when cutscene01's boundary/collision
    // tail is omitted. Do not run it twice during the ordinary return fade.
    public void UpdateFrame() { if (_nativeObjectPass) context.Transitions.UpdateCamera(); }
    public void UpdateDuringDialogueFrame() => UpdateFrame();
    private void RetireSceneActors()
    {
        foreach (var actor in _zeldas) { actor.Cancel(); context.Entities.RetireEntity(actor); }
        _zeldas.Clear();
        foreach (var flame in _flames) context.Entities.RetireEntity(flame);
        _flames.Clear();
        foreach (var effect in _clearedFlames) context.Entities.RetireEntity(effect);
        _clearedFlames.Clear();
    }
    public void Cancel()
    {
        bool active = NativeActive;
        RetireSceneActors();
        if (_ownsFade||_ganonHudHidden) context.Transitions.CancelNativeWhiteFade();
        _resources.ReleaseFullScreenFade(); _ownsFade = false;
        Cutscene = State = 0; _nativeObjectPass = false;
        if (active)
        {
            context.Entities.SetScreenShake(0, 0, 0);
            if (_linkCleared) context.Player.Visible = true;
        }
        _linkCleared = false;
        if(_ganonHudHidden)context.Hud.ShowStatusBar();_ganonHudHidden=false;
    }
}
