using Godot;
using System;

namespace oracleofages;

internal sealed partial class KnowItAllBirdCharacter : NpcCharacter
{
    private KnowItAllBirdRecord _data = null!;
    private OracleRandom _random = null!;
    private int _zFixed;
    private int _speedZ = -0xc0;
    internal bool Initialized { get; private set; }
    internal int Direction { get; set; }
    internal int TurnCounter { get; private set; }
    internal bool TalkingSignal { get; set; }
    internal bool Talking { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal KnowItAllBirdScriptHost Script { get; private set; } = null!;

    internal void InitializeBird(NpcRecord record, KnowItAllBirdDatabase database,
        OracleRandom random, Func<bool> textActive)
    {
        Initialize(record);
        _data = database.Get(record.SubId);
        _random = random;
        Script = new(this, database.Commands, textActive);
        SetScriptVisible(false);
        SetBlocksLink(false);
    }

    internal void InitializeNative()
    {
        if (Initialized) return;
        Initialized = true;
        Direction = _random.Next().Value & 1;
        TurnCounter = 30;
        SelectAnimation(Direction);
        SetFixedDrawPriority(BehindLinkZIndex); // objectSetVisible82
        SetScriptVisible(true);
        Script.Start();
    }

    internal void UpdateBird(Player player)
    {
        if (!Initialized) { InitializeNative(); return; }
        // interactionRunScript precedes the native substate on every slot visit.
        Script.Advance(player);
        if (!Talking)
        {
            if (TalkingSignal)
            {
                Talking = true;
                SelectAnimation(Direction + 2);
                return;
            }
            if (--TurnCounter == 0)
            {
                TurnCounter = 30;
                if ((_random.Next().Value & 7) == 0)
                {
                    Direction ^= 1;
                    SelectAnimation(Direction);
                    return; // no animation, collision, or priority tail on a turn
                }
            }
            ClearFixedDrawPriority();
            AnimateAsNpcOneUpdate(player);
            return;
        }

        AdvanceAnimationUpdates(1);
        if (TalkingSignal)
        {
            if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20))
                _speedZ = -0xc0;
        }
        else
        {
            Talking = false;
            TurnCounter = 60;
            _zFixed = 0; // speedZ deliberately survives, as in knowItAllBird.s
            SelectAnimation(Direction);
        }
        SetScriptDrawOffset(new Vector2(0, _zFixed >> 8));
    }

    internal void SelectText(bool tutorial) => SetDialogue(
        tutorial ? _data.TutorialTextId : BaseRecord.TextId,
        tutorial ? _data.Tutorial : BaseRecord.Message, canFace: false);

    private void SelectAnimation(int animation) => SetScriptAnimation(_data.Animations[animation]);
}
