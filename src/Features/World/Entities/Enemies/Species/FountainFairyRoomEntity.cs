using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Common enemyCode38, shared by all five Ages fairy fountains.</summary>
internal sealed partial class FountainFairyRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPlayerRestriction,
    IScreenTransitionPreloadRoomEntity
{
    private readonly FountainFairyDatabase _database;
    private readonly EnemyAnimationPlayer _animation;
    private readonly Action<int> _sound;
    private readonly Action<int, string, Vector2> _dialogue;
    private readonly Action _music;
    private readonly Func<int> _displayedHealth;
    private PuzzlePuffEffect? _puff;
    private Player? _controlledPlayer;
    private int _counter;
    private int _remainingHearts;
    private byte _floatCounter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State { get; private set; }
    internal int Height { get; private set; } = -16;
    internal int HeartCount { get; private set; }
    public bool DisablesSword => _controlledPlayer is not null;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;
    public bool DisablesPlayerContact => DisablesSword;
    public bool DisablesCompanion => DisablesSword;

    internal FountainFairyRoomEntity(ImportedEnemyDefinition visual,
        FountainFairyDatabase database, Vector2 position, Action<int> sound,
        Action<int, string, Vector2> dialogue, Action music, Func<int> displayedHealth)
    {
        _database = database;
        _sound = sound;
        _dialogue = dialogue;
        _music = music;
        _displayedHealth = displayedHealth;
        Position = position;
        Name = "FountainFairy_38_00";
        Visible = false;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations, visual.TileBase, visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(0);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // Only source state 0 runs during incoming-room initialization.
        if (State == 0) State = 1;
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        switch (State)
        {
            case 0:
                State = 1;
                return;
            case 1:
                _puff = new PuzzlePuffEffect { Name = "FountainFairyPuff_05_02" };
                _puff.Initialize(Position + new Vector2(0, Height), OracleSoundEngine.SndPoof,
                    playSound: _sound);
                spawns.Add(new FountainFairyPuffSpawn(_puff));
                _counter = 0x11;
                State = 2;
                _music();
                return;
            case 2:
                if (_puff is null || (_puff.CurrentParameter & 0x80) == 0) return;
                _puff = null;
                State = 3;
                goto case 3;
            case 3:
                if (LinkApproached(frame.Player))
                {
                    _controlledPlayer = frame.Player;
                    frame.Player.BeginCutsceneControl();
                    bool full = frame.Player.HealthQuarters == frame.Player.MaxHealthQuarters;
                    State = full ? 8 : 4;
                    if (full) _counter = 30;
                    int text = full ? 0x4105 : 0x4100;
                    _dialogue(text, _database.Text(text), frame.Player.Position);
                }
                break;
            case 4:
                State = 5;
                _counter = 12;
                _remainingHearts = 9;
                goto case 5;
            case 5:
                PlayHealingSound(frame.Counter);
                if (--_counter == 0)
                {
                    _counter = 12;
                    if (--_remainingHearts == 0)
                    {
                        _counter = 30;
                        State = 6;
                    }
                    else
                    {
                        HeartCount++;
                        spawns.Add(new FountainFairyHeartSpawn(this));
                    }
                }
                break;
            case 6:
                PlayHealingSound(frame.Counter);
                if (--_counter != 0) break;
                State = 7;
                frame.Player.RefillHealth();
                goto case 7;
            case 7:
                // Source falls through from state 6, including a second sound
                // request on its zero update when wFrameCounter & $07 is zero.
                PlayHealingSound(frame.Counter);
                if (HeartCount != 0) break;
                State = 8;
                _counter = 30;
                goto case 8;
            case 8:
                if (--_counter != 0) break;
                _counter = 60;
                State = 9;
                ReleasePlayer();
                _sound(OracleSoundEngine.SndFairyCutscene);
                goto case 9;
            case 9:
                if (--_counter == 0)
                {
                    Finished = true;
                    Visible = false;
                    return;
                }
                break;
            default:
                throw new InvalidOperationException($"ENEMY_GREAT_FAIRY $38: invalid state ${State:x2}.");
        }
        Animate(frame.Player);
        if (State == 9 && (_counter & 1) == 0) Visible = false;
    }

    private bool LinkApproached(Player player) =>
        player.AcceptsRoomEntityContact && player.InvincibilityFrames <= 0 &&
        !player.IsDying && !player.IsDrowning && !player.IsFallingInHole &&
        player.KnockbackFrames <= 0 && !player.CutsceneControlled &&
        (((int)player.Position.Y - (int)Position.Y - 0x10) & 0xff) < 0x21 &&
        (((int)player.Position.X - (int)Position.X + 0x18) & 0xff) < 0x31;

    private void Animate(Player player)
    {
        _floatCounter = unchecked((byte)(_floatCounter - 1));
        if ((_floatCounter & 7) == 0)
        {
            int offset = ((_floatCounter & 0x18) >> 3) - 2;
            if ((_floatCounter & 0x20) == 0) offset = -offset;
            Height = offset - 16;
        }
        _animation.Advance();
        Visible = true;
        ZIndex = player.Position.Y < Position.Y
            ? NpcCharacter.InFrontOfLinkZIndex : NpcCharacter.BehindLinkZIndex;
        QueueRedraw();
    }

    private void PlayHealingSound(int counter)
    {
        if ((counter & 7) == 0) _sound(0x8c); // SND_FAIRY_HEAL
    }

    internal FountainFairyHeartRoomEntity CreateHeart() => new(this, _database, _displayedHealth);
    internal void HeartFinished() => HeartCount--;

    private void ReleasePlayer()
    {
        _controlledPlayer?.EndCutsceneControl();
        _controlledPlayer = null;
    }

    public override void _ExitTree() => ReleasePlayer();
    public override void _Draw()
    {
        if (Visible)
            DrawTexture(_animation.CurrentTexture,
                _animation.CurrentOffset + SourceOamDrawOffset + new Vector2(0, Height));
    }
}

internal sealed record FountainFairyHeartSpawn(FountainFairyRoomEntity Owner) : RoomEntitySpawn;
internal sealed record FountainFairyPuffSpawn(PuzzlePuffEffect Effect) : RoomEntitySpawn;
