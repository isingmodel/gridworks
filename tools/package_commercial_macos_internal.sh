#!/bin/zsh

set -euo pipefail

# This opt-in unlocks the frozen historical V2 ExportRelease graph. It does not
# authorize or produce a current R2 candidate.
export GridworksLegacyV2Export=true

repository_dir=${0:A:h:h}
godot_bin=${GRIDWORKS_GODOT_BIN:-"$repository_dir/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"}
preset_name="Commercial macOS Internal Candidate"
candidate_name="Gridworks-macOS-1.0.0-internal"
dist_dir="$repository_dir/dist"
published_dir="$dist_dir/$candidate_name"
expected_world_sha256="c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14"
expected_campaign_sha256="078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a"
expected_product_version="1.0.0"
package_temp_dir=$(mktemp -d /private/tmp/gridworks-commercial-package.XXXXXX)
publish_temp_dir=""
godot_notice_relative="licenses/GODOT-4.7.1-COPYRIGHT.txt"
dotnet_license_relative="licenses/DOTNET-RUNTIME-8.0.29-LICENSE.txt"
dotnet_notice_relative="licenses/DOTNET-RUNTIME-8.0.29-THIRD-PARTY-NOTICES.txt"
world_fixture="$repository_dir/data/release-world-v2.json"
campaign_fixture="$repository_dir/data/release-campaign-v2.json"
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
portrait_records=(
    "game/assets/commercial/portraits/kang_minho.png:9001df0fad2161b224e3f16064d99e71efab0212dc8e87337c1ae43e12095e4b"
    "game/assets/commercial/portraits/lee_doyoon.png:131bb439074446c2cfdf2de5ae6ef2fc0852f38d72098bb99b4e6b871926621f"
    "game/assets/commercial/portraits/park_jihyeon.png:bb98b78da14205783b1ce0064de25e007816df57e2050d438a2fb2d328552190"
    "game/assets/commercial/portraits/yoon_seojin.png:1d44019497a55c606bd6e2bfde1f4651ff791296293aa5e1e743e9ac232cf230"
)
repository_asset_records=(
    "game/CommercialAudioLibrary.cs:cb1f55c3203aae97af0c19b382b5d780f0417ac6facb11216cc7ae343e045538"
    "game/CommercialTheme.tres:186f65de4455f8a6bdf6edc5b1c1a49d3de66964387fad82f5e429b5fac9a3e4"
    "game/icon.svg:59eef19061f84aa7e3ba897bd3319a35c067f0e74b49ffd55861bbe11297fb90"
    "game/default_bus_layout.tres:529670dc7786d5343d1d4e6199d472cad4d98fa776802bbeefe9687cecff60f1"
)

fail() {
    print -u2 -- "$1"
    exit 1
}

cleanup_temporary_directories() {
    if [[ -n $publish_temp_dir ]]; then
        [[ $publish_temp_dir == "$dist_dir/.$candidate_name.publish."* ]] ||
            fail "Refusing to clean unexpected publish temp: $publish_temp_dir"
        [[ ! -e $publish_temp_dir ]] || rm -rf -- "$publish_temp_dir"
    fi
    [[ ! -e $package_temp_dir ]] || rm -rf -- "$package_temp_dir"
}

sha256() {
    shasum -a 256 "$1" | awk '{print $1}'
}

verify_sha256() {
    local file_path=$1
    local expected=$2
    local actual
    actual=$(sha256 "$file_path")
    [[ $actual == $expected ]] ||
        fail "Unexpected SHA-256 for $file_path: $actual"
}

verify_asset_manifest_record() {
    local relative_path=$1
    local expected=$2
    local path_row_count
    local combined_row_count
    path_row_count=$(grep -F -c -- "\`$relative_path\`" \
        "$repository_dir/ASSET_MANIFEST.md" || true)
    [[ $path_row_count == 1 ]] ||
        fail "Asset manifest must contain exactly one path row: $relative_path"
    combined_row_count=$(grep -F -- "\`$relative_path\`" \
        "$repository_dir/ASSET_MANIFEST.md" | \
        grep -F -c -- "\`$expected\`" || true)
    [[ $combined_row_count == 1 ]] ||
        fail "Asset manifest must bind path and SHA-256 on one row: $relative_path $expected"
}

verify_no_repository_path() {
    local candidate_app=$1
    local target_file
    while IFS= read -r -d '' target_file; do
        if LC_ALL=C strings "$target_file" 2>/dev/null |
            grep -F "$repository_dir" >/dev/null; then
            fail "Package exposes the current repository path: $target_file"
        fi
    done < <(find "$candidate_app" -type f -print0)
}

if [[ $package_temp_dir != /private/tmp/gridworks-commercial-package.* ]]; then
    fail "Unexpected temporary directory: $package_temp_dir"
fi
trap cleanup_temporary_directories EXIT

if [[ ${1:-} == "--selftest" ]]; then
    [[ $# == 1 ]] || fail "usage: ${0:t} [--selftest]"
    verify_asset_manifest_record \
        "game/assets/commercial/portraits/yoon_seojin.png" \
        "1d44019497a55c606bd6e2bfde1f4651ff791296293aa5e1e743e9ac232cf230"
    if (verify_asset_manifest_record \
            "game/assets/commercial/portraits/yoon_seojin.png" \
            "9001df0fad2161b224e3f16064d99e71efab0212dc8e87337c1ae43e12095e4b") \
            >/dev/null 2>&1; then
        fail "Asset manifest accepted a hash from a different row."
    fi
    print "PACKAGE_SCRIPT_NEGATIVE_PROBES_PASS"
    exit 0
fi
[[ $# == 0 ]] || fail "usage: ${0:t} [--selftest]"

[[ -x $godot_bin ]] || fail "Godot executable not found: $godot_bin"
command -v dotnet >/dev/null || fail "dotnet was not found."
command -v codesign >/dev/null || fail "codesign was not found."
command -v lipo >/dev/null || fail "lipo was not found."
command -v plutil >/dev/null || fail "plutil was not found."
command -v ditto >/dev/null || fail "ditto was not found."
command -v zip >/dev/null || fail "zip was not found."
command -v strings >/dev/null || fail "strings was not found."
command -v shasum >/dev/null || fail "shasum was not found."
command -v git >/dev/null || fail "git was not found."

for required_document in "${root_documents[@]}"; do
    [[ -f $required_document ]] || fail "Required package document not found: $required_document"
done
for relative_document in "${license_relative_documents[@]}"; do
    [[ -f "$repository_dir/$relative_document" ]] ||
        fail "Required legal notice not found: $relative_document"
done

[[ -z $(git -C "$repository_dir" status --porcelain=v1 --untracked-files=all) ]] ||
    fail "Commercial package must be built from a clean committed checkout."
source_commit=$(git -C "$repository_dir" rev-parse --verify HEAD)

verify_sha256 "$world_fixture" "$expected_world_sha256"
verify_sha256 "$campaign_fixture" "$expected_campaign_sha256"
verify_sha256 \
    "$repository_dir/$godot_notice_relative" \
    "cb1980c88089573bcacd7221d777c689bb8bbd778799f24c27fca0fe5f774d6d"
verify_sha256 \
    "$repository_dir/$dotnet_license_relative" \
    "cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310"
verify_sha256 \
    "$repository_dir/$dotnet_notice_relative" \
    "97c1a7b3da6a4c6ad516448719f45114b41a4d4c5aa300a944476e2e4f5da438"
for portrait_record in "${portrait_records[@]}"; do
    portrait_path=${portrait_record%%:*}
    portrait_sha256=${portrait_record##*:}
    verify_sha256 "$repository_dir/$portrait_path" "$portrait_sha256"
    verify_asset_manifest_record "$portrait_path" "$portrait_sha256"
done
for repository_asset_record in "${repository_asset_records[@]}"; do
    repository_asset_path=${repository_asset_record%%:*}
    repository_asset_sha256=${repository_asset_record##*:}
    verify_sha256 "$repository_dir/$repository_asset_path" "$repository_asset_sha256"
    verify_asset_manifest_record "$repository_asset_path" "$repository_asset_sha256"
done
grep -F -- \
    '`OpenAI imagegen, user-directed project asset, 2026-08-19`' \
    "$repository_dir/ASSET_MANIFEST.md" >/dev/null ||
    fail "Asset manifest is missing the approved portrait provenance."

legacy_output_zip="$dist_dir/$candidate_name.zip"
legacy_output_manifest="$dist_dir/$candidate_name.manifest.txt"
legacy_output_sha256="$dist_dir/$candidate_name.sha256"
[[ ! -e $published_dir &&
    ! -e $legacy_output_zip &&
    ! -e $legacy_output_manifest &&
    ! -e $legacy_output_sha256 ]] ||
    fail "Move the existing candidate artifact set before packaging: $candidate_name"

raw_zip="$package_temp_dir/raw-export.zip"
stage_dir="$package_temp_dir/stage"
verification_dir="$package_temp_dir/verification"
manifest_path="$package_temp_dir/PACKAGE_MANIFEST.txt"
app_path="$stage_dir/Gridworks.app"

mkdir -p "$stage_dir" "$verification_dir" "$dist_dir"
publish_temp_dir=$(mktemp -d "$dist_dir/.$candidate_name.publish.XXXXXX")
[[ $publish_temp_dir == "$dist_dir/.$candidate_name.publish."* ]] ||
    fail "Unexpected same-volume publish temp: $publish_temp_dir"
final_zip="$publish_temp_dir/$candidate_name.zip"
publish_manifest="$publish_temp_dir/$candidate_name.manifest.txt"
publish_sha256="$publish_temp_dir/$candidate_name.sha256"

dotnet restore "$repository_dir/game/Gridworks.Game.csproj"
dotnet restore "$repository_dir/tools/Gridworks.PackageAudit/Gridworks.PackageAudit.csproj"
dotnet build \
    "$repository_dir/game/Gridworks.Game.csproj" \
    -c ExportRelease \
    -t:Rebuild \
    -p:CommercialProductVersion="$expected_product_version" \
    --no-restore

dotnet run \
    --project "$repository_dir/tools/Gridworks.PackageAudit/Gridworks.PackageAudit.csproj" \
    -c Release \
    -- \
    assembly \
    "$repository_dir/game/.godot/mono/temp/bin/ExportRelease/Gridworks.Game.dll" \
    "$world_fixture" \
    "$campaign_fixture" \
    "$source_commit" \
    "$expected_product_version" \
    i386
dotnet run \
    --project "$repository_dir/tools/Gridworks.PackageAudit/Gridworks.PackageAudit.csproj" \
    -c Release \
    -- \
    core \
    "$repository_dir/src/Gridworks.Core/bin/ExportRelease/net8.0/Gridworks.Core.dll"

"$godot_bin" \
    --headless \
    --path "$repository_dir/game" \
    --export-release "$preset_name" "$raw_zip" \
    --log-file "$package_temp_dir/export.log"

[[ -z $(git -C "$repository_dir" status --porcelain=v1 --untracked-files=all) ]] ||
    fail "Build or import changed the committed checkout; commit required import metadata first."

ditto -x -k "$raw_zip" "$stage_dir"
[[ -d $app_path ]] || fail "Godot export did not contain Gridworks.app."

dotnet run \
    --project "$repository_dir/tools/Gridworks.PackageAudit/Gridworks.PackageAudit.csproj" \
    -c Release \
    -- \
    app \
    "$app_path" \
    "$world_fixture" \
    "$campaign_fixture" \
    "$source_commit" \
    "$expected_product_version"
verify_no_repository_path "$app_path"

# Keep the final candidate explicitly internal: local ad-hoc signing only.
codesign \
    --force \
    --deep \
    --sign - \
    --options runtime \
    --preserve-metadata=entitlements \
    "$app_path"
codesign --verify --deep --strict --verbose=2 "$app_path"

executable_path="$app_path/Contents/MacOS/Gridworks"
architecture_list=$(lipo -archs "$executable_path")
if [[ $architecture_list != "x86_64 arm64" && $architecture_list != "arm64 x86_64" ]]; then
    fail "Unexpected executable architectures: $architecture_list"
fi

signature_details=$(codesign -dv --verbose=4 "$app_path" 2>&1)
[[ $signature_details == *"Signature=adhoc"* ]] ||
    fail "The app does not have the required ad-hoc signature."
[[ $signature_details != *"Authority="* ]] ||
    fail "The internal candidate unexpectedly has an external signing authority."

info_plist="$app_path/Contents/Info.plist"
bundle_identifier=$(plutil -extract CFBundleIdentifier raw "$info_plist")
short_version=$(plutil -extract CFBundleShortVersionString raw "$info_plist")
arm64_minimum=$(plutil -extract LSMinimumSystemVersionByArchitecture.arm64 raw "$info_plist")
x86_64_minimum=$(plutil -extract LSMinimumSystemVersionByArchitecture.x86_64 raw "$info_plist")
[[ $bundle_identifier == "com.gridworks.game" ]] ||
    fail "Unexpected bundle identifier: $bundle_identifier"
[[ $short_version == $expected_product_version ]] ||
    fail "Unexpected app version: $short_version"
[[ $arm64_minimum == "14.0" && $x86_64_minimum == "14.0" ]] ||
    fail "Unexpected macOS minimums: arm64=$arm64_minimum x86_64=$x86_64_minimum"

resources_path="$app_path/Contents/Resources"
arm64_game_assembly="$resources_path/data_Gridworks.Game_macos_arm64/Gridworks.Game.dll"
x86_64_game_assembly="$resources_path/data_Gridworks.Game_macos_x86_64/Gridworks.Game.dll"
arm64_core_assembly="$resources_path/data_Gridworks.Game_macos_arm64/Gridworks.Core.dll"
x86_64_core_assembly="$resources_path/data_Gridworks.Game_macos_x86_64/Gridworks.Core.dll"
pck_path="$resources_path/Gridworks.pck"
for architecture_payload in \
    "$arm64_game_assembly" \
    "$x86_64_game_assembly" \
    "$arm64_core_assembly" \
    "$x86_64_core_assembly" \
    "$pck_path"; do
    [[ -f $architecture_payload ]] ||
        fail "Required audited package payload is missing: $architecture_payload"
done
executable_sha256=$(sha256 "$executable_path")
arm64_game_assembly_sha256=$(sha256 "$arm64_game_assembly")
x86_64_game_assembly_sha256=$(sha256 "$x86_64_game_assembly")
arm64_core_assembly_sha256=$(sha256 "$arm64_core_assembly")
x86_64_core_assembly_sha256=$(sha256 "$x86_64_core_assembly")
pck_sha256=$(sha256 "$pck_path")

{
    print "format=gridworks.package-manifest.v2"
    print "candidate_status=INTERNAL_ADHOC"
    print "public_distribution=NOT_AUTHORIZED"
    print "new_install_full_campaign=NOT_RUN_BY_PACKAGER"
    print "source_commit=$source_commit"
    print "bundle_identifier=$bundle_identifier"
    print "version=$short_version"
    print "architectures=$architecture_list"
    print "minimum_macos=arm64:$arm64_minimum,x86_64:$x86_64_minimum"
    print "signature=adhoc"
    print "notarization=not_performed"
    print "world_sha256=$expected_world_sha256"
    print "campaign_sha256=$expected_campaign_sha256"
    print "executable_sha256=$executable_sha256"
    print "game_assembly_arm64_sha256=$arm64_game_assembly_sha256"
    print "game_assembly_x86_64_sha256=$x86_64_game_assembly_sha256"
    print "core_assembly_arm64_sha256=$arm64_core_assembly_sha256"
    print "core_assembly_x86_64_sha256=$x86_64_core_assembly_sha256"
    print "pck_sha256=$pck_sha256"
    for required_document in "${root_documents[@]}"; do
        print "document_sha256=${required_document:t}:$(sha256 "$required_document")"
    done
    for relative_document in "${license_relative_documents[@]}"; do
        print "document_sha256=$relative_document:$(sha256 "$repository_dir/$relative_document")"
    done
    for portrait_record in "${portrait_records[@]}"; do
        print "source_asset_sha256=$portrait_record"
    done
    for repository_asset_record in "${repository_asset_records[@]}"; do
        print "source_asset_sha256=$repository_asset_record"
    done
} > "$manifest_path"

ditto -c -k --sequesterRsrc --keepParent "$app_path" "$final_zip"
zip -q -j "$final_zip" "${root_documents[@]}" "$manifest_path"
(
    cd "$repository_dir"
    zip -q "$final_zip" "${license_relative_documents[@]}"
)

ditto -x -k "$final_zip" "$verification_dir"
verified_app="$verification_dir/Gridworks.app"
codesign --verify --deep --strict --verbose=2 "$verified_app"
dotnet run \
    --project "$repository_dir/tools/Gridworks.PackageAudit/Gridworks.PackageAudit.csproj" \
    -c Release \
    -- \
    app \
    "$verified_app" \
    "$world_fixture" \
    "$campaign_fixture" \
    "$source_commit" \
    "$expected_product_version"
verify_no_repository_path "$verified_app"
verified_resources="$verified_app/Contents/Resources"
verify_sha256 \
    "$verified_resources/data_Gridworks.Game_macos_arm64/Gridworks.Game.dll" \
    "$arm64_game_assembly_sha256"
verify_sha256 \
    "$verified_resources/data_Gridworks.Game_macos_x86_64/Gridworks.Game.dll" \
    "$x86_64_game_assembly_sha256"
verify_sha256 \
    "$verified_resources/data_Gridworks.Game_macos_arm64/Gridworks.Core.dll" \
    "$arm64_core_assembly_sha256"
verify_sha256 \
    "$verified_resources/data_Gridworks.Game_macos_x86_64/Gridworks.Core.dll" \
    "$x86_64_core_assembly_sha256"
verify_sha256 "$verified_resources/Gridworks.pck" "$pck_sha256"

for required_document in "${root_documents[@]}"; do
    packaged_name=${required_document:t}
    [[ -f "$verification_dir/$packaged_name" ]] ||
        fail "Packaged document not found: $packaged_name"
    cmp -s "$required_document" "$verification_dir/$packaged_name" ||
        fail "Packaged document differs from source: $packaged_name"
done
for relative_document in "${license_relative_documents[@]}"; do
    [[ -f "$verification_dir/$relative_document" ]] ||
        fail "Packaged legal notice not found: $relative_document"
    cmp -s "$repository_dir/$relative_document" "$verification_dir/$relative_document" ||
        fail "Packaged legal notice differs from source: $relative_document"
done
cmp -s "$manifest_path" "$verification_dir/PACKAGE_MANIFEST.txt" ||
    fail "Packaged manifest differs from the generated manifest."

archive_sha256=$(sha256 "$final_zip")
manifest_sha256=$(sha256 "$manifest_path")
print "$archive_sha256  $candidate_name.zip" > "$publish_sha256"
cp "$manifest_path" "$publish_manifest"

cmp -s "$manifest_path" "$publish_manifest" ||
    fail "Publish-stage manifest differs from the verified package manifest."
(
    cd "$publish_temp_dir"
    shasum -a 256 -c "$candidate_name.sha256"
)
publish_entry_count=$(find "$publish_temp_dir" -mindepth 1 -maxdepth 1 | \
    wc -l | tr -d '[:space:]')
publish_file_count=$(find "$publish_temp_dir" -mindepth 1 -maxdepth 1 -type f | \
    wc -l | tr -d '[:space:]')
[[ $publish_entry_count == 3 && $publish_file_count == 3 &&
    -f $final_zip && -f $publish_manifest && -f $publish_sha256 ]] ||
    fail "Publish stage must contain exactly the verified ZIP, manifest, and SHA-256."

# The complete artifact set is published by one same-volume directory rename.
mv "$publish_temp_dir" "$published_dir"
publish_temp_dir=""

print "candidate_status=INTERNAL_ADHOC"
print "artifact_directory=$published_dir"
print "architectures=$architecture_list"
print "minimum_macos=arm64:$arm64_minimum,x86_64:$x86_64_minimum"
print "signature=adhoc"
print "notarization=not_performed"
print "new_install_full_campaign=NOT_RUN_BY_PACKAGER"
print "pck_sha256=$pck_sha256"
print "archive_sha256=$archive_sha256"
print "manifest_sha256=$manifest_sha256"
