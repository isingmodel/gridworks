#!/bin/zsh

set -euo pipefail

repository_dir=${0:A:h:h}
godot_bin=${GRIDWORKS_GODOT_BIN:-"$repository_dir/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"}
output_zip="$repository_dir/dist/Gridworks-macOS-0.1.0.zip"
package_temp_dir=$(mktemp -d /private/tmp/gridworks-macos-package.XXXXXX)
godot_notice_relative="licenses/GODOT-4.7.1-COPYRIGHT.txt"
dotnet_license_relative="licenses/DOTNET-RUNTIME-8.0.29-LICENSE.txt"
dotnet_notice_relative="licenses/DOTNET-RUNTIME-8.0.29-THIRD-PARTY-NOTICES.txt"
root_documents=(
    "$repository_dir/INSTALL.md"
    "$repository_dir/CREDITS.md"
    "$repository_dir/ASSET_MANIFEST.md"
    "$repository_dir/THIRD_PARTY_NOTICES.md"
    "$repository_dir/LICENSE.md"
)
license_relative_documents=(
    "$godot_notice_relative"
    "$dotnet_license_relative"
    "$dotnet_notice_relative"
)
required_documents=("${root_documents[@]}")
for relative_document in "${license_relative_documents[@]}"; do
    required_documents+=("$repository_dir/$relative_document")
done

verify_sha256() {
    local file_path=$1
    local expected=$2
    local actual
    actual=$(shasum -a 256 "$file_path" | awk '{print $1}')
    if [[ $actual != $expected ]]; then
        print -u2 "Unexpected SHA-256 for $file_path: $actual"
        return 1
    fi
}

verify_release_payload() {
    local candidate_app=$1
    local resources="$candidate_app/Contents/Resources"
    local unexpected_pdb
    local game_assembly_count
    local core_assembly_count

    unexpected_pdb=$(find "$candidate_app" -type f -name '*.pdb' -print -quit)
    if [[ -n $unexpected_pdb ]]; then
        print -u2 "Release package contains a debug symbol file: $unexpected_pdb"
        return 1
    fi

    game_assembly_count=$(find "$resources" -type f -name 'Gridworks.Game.dll' | wc -l | tr -d ' ')
    core_assembly_count=$(find "$resources" -type f -name 'Gridworks.Core.dll' | wc -l | tr -d ' ')
    if [[ $game_assembly_count != 2 || $core_assembly_count != 2 ]]; then
        print -u2 "Unexpected managed assembly count: Game=$game_assembly_count Core=$core_assembly_count"
        return 1
    fi

    while IFS= read -r managed_binary; do
        if LC_ALL=C strings "$managed_binary" | grep -E '/(Users|home|private/tmp)/' >/dev/null; then
            print -u2 "Release assembly exposes a local absolute path: $managed_binary"
            return 1
        fi
    done < <(find "$resources" -type f \( -name 'Gridworks.Game.dll' -o -name 'Gridworks.Core.dll' \))

    while IFS= read -r game_assembly; do
        if LC_ALL=C strings "$game_assembly" | grep -E \
            'Gridworks.Game.EmbeddedData.(product-|release-world-v1|release-campaign-v1|commercial-core-slice-v1)' \
            >/dev/null; then
            print -u2 "Release assembly contains a prototype or v1 data resource: $game_assembly"
            return 1
        fi
        if ! LC_ALL=C strings "$game_assembly" | grep -F 'Gridworks.Game.EmbeddedData.release-world-v2.json' >/dev/null ||
           ! LC_ALL=C strings "$game_assembly" | grep -F 'Gridworks.Game.EmbeddedData.release-campaign-v2.json' >/dev/null ||
           ! LC_ALL=C strings "$game_assembly" | grep -F 'Gridworks.Game.EmbeddedData.commercial-build-identity-v1.json' >/dev/null; then
            print -u2 "Release assembly is missing commercial v2 data or build identity: $game_assembly"
            return 1
        fi
    done < <(find "$resources" -type f -name 'Gridworks.Game.dll')

    if LC_ALL=C strings "$candidate_app/Contents/MacOS/Gridworks" | grep -E \
        'res://(ProductMain|ReleaseMain|Prototype|Scope1|CommercialPrototype).*\.tscn' >/dev/null; then
        print -u2 "Release package contains a prototype or v1 entry scene."
        return 1
    fi
}

if [[ $package_temp_dir != /private/tmp/gridworks-macos-package.* ]]; then
    print -u2 "Unexpected temporary directory: $package_temp_dir"
    exit 1
fi

trap 'rm -rf -- "$package_temp_dir"' EXIT

if [[ -n $(git -C "$repository_dir" status --porcelain --untracked-files=all) ]]; then
    print -u2 "Internal candidate must be built from a clean committed checkout."
    exit 1
fi

if [[ ! -x $godot_bin ]]; then
    print -u2 "Godot executable not found: $godot_bin"
    exit 1
fi

for required_document in "${required_documents[@]}"; do
    if [[ ! -f $required_document ]]; then
        print -u2 "Required package document not found: $required_document"
        exit 1
    fi
done

verify_sha256 \
    "$repository_dir/$godot_notice_relative" \
    "cb1980c88089573bcacd7221d777c689bb8bbd778799f24c27fca0fe5f774d6d"
verify_sha256 \
    "$repository_dir/$dotnet_license_relative" \
    "cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310"
verify_sha256 \
    "$repository_dir/$dotnet_notice_relative" \
    "97c1a7b3da6a4c6ad516448719f45114b41a4d4c5aa300a944476e2e4f5da438"

raw_zip="$package_temp_dir/raw-export.zip"
stage_dir="$package_temp_dir/stage"
verification_dir="$package_temp_dir/verification"
final_zip="$package_temp_dir/Gridworks-macOS-0.1.0.zip"
app_path="$stage_dir/Gridworks.app"
smoke_storage="$package_temp_dir/smoke-storage"
smoke_save="$smoke_storage/release-campaign-save-v3.json"

mkdir -p "$stage_dir" "$verification_dir" "$smoke_storage" "$repository_dir/dist"

"$godot_bin" \
    --headless \
    --path "$repository_dir/game" \
    --export-release "macOS Internal Test" "$raw_zip" \
    --log-file "$package_temp_dir/export.log"

ditto -x -k "$raw_zip" "$stage_dir"
# A previous Godot RID publish can leave portable symbols in its generated
# output directory. They are not part of the distributable app.
find "$app_path" -type f -name '*.pdb' -delete
verify_release_payload "$app_path"

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
zip -q -j "$final_zip" "${root_documents[@]}"
(
    cd "$repository_dir"
    zip -q "$final_zip" "${license_relative_documents[@]}"
)

ditto -x -k "$final_zip" "$verification_dir"
verified_app="$verification_dir/Gridworks.app"
verified_executable="$verified_app/Contents/MacOS/Gridworks"
codesign --verify --deep --strict --verbose=2 "$verified_app"
arch -arm64 "$verified_executable" --version
verify_release_payload "$verified_app"

for required_document in "${root_documents[@]}"; do
    if [[ ! -f "$verification_dir/${required_document:t}" ]]; then
        print -u2 "Packaged document not found: ${required_document:t}"
        exit 1
    fi
done
for relative_document in "${license_relative_documents[@]}"; do
    if [[ ! -f "$verification_dir/$relative_document" ]]; then
        print -u2 "Packaged legal notice not found: $relative_document"
        exit 1
    fi
done

verify_sha256 \
    "$verification_dir/$godot_notice_relative" \
    "cb1980c88089573bcacd7221d777c689bb8bbd778799f24c27fca0fe5f774d6d"
verify_sha256 \
    "$verification_dir/$dotnet_license_relative" \
    "cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310"
verify_sha256 \
    "$verification_dir/$dotnet_notice_relative" \
    "97c1a7b3da6a4c6ad516448719f45114b41a4d4c5aa300a944476e2e4f5da438"

(
    cd "$verification_dir"
    GRIDWORKS_COMMERCIAL_STORAGE_DIRECTORY="$smoke_storage" \
    GRIDWORKS_STAGE_F_SMOKE_SAVE_PATH="$smoke_save" \
    arch -arm64 "$verified_executable" \
        --headless \
        --log-file "$package_temp_dir/campaign-checkpoint.log" \
        -- \
        --commercial-campaign-stage-f-checkpoint-smoke
    GRIDWORKS_COMMERCIAL_STORAGE_DIRECTORY="$smoke_storage" \
    GRIDWORKS_STAGE_F_SMOKE_SAVE_PATH="$smoke_save" \
    arch -arm64 "$verified_executable" \
        --headless \
        --log-file "$package_temp_dir/campaign-completion.log" \
        -- \
        --commercial-campaign-stage-f-completion-smoke
    GRIDWORKS_COMMERCIAL_STORAGE_DIRECTORY="$smoke_storage" \
    GRIDWORKS_STAGE_F_SMOKE_SAVE_PATH="$smoke_save" \
    arch -arm64 "$verified_executable" \
        --headless \
        --log-file "$package_temp_dir/campaign-completed-resume.log" \
        -- \
        --commercial-campaign-stage-f-completed-resume-smoke
)

if ! grep -Fq "COMMERCIAL_CAMPAIGN_STAGE_F_CHECKPOINT_SMOKE_PASS" "$package_temp_dir/campaign-checkpoint.log" ||
   ! grep -Fq "COMMERCIAL_CAMPAIGN_STAGE_F_COMPLETION_SMOKE_PASS" "$package_temp_dir/campaign-completion.log" ||
   ! grep -Fq "COMMERCIAL_CAMPAIGN_STAGE_F_COMPLETED_RESUME_SMOKE_PASS" "$package_temp_dir/campaign-completed-resume.log"; then
    print -u2 "The isolated commercial campaign smoke did not reach all completion markers."
    exit 1
fi

mv -f "$final_zip" "$output_zip"
print "architectures=$architecture_list"
print "minimum_macos=arm64:$arm64_minimum,x86_64:$x86_64_minimum"
print "signature=adhoc"
print "campaign_smoke=checkpoint+fresh-process-completion+completed-resume"
print "world_sha256=$(shasum -a 256 "$repository_dir/data/release-world-v2.json" | awk '{print $1}')"
print "campaign_sha256=$(shasum -a 256 "$repository_dir/data/release-campaign-v2.json" | awk '{print $1}')"
print "build_identity_sha256=$(shasum -a 256 "$repository_dir/data/commercial-build-identity-v1.json" | awk '{print $1}')"
print "source_commit=$(git -C "$repository_dir" rev-parse HEAD)"
shasum -a 256 "$output_zip"
