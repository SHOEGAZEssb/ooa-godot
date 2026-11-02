
namespace Oracles;

// Ages dimitriState0 and stateA var03=$00/$01. Seasons has a different stateA.
internal sealed class AgesDimitriScriptedCompanionRoomEntity(
    NpcCharacter npc, OracleRuntimeState memory, OracleSaveData save, Func<OracleRoomData> currentRoom,
    DimitriVisualDatabase data, Action<ScriptedCompanionRoomEntity, Player, bool> transfer,
    Action<byte> writeObjects, Action<byte> writeMenu)
    : ScriptedCompanionRoomEntity(npc, SpecialObjectId.Dimitri, memory, currentRoom)
{
    protected override void UpdateNativeFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Npc.Position = OracleObjectMath.ToPixelPosition(NativeSpecialObjectPosition);
        if (RoomInitialization is { Pending: true } initialization)
        {
            Vector2 position = NativeSpecialObjectPosition;
            if (!initialization.Advance(ref position)) return;
            NativeSpecialObjectPosition = position;
            Finished = initialization.Rejected;
            if (Finished) return;
            Memory.SetWramByte(0xd108, 2); Memory.SetWramByte(0xd109, 2);
            Npc.SetActive(true);
            int flags = save.ReadField("wDimitriState");
            if ((flags & 0x80) != 0 || (flags & 0x60) == 0x20)
            {
                SetAnimation(0x1c + 2 + Memory.ReadWramByte(0xd138)); SetVisible(0x81);
                transfer(this, frame.Player, false); return;
            }
            Memory.SetWramByte(0xd104, 0x0a);
            Npc.SetScriptButtonSensitive(true);
            int animation = save.HasGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest) ? 0x1e : 0x24;
            Memory.SetWramByte(0xd13f, (byte)animation); SetAnimation(animation);
            Memory.SetWramByte(0xd126, 4); Memory.SetWramByte(0xd127, 8);
            SetVisible(0x81); PublishAnimation(); return;
        }
        switch (Memory.ReadWramByte(0xd103))
        {
            case 0:
                if (Memory.ReadWramByte(0xd13d) != 0) writeObjects(0x81);
                int animation = Memory.ReadWramByte(0xd13f);
                if (Memory.ReadWramByte(0xd130) != animation) SetAnimation(animation);
                LinkObjectBlocking.PreventPassing(frame.Player, Npc.Position, 4, 8);
                Npc.AdvanceAnimationUpdates(1); SetVisible(0xc7);
                if ((save.ReadField("wDimitriState") & 0x80) == 0) break;
                SetVisible(0xc1); Memory.SetField("wStatusBarNeedsRefresh", 0xff);
                if (ForceMount(frame.Player, removeButton: false)) return;
                break;
            case 1:
                if (ForceMount(frame.Player, removeButton: true)) return;
                break;
            default:
                throw new NotSupportedException($"dimitri.s: Ages SPECIALOBJECT $0c state $0a var03=${Memory.ReadWramByte(0xd103):x2} requires its native departure handler.");
        }
        PublishAnimation();
    }

    private bool ForceMount(Player player, bool removeButton)
    {
        if (removeButton) Npc.SetScriptButtonSensitive(false);
        SetAnimation(0x1c + Memory.ReadWramByte(0xd108) + Memory.ReadWramByte(0xd138));
        if (!TryForceCompanionMount(player,writeMenu)) return false;
        PublishAnimation(); transfer(this, player, true); return true;
    }
    private void SetVisible(byte visible)
    {
        Memory.SetWramByte(0xd11a, visible);
        Npc.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible), withTerrainEffects: (visible & 0x40) != 0);
    }
    private void SetAnimation(int animation)
    {
        if ((uint)animation >= data.Animations.Length)
            throw new NotSupportedException($"dimitri.s: animation ${animation:x2} has no imported stream.");
        Npc.SetScriptAnimation(data.Animations[animation], data.SourceOffsets[animation]);
        Memory.SetWramByte(0xd130, (byte)animation); PublishAnimation();
    }
    private void PublishAnimation()
    {
        CompanionNativeAnimationDatabase.For(data.AssetRoot).PublishAnimation(Memory,SpecialObjectId.Dimitri,
            Memory.ReadWramByte(0xd130),Npc.CurrentAnimationFrame,Npc.CurrentAnimationCounter,Npc.CurrentAnimationParameter);
    }
}
