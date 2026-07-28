#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
artifact_root="${ARTIFACT_DIR:-$repo_root/artifacts/modern-compat}"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/neo-confuser-modern.XXXXXX")"

cleanup() {
  rm -rf -- "$work_dir"
}
trap cleanup EXIT

samples=(
  "GenericResolutionSample|$script_dir/GenericResolutionSample/GenericResolutionSample.csproj|GenericResolutionSample"
  "BasicFlow|$repo_root/UnitTest/Samples/BasicFlow/BasicFlow.csproj|BasicFlow"
  "Constants|$repo_root/UnitTest/Samples/Constants/Constants.csproj|Constants"
  "ExceptionFlow|$repo_root/UnitTest/Samples/ExceptionFlow/ExceptionFlow.csproj|ExceptionFlow"
)
profiles=(
  "$script_dir/rename-only.crproj"
  "$script_dir/normal.crproj"
)

dotnet tool restore --tool-manifest "$repo_root/.config/dotnet-tools.json"
dotnet build "$repo_root/Confuser.CLI/Confuser.CLI.csproj" \
  --configuration "$configuration" \
  --nologo

for sample in "${samples[@]}"; do
  IFS="|" read -r _ project _ <<<"$sample"
  dotnet build "$project" --configuration "$configuration" --nologo
done

cli="$repo_root/Confuser.CLI/bin/$configuration/net8.0/Confuser.CLI.dll"

disassemble() {
  local assembly="$1"
  local destination="$2"

  (
    cd "$repo_root"
    dotnet tool run ilspycmd -- \
      --disable-updatecheck \
      --ilcode \
      "$assembly"
  ) >"$destination"
}

execute_protected() {
  local tfm="$1"
  local source_dir="$2"
  local output_dir="$3"
  local assembly_name="$4"
  local runtime_log="$5"

  if [[ "$tfm" == "net8.0" ]]; then
    dotnet exec \
      --runtimeconfig "$source_dir/$assembly_name.runtimeconfig.json" \
      --depsfile "$source_dir/$assembly_name.deps.json" \
      "$output_dir/$assembly_name.dll" | tee "$runtime_log"
    return
  fi

  if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required to execute the .NET 5 compatibility test." >&2
    return 2
  fi

  docker run --rm \
    --volume "$source_dir:/sample:ro" \
    --volume "$output_dir:/protected:ro" \
    mcr.microsoft.com/dotnet/runtime:5.0 \
    dotnet exec \
      --runtimeconfig "/sample/$assembly_name.runtimeconfig.json" \
      --depsfile "/sample/$assembly_name.deps.json" \
      "/protected/$assembly_name.dll" | tee "$runtime_log"
}

protect() {
  local sample_name="$1"
  local project="$2"
  local assembly_name="$3"
  local tfm="$4"
  local profile="$5"
  local profile_name
  local project_dir
  local source_dir
  local output_dir
  local artifact_dir

  profile_name="$(basename "$profile" .crproj)"
  project_dir="$(dirname "$project")"
  source_dir="$project_dir/bin/$configuration/$tfm"
  output_dir="$work_dir/$sample_name/$profile_name/$tfm"
  artifact_dir="$artifact_root/$sample_name/$profile_name/$tfm"
  mkdir -p "$output_dir" "$artifact_dir"

  dotnet "$cli" \
    --no-pause \
    --out "$output_dir" \
    "$source_dir/$assembly_name.dll" \
    "$profile" | tee "$artifact_dir/obfuscator.log"

  disassemble "$source_dir/$assembly_name.dll" "$artifact_dir/original.il"
  disassemble "$output_dir/$assembly_name.dll" "$artifact_dir/protected.il"
  cp "$output_dir/$assembly_name.dll" "$artifact_dir/$assembly_name.protected.dll"

  (
    cd "$source_dir"
    sha256sum "$assembly_name.dll"
  ) >"$artifact_dir/original.sha256"
  (
    cd "$output_dir"
    sha256sum "$assembly_name.dll"
  ) >"$artifact_dir/protected.sha256"

  if cmp -s "$artifact_dir/original.il" "$artifact_dir/protected.il"; then
    echo "Protected IL is identical to the original for $sample_name/$profile_name/$tfm." >&2
    return 1
  fi

  execute_protected \
    "$tfm" \
    "$source_dir" \
    "$output_dir" \
    "$assembly_name" \
    "$artifact_dir/runtime.log"
  grep -q "RESULT:PASS" "$artifact_dir/runtime.log"
}

mkdir -p "$artifact_root"
for sample in "${samples[@]}"; do
  IFS="|" read -r sample_name project assembly_name <<<"$sample"
  for profile in "${profiles[@]}"; do
    protect "$sample_name" "$project" "$assembly_name" net8.0 "$profile"
    protect "$sample_name" "$project" "$assembly_name" net5.0 "$profile"
  done
done

echo "Modern compatibility checks passed for net5.0 and net8.0."
echo "Disassemblies and protected assemblies: $artifact_root"
