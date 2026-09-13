using Godot;
using System;

namespace oracleofages;

/// <summary>Native $4f:$02/$03 tail and its independent eight-byte displacement buffer.</summary>
internal partial class MoldormTailCharacter : EnemyCharacter
{
    private readonly byte[] _offsets = new byte[8];
    private OracleRandom _random = null!;
    private int _lastY, _lastX, _index;
    private bool _justHit;
    internal ImportedEnemyDefinition Record { get; private set; }
    private EnemyCharacter _initialParent = null!;
    private Func<int, IRoomEntity?>? _resolveSlot;
    internal int NativeSlot { get; private set; } = -1;
    internal int ParentSlot { get; private set; } = -1;
    internal Node2D? Parent => ParentSlot >= 0 ? _resolveSlot?.Invoke(ParentSlot)?.Node :
        GodotObject.IsInstanceValid(_initialParent) && !_initialParent.IsDead ? _initialParent : null;
    internal MoldormCharacter? Head { get; set; }

    internal void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve)
    {
        NativeSlot = slot; _resolveSlot = resolve;
        if (Head is not null)
        {
            if (SubId == 2) Head.Tail1Slot = slot;
            else Head.Tail2Slot = slot;
        }
    }
    internal int State { get; private set; }
    internal int SubId { get; private set; }
    internal override bool CollisionEnabled => State == 8 && base.CollisionEnabled;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, EnemyCharacter parent)
    {
        SubId = record.SubId;
        Record = record;
        if (SubId is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(record));
        _initialParent = parent; _random = random;
        ParentSlot = parent switch { MoldormCharacter head => head.NativeSlot, MoldormTailCharacter tail => tail.NativeSlot, _ => -1 };
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureHazards(room, animateWhileFallingInHole: false);
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain);
        Visible = false;
        ZIndex = 10;
    }

    internal void InitializeState()
    {
        if (State != 0) return;
        _random.Next();
        State = 8;
        RestartAnimation(SubId + 6);
        Visible = true;
    }

    internal void UpdateFrame()
    {
        if (IsDead) return;
        Node2D? parent = Parent;
        // moldorm_checkHazards precedes every species status/state dispatch.
        // Even an already falling tail needs its parent's live hazard byte.
        if (parent is EnemyCharacter { DiedInHazard: true } &&
            (ContinueHazard() || CheckHazards()))
        {
            _justHit = false;
            return;
        }
        if (State == 0) { InitializeState(); return; }
        AdvanceInvincibilityCounter();
        if (_justHit) _justHit = false; // Tail JUST_HIT falls through to its ordinary state handler.
        else if (KnockbackCounter > 0) KnockbackCounter--;
        else if (Health == 0) { Finish(); return; }
        if (State == 9 && parent is null) { Finish(); return; }
        // State8 reads the page's zeroed coordinates even after deletion;
        // the enabled-byte guard belongs to state9 only.
        int y = OracleObjectPosition.HighByte(parent?.Position.Y ?? 0);
        int x = OracleObjectPosition.HighByte(parent?.Position.X ?? 0);
        if (State == 8)
        {
            State = 9; _lastY = y; _lastX = x;
            Array.Fill(_offsets, (byte)0x88);
            return;
        }
        byte dy = unchecked((byte)(y - _lastY + 8));
        byte dx = unchecked((byte)(x - _lastX + 8));
        _offsets[_index] = unchecked((byte)(((dy << 4) | (dy >> 4)) | dx));
        _lastY = y; _lastX = x;
        _index = (_index + 1) & 7;
        byte delayed = _offsets[_index];
        Position = new Vector2(
            (OracleObjectPosition.HighByte(Position.X) + (delayed & 15) - 8) & 255,
            (OracleObjectPosition.HighByte(Position.Y) + (delayed >> 4) - 8) & 255);
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 source, int damage)
    {
        if (!base.TakeSwordHit(source, damage)) return false;
        _justHit = true;
        return true;
    }

    internal override bool TryApplyShieldBump(Rect2 hitbox, Vector2 sourcePosition, EnemyKnockbackStrength strength)
    {
        if (!base.TryApplyShieldBump(hitbox, sourcePosition, strength)) return false;
        _justHit = true;
        return true;
    }
}
