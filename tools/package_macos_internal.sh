#!/bin/zsh

set -euo pipefail

repository_dir=${0:A:h:h}
godot_bin=${GRIDWORKS_GODOT_BIN:-"$repository_dir/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"}
output_zip="$repository_dir/dist/Gridworks-macOS-0.1.0.zip"
package_temp_dir=$(mktemp -d /private/tmp/gridworks-macos-package.XXXXXX)

if [[ $package_temp_dir != /private/tmp/gridworks-macos-package.* ]]; then
    print -u2 "Unexpected temporary directory: $package_temp_dir"
    exit 1
fi

trap 'rm -rf -- "$package_temp_dir"' EXIT

if [[ ! -x $godot_bin ]]; then
    print -u2 "Godot executable not found: $godot_bin"
    exit 1
fi

raw_zip="$package_temp_dir/raw-export.zip"
stage_dir="$package_temp_dir/stage"
verification_dir="$package_temp_dir/verification"
final_zip="$package_temp_dir/Gridworks-macOS-0.1.0.zip"
app_path="$stage_dir/Gridworks.app"

mkdir -p "$stage_dir" "$verification_dir" "$repository_dir/dist"

"$godot_bin" \
    --headless \
    --path "$repository_dir/game" \
    --export-release "macOS Internal Test" "$raw_zip" \
    --log-file "$package_temp_dir/export.log"

ditto -x -k "$raw_zip" "$stage_dir"

# Godot's built-in ad-hoc signature verifies on disk but does not launch on the
# current macOS test host. Re-sign the complete extracted bundle locally while
# preserving the JIT entitlements required by the bundled .NET runtime.
codesign \
    --force \
    --deep \
    --sign - \
    --options runtime \
    --preserve-metadata=entitlements \
    "$app_path"
codesign --verify --deep --strict --verbose=2 "$app_path"

ditto -c -k --sequesterRsrc --keepParent "$app_path" "$final_zip"
zip -q -j "$final_zip" \
    "$repository_dir/INSTALL.md" \
    "$repository_dir/CREDITS.md" \
    "$repository_dir/THIRD_PARTY_NOTICES.md" \
    "$repository_dir/LICENSE.md"

ditto -x -k "$final_zip" "$verification_dir"
codesign --verify --deep --strict --verbose=2 "$verification_dir/Gridworks.app"
arch -arm64 "$verification_dir/Gridworks.app/Contents/MacOS/Gridworks" --version

mv -f "$final_zip" "$output_zip"
shasum -a 256 "$output_zip"
