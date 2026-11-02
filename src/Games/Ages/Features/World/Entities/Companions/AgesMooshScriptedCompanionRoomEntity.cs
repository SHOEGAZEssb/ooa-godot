
namespace Oracles;

// Ages mooshStateA var03 $00-$06; Seasons has a different native cutscene dispatch.
internal sealed class AgesMooshScriptedCompanionRoomEntity(
    NpcCharacter npc, OracleRuntimeState memory, OracleSaveData save, Func<OracleRoomData> currentRoom,
    CompanionVisualDatabase visual, MooshGoodbyeEventRecord farewell,
    Action<ScriptedCompanionRoomEntity, Player, bool> transfer,
    Action<byte> writeObjects, Action<byte> writeMenu, Action<int, string, Vector2> showText)
    : ScriptedCompanionRoomEntity(npc, SpecialObjectId.Moosh, memory, currentRoom)
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
            Memory.SetWramByte(0xd126, 6); Memory.SetWramByte(0xd127, 6);
            Npc.SetActive(true);
            int flags = save.ReadField("wMooshState");
            int activeRoom = Room.Id;
            if ((flags & 0x80) != 0 || activeRoom != 0x54 && (flags & 0x60) == 0x20 &&
                (!save.HasTreasure(TreasureId.ChevalRope) || activeRoom != 0x6b))
            {
                SetAnimation(3 + Memory.ReadWramByte(0xd138)); SetPriority(ObjectDrawPriority.FromVisible(0xc1));
                PublishAnimation();
                transfer(this, frame.Player, false);
                return;
            }
            Memory.SetWramByte(0xd104, 0x0a);
        }
        switch (Memory.ReadWramByte(0xd103))
        {
            case 0:
            case 2:
                bool addButton = Memory.ReadWramByte(0xd103) == 2 || (save.ReadField("wMooshState") & 0x60) == 0x60;
                bool goodbye = Memory.ReadWramByte(0xd103) == 0 && (save.ReadField("wMooshState") & 0x60) == 0x20;
                Memory.SetWramByte(0xd103, 1);
                if (goodbye)
                {
                    writeMenu(1); writeObjects(1);
                    Memory.SetWramByte(0xd103, 4);
                    SetAnimation(farewell.InitialAnimation);
                    SetPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
                    break;
                }
                Npc.SetScriptButtonSensitive(addButton);
                int animation = save.HasGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest) ? 3 : 0;
                Memory.SetWramByte(0xd13f, (byte)animation);
                SetAnimation(animation);
                SetPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
                break;
            case 1:
                if (Memory.ReadWramByte(0xd13d) != 0)
                {
                    writeObjects(1); writeMenu(1);
                }
                UpdateAsNpc(frame.Player);
                if ((save.ReadField("wMooshState") & 0x80) != 0 && ForceMount(frame.Player)) return;
                break;
            case 3:
                UpdateAsNpc(frame.Player);
                if ((save.ReadField("wMooshState") & 0x20) == 0) break;
                Memory.SetField("wStatusBarNeedsRefresh", 0xff);
                if (ForceMount(frame.Player)) return;
                break;
            case 4:
                Memory.SetWramByte(0xd103, 5);
                showText(farewell.TextId, farewell.Text, NativeSpecialObjectPosition);
                break;
            case 5:
                WriteWord(0xd114, farewell.InitialSpeedZ);
                Memory.SetWramByte(0xd109, (byte)farewell.FlightAngle);
                Memory.SetWramByte(0xd110, (byte)farewell.FlightSpeed);
                SetAnimation(farewell.FlightAnimation);
                Memory.SetWramByte(0xd103, 6);
                break;
            case 6:
                Npc.AdvanceAnimationUpdates(1);
                if (Memory.ReadWramByte(0xd115) != 0)
                {
                    int z = SignedWord(0xd10e), speedZ = SignedWord(0xd114);
                    OracleObjectMath.UpdateSpeedZ(ref z, ref speedZ, farewell.FlightGravity);
                    WriteWord(0xd10e, z); WriteWord(0xd114, speedZ);
                }
                else
                {
                    OracleObjectPosition position = NativeObjectMovement.ApplySpeed(Memory,
                        OracleObjectMovement.Shared.PositionFromPixels(NativeSpecialObjectPosition),
                        Memory.ReadWramByte(0xd110), Memory.ReadWramByte(0xd109));
                    CompanionRuntimeState.UpdatePosition(Memory, SpecialObjectId.Moosh, position.PrecisePosition);
                    NativeSpecialObjectPosition = position.PrecisePosition;
                    if (Memory.ReadWramByte(0xd10b) >= farewell.ExitY)
                    {
                        writeObjects(0); writeMenu(0);
                        CompanionRuntimeState.ForgetRemembered(Memory);
                        save.WriteWramByte(farewell.MooshStateAddress,
                            (byte)(save.ReadWramByte(farewell.MooshStateAddress) | farewell.LeftMask));
                        CompanionRuntimeState.Clear(Memory, SpecialObjectId.Moosh);
                        Finished = true;
                        Npc.SetActive(false);
                        return;
                    }
                }
                break;
            default:
                throw new NotSupportedException($"moosh.s: Ages SPECIALOBJECT_MOOSH $0d state $0a var03=${Memory.ReadWramByte(0xd103):x2} is outside the native dispatch $00-$06.");
        }
        PublishAnimation();
    }

    private int SignedWord(int address) => unchecked((short)(Memory.ReadWramByte(address) | Memory.ReadWramByte(address + 1) << 8));
    private void WriteWord(int address, int value)
    { Memory.SetWramByte(address, unchecked((byte)value)); Memory.SetWramByte(address + 1, unchecked((byte)(value >> 8))); }

    private void UpdateAsNpc(Player player)
    {
        int animation = Memory.ReadWramByte(0xd13f);
        if (Memory.ReadWramByte(0xd130) != animation) SetAnimation(animation);
        LinkObjectBlocking.PreventPassing(player, Npc.Position, 6, 6);
        Npc.AdvanceAnimationUpdates(1);
        int z = unchecked((sbyte)Memory.ReadWramByte(0xd10f));
        SetPriority(ObjectDrawPriority.CompanionRelativeToLink(Npc.Position.Y, player.Position.Y, z));
    }

    private bool ForceMount(Player player)
    {
        Memory.SetWramByte(0xd13d, 0);
        Npc.SetScriptButtonSensitive(false);
        SetAnimation(1 + Memory.ReadWramByte(0xd108) + Memory.ReadWramByte(0xd138));
        byte menu = Memory.ReadField("wMenuDisabled");
        writeMenu(0);
        player.ResetEnemyInvincibility(); Memory.SetWramByte(0xd02b, 0);
        bool allowed = !CompanionRuntimeState.MountingDisabled(Memory) && player.CanMountCompanion;
        writeMenu(menu);
        if (!allowed) return false;
        Memory.SetWramByte(0xd104, 3);
        Memory.SetField("wDisableWarpTiles", 1); Memory.SetField("wWarpsDisabled", 1);
        PublishAnimation();
        transfer(this, player, true);
        return true;
    }

    private void SetAnimation(int animation)
    {
        if ((uint)animation >= visual.Animations.Length)
            throw new NotSupportedException($"moosh.s: Ages Moosh animation ${animation:x2} has no imported stream.");
        Npc.SetScriptAnimation(visual.Animations[animation],visual.SourceOffsets[animation]);
        Memory.SetWramByte(0xd130, (byte)animation);
    }

    private void SetPriority(int priority)
    {
        Npc.SetFixedDrawPriority(priority, withTerrainEffects: true);
        Memory.SetWramByte(0xd11a, (byte)(0xc0 | ObjectDrawPriority.ToVisibleBucket(priority)));
    }

    private void PublishAnimation()
    {
        CompanionNativeAnimationDatabase.For(visual.AssetRoot).PublishAnimation(Memory,SpecialObjectId.Moosh,
            Memory.ReadWramByte(0xd130),Npc.CurrentAnimationFrame,Npc.CurrentAnimationCounter,Npc.CurrentAnimationParameter);
    }
}
