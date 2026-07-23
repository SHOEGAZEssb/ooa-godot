using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared runtime state for INTERAC_BOY $3c:$0e, INTERAC_MALE_VILLAGER
/// $3a:$0c, INTERAC_PAST_GUY $43:$06, and their INTERAC_BALL $95 in room
/// 1:49. The original actors synchronize through cfd3, so modeling them as
/// independent generic NPCs would lose both the catch timing and stone gate.
/// </summary>
internal sealed class Room149FamilyInteraction
{

    private const int EssenceAddress = 0xc6bf;
    private const int D7EssenceMask = 0x40;
    private const int VeranRoomGroup = 4;
    private const int VeranRoom = 0xfc;
    private const byte VeranDefeatedMask = 0x80;

    private readonly OracleSaveData? _save;
    private readonly Room149FamilyDatabase _database;
    private readonly NpcCharacter _boy;
    private readonly NpcCharacter _father;
    private readonly NpcCharacter _observer;
    private readonly Room149Ball _ball;
    private int _boyCounter;
    private int _fatherCounter;
    private ScriptPhase _boyPhase;
    private ScriptPhase _fatherPhase;
    private int _ballSignal;

    public FamilyState State { get; private set; }

    public Room149FamilyInteraction(
        OracleSaveData? save,
        Room149FamilyDatabase database,
        NpcCharacter boy,
        NpcCharacter father,
        NpcCharacter observer,
        Room149Ball ball)
    {
        _save = save;
        _database = database;
        _boy = boy;
        _father = father;
        _observer = observer;
        _ball = ball;
        RefreshSaveState(force: true);
    }

    public void RefreshSaveState() => RefreshSaveState(force: false);

    public void UpdateBoy(RoomEntityFrame frame)
    {
        if (State == FamilyState.FatherStone)
        {
            // boyRunSubid0e calls interactionAnimate2Times while var03 is 1.
            _boy.AdvanceAnimationUpdates(2);
        }
        else if (--_boyCounter == 0)
        {
            if (_boyPhase == ScriptPhase.SetAnimation)
            {
                _boy.SetScriptAnimation(_database.Visual("boy").Animation);
                _boyPhase = ScriptPhase.Throw;
                _boyCounter = 30;
            }
            else
            {
                // loadNextAnimationFrameAndMore parameter $02 writes cfd3 and
                // forces the next animation frame on the same update.
                _boy.ForceNextAnimationFrame();
                _ballSignal = 2;
                _boyPhase = ScriptPhase.SetAnimation;
                _boyCounter = 90;
            }
        }

        _boy.PreventPlayerPassing(frame.Player);
        _boy.UpdateDrawPriority(frame.Player.Position);
    }

    public void UpdateFather(RoomEntityFrame frame)
    {
        if (State != FamilyState.FatherStone && --_fatherCounter == 0)
        {
            if (_fatherPhase == ScriptPhase.SetAnimation)
            {
                _father.SetScriptAnimation(
                    _database.Visual("father-throw").Animation);
                _fatherPhase = ScriptPhase.Throw;
                _fatherCounter = 30;
            }
            else
            {
                // The father's helper writes cfd3=$01 after its 60+30 waits.
                _father.ForceNextAnimationFrame();
                _ballSignal = 1;
                _fatherPhase = ScriptPhase.SetAnimation;
                _fatherCounter = 90;
            }
        }

        _father.PreventPlayerPassing(frame.Player);
        _father.UpdateDrawPriority(frame.Player.Position);
    }

    public void UpdateObserver(RoomEntityFrame frame)
    {
        if (State != FamilyState.FatherStone)
            _observer.AdvanceAnimationUpdates(1);
        _observer.PreventPlayerPassing(frame.Player);
        _observer.UpdateDrawPriority(frame.Player.Position);
    }

    public void UpdateBall()
    {
        if (_ballSignal != 0 && _ball.Idle)
        {
            _ball.Launch(_ballSignal);
            _ballSignal = 0;
            // @substate0 installs the angle and Z speed, then returns. Motion
            // begins when the ball handler next runs in @substate1.
            return;
        }
        _ball.UpdateFrame();
    }

    private void RefreshSaveState(bool force)
    {
        bool veranDefeated = _save?.HasRoomFlag(
            VeranRoomGroup, VeranRoom, VeranDefeatedMask) == true;
        bool d7Complete = ((_save?.ReadWramByte(EssenceAddress) ?? 0) &
            D7EssenceMask) != 0;
        FamilyState next = veranDefeated
            ? FamilyState.PlayingAfterVeran
            : d7Complete
                ? FamilyState.FatherStone
                : FamilyState.PlayingBeforeD7;
        if (!force && next == State)
            return;

        State = next;
        _ballSignal = 0;
        _boy.Position = new Vector2(
            next == FamilyState.FatherStone ? 0x48 : 0x78, 0x48);
        _father.Position = new Vector2(0x38, 0x48);
        _observer.Position = new Vector2(0x78, 0x28);

        _boy.SetScriptPaletteOverride(null);
        _boy.SetScriptAnimation(_database.Visual("boy").Animation);
        _boy.SetDialogue(
            next switch
            {
                FamilyState.FatherStone => 0x251b,
                FamilyState.PlayingAfterVeran => 0x251e,
                _ => 0x251d
            },
            _database.Text(next switch
            {
                FamilyState.FatherStone => 0x251b,
                FamilyState.PlayingAfterVeran => 0x251e,
                _ => 0x251d
            }),
            canFace: false,
            textPosition: next == FamilyState.FatherStone ? 2 : 0);

        if (next == FamilyState.FatherStone)
        {
            _father.SetScriptPaletteOverride(_database.StonePalette);
            _father.SetScriptAnimation(
                _database.Visual("father-stone").Animation);
            _father.SetDialogue(0, string.Empty, canFace: false);
            _observer.SetScriptPaletteOverride(_database.StonePalette);
            _observer.SetScriptAnimation(
                _database.Visual("observer").Animation);
            _observer.SetDialogue(0, string.Empty, canFace: false);
            _ball.SetActive(false);
        }
        else
        {
            _father.SetScriptPaletteOverride(null);
            _father.SetScriptAnimation(
                _database.Visual("father-default").Animation);
            int fatherText = next == FamilyState.PlayingAfterVeran
                ? 0x1443
                : 0x1442;
            _father.SetDialogue(
                fatherText, _database.Text(fatherText), canFace: false);

            _observer.SetScriptPaletteOverride(null);
            _observer.SetScriptAnimation(
                _database.Visual("observer").Animation);
            _observer.SetDialogue(
                0x1712, _database.Text(0x1712), canFace: false);
            _ball.SetActive(true);
        }

        // boySubid0eScript begins directly at @playCatch (30, throw, 90),
        // while villagerSubid0cScript begins at 60, animation $01, 30, throw.
        _boyPhase = ScriptPhase.Throw;
        _boyCounter = 30;
        _fatherPhase = ScriptPhase.SetAnimation;
        _fatherCounter = 60;
        _ball.Reset();
    }
}
