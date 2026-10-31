namespace Oracles;

// code/specialObjectAnimationsAndDamage.s:func_4553@getLinkWalkingAnimation.
internal static class LinkWalkingAnimation
{
    internal static LinkWalkingAnimationSelection Select(LinkWalkingAnimationInputs input)
    {
        if (input.Underwater) return Found(10, input.WallPush, input.ClimbingVine);
        // This native RET leaves A=grabState, without writing var34 or direction.
        if ((input.GrabState & 0x40) != 0) return new(input.GrabState, false, false, false);
        int variant = input.GrabState == 0 ? 0 : 2;
        if (input.Minecart) variant++;
        if (variant != 0) return Found(variant, false, input.ClimbingVine);
        if (input.MagnetGloveState != 0) return Found(9, false, input.ClimbingVine);
        if (input.UsingShield != 0) return Found(input.UsingShield < 2 ? 7 : 8, false, input.ClimbingVine);
        bool pushing = !input.TurningDisabled && (input.PushOverride == 1 ||
            (input.PushOverride & 0x80) == 0 && !input.ClimbingVine && !input.TextActive && input.WallPush);
        if (pushing) return Found(4, true, input.ClimbingVine);
        return Found(input.ShieldEquipped ? input.ShieldLevel == 1 ? 5 : 6 : 0, false, input.ClimbingVine);
    }

    private static LinkWalkingAnimationSelection Found(int variant, bool pushing, bool vine) =>
        new(variant * 4, true, pushing, vine);
}

internal readonly record struct LinkWalkingAnimationInputs(bool Underwater, int GrabState, bool Minecart,
    int MagnetGloveState, int UsingShield, bool TurningDisabled, int PushOverride, bool ClimbingVine,
    bool TextActive, bool WallPush, bool ShieldEquipped, int ShieldLevel);
internal readonly record struct LinkWalkingAnimationSelection(int Offset, bool WritesVariant, bool PublishesPush, bool FacesUp);
