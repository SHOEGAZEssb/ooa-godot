# Generated-asset mods

Mods are optional overlays on the generated runtime assets. They do not change
the clean-ROM importer, the supported ROM check, or the canonical files below
`assets/oracle/`. A run with any enabled mod is outside the project's vanilla
fidelity guarantee.

## Directory and manifest

The default directory is Godot's per-user `user://mods` directory. During
development or when another launcher owns the installation, select an explicit
directory after Godot's `--` separator:

```powershell
& godot --path . -- --mods-dir='D:\Oracle\ooa-godot-mods'
```

Each immediate child directory is one mod:

```text
ooa-godot-mods/
└── example-ui/
    ├── manifest.json
    └── assets/
        └── oracle/
            ├── gfx/
            │   └── gfx_hud.png
            └── menu/
                └── inventory_item_slots.tsv
```

`manifest.json` has this initial format:

```json
{
  "id": "example-ui",
  "name": "Example UI",
  "version": "1.0.0",
  "priority": 100,
  "enabled": true
}
```

`id` and `version` are required. IDs must be unique and may contain ASCII
letters, digits, `.`, `_`, and `-`. `name`, `priority`, and `enabled` are
optional; their defaults are the ID, `100`, and `true`.

## Resolution and diagnostics

Mods are ordered by ascending numeric priority and then by ordinal ID. Asset
resolution checks that order in reverse, so the greatest priority wins; equal
priorities resolve to the ordinally greatest ID. Disabled mods remain visible
in startup diagnostics but never provide assets.

This first version accepts complete `.tsv` generated-table replacements and
complete `.png` image replacements. The path below `assets/oracle/` must match
the canonical asset exactly. Missing overrides always fall back to the
canonical generated asset.

Overridden tables still pass the production schema, header, key, and typed
value validation used by the vanilla table. Canonical tables continue to pass
the generated manifest's version, record-count, and SHA-256 checks. Mod files
are deliberately not added to or substituted for that clean-import manifest.

Startup output identifies the selected directory, every valid mod and its
priority, and the exact manifest path for malformed JSON, invalid fields, or
duplicate IDs. An invalid manifest is skipped without disabling valid mods.

Use `--no-mods` after Godot's `--` separator for a deterministic vanilla run:

```powershell
& godot --path . -- --no-mods
```

Validation runs disable mods automatically. IPS/BPS patches, runtime scripts,
audio, raw binary assets, and importer changes are intentionally outside this
first overlay contract.
