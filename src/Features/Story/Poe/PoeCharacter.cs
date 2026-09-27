using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_POE's native state-1 animation, facing, collision, and priority
/// wrapper. poeScript owns its movement and disappearance bytes.
/// </summary>
internal sealed partial class PoeCharacter : NpcCharacter
{
    internal bool Disappearing { get; private set; }
    internal bool NoFace { get; private set; }
    internal bool TransitionInitialized { get; private set; }

    internal void PrepareTransition(PoeEventRecord record, OracleSaveData? saveData, Player? player)
    {
        if (TransitionInitialized)
            return;
        SetActive(PoeEvent.VariantVisible(Record, record, saveData));
        if (Active)
        {
            // poe.s state 0 falls through to poeScript's initcollisions and
            // checkabutton, then npcFaceLinkAndAnimate, before state 1 freezes.
            InitializeCollisionRadii();
            SetScriptButtonSensitive(true);
            ResetNativeNpcFacingState();
            if (player is not null)
                UpdatePoe(player);
        }
        TransitionInitialized = true;
    }

    internal void InitializePoe(NpcRecord record, PoeEventRecord poe)
    {
        Initialize(record with
        {
            CanFace = true,
            UpAnimation = poe.Animation(0),
            RightAnimation = poe.Animation(1),
            DownAnimation = poe.Animation(2),
            LeftAnimation = poe.Animation(3)
        });
        SetAnimationRate(0.0f);
    }

    internal void SetDisappearing(bool disappearing) =>
        Disappearing = disappearing;

    internal void SetNoFace(bool noFace) => NoFace = noFace;

    internal void UpdatePoe(Player player)
    {
        if (Disappearing)
            return;
        if (NoFace)
            AnimateAndUpdateDrawPriorityOneUpdate(player);
        else
            FaceLinkAndAnimateOneUpdate(player);
    }
}
