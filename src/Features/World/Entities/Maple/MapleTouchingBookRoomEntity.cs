using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_TOUCHING_BOOK $a5 owns its admission, flight and trade protocol.
// Maple consumes its previous INTERACTION-pass angle in SPECIALOBJECT order.
internal sealed partial class MapleTouchingBookRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly MapleEncounter _maple;
    private readonly OracleRuntimeState _memory;
    private readonly Action<int> _sound;
    private readonly Texture2D _texture;
    private readonly Func<Vector2, bool> _createOar;
    private int _speedZ;
    private int _counter;
    private bool _finished;
    public Node2D Node => this;
    public bool Finished => _finished || _maple.Finished;
    internal int State { get; private set; }
    internal bool Landed => State == 2;
    internal int Angle { get; private set; }
    internal int ZFixed { get; set; }
    internal int SpeedZ => _speedZ;
    internal Vector2 PrecisePosition { get; set; }

    internal MapleTouchingBookRoomEntity(MapleTouchingBookSpawn spawn,
        OracleRuntimeState memory, Action<int> sound, Func<Vector2, bool> createOar)
    {
        _maple = spawn.Maple; _memory = memory; _sound = sound; _createOar = createOar;
        Name = "MapleTouchingBook";
        PrecisePosition = spawn.Position.Floor(); Position = PrecisePosition;
        ZFixed = (spawn.ZFixed >> 8) << 8;
        Visible = false;
        var visual = spawn.Visual;
        AnimationFrameDefinition frame = OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames[0];
        _texture = OracleOamCompositor.BuildOamTexture(OracleGraphicsCache.LoadImage(
            AgesGameContent.AssetRoot + $"gfx/{visual.Sprite}.png"), frame.EncodedOam, visual.TileBase, visual.Palette);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0)
        {
            // touchingBook.s state0 writes the menu lock even while waiting.
            _memory.SetWramByte(AgesWramAddress.wMenuDisabled, 1);
            if (frame.Player.KnockbackFrames != 0) return;
            // HL still addresses w1Link.knockbackCounter when objectTakePosition
            // copies high XYZ, never Maple's precise position or height.
            PrecisePosition = frame.Player.Position.Floor();
            ZFixed = (frame.Player.ItemCreationZFixed >> 8) << 8;
            Angle = OracleObjectMovement.Shared.RelativeAngle(PrecisePosition, new(0x50, 0x38)) & 0x1c;
            _speedZ = -0x100;
            State = 1; Visible = frame.Player.Visible; ZIndex = frame.Player.ZIndex;
            _sound(SoundId.SndGainHeart);
            // Native state0 yields: gravity/planar movement start next visit.
        }
        else if (State == 1)
        {
            int z = ZFixed;
            if (OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, 0x20))
                State = 2;
            else
            {
                Vector2 precise = PrecisePosition;
                NativeObjectMovement.ApplySpeed(_memory, ref precise, 0x28, Angle);
                PrecisePosition = precise;
            }
            ZFixed = z;
        }
        else UpdateTrade(frame.Player);
        Position = PrecisePosition.Floor(); QueueRedraw();
    }

    private void UpdateTrade(Player player)
    {
        switch (State)
        {
            case 2:
                if (!UpdateMapleAngle()) return;
                _maple.SetBookMovementAngle(0xff);
                State = 3; Visible = false;
                _sound(SoundId.SndGetSeed);
                _maple.ShowBookText(0x070e, player);
                return;
            case 3:
                if (_maple.BookTextActive) return;
                int x = (int)player.Position.X & 0xff;
                PrecisePosition = new Vector2((x + (x < 0x58 ? 0x10 : -0x10)) & 0xff,
                    (int)player.Position.Y & 0xff) + (PrecisePosition - PrecisePosition.Floor());
                _maple.SetBookMovementAngle(0);
                State = 4;
                return;
            case 4:
                if (!UpdateMapleAngle()) return;
                _maple.SetBookMovementAngle(0xff);
                Vector2 facing = OracleObjectMath.StrictCardinalVector((_maple.BookDirection ^ 2) * 8);
                player.Face(new Vector2I((int)facing.X, (int)facing.Y));
                State = 5;
                _maple.ShowBookText(0x070f, player);
                return;
            case 5:
                if (_maple.BookTextActive) return;
                _maple.BookDirection = (_maple.BookDirection ^ 2) | 0x80;
                State = 6;
                _maple.ShowBookText(0x0710, player);
                return;
            case 6:
                if (_maple.BookTextActive) return;
                _maple.BookDirection &= 0x7f;
                State = 7;
                return;
            case 7:
                // Checked createTreasure: retain this state and retry when
                // the shared interaction pool is full. The $60 owner grants.
                if (!_createOar(player.Position.Floor())) return;
                _counter = 2;
                State = 8;
                return;
            case 8:
                if (_counter != 0) { _counter--; return; }
                if (_maple.BookTextActive) return;
                _memory.SetWramByte(AgesWramAddress.wDisabledObjects, 1);
                _maple.SignalBookDeparture();
                Finish();
                return;
            default:
                throw new InvalidOperationException($"touchingBook.s INTERAC$a5 unsupported state ${State:x2}.");
        }
    }

    private bool UpdateMapleAngle()
    {
        Vector2 target = _maple.BookTargetPosition.Floor();
        if (_maple.BookTradeAccepted && PrecisePosition.Floor() == target) return true;
        // Preserve the source's book-to-Maple angle followed by XOR $10;
        // reversing the operands can round a boundary into a different angle.
        _maple.SetBookMovementAngle(OracleObjectMovement.Shared.RelativeAngle(PrecisePosition, target) ^ 0x10);
        return false;
    }

    internal void Finish() { _finished = true; Visible = false; }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(_texture, new Vector2(-16, -16 + (ZFixed >> 8)) + TransitionDrawOffset);
    }
}

internal sealed record MapleTouchingBookSpawn(MapleEncounter Maple, Vector2 Position,
    int ZFixed, MapleBookVisualRecord Visual) : RoomEntitySpawn;
