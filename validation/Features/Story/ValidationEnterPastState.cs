namespace oracleofages;

internal static class ValidationEnterPastState
{
    internal static EnterPastEventStage Stage(EnterPastEvent owner)
    {
        if (!owner.HasState)
            return EnterPastEventStage.Inactive;
        if (owner.Context.DialogueOpen)
            return EnterPastEventStage.Dialogue;

        int updates = owner.CurrentCommandUpdates;
        return owner.CurrentCommandIndex switch
        {
            0 => EnterPastEventStage.Begin,
            1 => updates == 0 ? EnterPastEventStage.InstallIntroWait : EnterPastEventStage.IntroWait,
            2 or 3 => EnterPastEventStage.PreJumpWait,
            4 => updates <= 1 ? EnterPastEventStage.BeginJump : EnterPastEventStage.Jump,
            5 => updates == 0 ? EnterPastEventStage.InstallPostJumpWait : EnterPastEventStage.PostJumpWait,
            6 => EnterPastEventStage.Dialogue,
            7 => EnterPastEventStage.PostTextWait,
            8 => EnterPastEventStage.StartFirstDown,
            9 => updates == 0 ? EnterPastEventStage.StartFirstDown : EnterPastEventStage.FirstDown,
            10 => updates == 0 ? EnterPastEventStage.FirstDown : EnterPastEventStage.Right,
            11 => updates == 0 ? EnterPastEventStage.Right : EnterPastEventStage.SecondDown,
            12 or 13 => updates == 0 ? EnterPastEventStage.StartSlowDown : EnterPastEventStage.SlowDown,
            14 or 15 => updates == 0 ? EnterPastEventStage.StartFinalDown : EnterPastEventStage.FinalDown,
            16 or 17 or 18 => EnterPastEventStage.FinalDown,
            _ => EnterPastEventStage.Inactive
        };
    }

}

internal enum EnterPastEventStage
{
    Inactive,
    Begin,
    InstallIntroWait,
    IntroWait,
    PreJumpWait,
    BeginJump,
    Jump,
    InstallPostJumpWait,
    PostJumpWait,
    Dialogue,
    PostTextWait,
    StartFirstDown,
    FirstDown,
    Right,
    SecondDown,
    StartSlowDown,
    SlowDown,
    StartFinalDown,
    FinalDown
}
