using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlackTowerSeedRoomEntity : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEventContext _context;
    private readonly BlackTowerSeedDatabase _data;
    private readonly BlackTowerSeedEvent _owner;
    private readonly NpcCharacter? _actor;
    private Vector2 _precise;
    private int _speed, _z;
    internal int Id { get; }
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal Vector2 PrecisePosition => _precise;
    internal int ZHigh => _z;
    public Node2D Node { get; }
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0 || Id == 0x84;
    public bool UpdatesDuringRoomEntityFreeze => State == 0 || Id == 0x84;
    internal BlackTowerSeedRoomEntity(RoomEventContext context, BlackTowerSeedDatabase data, BlackTowerSeedEvent owner,
        int id, int subid, Vector2 position, int angle, int counter, int zHigh)
    {
        _context = context; _data = data; _owner = owner; Id = id; SubId = subid;
        _precise = position; Angle = angle; Counter = counter; _z = zHigh;
        Node = id == 0xc6 ? new Node2D() : _actor = data.Actor(id, subid, position);
        _actor?.SetScriptDrawOffset(Vector2.Down * _z);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor?.SetTransitionDrawOffset(offset);
    private byte Read(int address) => _context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => _context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    private void Position(Vector2 position) { _precise = position; Node.Position = position.Floor(); }
    private void Move() => Node.Position = NativeObjectMovement.ApplySpeed(_context.Entities.RuntimeState, ref _precise, _speed, Angle);
    private void Finish() { Finished = true; Node.Visible = false; }
    private void Sound(int sound) => _context.Sound.PlaySound(sound);
    private void BehaviorTile(int packed, int tile)
    {
        _context.Rooms.CurrentRoom.SetPositionTileAndCollision(new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8),
            (byte)tile, null, _context.AnimationTick(), preserveRenderedTile: true);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Id == 0xc6) { Door(frame.Player); return; }
        if (Id == 0x84) { Sparkle(frame.Counter); return; }
        if (SubId == 0) Seed(frame.Player, frame.Counter);
        else Essence(frame.Counter);
        if (_actor is not null) _actor.SetScriptDrawOffset(Vector2.Down * _z);
    }
    private void Door(Player player)
    {
        switch (State)
        {
            case 0:
                if (_context.Rooms.SaveData.HasRoomFlag(4, 0xea, 0x40))
                { BehaviorTile(0x47, 0x44); Finish(); return; }
                if (!_context.Inventory.HasTreasure(TreasureId.MakuSeed))
                {
                    foreach (int packed in new[] { 0x44, 0x47, 0x4a }) BehaviorTile(packed, 0x44);
                    Write(WramAddress.wDisableWarps, 0x44); Finish(); return;
                }
                player.ClearNativeItemParents(); player.PutOnGroundForScript(); player.ResetPortalDamageState();
                player.RequestNativeForcedMovement(0x70, 8, 1);
                Counter = 0x70; State = 1; _owner.SetMasks(true);
                Sound(_context.Sound.ActiveMusic); return;
            case 1:
                if (--Counter != 0) return;
                Counter = 30; State = 2; player.Face(Vector2I.Up); return;
            case 2:
                if (--Counter != 0) return;
                _owner.Spawn(0xd7, 0, Vector2.Zero); Finish(); return;
            default: throw new InvalidOperationException($"blackTowerDoorHandler.s state ${State:x2}.");
        }
    }
    private void Seed(Player player, int frame)
    {
        switch (State)
        {
            case 0:
                State = 1; Position(player.Position.Floor() + new Vector2(0, -14));
                player.RequestState08Control(_owner);
                Sound(SoundId.SndCtrlStopSfx); Sound(SoundId.SndDropEssence);
                // The source installs the parent's timer only if its sparkle
                // allocation succeeds; exhaustion deliberately wraps zero.
                if (_owner.Spawn(0x84, 4, _precise, counter: 120))
                { Counter = 120; _actor!.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex); }
                return;
            case 1:
                player.BeginGetItemTwoHandPose();
                Counter = (Counter - 1) & 255;
                if (Counter != 0) return;
                Counter = 0x40; _speed = 0x14; State = 2; return;
            case 2:
                Move(); SmallSparkle(frame);
                if (--Counter != 0) return;
                Counter = 120; player.EndGetItemTwoHandPose(); Position(new(0x78, 0x58));
                Sound(SoundId.SndPieceOfPower); _owner.Fade(false, 3); State = 3; return;
            case 3:
                SmallSparkle(frame);
                if ((frame & 7) == 0) _z = unchecked((sbyte)_data.Table("heights")[(frame & 0x38) >> 3]);
                Rotation(frame); return;
            case 4: Complete(player); return;
            default: throw new InvalidOperationException($"makuSeedAndEssences.s seed state ${State:x2}.");
        }
    }
    private void Rotation(int frame)
    {
        switch (Substate)
        {
            case 0:
                if (--Counter == 0) { Counter = 20; Counter2 = 8; Substate = 1; } return;
            case 1:
                if (--Counter != 0) return;
                Counter = 20; Counter2--;
                if (Counter2 < 0)
                    throw new NotSupportedException("makuSeedAndEssences.s:@state3Substate1 exhausted INTERACTION slots at the final essence; counter2 wrapped past @essenceSpawnerData.");
                byte[] spawns = _data.Table("essence-spawns");
                if (!_owner.Spawn(0xd7, spawns[Counter2 * 2], _precise, spawns[Counter2 * 2 + 1], zHigh: _z)) return;
                if (Counter2 == 0) { Substate++; Counter = 120; } return;
            case 2:
                if (--Counter != 0) return;
                Counter = 60; Write(0xcfc0, 1); Write(0xcfc1, 0x20); Substate++; return;
            case >= 3 and <= 7:
                if ((frame & 3) == 0) Write(0xcfc1, Read(0xcfc1) + ((Substate & 1) == 0 ? 1 : -1));
                if (--Counter == 0) { Counter = 60; Substate++; } return;
            case 8:
                Write(0xcfc1, Read(0xcfc1) + 1); Sound(SoundId.SndFadeOut); _owner.Fade(true, 4); Substate++; return;
            case 9:
                Write(0xcfc1, Read(0xcfc1) + 1);
                if (_context.Transitions.NativePaletteFadeActive) return;
                State++; Substate = 0; _actor!.Visible = false; return;
            default: throw new InvalidOperationException($"makuSeedAndEssences.s state $03/${Substate:x2}.");
        }
    }
    private void Complete(Player player)
    {
        switch (Substate)
        {
            case 0:
                byte[] tiles = _data.Table("tiles");
                for (int index = 0; tiles[index] != 0; index += 2)
                    _context.Rooms.TrySetTile(tiles[index + 1], tiles[index]);
                Counter = 30; Substate++; return;
            case 1:
                if (--Counter != 0) return;
                Counter = 120; _owner.Fade(false, 8); Substate++; return;
            case 2:
                if (_context.Transitions.NativePaletteFadeActive) return;
                Sound(SoundId.SndSolvePuzzle2); Substate++; return;
            case 3:
                if (--Counter != 0) return;
                _context.Rooms.SaveData.SetRoomFlag(4, 0xea, 0x40);
                BehaviorTile(0x47, 0x44);
                if(_context.Rooms.SaveData.IsLinkedGame){_owner.BlackFadeOut();Substate++;return;}
                _owner.SetMasks(false);
                player.EndCutsceneControl(_owner); Sound(_context.Sound.ActiveMusic); Finish(); return;
            case 4:
                if(_context.Transitions.NativePaletteFadeActive)return;
                Finish();_owner.BeginLinkedCutscene();return;
            default: throw new InvalidOperationException($"makuSeedAndEssences.s state $04/${Substate:x2}.");
        }
    }
    private void Essence(int frame)
    {
        switch (State)
        {
            case 0:
                State = 1; Counter = 16; _speed = 0x50; _z = 0; Sound(SoundId.SndPoof);
                Position(new(((int)_precise.X & 0xf0) + 8, ((int)_precise.Y & 0xf0) + 8));
                _actor!.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex); return;
            case 1: Move(); if (--Counter == 0) State++; return;
            case 2: if (Read(0xcfc0) != 0) State++; return;
            case 3:
                Vector2 screen = _context.Entities.ToScreen(_precise.Floor());
                if ((((int)screen.Y + 7) & 255) >= 0x8f || (((int)screen.X + 7) & 255) >= 0xaf)
                { Finish(); return; }
                if ((frame & 1) != 0) return;
                Angle = (Angle + 1) & 31;
                if (Angle == 0) Sound(SoundId.SndCircling);
                Vector2I circle = new Vector2I(0x78, 0x58) + NativeObjectMovement.CircleArcOffset(_context.Entities.RuntimeState, Read(0xcfc1), Angle);
                Position(new Vector2(circle.X & 255, circle.Y & 255) + new Vector2(_precise.X % 1, _precise.Y % 1)); return;
            default: throw new InvalidOperationException($"makuSeedAndEssences.s essence state ${State:x2}.");
        }
    }
    private void SmallSparkle(int frame)
    {
        if ((frame & 7) != 0) return;
        byte[] offsets = _data.Table("sparkle-offsets"); int index = (frame & 0x38) >> 2;
        _owner.Spawn(0x84, 3, _precise.Floor() + new Vector2(unchecked((sbyte)offsets[index + 1]), unchecked((sbyte)offsets[index])), zHigh: _z);
    }
    private void Sparkle(int frame)
    {
        if (State == 0)
        {
            State = 1;
            _actor!.SetFixedDrawPriority(SubId == 4 ? ObjectDrawPriority.FixedHighPriorityZIndex : ObjectDrawPriority.FixedLowPriorityZIndex);
            return;
        }
        if (SubId == 4)
        {
            if (--Counter == 0) { Finish(); return; }
            _actor!.AdvanceAnimationUpdates(1); _actor.Visible = (frame & 1) == 0;
        }
        else if (_actor!.CurrentAnimationParameter == 0xff) Finish();
        else _actor!.AdvanceAnimationUpdates(1);
    }
}
