#!/usr/bin/env bash
# Vanilla upstream build, isolated from the importer's pinned submodule.
set -euo pipefail

stage="$1"
repository="$2"
revision="$3"
wla_version="$4"
archive_sha256="$5"
link_version="$6"

case "$(uname -s)" in
  MSYS*|MINGW*)
    stage="$(cygpath -u "$stage")"
    for tool in git make python3 curl unzip sha256sum; do
      command -v "$tool" >/dev/null || {
        echo "Missing $tool. Install MSYS2 packages: git make python python-yaml curl unzip." >&2
        exit 1
      }
    done
    archive="$stage/wla-dx.zip"
    curl --fail --location --silent --show-error --retry 3 \
      "https://github.com/vhelin/wla-dx/releases/download/v${wla_version}/wla_dx_v${wla_version}_Win64.zip" \
      --output "$archive"
    echo "${archive_sha256} *${archive}" | sha256sum --check --strict
    unzip -q "$archive" -d "$stage/wla-dx"
    export PATH="$stage/wla-dx/wla_dx_v${wla_version}_Win64:$PATH"
    ;;
  Linux*)
    for tool in git make python3 wla-gb wlalink; do
      command -v "$tool" >/dev/null || {
        echo "Missing $tool. Install Git, Make, Python 3/PyYAML and WLA-DX ${wla_version}." >&2
        exit 1
      }
    done
    # The linker has its own version number in the WLA-DX release.
    # Both tools print versions in usage output and may return nonzero.
    for tool in wla-gb wlalink; do
      expected_version="$wla_version"
      if [[ "$tool" == wlalink ]]; then expected_version="$link_version"; fi
      version_regex="${expected_version//./\\.}"
      banner="$("$tool" 2>&1 || true)"
      if ! grep -Eq "(^|[[:space:]])v?${version_regex}([[:space:]]|$)" <<< "$banner"; then
        echo "Expected $tool ${expected_version} from WLA-DX ${wla_version}; reported $banner" >&2
        exit 1
      fi
    done
    ;;
  *) echo 'ROM building supports Windows with MSYS2 and Linux.' >&2; exit 1 ;;
esac

python3 -c 'import yaml' || {
  echo 'Python 3 needs PyYAML (MSYS2: pacman -S python-yaml).' >&2
  exit 1
}
source_root="$stage/disassembly"
mkdir -p "$source_root"
git -C "$source_root" init --quiet
git -C "$source_root" remote add origin "$repository"
git -C "$source_root" fetch --quiet --depth 1 origin "$revision"
git -C "$source_root" checkout --quiet --detach FETCH_HEAD
make -C "$source_root" ages BUILD_VANILLA=true
