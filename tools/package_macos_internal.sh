#!/bin/zsh

set -euo pipefail

repository_dir=${0:A:h:h}
godot_bin=${GRIDWORKS_GODOT_BIN:-"$repository_dir/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"}
output_zip="$repository_dir/dist/Gridworks-macOS-0.1.0.zip"
package_temp_dir=$(mktemp -d /private/tmp/gridworks-macos-package.XXXXXX)
required_documents=(
    "$repository_dir/INSTALL.md"
    "$repository_dir/CREDITS.md"
    "$repository_dir/THIRD_PARTY_NOTICES.md"
    "$repository_dir/LICENSE.md"
)

if [[ $package_temp_dir != /private/tmp/gridworks-macos-package.* ]]; then
    print -u2 "Unexpected temporary directory: $package_temp_dir"
    exit 1
fi

trap 'rm -rf -- "$package_temp_dir"' EXIT

if [[ ! -x $godot_bin ]]; then
    print -u2 "Godot executable not found: $godot_bin"
    exit 1
fi

for required_document in $required_documents; do
    if [[ ! -f $required_document ]]; then
        print -u2 "Required package document not found: $required_document"
        exit 1
    fi
done

raw_zip="$package_temp_dir/raw-export.zip"
stage_dir="$package_temp_dir/stage"
verification_dir="$package_temp_dir/verification"
final_zip="$package_temp_dir/Gridworks-macOS-0.1.0.zip"
app_path="$stage_dir/Gridworks.app"
smoke_storage="$package_temp_dir/smoke-storage"

mkdir -p "$stage_dir" "$verification_dir" "$smoke_storage" "$repository_dir/dist"

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

architecture_list=$(lipo -archs "$app_path/Contents/MacOS/Gridworks")
if [[ $architecture_list != "x86_64 arm64" && $architecture_list != "arm64 x86_64" ]]; then
    print -u2 "Unexpected executable architectures: $architecture_list"
    exit 1
fi

signature_details=$(codesign -dv --verbose=4 "$app_path" 2>&1)
if [[ $signature_details != *"Signature=adhoc"* ]]; then
    print -u2 "The app does not have the required ad-hoc signature."
    exit 1
fi

info_plist="$app_path/Contents/Info.plist"
arm64_minimum=$(plutil -extract LSMinimumSystemVersionByArchitecture.arm64 raw "$info_plist")
x86_64_minimum=$(plutil -extract LSMinimumSystemVersionByArchitecture.x86_64 raw "$info_plist")
if [[ $arm64_minimum != "14.0" || $x86_64_minimum != "14.0" ]]; then
    print -u2 "Unexpected macOS minimums: arm64=$arm64_minimum x86_64=$x86_64_minimum"
    exit 1
fi

ditto -c -k --sequesterRsrc --keepParent "$app_path" "$final_zip"
zip -q -j "$final_zip" $required_documents

ditto -x -k "$final_zip" "$verification_dir"
verified_app="$verification_dir/Gridworks.app"
verified_executable="$verified_app/Contents/MacOS/Gridworks"
codesign --verify --deep --strict --verbose=2 "$verified_app"
arch -arm64 "$verified_executable" --version

for required_document in $required_documents; do
    if [[ ! -f "$verification_dir/${required_document:t}" ]]; then
        print -u2 "Packaged document not found: ${required_document:t}"
        exit 1
    fi
done

(
    cd "$verification_dir"
    arch -arm64 "$verified_executable" \
        --headless \
        --log-file "$package_temp_dir/campaign-save.log" \
        -- \
        --release-campaign-smoke save \
        --storage-directory "$smoke_storage" \
        --session-id package-save
    arch -arm64 "$verified_executable" \
        --headless \
        --log-file "$package_temp_dir/campaign-continue.log" \
        -- \
        --release-campaign-smoke continue \
        --storage-directory "$smoke_storage" \
        --session-id package-continue
)

if ! grep -Fq "RELEASE_CAMPAIGN_SAVE_SMOKE_PASS" "$package_temp_dir/campaign-save.log" ||
   ! grep -Fq "RELEASE_CAMPAIGN_COMPLETE_SMOKE_PASS" "$package_temp_dir/campaign-continue.log"; then
    print -u2 "The isolated two-process campaign smoke did not reach its completion markers."
    exit 1
fi

mv -f "$final_zip" "$output_zip"
print "architectures=$architecture_list"
print "minimum_macos=arm64:$arm64_minimum,x86_64:$x86_64_minimum"
print "signature=adhoc"
print "campaign_smoke=save+fresh-process-continue"
shasum -a 256 "$output_zip"
