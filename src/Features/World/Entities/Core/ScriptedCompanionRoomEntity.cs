using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// A native D1 actor can use the NPC presenter without becoming an INTERACTION.
internal abstract class ScriptedCompanionRoomEntity(
    NpcCharacter npc, int id, OracleRuntimeState memory, Func<OracleRoomData> currentRoom)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
        INativeSpecialObjectRoomEntity, IFixedRoomEntity, IRoomInitializedCompanion,
        IRoomEntityLifetime, ITalkTarget, IScreenTransitionPreloadRoomEntity
{
    private readonly Vector2 _birthPosition = npc.Position;
    internal NpcCharacter Npc => Entity;
    protected OracleRuntimeState Memory => memory;
    protected OracleRoomData Room => currentRoom();
    public int NativeSpecialObjectId => id;
    public Vector2 NativeSpecialObjectPosition
    {
        get
        {
            if(!CompanionRuntimeState.IsActive(memory,id))return _birthPosition;
            Vector2 position=CompanionRuntimeState.Read(memory).Position;
            // A retained native byte position keeps its existing offscreen world projection.
            if(_birthPosition.X<0 && position.X>=248)position.X-=256;
            if(_birthPosition.Y<0 && position.Y>=248)position.Y-=256;
            return position;
        }
        set => Entity.Position = OracleObjectMath.ToPixelPosition(value);
    }
    public CompanionRoomInitialization? RoomInitialization { get; set; }
    public bool Finished { get; internal set; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        UpdateNativeFrame(frame, spawns);
        if (!Entity.IsRetired) CompleteNativeUpdate();
    }
    protected abstract void UpdateNativeFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns);
    protected bool TryForceCompanionMount(Player player,Action<byte> writeMenu)
    {
        byte menu=Memory.ReadField("wMenuDisabled");writeMenu(0);
        player.ResetEnemyInvincibility();Memory.SetWramByte(0xd02b,0);
        bool allowed=!CompanionRuntimeState.MountingDisabled(Memory) && player.CanMountCompanion;
        writeMenu(menu);
        if(!allowed)return false;
        Memory.SetWramByte(0xd104,3);
        Memory.SetField("wDisableWarpTiles",1);Memory.SetField("wWarpsDisabled",1);
        return true;
    }
    internal void CompleteNativeUpdate()
    {
        if (!CompanionRuntimeState.IsActive(memory, id)) return;
        if (id == SpecialObjectId.Dimitri) memory.SetField("wDimitriHitNpc", 0);
        CompanionNativeTerrain.Update(memory, Room);
        Entity.SetTerrainEffectsEnabled((memory.ReadWramByte(0xd11a) & 0x40) != 0);
        Entity.SetScriptDrawOffset(new Vector2(0, unchecked((sbyte)memory.ReadWramByte(0xd10f))));
    }
    public NpcCharacter? FindTalkTarget(Player player) =>
        (CompanionRuntimeState.ReadScriptButton(memory) & 1) == 0 && ContainsAButtonPoint(player) ? Entity : null;
    private bool ContainsAButtonPoint(Player player) => Entity.CanScriptTalkTo(player,
        memory.ReadWramByte(0xd126), memory.ReadWramByte(0xd127), 10);
    internal bool TryInteract(Player player)
    {
        if (!CompanionRuntimeState.IsActive(memory, id) || !Entity.ScriptButtonSensitive ||
            CompanionRuntimeState.ReadScriptButton(memory) != 0 || !ContainsAButtonPoint(player)) return false;
        CompanionRuntimeState.WriteScriptButton(memory, 1);
        return true;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
}
