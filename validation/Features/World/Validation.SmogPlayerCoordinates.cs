using Godot;
using System.Reflection;
using System;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogPlayerCoordinates()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var airborneField = typeof(Player).GetField("_topDownAirborne",flags)!;
        var drawField = typeof(Player).GetField("_cutsceneDrawZFixed",flags)!;
        foreach (bool airborne in new[] { false,true })
        {
            LoadValidationRoom(0,0x60); _entities.Clear();
            _player.SetSwitchHookPosition(new(72.25f,88.75f),0x5a);
            airborneField.SetValue(_player,airborne);
            for (int step = 1; step <= 7; step++)
            {
                _player.SetScriptedZHigh(unchecked((byte)(_player.ScriptedZHigh - 1)));
                FailIf(_player.SwitchHookZFixed != (-step * 256 | 0x5a) ||
                    _player.ScriptedZHigh != 256 - step || _player.TopDownAirborne != airborne ||
                    (int)drawField.GetValue(_player)! != (airborne ? 0 : -step * 256),
                    "Smog lift must modify only zh, retain zl/in-air state and avoid applying height twice to airborne drawing.");
            }
            _player.SetScriptedCoordinateHigh(false,0x58);
            _player.SetScriptedCoordinateHigh(true,0x78);
            // Read the authoritative fixed-point position through the existing
            // switch-hook accessor's setter counterpart in this fixture.
            var precise = (Vector2)typeof(Player).GetField("_precisePosition",flags)!.GetValue(_player)!;
            FailIf(precise != new Vector2(120.25f,88.75f) || _player.ScriptedZHigh != 0xf9,
                "Smog repositioning must retain Link's low X/Y bytes and lifted Z.");
            for (int step = 0; step < 7; step++)
                _player.SetScriptedZHigh(unchecked((byte)(_player.ScriptedZHigh + 1)));
            FailIf(_player.ScriptedZHigh != 0 || _player.SwitchHookZFixed != 0x5a ||
                _player.TopDownAirborne != airborne || (int)drawField.GetValue(_player)! != 0,
                "Smog lowering must wrap zh to zero while preserving zl and the preexisting in-air flag.");
        }
        LoadValidationRoom(0,0x60);
        _entities.Clear();
        FailIf(!_player.NativeNormalStateForInteraction || _player.NativeInAirForInteraction,
            "Ordinary grounded Link must expose state$01 and zero wLinkInAir.");
        foreach (string field in new[] { "_electricShockPending", "_deathPending", "_enemyGrabRequested", "_topDownAirborne", "_usingShield" })
        {
            var member = typeof(Player).GetField(field,flags)!;
            member.SetValue(_player,true);
            FailIf(!_player.NativeNormalStateForInteraction || _player.NativeInAirForInteraction != (field == "_topDownAirborne"),
                $"Native state$01 must survive {field}; only an actual in-air owner changes wLinkInAir.");
            member.SetValue(_player,false);
        }
        _player.BeginGale();
        FailIf(!_player.NativeNormalStateForInteraction,"Gale item pass queues state$07 without immediately replacing Link state$01.");
        typeof(Player).GetField("_galePending",flags)!.SetValue(_player,false);
        FailIf(_player.NativeNormalStateForInteraction,"Consumed gale request must expose a non-normal Link state.");
        LoadValidationRoom(0,0x60); _entities.Clear();
        foreach (string field in new[] { "_deathAnimationActive", "_enemyGrabSubstate", "_forcedState08Phase" })
        {
            var member = typeof(Player).GetField(field,flags)!;
            object old = member.GetValue(_player)!;
            member.SetValue(_player,field == "_deathAnimationActive" ? (object)true : 2);
            FailIf(_player.NativeNormalStateForInteraction,$"Active native state owner {field} must reject Smog's state$01 gate.");
            member.SetValue(_player,old);
        }
        _player.SetScriptedZHigh(0xf9);
        FailIf(_player.NativeInAirForInteraction,"A frozen scripted zh lift must not manufacture a wLinkInAir transition.");
        _player.SetScriptedZHigh(0);
        _player.BeginCutsceneControl();
        bool diagnosed = false;
        try { _ = _player.NativeNormalStateForInteraction; }
        catch (NotSupportedException error) { diagnosed = error.Message.Contains("INTERAC$33"); }
        FailIf(!diagnosed,"Unresolved cutscene state boundaries must diagnose rather than guess from immobility.");
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Smog scripted Link high-byte XYZ writes, fractional preservation and grounded/airborne draw offsets.");
    }
}
