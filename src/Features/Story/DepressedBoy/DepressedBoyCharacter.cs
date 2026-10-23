using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_BOY $3c:$07's native facing, animation, solidity, and priority
/// wrapper. boySubid07Script owns var3d while the Funny Joke cutscene runs.
/// </summary>
internal sealed partial class DepressedBoyCharacter : NpcCharacter
{
    internal bool CutscenePose { get; private set; }

    internal void InitializeDepressedBoy(NpcRecord record)
    {
        if (record is not
            {
                Group: 2,
                Room: 0xf3,
                Id: InteractionId.Boy,
                SubId: 0x07,
                Var03: 0x00
            })
        {
            throw new InvalidOperationException(
                "DepressedBoyCharacter requires room 2:f3 " +
                "INTERAC_BOY $3c:$07 var03=$00.");
        }

        Initialize(record with { CanFace = true });
        SetAnimationRate(0.0f);
        ResetNativeNpcFacingState();
    }

    internal void SetCutscenePose(bool active) => CutscenePose = active;

    internal void UpdateDepressedBoy(Player player)
    {
        if (CutscenePose)
            AnimateAndUpdateDrawPriorityOneUpdate(player);
        else
            FaceLinkAndAnimateOneUpdate(player);
    }
}
