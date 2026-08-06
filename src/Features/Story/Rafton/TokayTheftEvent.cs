using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs the five INTERAC_TOKAY $48:$00-$04 thieves reached directly from the
/// room $1:$a8 raftwreck warp.
/// </summary>
internal sealed class TokayTheftEvent : IRoomEntryEvent
{
    private const int ThiefCount = 5;
    private readonly RoomEventContext _context;
    private readonly TokayTheftEventDatabase _database = new();
    private readonly List<ThiefState> _thieves = [];
    private bool _active;
    private bool _initializing;
    private ScriptStage _scriptStage;
    private int _scriptCounter;
    private int _stealCounter;
    private int _stolenCount;
    private int _linkZFixed;
    private int _linkSpeedZ;
    private int _linkFrame;
    private int _linkFrameCounter;

    internal TokayTheftEvent(RoomEventContext context) => _context = context;

    public bool HasState => _active;
    public bool BlocksGameplay => _active;
    internal bool MenusDisabled => _active;
    internal TokayTheftEventDatabase Database => _database;
    internal int ScriptCounter => _scriptCounter;
    internal int LinkFrame => _linkFrame;
    internal int LinkFrameCounter => _linkFrameCounter;
    internal int StolenCount => _stolenCount;
    internal int ActiveThiefCount => _thieves.FindAll(thief => thief.Actor.Active).Count;
    internal int AccessoryCount => _thieves.FindAll(thief => thief.Accessory?.Active == true).Count;

    public bool Matches(int group, OracleRoomData room) =>
        group == _database.Record.Group && room.Id == _database.Record.Room;

    public void Start(OracleRoomData room)
    {
        Cancel();
        TokayTheftEventRecord record = _database.Record;
        for (int subId = 0; subId < ThiefCount; subId++)
        {
            NpcCharacter actor = _context.RequireNpc(
                record.Group, record.Room, record.InteractionId, subId,
                "Tokay thief");
            actor.SetBlocksLink(false);
            if (_context.Rooms.SaveData.HasRoomFlag(
                    record.Group, record.Room, record.RoomFlag))
            {
                actor.SetActive(false);
                continue;
            }

            actor.SetAnimationRate(0.0f);
            actor.SetScriptAnimation(
                _database.Animation(record.InitialAnimations[subId]));
            _thieves.Add(new ThiefState(actor, subId, actor.Position));
        }
        if (_thieves.Count == 0)
            return;
        if (_thieves.Count != ThiefCount)
            throw new InvalidOperationException(
                $"Room 1:aa instantiated {_thieves.Count} of 5 Tokay thieves.");

        _context.Player.BeginCutsceneControl();
        _active = true;
        _initializing = true;
        _scriptStage = ScriptStage.InitialWait;
        _scriptCounter = record.LinkWait;
        _stealCounter = record.StealFirstWait;
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
    }

    public void UpdateFrame()
    {
        if (_initializing)
        {
            _initializing = false;
            _linkFrame = 0;
            _linkFrameCounter = _database.LinkFrames[0].Duration;
            _context.Player.SetCutsceneSpriteFrame(_database.LinkFrames[0]);
            return;
        }

        if (_scriptStage == ScriptStage.InitialWait)
            AdvanceWashedUpLinkAnimation();
        if (_scriptStage <= ScriptStage.FinalFreeze)
        {
            AdvanceTheft();
            bool animationsFrozen = _scriptStage is
                ScriptStage.FirstFreeze or ScriptStage.FinalFreeze;
            if (!animationsFrozen)
            {
                foreach (ThiefState thief in _thieves)
                    thief.Actor.AdvanceAnimationUpdates(1);
            }
            AdvanceScripts();
        }
        else
        {
            AdvanceNativeExits();
        }
        AdvanceLinkJump();
        SynchronizeAccessories();
    }

    public void Cancel()
    {
        bool releaseControl = _active;
        foreach (ThiefState thief in _thieves)
        {
            thief.Accessory?.SetActive(false);
            thief.Actor.SetScriptDrawOffset(Vector2.Zero);
        }
        _thieves.Clear();
        _active = false;
        _initializing = false;
        _scriptStage = ScriptStage.InitialWait;
        _scriptCounter = 0;
        _stealCounter = 0;
        _stolenCount = 0;
        _linkZFixed = 0;
        _linkSpeedZ = 0;
        _linkFrame = 0;
        _linkFrameCounter = 0;
        _context.Player.SetCutsceneDrawZFixed(0);
        _context.Player.SetCutsceneSpriteFrame(null);
        if (releaseControl)
            _context.Player.EndCutsceneControl();
    }

    private void AdvanceTheft()
    {
        if (--_stealCounter != 0)
            return;
        _stealCounter = _database.Record.StealRepeatWait;
        if (_stolenCount >= _database.Record.StolenItems.Length)
            return;

        int treasure = _database.Record.StolenItems[_stolenCount++];
        _context.Inventory.LoseTreasure(treasure);
        if (treasure == TreasureDatabase.TreasureSeedSatchel)
        {
            _context.Inventory.LoseTreasure(TreasureDatabase.TreasureEmberSeeds);
            _context.Inventory.LoseTreasure(TreasureDatabase.TreasureEmberSeeds + 4);
        }
        _context.Sound.PlaySound(OracleSoundEngine.SndUnknown5);
    }

    private void AdvanceScripts()
    {
        switch (_scriptStage)
        {
            case ScriptStage.InitialWait:
                if (--_scriptCounter != 0)
                    return;
                foreach (ThiefState thief in _thieves)
                    thief.Actor.SetScriptAnimation(_database.Animation(0));
                _context.Player.SetCutsceneSpriteFrame(null);
                _context.Player.Face(Vector2I.Down);
                _linkZFixed = 0;
                _linkSpeedZ = -0x200;
                _context.Sound.PlaySound(OracleSoundEngine.SndJump);
                _context.Sound.PlaySound(OracleSoundEngine.SndStrike);
                SetStage(ScriptStage.Down, 0x10);
                return;

            case ScriptStage.Down:
                ApplyCommonSpeed(_database.Record.DownSpeed, 0x10);
                if (--_scriptCounter == 0)
                    SetStage(ScriptStage.DownWait, 30);
                return;

            case ScriptStage.DownWait:
                if (--_scriptCounter == 0)
                    SetStage(ScriptStage.FirstRight, 0x20);
                return;

            case ScriptStage.FirstRight:
                ApplyCommonSpeed(_database.Record.RightSpeed, 0x08);
                if (--_scriptCounter == 0)
                    SetStage(ScriptStage.FirstFreeze, 60);
                return;

            case ScriptStage.FirstFreeze:
                if (--_scriptCounter == 0)
                    SetStage(ScriptStage.SecondRight, 0x20);
                return;

            case ScriptStage.SecondRight:
                ApplyCommonSpeed(_database.Record.RightSpeed, 0x08);
                if (--_scriptCounter == 0)
                    SetStage(ScriptStage.FinalFreeze, 60);
                return;

            case ScriptStage.FinalFreeze:
                if (--_scriptCounter == 0)
                    FinishScripts();
                return;
        }
    }

    private void SetStage(ScriptStage stage, int counter)
    {
        _scriptStage = stage;
        _scriptCounter = counter;
    }

    private void ApplyCommonSpeed(int speed, int angle)
    {
        // applyspeed decrements its byte before applying movement; $10/$20
        // therefore produce 15/31 movement updates.
        if (_scriptCounter == 1)
            return;
        foreach (ThiefState thief in _thieves)
            Move(thief, speed, angle);
    }

    private void FinishScripts()
    {
        _scriptStage = ScriptStage.NativeExit;
        foreach (ThiefState thief in _thieves)
        {
            thief.Stage = NativeStage.ItemWait;
            thief.Counter = _database.Record.ItemWait;
            thief.Actor.SetScriptAnimation(_database.Animation(5));
            thief.Accessory = SpawnAccessory(thief);
        }
        _context.Sound.PlaySound(OracleSoundEngine.SndGetItem);
    }

    private NpcCharacter SpawnAccessory(ThiefState thief)
    {
        TokayAccessoryRecord visual = _database.Accessory(thief.SubId);
        Vector2 position = thief.Actor.Position + new Vector2(0, -12);
        var npc = new NpcRecord(
            _database.Record.Group, _database.Record.Room, 0x63, visual.SubId,
            Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.X), 0, 0,
            visual.Sprite, visual.TileBase, visual.Palette, 0, false,
            visual.Animation, visual.Animation, visual.Animation, visual.Animation,
            string.Empty, NpcImplementationClassification.EventOwned);
        NpcCharacter accessory = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(npc, $"TokayTheftAccessory{thief.SubId}"));
        accessory.Position = position;
        accessory.SetScriptAnimation(visual.Animation);
        accessory.SetAnimationRate(0.0f);
        accessory.SetBlocksLink(false);
        return accessory;
    }

    private void AdvanceNativeExits()
    {
        foreach (ThiefState thief in _thieves)
        {
            switch (thief.Stage)
            {
                case NativeStage.ItemWait:
                    if (--thief.Counter == 0)
                    {
                        thief.Counter = (thief.SubId << 4) + 0x14;
                        thief.Stage = NativeStage.Stagger;
                    }
                    break;
                case NativeStage.Stagger:
                    thief.Actor.AdvanceAnimationUpdates(3);
                    if (--thief.Counter == 0)
                        BeginThiefJump(thief, NativeStage.FirstJump);
                    break;
                case NativeStage.FirstJump:
                    if (!AdvanceThiefJump(thief, moveWhileAirborne: true))
                        break;
                    thief.Actor.SetScriptAnimation(_database.Animation(5));
                    thief.Counter = 6;
                    thief.Stage = NativeStage.LandWait;
                    break;
                case NativeStage.LandWait:
                    if (--thief.Counter == 0)
                        BeginThiefJump(thief, NativeStage.ExitJump);
                    break;
                case NativeStage.ExitJump:
                    Move(thief, _database.Record.JumpSpeed, _database.Record.JumpAngle);
                    if (!OracleObjectMath.IsInsideOriginalScreenBoundary(thief.Position))
                    {
                        RetireThief(thief);
                        break;
                    }
                    AdvanceThiefZ(thief);
                    break;
                case NativeStage.FinalWait:
                    if (--thief.Counter == 0)
                        Complete();
                    break;
            }
        }
    }

    private void BeginThiefJump(ThiefState thief, NativeStage stage)
    {
        thief.Stage = stage;
        thief.ZFixed = 0;
        thief.SpeedZ = _database.Record.JumpZFixed;
        // tokayThief_jump uses specialObjectAnimate once after forcing its
        // animCounter to one.
        thief.Actor.AdvanceAnimationUpdates(1);
        _context.Sound.PlaySound(OracleSoundEngine.SndJump);
    }

    private bool AdvanceThiefJump(ThiefState thief, bool moveWhileAirborne)
    {
        bool landed = AdvanceThiefZ(thief);
        if (!landed && moveWhileAirborne)
            Move(thief, _database.Record.JumpSpeed, _database.Record.JumpAngle);
        return landed;
    }

    private bool AdvanceThiefZ(ThiefState thief)
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref thief.ZFixed, ref thief.SpeedZ,
            _database.Record.JumpGravityFixed);
        thief.Actor.SetScriptDrawOffset(
            landed ? Vector2.Zero : new Vector2(0, thief.ZFixed / 256.0f));
        return landed;
    }

    private void RetireThief(ThiefState thief)
    {
        thief.Actor.SetActive(false);
        thief.Accessory?.SetActive(false);
        if (thief.SubId == 3)
        {
            thief.Stage = NativeStage.FinalWait;
            thief.Counter = _database.Record.FinalWait;
        }
        else
        {
            thief.Stage = NativeStage.Deleted;
        }
    }

    private void Complete()
    {
        TokayTheftEventRecord record = _database.Record;
        _context.Rooms.SaveData.SetRoomFlag(
            record.Group, record.Room, record.RoomFlag);
        _context.Sound.PlayRoomMusic(record.Group, record.Room);
        _context.Player.SetLocalRespawnPosition(_context.Player.Position);
        _context.Rooms.SaveData.SetDeathRespawnPoint(
            record.Group, record.Room, 0, 2,
            Mathf.Clamp(Mathf.FloorToInt(_context.Player.Position.Y), 0, 0xff),
            Mathf.Clamp(Mathf.FloorToInt(_context.Player.Position.X), 0, 0xff));
        _context.Player.SetCutsceneDrawZFixed(0);
        _context.Player.EndCutsceneControl();
        _active = false;
    }

    private void AdvanceLinkJump()
    {
        if (_linkZFixed == 0 && _linkSpeedZ == 0)
            return;
        if (OracleObjectMath.UpdateSpeedZ(
                ref _linkZFixed, ref _linkSpeedZ, 0x20))
        {
            _linkZFixed = 0;
            _linkSpeedZ = 0;
        }
        _context.Player.SetCutsceneDrawZFixed(_linkZFixed);
    }

    private void AdvanceWashedUpLinkAnimation()
    {
        if (--_linkFrameCounter != 0)
            return;
        _linkFrame++;
        if (_linkFrame >= _database.LinkFrames.Length)
            _linkFrame = _database.LinkLoopStart;
        _linkFrameCounter = _database.LinkFrames[_linkFrame].Duration;
        _context.Player.SetCutsceneSpriteFrame(_database.LinkFrames[_linkFrame]);
    }

    private void SynchronizeAccessories()
    {
        foreach (ThiefState thief in _thieves)
        {
            if (thief.Accessory?.Active == true)
            {
                thief.Accessory.Position = thief.Actor.Position +
                    thief.Actor.ScriptDrawOffset + new Vector2(0, -12);
            }
        }
    }

    private static void Move(ThiefState thief, int speed, int angle)
    {
        OracleObjectMovement.Shared.ApplySpeed(ref thief.Position, speed, angle);
        thief.Actor.SetStatePosition(OracleObjectMath.ToPixelPosition(thief.Position));
    }

    private enum ScriptStage
    {
        InitialWait,
        Down,
        DownWait,
        FirstRight,
        FirstFreeze,
        SecondRight,
        FinalFreeze,
        NativeExit
    }

    private enum NativeStage
    {
        ItemWait,
        Stagger,
        FirstJump,
        LandWait,
        ExitJump,
        FinalWait,
        Deleted
    }

    private sealed class ThiefState(
        NpcCharacter actor, int subId, Vector2 position)
    {
        internal NpcCharacter Actor { get; } = actor;
        internal int SubId { get; } = subId;
        internal Vector2 Position = position;
        internal NpcCharacter? Accessory;
        internal NativeStage Stage;
        internal int Counter;
        internal int ZFixed;
        internal int SpeedZ;
    }
}
