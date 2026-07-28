namespace oracleofages;

/// <summary>
/// Narrow lifecycle shared by short-lived room effects whose entity adapters
/// only forward one original update, completion, and transition draw offset.
/// </summary>
public abstract partial class FixedEffectNode2D : TransitionOffsetNode2D
{
    internal abstract bool Finished { get; private protected set; }
    internal abstract void UpdateFrame();
}
