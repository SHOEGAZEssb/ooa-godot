using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// moosh.s: Seasons state0 and native waiting/force-mount states in stateA.
internal sealed class SeasonsMooshScriptedCompanionRoomEntity(NpcCharacter npc, OracleRuntimeState memory,
    OracleSaveData save, Func<OracleRoomData> currentRoom, CompanionVisualDatabase visual,
    Action<ScriptedCompanionRoomEntity, Player, bool> transfer)
    : ScriptedCompanionRoomEntity(npc, SpecialObjectId.Moosh, memory, currentRoom)
{
    private int _paletteFlags = -1;

    protected override void UpdateNativeFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Npc.Position = OracleObjectMath.ToPixelPosition(NativeSpecialObjectPosition);
        if (RoomInitialization is { Pending: true } initialization)
        {
            Vector2 position = NativeSpecialObjectPosition;
            if (!initialization.Advance(ref position)) return;
            NativeSpecialObjectPosition = position; Finished = initialization.Rejected;
            if (Finished) return;
            Memory.SetWramByte(0xd126, 6); Memory.SetWramByte(0xd127, 6);
            Memory.SetWramByte(0xd108, 2); Memory.SetWramByte(0xd109, 2); Npc.SetActive(true);
            int flags = save.ReadField("wMooshState");
            if ((flags & 0x80) != 0 || save.ReadField("wAnimalCompanion") == 0x0d &&
                ((flags & 0x20) != 0 ? Room.Id != 0x2f : Room.Id != 0x90))
            {
                SetAnimation(3 + Memory.ReadWramByte(0xd138)); SetVisible(0xc1);
                transfer(this, frame.Player, false); return;
            }
            Memory.SetWramByte(0xd104, 0x0a);
        }
        if (Memory.ReadWramByte(0xd104) != 0x0a)
            throw new NotSupportedException($"moosh.s: Seasons scripted state${Memory.ReadWramByte(0xd104):x2} requires its ordinary body.");
        switch (Memory.ReadWramByte(0xd103))
        {
            case 0:
                Memory.SetWramByte(0xd103, 1);
                if (save.ReadField("wAnimalCompanion") == 0x0d && (save.ReadField("wMooshState") & 0x20) == 0)
                {
                    Memory.SetWramByte(0xd103, 2);
                    SetAnimation(1 + Memory.ReadWramByte(0xd108) + Memory.ReadWramByte(0xd138));
                }
                else
                {
                    Memory.SetWramByte(0xd13f, 0); SetAnimation(0);
                }
                SetVisible(0xc3); Npc.SetScriptButtonSensitive(true);
                break;
            case 1:
            case 7:
                UpdateAsNpc(frame.Player);
                if ((save.ReadField("wMooshState") & 0x80) != 0)
                {
                    if (ForceMount(frame.Player)) return;
                }
                else if (Memory.ReadWramByte(0xd13d) != 0)
                    Memory.SetField("wDisabledObjects", 0x81);
                break;
            case 2:
                byte invincibility = Memory.ReadWramByte(0xd12b);
                if (invincibility != 0)
                {
                    Memory.SetWramByte(0xd12b, (byte)(invincibility - 1));
                    byte flags = (frame.Counter & 4) == 0 ? (byte)0x0d : Memory.ReadWramByte(0xd11b);
                    Memory.SetWramByte(0xd11c, flags);
                    if (_paletteFlags != flags)
                    {
                        _paletteFlags = flags;
                        Npc.SetScriptPaletteOverride(flags == 0x0d ? OracleOamCompositor.GetStandardSpritePalette(5) : null);
                    }
                }
                break;
            case 5:
            case 6:
                UpdateAsNpc(frame.Player);
                if ((save.ReadField("wMooshState") & 0x20) != 0)
                {
                    Memory.SetField("wStatusBarNeedsRefresh", 0xff);
                    if (ForceMount(frame.Player)) return;
                }
                break;
            default:
                throw new NotSupportedException($"moosh.s: Seasons state$0a var03=${Memory.ReadWramByte(0xd103):x2} requires its scripted movement/departure binding.");
        }
        PublishAnimation();
    }

    private void UpdateAsNpc(Player player)
    {
        int animation = Memory.ReadWramByte(0xd13f);
        if (Memory.ReadWramByte(0xd130) != animation) SetAnimation(animation);
        LinkObjectBlocking.PreventPassing(player, Npc.Position, 6, 6);
        Npc.AdvanceAnimationUpdates(1);
        int priority = ObjectDrawPriority.CompanionRelativeToLink(Npc.Position.Y, player.Position.Y,
            unchecked((sbyte)Memory.ReadWramByte(0xd10f)));
        SetVisible((byte)(0xc0 | ObjectDrawPriority.ToVisibleBucket(priority)));
    }

    private bool ForceMount(Player player)
    {
        Memory.SetWramByte(0xd13d, 0); Npc.SetScriptButtonSensitive(false);
        SetAnimation(1 + Memory.ReadWramByte(0xd108) + Memory.ReadWramByte(0xd138));
        if (!TryForceCompanionMount(player, value => Memory.SetField("wMenuDisabled", value))) return false;
        PublishAnimation(); transfer(this, player, true); return true;
    }

    private void SetAnimation(int animation)
    {
        if ((uint)animation >= visual.Animations.Length)
            throw new NotSupportedException($"moosh.s: Seasons animation${animation:x2} has no imported stream.");
        Npc.SetScriptAnimation(visual.Animations[animation], visual.SourceOffsets[animation]);
        Memory.SetWramByte(0xd130, (byte)animation); PublishAnimation();
    }

    private void SetVisible(byte visible)
    {
        Memory.SetWramByte(0xd11a, visible);
        Npc.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible), withTerrainEffects: (visible & 0x40) != 0);
    }

    private void PublishAnimation()
    {
        CompanionNativeAnimationDatabase.For(visual.AssetRoot).PublishAnimation(Memory,SpecialObjectId.Moosh,
            Memory.ReadWramByte(0xd130),Npc.CurrentAnimationFrame,Npc.CurrentAnimationCounter,Npc.CurrentAnimationParameter);
    }
}
