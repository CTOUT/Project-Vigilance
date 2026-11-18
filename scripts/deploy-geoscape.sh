#!/usr/bin/env bash
#
# deploy-geoscape.sh - Deploys the Geoscape mod to the Linux RimWorld Mods directory.
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

show_help() {
	cat <<EOF
Usage: $(basename "$0") [options] [mods_directory]

Deploys Project Vigilance: Overland Incursions (Geoscape) to the local Linux
RimWorld Mods directory using a symbolic link (or copy) to clean staging
(dist/ProjectVigilance-OverlandIncursions, ~2.3 MB, excluding Source/).

Options:
  -p, --path <path>    Specify custom RimWorld Mods directory
                       (Default: $DEFAULT_MODS_PATH)
  -b, --skip-build     Skip compiling the .NET 10 project
  -d, --dev            Link/copy raw Geoscape/ directory instead of clean dist/ staging
  -c, --copy           Copy mod directory instead of creating a symlink
  -r, --rebuild        Force clean rebuild (dotnet build --no-incremental)
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

echo -e "${CYAN}==> [Project Vigilance] Starting Geoscape deployment pipeline ($OS_NAME)...${NC}"

# 1. Resolve paths
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
GEOSCAPE_DIR="$REPO_ROOT/Geoscape"
STAGING_DIR="$REPO_ROOT/dist/ProjectVigilance-OverlandIncursions"
PROJECT_FILE="$GEOSCAPE_DIR/Source/Geoscape/Geoscape.csproj"
TARGET_DLL="$GEOSCAPE_DIR/1.7/Assemblies/Geoscape.dll"

# Expand tilde if present in user-provided path
RIMWORLD_MODS_PATH="${RIMWORLD_MODS_PATH/#\~/$HOME}"

if [[ ! -f $PROJECT_FILE ]]; then
	echo -e "${RED}Error: Project file not found at: $PROJECT_FILE${NC}" >&2
	exit 1
fi

# Ensure valid runtime directory for .NET / NuGet mutexes (fixes errors in sandboxed or headless Linux sessions)
if [[ -z ${XDG_RUNTIME_DIR:-} || ! -d ${XDG_RUNTIME_DIR:-} ]]; then
	export XDG_RUNTIME_DIR="/tmp"
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

rotate_if_locked "$TARGET_DLL"
rotate_if_locked "$STAGING_DIR/1.7/Assemblies/Geoscape.dll"

# 3. Compile .NET 10 binary (MSBuild StageCleanDistribution stages dist/ automatically)
if [[ $SKIP_BUILD == false ]]; then
	if command -v dotnet >/dev/null 2>&1; then
		echo -e "${YELLOW}--> Compiling Geoscape.csproj targeting .NET 10 (Release)...${NC}"
		BUILD_ARGS=("$PROJECT_FILE" "--configuration" "Release")
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
		if [[ -f $TARGET_DLL ]]; then
			echo -e "${YELLOW}--> Warning: 'dotnet' command not found in PATH, but pre-compiled Geoscape.dll is present.${NC}"
			echo -e "${YELLOW}    Skipping compilation and proceeding with existing assembly.${NC}"
		else
			echo -e "${RED}--> Error: 'dotnet' SDK is not installed or not in PATH, and no pre-compiled binary exists at:${NC}" >&2
			echo -e "    $TARGET_DLL" >&2
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
find "$GEOSCAPE_DIR/Source" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
rm -f "$GEOSCAPE_DIR/1.7/Assemblies/"*.old "$STAGING_DIR/1.7/Assemblies/"*.old 2>/dev/null || true

stage_clean_distribution() {
	if [[ -f "$STAGING_DIR/About/PublishedFileId.txt" ]]; then
		cp -f "$STAGING_DIR/About/PublishedFileId.txt" "$GEOSCAPE_DIR/About/PublishedFileId.txt"
	fi

	mkdir -p "$STAGING_DIR/About" "$STAGING_DIR/1.7/Assemblies"
	rm -rf "$STAGING_DIR/Source" "$STAGING_DIR/1.7/Defs" "$STAGING_DIR/1.7/Patches" "$STAGING_DIR/1.7/Textures" "$STAGING_DIR/1.7/Languages"
	rm -f "$STAGING_DIR/1.7/Assets.zip" "$STAGING_DIR/1.7/Assets.zip."* "$STAGING_DIR/1.7/Assemblies/"*.old 2>/dev/null || true

	for about_file in About.xml Preview.png Icon.png ModIcon.png PublishedFileId.txt; do
		if [[ -f "$GEOSCAPE_DIR/About/$about_file" ]]; then
			cp -f "$GEOSCAPE_DIR/About/$about_file" "$STAGING_DIR/About/$about_file"
		fi
	done

	if [[ -f "$GEOSCAPE_DIR/1.7/Assemblies/Geoscape.dll" ]]; then
		cp -f "$GEOSCAPE_DIR/1.7/Assemblies/Geoscape.dll" "$STAGING_DIR/1.7/Assemblies/Geoscape.dll"
	fi
	if [[ -f "$GEOSCAPE_DIR/1.7/Assemblies/Geoscape.pdb" ]]; then
		cp -f "$GEOSCAPE_DIR/1.7/Assemblies/Geoscape.pdb" "$STAGING_DIR/1.7/Assemblies/Geoscape.pdb"
	fi

	for subdir in Defs Patches Textures Languages; do
		if [[ -d "$GEOSCAPE_DIR/1.7/$subdir" ]]; then
			mkdir -p "$STAGING_DIR/1.7/$subdir"
			cp -a "$GEOSCAPE_DIR/1.7/$subdir"/. "$STAGING_DIR/1.7/$subdir"/
		fi
	done

	[[ -f "$REPO_ROOT/LICENSE" ]] && cp -f "$REPO_ROOT/LICENSE" "$STAGING_DIR/LICENSE"
	[[ -f "$GEOSCAPE_DIR/README.md" ]] && cp -f "$GEOSCAPE_DIR/README.md" "$STAGING_DIR/README.md"
}

if [[ $DEV_MODE == true ]]; then
	DEPLOY_SOURCE_DIR="$GEOSCAPE_DIR"
else
	stage_clean_distribution
	DEPLOY_SOURCE_DIR="$STAGING_DIR"
fi

# 4. Validate or create target Mods directory
if [[ ! -d $RIMWORLD_MODS_PATH ]]; then
	echo -e "${YELLOW}--> RimWorld Mods directory does not exist. Creating:${NC} $RIMWORLD_MODS_PATH"
	mkdir -p "$RIMWORLD_MODS_PATH"
fi

# 5. Create or verify symbolic link (or perform copy)
TARGET_DESTINATION="$RIMWORLD_MODS_PATH/ProjectVigilance-Geoscape"

# Handle Flatpak sandbox permissions when using symlink mode
if [[ $COPY_MODE == false && "$RIMWORLD_MODS_PATH" == *"/com.valvesoftware.Steam/"* ]]; then
	if command -v flatpak >/dev/null 2>&1; then
		if ! flatpak run --command=test com.valvesoftware.Steam -d "$DEPLOY_SOURCE_DIR" 2>/dev/null; then
			echo -e "${YELLOW}--> Flatpak Steam sandbox detected without access to project directory.${NC}"
			echo -e "${YELLOW}    Granting Flatpak filesystem permission to read $REPO_ROOT...${NC}"
			flatpak override --user com.valvesoftware.Steam --filesystem="$REPO_ROOT:ro"
		fi
	fi
fi

if [[ $COPY_MODE == true ]]; then
	echo -e "${YELLOW}--> Copy mode enabled. Syncing mod files to: $TARGET_DESTINATION${NC}"
	rm -rf "$TARGET_DESTINATION"
	mkdir -p "$TARGET_DESTINATION"
	cp -a "$DEPLOY_SOURCE_DIR"/. "$TARGET_DESTINATION"/
	echo -e "${GREEN}--> Copied mod successfully: $TARGET_DESTINATION${NC}"
else
	if [[ -L $TARGET_DESTINATION ]]; then
		CURRENT_TARGET="$(readlink -f "$TARGET_DESTINATION" || true)"
		if [[ $CURRENT_TARGET == "$DEPLOY_SOURCE_DIR" ]]; then
			echo -e "${GREEN}--> Symlink already exists and is healthy: $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR${NC}"
		else
			echo -e "${YELLOW}--> Symlink points to $CURRENT_TARGET. Updating to $DEPLOY_SOURCE_DIR...${NC}"
			ln -sfn "$DEPLOY_SOURCE_DIR" "$TARGET_DESTINATION"
			echo -e "${GREEN}--> Updated symlink: $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR${NC}"
		fi
	elif [[ -e $TARGET_DESTINATION ]]; then
		echo -e "${YELLOW}--> Target exists and is not a symlink. Replacing with symlink: $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR${NC}"
		rm -rf "$TARGET_DESTINATION"
		ln -sfn "$DEPLOY_SOURCE_DIR" "$TARGET_DESTINATION"
		echo -e "${GREEN}--> Created symlink: $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR${NC}"
	else
		ln -sfn "$DEPLOY_SOURCE_DIR" "$TARGET_DESTINATION"
		echo -e "${GREEN}--> Created symlink: $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR${NC}"
	fi
fi

# 6. Verification
if [[ -f "$TARGET_DESTINATION/About/About.xml" ]]; then
	echo -e "${GREEN}--> Verified: About/About.xml accessible via deployment.${NC}"
fi

if [[ -f "$TARGET_DESTINATION/1.7/Assemblies/Geoscape.dll" ]]; then
	echo -e "${GREEN}--> Verified: 1.7/Assemblies/Geoscape.dll present.${NC}"
else
	echo -e "${YELLOW}--> Note: 1.7/Assemblies/Geoscape.dll not found yet. Run 'dotnet build' to produce the binary.${NC}"
fi

# Verify accessibility inside Flatpak container if applicable
if [[ "$RIMWORLD_MODS_PATH" == *"/com.valvesoftware.Steam/"* ]] && command -v flatpak >/dev/null 2>&1; then
	if flatpak run --command=test com.valvesoftware.Steam -f "$TARGET_DESTINATION/About/About.xml" 2>/dev/null; then
		echo -e "${GREEN}--> Verified: Mod accessible inside Flatpak Steam sandbox.${NC}"
	else
		echo -e "${RED}--> Warning: Flatpak Steam cannot read files through symlink.${NC}" >&2
		echo -e "${YELLOW}    Run with --copy mode, or grant Flatpak filesystem permission:${NC}" >&2
		echo -e "      flatpak override --user com.valvesoftware.Steam --filesystem=$REPO_ROOT:ro" >&2
	fi
fi

DEPLOY_SIZE="$(du -sh "$DEPLOY_SOURCE_DIR" 2>/dev/null | cut -f1 || echo 'unknown')"
MODE_LABEL="Clean staging (Workshop-ready)"
if [[ $DEV_MODE == true ]]; then
	MODE_LABEL="Dev (raw Geoscape)"
fi
echo -e "${CYAN}==> [Project Vigilance] Deployment complete [$MODE_LABEL]: $TARGET_DESTINATION -> $DEPLOY_SOURCE_DIR ($DEPLOY_SIZE)${NC}"
