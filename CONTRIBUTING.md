# Contributing to ooa-godot

Gameplay, validation, tools, and documentation contributions are welcome.
Keep each pull request focused, include a deterministic validation for behavior
changes, and report exactly what you ran.

## Development branch and pull requests

The active Epoch-maintained development branch is
[`codex/mod-asset-overlays`](https://github.com/ZakyPew/ooa-godot/tree/codex/mod-asset-overlays).
Fork this repository, create your feature branch from that branch, push it to
your fork, and open a PR here with the base branch set to
`codex/mod-asset-overlays`. Do not target the upstream project's repository.

Example from an Epoch checkout:

```powershell
cd backends/ooa-godot
git remote add my-fork https://github.com/YOUR-ACCOUNT/ooa-godot.git
git switch -c my-feature
# make and validate one focused change
git diff --check
git push -u my-fork my-feature
```

If `my-fork` already exists, use it rather than adding it again. For a change
that also needs launcher work, keep that in a separate Epoch PR. The Epoch PR
can then update the pinned submodule commit and link the reviewed Godot PR.

## Build and validation

- Godot 4.7.1 with .NET support
- .NET 8 SDK
- PowerShell for repository scripts

Run these from the repository root:

```powershell
dotnet build
& .\tools\validate_parallel.ps1
git diff --check
```

Build with zero warnings/errors. Gameplay changes require a focused source-backed
regression and the full parallel validation suite (all registered scenarios).
Documentation-only changes need `git diff --check` and link review. See
[development](docs/development.md), [validation](docs/validation.md), and
[project principles](docs/project-principles.md) for the detailed workflow.

## Attribution, contributions, and game content

Epoch's maintainer reports that the original project creator authorized the
`ZakyPew/ooa-godot` fork to continue development, modify and publicly
redistribute the project, and accept community contributions. This permission
does not transfer the creator's ownership of pre-existing code or grant rights
to Nintendo game content. Preserve existing authorship and notices; do not
claim sole authorship of inherited code.

The repository currently has no tracked general-purpose software license.
This project-specific authorization is not a blanket license for unrelated
third parties to reuse the code. By submitting a contribution, you confirm you
have authority to submit it and permit the maintainers to include, modify, and
publicly redistribute it as part of `ooa-godot` and Epoch & Equinox. You retain
ownership of your original contribution. Ask before adding code or assets from
another source.

Never commit or attach a ROM, ROM dump, personal save, generated ROM-derived
asset, or other Nintendo game content. Use legally obtained game files locally
for validation; generated personal data belongs in ignored local paths, not a
PR. The Epoch root `LICENSE` applies only to Epoch-owned files and does not
license this submodule or Nintendo content. This is project guidance, not
legal advice.
