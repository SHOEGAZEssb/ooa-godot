# Seasons interaction, event and cutscene coverage

This inventory covers the standard new-game opening. A runtime route is not
an exhaustive ROM-parity claim. The remainder of Seasons is outside this
inventory. Positioned characters are listed in the [NPC ledger](npc-interaction-coverage.md).

| Inventory | Runtime routes | Partial | Total |
| --- | ---: | ---: | ---: |
| Opening native cutscene entries | 4 | 0 | 4 |
| Opening Link cutscene variants | 3 | 0 | 3 |
| Opening interaction families | 11 | 0 | 11 |
| Frontend title and file-menu routes | 1 | 1 | 2 |

| Source entry | Supported boundary | Status |
| --- | --- | --- |
| `intro_titlescreen` | Native title, prompt, Start, shared RNG and file-select fade; attract replay unavailable | Partial |
| `fileSelectMode0-$05` | Standard name entry, message speed, file creation, copy and erase | Runtime route |
| Cutscene `$0d` | Standard pregame summons | Runtime route |
| Cutscene `$06` | Din dancing and capture | Runtime route |
| Cutscene `$07` | Din imprisoned by Onox | Runtime route |
| Cutscene `$08` | Temple sinking and season changes | Runtime route |
| Link `$08:$01` | Dance and tornado | Runtime route |
| Link `$08:$02` | Collapsed wake-up | Runtime route |
| Link `$08:$0a` | Pregame summons | Runtime route |
| Interaction `$a5:$09` | Din discovers Link | Runtime route |
| Interaction `$4e:$00-$07,$0b` | Standard troupe, dance and tornado | Runtime route |
| Interaction `$4f:$00-$04` | Standard imprisonment actors and effects | Runtime route |
| Interaction `$9d:$00-$01` | Standard Impa introduction and immediate repeat conversation | Runtime route |
| Interaction `$85:$00` | Nearby carnival music | Runtime route |
| Interaction `$88:$00-$01` | Castle lightning | Runtime route |
| Interaction `$77:$00-$01` | Temple scenery | Runtime route |
| Interaction `$86:$00-$04` | Temple explosions | Runtime route |
| Interaction `$9f:$00` | Exclamation effect | Runtime route |
| Interaction `$05:$00` | Puff effect | Runtime route |
| Interaction `$08:$00` | Destruction debris | Runtime route |

| Outstanding boundary | Status |
| --- | --- |
| Linked-game introduction and linked troupe/Impa dialogue | Unimplemented |
| Seasons attract cinematic and title timeout replay | Unimplemented |
| Seasons gameplay saving and resuming after the opening | Unimplemented |
| Progression beyond control returning in room `$00:$b6` | Unimplemented |
| Exhaustive executed clean-US-ROM comparison | Unproven |
