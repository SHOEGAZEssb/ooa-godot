using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class PuzzleTrapResetRoomEntity(PuzzleTrapResetRecord data,
    OracleRoomData room,OracleRuntimeState runtime,Action<int> sound,Action<Warp> warp) : Node2D, IRoomEntity,
        IFixedRoomEntity, IPlayerRestriction, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    private bool _locked;
    public Node2D Node => this;
    public bool FreezesPlayerUpdates => _locked;
    public bool DisablesMenus => _locked;
    public bool MenuDisablesWarpTiles => _locked; // checkTileWarps reads wMenuDisabled.
    public bool DisablesPlayerContact => _locked; // wMenuDisabled also gates Link collisions.
    public bool DisablesSword => false;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        State = 1;
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        if (State == 0) { State = 1; return; }
        Counter = (Counter - 1) & 0xff;
        if (Counter != 0) return;
        if (State == 2)
        {
            _locked = false; // Source clears both bytes before requesting the warp.
            warp(data.Warp);
            return;
        }
        Counter = data.Interval;
        byte position = (byte)(((int)frame.Player.Position.Y & 0xf0) | (((int)frame.Player.Position.X & 0xf0) >> 4));
        if (!IsTrapped(position) || !frame.Player.NativePuzzleResetVulnerable) return;
        _locked = true;
        sound(OracleSoundEngine.SndError);
        Counter = data.Delay;
        State = 2;
    }
    internal bool IsTrapped(byte position)
    {
        int unknownFarProbe = -1;
        for (int i = 0; i < data.Offsets.Count; i++)
        {
            byte target = (byte)(position + data.Offsets[i]);
            if (target >= 0xf0)
            {
                // bank3.init clears this upper scratch tail. Normal room
                // placement uses $cec0-$cedf; Wizzrobes stop at $ceef; map,
                // secret and movement temporaries also end below $cef0.
                // Retain the actual WRAM byte rather than inventing a wall.
                if (runtime.ReadWramByte(0xce00 + target) == 0) return false;
                if ((i & 1) != 0) continue;
                // Near probes read the corresponding layout-page byte even
                // outside wRoomLayout. $cff0-$cfff belongs to script scratch;
                // a zero byte skips the far probe, just as a zero edge tile.
                if (runtime.ReadWramByte(0xcf00 + target) == 0) i++;
                continue;
            }
            if (target >= 0xc0)
            {
                // $cec0-$ceff is shared scratch, not collision-buffer padding.
                // A later known open probe still proves "not trapped". Never
                // invent a solid value when the final result depends on scratch.
                if ((i & 1) == 0)
                    throw new NotSupportedException($"{data.Source}: near trap probe ${target:x2} reaches unrepresented scratch/layout state.");
                unknownFarProbe = target;
                continue;
            }
            Vector2 center = new((target & 15)*16+8,(target >> 4)*16+8);
            if (room.GetTerrainInfo(center).Collision == 0) return false;
            if ((i & 1) != 0) continue;
            // loadRoomLayout clears $00-$bf; large rooms retain the padding
            // column in their 16-byte stride. A zero edge skips its far probe.
            byte tile = target < room.Layout.Length ? room.Layout[target] : (byte)0;
            if (tile == 0) i++;
        }
        if (unknownFarProbe >= 0)
            throw new NotSupportedException($"{data.Source}: trap result depends on unrepresented scratch collision probe ${unknownFarProbe:x2}.");
        return true;
    }
}
