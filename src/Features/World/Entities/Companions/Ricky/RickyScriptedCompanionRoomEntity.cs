using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// rickyState0 and rickyStateA var03=$00/$01, using the script actor presenter.
internal sealed class RickyScriptedCompanionRoomEntity(
    NpcCharacter npc, OracleRuntimeState memory, OracleSaveData save, Func<OracleRoomData> currentRoom,
    CompanionVisualDatabase visual, int jumpSpeedZ, int jumpGravity,
    Action<ScriptedCompanionRoomEntity, Player, bool> transfer,
    Action<byte> writeMenu)
    : ScriptedCompanionRoomEntity(npc, SpecialObjectId.Ricky, memory, currentRoom)
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
            Memory.SetWramByte(0xd139, 0x10);
            var initial = SelectInitialState(save.Game, save.ReadField("wRickyState"));
            int animation = initial.Animation;
            Npc.SetScriptAnimation(visual.Animations[animation], visual.SourceOffsets[animation]);
            Npc.SetActive(true);
            Npc.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            Memory.SetWramByte(0xd11a, 0xc1);
            Memory.SetWramByte(0xd130, (byte)animation);
            PublishAnimation();
            if (initial.State == 1)
            {
                transfer(this, frame.Player, false);
                return;
            }
            Memory.SetWramByte(0xd104, 0x0a);
            Npc.SetScriptButtonSensitive(true);
            return;
        }
        switch (Memory.ReadWramByte(0xd103))
        {
            case 0:
                LinkObjectBlocking.PreventPassing(frame.Player, Npc.Position, 6, 6);
                Npc.ClearFixedDrawPriority();
                Npc.AnimateAndUpdateDrawPriorityOneUpdate(frame.Player);
                int z = unchecked((short)(Memory.ReadWramByte(0xd10e) | Memory.ReadWramByte(0xd10f) << 8));
                int priority = ObjectDrawPriority.CompanionRelativeToLink(Npc.Position.Y, frame.Player.Position.Y, z >> 8);
                Npc.SetFixedDrawPriority(priority, withTerrainEffects: true);
                Memory.SetWramByte(0xd11a, (byte)(0xc0 | ObjectDrawPriority.ToVisibleBucket(priority)));
                int speedZ = unchecked((short)(Memory.ReadWramByte(0xd114) | Memory.ReadWramByte(0xd115) << 8));
                if ((Npc.CurrentAnimationParameter & 0x80) != 0) speedZ = jumpSpeedZ;
                else OracleObjectMath.UpdateSpeedZ(ref z, ref speedZ, jumpGravity);
                Memory.SetWramByte(0xd10e, unchecked((byte)z)); Memory.SetWramByte(0xd10f, unchecked((byte)(z >> 8)));
                Memory.SetWramByte(0xd114, unchecked((byte)speedZ)); Memory.SetWramByte(0xd115, unchecked((byte)(speedZ >> 8)));
                Npc.SetScriptDrawOffset(new Vector2(0, z >> 8));
                PublishAnimation();
                return;
            case 1:
                // companionForceMount retries after temporarily clearing menu/invincibility.
                Npc.SetScriptButtonSensitive(false);
                if (!TryForceCompanionMount(frame.Player,writeMenu)) return;
                transfer(this, frame.Player, true);
                return;
            default:
                throw new NotSupportedException($"ricky.s: SPECIALOBJECT_RICKY $0b state $0a var03=${Memory.ReadWramByte(0xd103):x2} is not supported by the scripted D1 presentation.");
        }
    }

    private void PublishAnimation()
    {
        CompanionNativeAnimationDatabase.For(visual.AssetRoot).PublishAnimation(Memory,SpecialObjectId.Ricky,
            Memory.ReadWramByte(0xd130),Npc.CurrentAnimationFrame,Npc.CurrentAnimationCounter,Npc.CurrentAnimationParameter);
    }

    internal static (int State, int Animation) SelectInitialState(OracleGame game, int flags)
    {
        // rickyState0's ROM_SEASONS branch ignores the Ages quest bits $20/$40.
        bool permanent = (flags & 0x80) != 0;
        bool ages = game == OracleGame.Ages;
        return (permanent || ages && (flags & 0x60) == 0x20 ? 1 : 0x0a,
            permanent || ages && (flags & 0x60) != 0 ? 0x17 : 0);
    }
}
