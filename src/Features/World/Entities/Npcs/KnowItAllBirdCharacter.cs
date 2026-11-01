using Godot;
using System;

namespace Oracles;

internal partial class KnowItAllBirdCharacter : NpcCharacter
{
    private string[] _animations = null!;
    private OracleRandom _random = null!;
    private int _zFixed;
    private int _speedZ = -0xc0;
    private Action<Player> _advanceScript = null!;
    private Action _startScript = null!;
    internal bool Initialized { get; private set; }
    internal int Direction { get; set; }
    internal int TurnCounter { get; private set; }
    internal bool TalkingSignal { get; set; }
    internal bool Talking { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;

    internal void InitializeBirdBody(NpcRecord record,string[] animations,string assetRoot,
        OracleRandom random,Action<Player> advanceScript,Action startScript)
    {
        AssetRoot=assetRoot;
        Initialize(record,assetRoot);
        _animations=animations;_random=random;_advanceScript=advanceScript;_startScript=startScript;
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
        SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex); // objectSetVisible82
        SetScriptVisible(true);
        _startScript();
    }

    internal void UpdateBird(Player player)
    {
        if (!Initialized) { InitializeNative(); return; }
        // interactionRunScript precedes the native substate on every slot visit.
        _advanceScript(player);
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

    private void SelectAnimation(int animation) => SetScriptAnimation(_animations[animation]);
}
