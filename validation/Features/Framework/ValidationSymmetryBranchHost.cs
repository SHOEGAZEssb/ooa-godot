namespace oracleofages;

internal sealed class ValidationSymmetryBranchHost : CutsceneCommandHost
{
    public override bool DialogueOpen => false;
    public override ICutsceneCommandTraceSink TraceSink { get; } = new ValidationCutsceneTrace();
}
