#!/usr/bin/env bash
#
# deploy-geoscape.sh - Deploys all 5 Project Vigilance modular packages to the Linux RimWorld Mods directory.
#
# Default target: ~/.local/share/Steam/steamapps/common/RimWorld/Mods/
#
# Idempotent and safe to run repeatedly across Zorin OS, Ubuntu, Debian, CachyOS,
# and other Linux distributions.

set -euo pipefail

CYAN='\033[0;36m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'

# Detect OS distribution name
OS_NAME="Linux"
if [[ -f /etc/os-release ]]; then
	# shellcheck source=/dev/null
	source /etc/os-release
	OS_NAME="${PRETTY_NAME:-Linux}"
fi

# Detect RimWorld Mods directory across native Steam, Flatpak Steam, and Snap Steam
detect_default_mods_path() {
	if [[ -n ${RIMWORLD_MODS_PATH:-} ]]; then
		echo "$RIMWORLD_MODS_PATH"
		return
	fi

	local candidates=(
		"$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld/Mods"
		"${XDG_DATA_HOME:-$HOME/.local/share}/Steam/steamapps/common/RimWorld/Mods"
		"$HOME/.steam/steam/steamapps/common/RimWorld/Mods"
		"$HOME/.steam/root/steamapps/common/RimWorld/Mods"
		"$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/RimWorld/Mods"
		"$HOME/snap/steam/common/.local/share/Steam/steamapps/common/RimWorld/Mods"
	)

	# 1. Prefer candidate where RimWorld binary or Assembly-CSharp.dll is present
	for candidate in "${candidates[@]}"; do
		local parent_rimworld="${candidate%/Mods}"
		if [[ -f "$parent_rimworld/Assembly-CSharp.dll" || -f "$parent_rimworld/RimWorldLinux" || -f "$parent_rimworld/RimWorldWin64.exe" ]]; then
			echo "$candidate"
			return
		fi
	done

	# 2. Check candidate where Mods directory already exists
	for candidate in "${candidates[@]}"; do
		if [[ -d $candidate ]]; then
			echo "$candidate"
			return
		fi
	done

	# Fallback to standard Steam default
	echo "${XDG_DATA_HOME:-$HOME/.local/share}/Steam/steamapps/common/RimWorld/Mods"
}

# Default paths
DEFAULT_MODS_PATH="$(detect_default_mods_path)"
RIMWORLD_MODS_PATH="$DEFAULT_MODS_PATH"
SKIP_BUILD=false
REBUILD=false
COPY_MODE=false
DEV_MODE=false
FORCE_REPLACE=false

show_help() {
	cat <<EOF
Usage: $(basename "$0") [options] [mods_directory]

Deploys all 5 Project Vigilance modular packages (Core, Geoscape, Orbital,
Aquatics, Thermal) to the local Linux RimWorld Mods directory using symbolic
links (or copies) pointing to clean dist/ staging (~2.3 MB combined,
excluding Source/ and build caches).

Options:
  -p, --path <path>    Specify custom RimWorld Mods directory
                       (Default: $DEFAULT_MODS_PATH)
  -b, --skip-build     Skip compiling the .NET 10 solution
  -d, --dev            Link/copy raw <Module>/ directories instead of clean dist/ staging
  -c, --copy           Copy mod directories instead of creating symlinks
  -r, --rebuild        Force clean rebuild (dotnet build --no-incremental)
  -f, --force-replace  Allow replacing an existing non-symlink directory
  -h, --help           Show this help message and exit

Detected Environment: $OS_NAME

Examples:
  $(basename "$0")
  $(basename "$0") --dev
  $(basename "$0") --skip-build
  $(basename "$0") -p ~/.local/share/Steam/steamapps/common/RimWorld/Mods
EOF
}

# Parse options
while [[ $# -gt 0 ]]; do
	case "$1" in
	-p | --path)
		if [[ -z ${2:-} ]]; then
			echo -e "${RED}Error: --path requires a non-empty argument.${NC}" >&2
			exit 1
		fi
		RIMWORLD_MODS_PATH="$2"
		shift 2
		;;
	-b | --skip-build)
		SKIP_BUILD=true
		shift
		;;
	-d | --dev)
		DEV_MODE=true
		shift
		;;
	-c | --copy)
		COPY_MODE=true
		shift
		;;
	-r | --rebuild)
		REBUILD=true
		shift
		;;
	-f | --force-replace)
		FORCE_REPLACE=true
		shift
		;;
	-h | --help)
		show_help
		exit 0
		;;
	-*)
		echo -e "${RED}Error: Unknown option '$1'. Use --help for usage.${NC}" >&2
		exit 1
		;;
	*)
		# Positional argument treated as mods path
		RIMWORLD_MODS_PATH="$1"
		shift
		;;
	esac
done

echo -e "${CYAN}==> [Project Vigilance] Starting modular suite deployment pipeline ($OS_NAME)...${NC}"

# 1. Resolve paths & module registry
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
SOLUTION_FILE="$REPO_ROOT/Geoscape/Source/Geoscape.slnx"

# Format: "<SourceFolder>:<PackageName>:<AssemblyBase>"
MODULES=(
	"Core:ProjectVigilance-Core:ProjectVigilance.Core"
	"Geoscape:ProjectVigilance-Geoscape:Geoscape"
	"Orbital:ProjectVigilance-Orbital:ProjectVigilance.Orbital"
	"Aquatics:ProjectVigilance-Aquatics:ProjectVigilance.Aquatics"
	"Thermal:ProjectVigilance-Thermal:ProjectVigilance.Thermal"
)

# Expand tilde if present in user-provided path
RIMWORLD_MODS_PATH="${RIMWORLD_MODS_PATH/#\~/$HOME}"

if [[ ! -f $SOLUTION_FILE ]]; then
	echo -e "${RED}Error: Solution file not found at: $SOLUTION_FILE${NC}" >&2
	exit 1
fi

# Ensure valid private runtime directory for .NET / NuGet mutexes
if [[ -z ${XDG_RUNTIME_DIR:-} || ! -d ${XDG_RUNTIME_DIR:-} ]]; then
	PV_TEMP_RUNTIME_DIR="$(mktemp -d "${TMPDIR:-/tmp}/pv-runtime.XXXXXX")"
	chmod 700 "$PV_TEMP_RUNTIME_DIR"
	export XDG_RUNTIME_DIR="$PV_TEMP_RUNTIME_DIR"
	trap 'rm -rf "$PV_TEMP_RUNTIME_DIR"' EXIT
fi

# 2. Handle active assembly locks if RimWorld is running
rotate_if_locked() {
	local dll_path="$1"
	if [[ -f $dll_path ]]; then
		rm -f "${dll_path}.old" 2>/dev/null || true
		if command -v fuser >/dev/null 2>&1 && fuser "$dll_path" >/dev/null 2>&1; then
			echo -e "${YELLOW}--> Active file lock detected on $(basename "$dll_path") (RimWorld running). Rotating assembly...${NC}"
			mv -f "$dll_path" "${dll_path}.old"
		elif command -v lsof >/dev/null 2>&1 && lsof "$dll_path" >/dev/null 2>&1; then
			echo -e "${YELLOW}--> Active file lock detected on $(basename "$dll_path") (RimWorld running). Rotating assembly...${NC}"
			mv -f "$dll_path" "${dll_path}.old"
		fi
	fi
}

for entry in "${MODULES[@]}"; do
	IFS=':' read -r mod_src pkg_name asm_base <<<"$entry"
	rotate_if_locked "$REPO_ROOT/$mod_src/1.7/Assemblies/$asm_base.dll"
	rotate_if_locked "$REPO_ROOT/dist/$pkg_name/1.7/Assemblies/$asm_base.dll"
done

# 3. Compile .NET 10 suite solution (Directory.Build.targets auto-stages clean dist/ modules)
if [[ $SKIP_BUILD == false ]]; then
	if command -v dotnet >/dev/null 2>&1; then
		echo -e "${YELLOW}--> Compiling Geoscape.slnx targeting .NET 10 (Release)...${NC}"
		BUILD_ARGS=("$SOLUTION_FILE" "--configuration" "Release")
		if [[ $REBUILD == true ]]; then
			BUILD_ARGS+=("--no-incremental")
		fi

		# If RimWorld directory is identified from mods path, pass RimWorldPath and HarmonyPath
		RIMWORLD_ROOT="${RIMWORLD_MODS_PATH%/Mods}"
		if [[ -d $RIMWORLD_ROOT ]]; then
			BUILD_ARGS+=("-p:RimWorldPath=$RIMWORLD_ROOT")
			STEAMAPPS_DIR="${RIMWORLD_ROOT%/common/RimWorld}"
			HARMONY_CANDIDATE="$STEAMAPPS_DIR/workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll"
			if [[ -f $HARMONY_CANDIDATE ]]; then
				BUILD_ARGS+=("-p:HarmonyPath=$HARMONY_CANDIDATE")
			fi
		fi

		dotnet build "${BUILD_ARGS[@]}"
		echo -e "${GREEN}--> Compilation succeeded.${NC}"
	else
		GEOSCAPE_DLL="$REPO_ROOT/Geoscape/1.7/Assemblies/Geoscape.dll"
		if [[ -f $GEOSCAPE_DLL ]]; then
			echo -e "${YELLOW}--> Warning: 'dotnet' command not found in PATH, but pre-compiled assemblies are present.${NC}"
			echo -e "${YELLOW}    Skipping compilation and proceeding with existing assemblies.${NC}"
		else
			echo -e "${RED}--> Error: 'dotnet' SDK is not installed or not in PATH, and no pre-compiled binary exists at:${NC}" >&2
			echo -e "    $GEOSCAPE_DLL" >&2
			if command -v apt-get >/dev/null 2>&1; then
				echo -e "${YELLOW}    To compile on Zorin OS / Ubuntu / Debian, install .NET SDK:${NC}" >&2
				echo -e "      sudo apt update && sudo apt install -y dotnet-sdk-10.0" >&2
			elif command -v pacman >/dev/null 2>&1; then
				echo -e "${YELLOW}    To compile on CachyOS / Arch, install .NET SDK:${NC}" >&2
				echo -e "      sudo pacman -S dotnet-sdk-10.0" >&2
			else
				echo -e "${YELLOW}    Install the .NET 10 SDK from: https://dotnet.microsoft.com/download${NC}" >&2
			fi
			echo -e "    Or run with --skip-build if testing XML Defs/Patches only." >&2
			exit 1
		fi
	fi
else
	echo -e "${YELLOW}--> Skipping compilation as requested (--skip-build).${NC}"
fi

# Clean any stray in-source bin/obj directories and unlocked .old backups
for entry in "${MODULES[@]}"; do
	IFS=':' read -r mod_src pkg_name asm_base <<<"$entry"
	if [[ -d "$REPO_ROOT/$mod_src/Source" ]]; then
		find "$REPO_ROOT/$mod_src/Source" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
	fi
	rm -f "$REPO_ROOT/$mod_src/1.7/Assemblies/"*.old "$REPO_ROOT/dist/$pkg_name/1.7/Assemblies/"*.old 2>/dev/null || true
done

stage_clean_distribution() {
	local src_dir="$1"
	local staging_dir="$2"
	local asm_base="$3"

	if [[ -f "$staging_dir/About/PublishedFileId.txt" ]]; then
		cp -f "$staging_dir/About/PublishedFileId.txt" "$src_dir/About/PublishedFileId.txt"
	fi

	mkdir -p "$staging_dir/About" "$staging_dir/1.7/Assemblies"
	rm -rf "$staging_dir/Source" "$staging_dir/1.7/Defs" "$staging_dir/1.7/Patches" "$staging_dir/1.7/Textures" "$staging_dir/1.7/Languages" "$staging_dir/1.7/Sounds" "$staging_dir/1.7/Shaders"
	rm -f "$staging_dir/1.7/Assets.zip" "$staging_dir/1.7/Assets.zip."* "$staging_dir/1.7/Assemblies/"*.old 2>/dev/null || true

	for about_file in About.xml Preview.png ModIcon.png PublishedFileId.txt; do
		if [[ -f "$src_dir/About/$about_file" ]]; then
			cp -f "$src_dir/About/$about_file" "$staging_dir/About/$about_file"
		fi
	done

	if [[ -f "$src_dir/1.7/Assemblies/$asm_base.dll" ]]; then
		cp -f "$src_dir/1.7/Assemblies/$asm_base.dll" "$staging_dir/1.7/Assemblies/$asm_base.dll"
	fi
	if [[ -f "$src_dir/1.7/Assemblies/$asm_base.pdb" ]]; then
		cp -f "$src_dir/1.7/Assemblies/$asm_base.pdb" "$staging_dir/1.7/Assemblies/$asm_base.pdb"
	fi

	for subdir in Defs Patches Textures Languages Sounds Shaders; do
		if [[ -d "$src_dir/1.7/$subdir" ]]; then
			mkdir -p "$staging_dir/1.7/$subdir"
			cp -a "$src_dir/1.7/$subdir"/. "$staging_dir/1.7/$subdir"/
		fi
	done

	[[ -f "$REPO_ROOT/LICENSE" ]] && cp -f "$REPO_ROOT/LICENSE" "$staging_dir/LICENSE"
	[[ -f "$src_dir/README.md" ]] && cp -f "$src_dir/README.md" "$staging_dir/README.md"
}

# 4. Validate or create target Mods directory
if [[ ! -d $RIMWORLD_MODS_PATH ]]; then
	echo -e "${YELLOW}--> RimWorld Mods directory does not exist. Creating:${NC} $RIMWORLD_MODS_PATH"
	mkdir -p "$RIMWORLD_MODS_PATH"
fi

# Handle Flatpak sandbox permissions when using symlink mode
if [[ $COPY_MODE == false && "$RIMWORLD_MODS_PATH" == *"/com.valvesoftware.Steam/"* ]]; then
	if command -v flatpak >/dev/null 2>&1; then
		SAMPLE_TARGET="$REPO_ROOT/dist/ProjectVigilance-Geoscape"
		if [[ $DEV_MODE == true ]]; then
			SAMPLE_TARGET="$REPO_ROOT/Geoscape"
		fi
		if ! flatpak run --command=test com.valvesoftware.Steam -d "$SAMPLE_TARGET" 2>/dev/null; then
			echo -e "${YELLOW}--> Flatpak Steam sandbox detected without access to project directory.${NC}"
			echo -e "${YELLOW}    Granting Flatpak filesystem permission to read $REPO_ROOT...${NC}"
			flatpak override --user com.valvesoftware.Steam --filesystem="$REPO_ROOT:ro"
		fi
	fi
fi

# 5. Create or update symbolic links (or copies) for all 5 modules
for entry in "${MODULES[@]}"; do
	IFS=':' read -r mod_src pkg_name asm_base <<<"$entry"
	SRC_DIR="$REPO_ROOT/$mod_src"
	STAGING_DIR="$REPO_ROOT/dist/$pkg_name"
	TARGET_DESTINATION="$RIMWORLD_MODS_PATH/$pkg_name"

	if [[ $DEV_MODE == true ]]; then
		DEPLOY_SOURCE_DIR="$SRC_DIR"
	else
		stage_clean_distribution "$SRC_DIR" "$STAGING_DIR" "$asm_base"
		DEPLOY_SOURCE_DIR="$STAGING_DIR"
	fi

	if [[ $COPY_MODE == true ]]; then
		if [[ -e $TARGET_DESTINATION && ! -L $TARGET_DESTINATION && $FORCE_REPLACE != true ]]; then
			echo -e "${RED}Error: Refusing to delete non-symlink directory '$TARGET_DESTINATION' without --force-replace.${NC}" >&2
			exit 1
		fi
		echo -e "${YELLOW}--> Copy mode: syncing $pkg_name to $TARGET_DESTINATION${NC}"
		rm -rf "$TARGET_DESTINATION"
		mkdir -p "$TARGET_DESTINATION"
		cp -a "$DEPLOY_SOURCE_DIR"/. "$TARGET_DESTINATION"/
	else
		if [[ -L $TARGET_DESTINATION ]]; then
			CURRENT_TARGET="$(readlink -f "$TARGET_DESTINATION" || true)"
			if [[ $CURRENT_TARGET == "$DEPLOY_SOURCE_DIR" ]]; then
				echo -e "${GREEN}--> Symlink healthy: $pkg_name -> $DEPLOY_SOURCE_DIR${NC}"
			else
				echo -e "${YELLOW}--> Updating symlink $pkg_name -> $DEPLOY_SOURCE_DIR${NC}"
				ln -sfn "$DEPLOY_SOURCE_DIR" "$TARGET_DESTINATION"
			fi
		elif [[ -e $TARGET_DESTINATION ]]; then
			if [[ $FORCE_REPLACE != true ]]; then
				echo -e "${RED}Error: Refusing to replace non-symlink directory '$TARGET_DESTINATION' without --force-replace.${NC}" >&2
				exit 1
			fi
			echo -e "${YELLOW}--> Replacing non-symlink $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR${NC}"
			rm -rf "$TARGET_DESTINATION"
			ln -sfn "$DEPLOY_SOURCE_DIR" "$TARGET_DESTINATION"
		else
			ln -sfn "$DEPLOY_SOURCE_DIR" "$TARGET_DESTINATION"
			echo -e "${GREEN}--> Created symlink: $pkg_name -> $DEPLOY_SOURCE_DIR${NC}"
		fi
	fi

	MOD_SIZE="$(du -sh "$DEPLOY_SOURCE_DIR" 2>/dev/null | cut -f1 || echo 'unknown')"
	echo "    $pkg_name size: $MOD_SIZE"
done

# 6. Flatpak verification
if [[ "$RIMWORLD_MODS_PATH" == *"/com.valvesoftware.Steam/"* ]] && command -v flatpak >/dev/null 2>&1; then
	if flatpak run --command=test com.valvesoftware.Steam -f "$RIMWORLD_MODS_PATH/ProjectVigilance-Geoscape/About/About.xml" 2>/dev/null; then
		echo -e "${GREEN}--> Verified: Modular suite accessible inside Flatpak Steam sandbox.${NC}"
	else
		echo -e "${RED}--> Warning: Flatpak Steam cannot read files through symlink.${NC}" >&2
		echo -e "${YELLOW}    Run with --copy mode, or grant Flatpak filesystem permission:${NC}" >&2
		echo -e "      flatpak override --user com.valvesoftware.Steam --filesystem=$REPO_ROOT:ro" >&2
	fi
fi

MODE_LABEL="Clean staging (Workshop-ready)"
if [[ $DEV_MODE == true ]]; then
	MODE_LABEL="Dev (raw module trees)"
fi
echo -e "${CYAN}==> [Project Vigilance] Deployment complete [$MODE_LABEL] for all 5 modules in: $RIMWORLD_MODS_PATH${NC}"
