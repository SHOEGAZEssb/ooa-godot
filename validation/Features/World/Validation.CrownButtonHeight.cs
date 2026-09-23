using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownButtonHeight()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if(batch) scheduler.Advance(count/60.0,update);
                else for(int i=0;i<count;i++) scheduler.Advance(1.0/60.0,update);
            }
            LoadValidationRoom(4,0xbc);
            _player.WarpTo(new(72,56));
            FailIf(_currentRoom.IsSolid(_player.Position),"Height fixture must overlap the actual floor button, not solid terrain.");
            var button=_entities.Entities<GroundButtonRoomEntity>().Single(b=>b.PackedPosition==0x34);
            var owner=new object();
            _player.BeginCutsceneControl(owner:owner);
            try
            {
                _player.SetScriptedZHigh(0xff);
                Step(2);
                FailIf(button.Pressed || _player.ObjectZHigh!=0xff,
                    "Grounded scripted Link at zh$ff must not press PART$09.");
                _player.SetScriptedZHigh(0);
                Step();
                FailIf(!button.Pressed,"Setting only zh to zero must permit button pressure on the next PART update.");
                _player.SetScriptedZHigh(1);
                Step(2);
                FailIf(!button.Pressed,"Nonzero zh during XY overlap returns before releasing an already pressed button.");
                _player.SetScriptedPosition(new(24,24));
                Step();
                FailIf(button.Pressed,"Moving outside XY overlap must permit release even while Link's zh is nonzero.");
                _player.SetScriptedPosition(new(72,56));
                _player.SetCutsceneDrawZFixed(-1);
                Step();
                FailIf(button.Pressed || _player.ObjectZHigh!=0xff,
                    "Fixed-point scripted z=-1 has high byte$ff and must block pressure.");
                _player.SetCutsceneDrawZFixed(255);
                Step();
                FailIf(!button.Pressed || _player.ObjectZHigh!=0,
                    "Only zh is tested: fractional z=$00ff must permit pressure.");
                _player.SetScriptedPosition(new(24,24)); Step();
                _player.SetScriptedPosition(new(72,56));
                _player.BeginNewGameSlowFall(0);
                Step();
                FailIf(!button.Pressed || !_player.IsNewGameSlowFalling,
                    "A fall-state flag alone must not reject zero-height Link pressure.");
                _player.EndNewGameSlowFall();
            }
            finally
            {
                _player.EndNewGameSlowFall();
                _player.SetCutsceneDrawZFixed(0);
                _player.EndCutsceneControl(owner);
            }
            _player.WarpTo(new(24,24)); Step();
            FailIf(button.Pressed,"Cancelling scripted ownership and leaving the button must release pressure.");
            LoadValidationRoom(0,0x60);
        }
    }
}
