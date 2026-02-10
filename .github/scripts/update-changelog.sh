#!/bin/bash
set -e

# Update CHANGELOG.md with a new version entry
# Usage: ./update-changelog.sh <version> [previous-version]
#
# This script:
# 1. Extracts version from tag (v1.0.8 -> 1.0.8)
# 2. Generates changelog entry from commits since last version
# 3. Inserts entry after [Unreleased] section
# 4. Updates reference links at bottom

VERSION="$1"
PREV_VERSION="${2:-}"
RANGE_END_OVERRIDE="${3:-}"

if [[ -z "$VERSION" ]]; then
  echo "Usage: $0 <version> [previous-version]"
  echo "Example: $0 1.0.8 1.0.7"
  exit 1
fi

# Strip 'v' prefix if present
VERSION="${VERSION#v}"
PREV_VERSION="${PREV_VERSION#v}"

detect_previous_version() {
  local target_version="$1"
  local known_versions prev latest

  # Prefer semver ordering against existing tags so we can resolve the immediate
  # previous version even when generating changelog for an untagged future release.
  known_versions=$(git tag --list 'v*' | sed 's/^v//' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.]+)?$' || true)
  if [[ -n "$known_versions" ]]; then
    prev=$(printf '%s\n%s\n' "$known_versions" "$target_version" | sort -V | awk -v v="$target_version" '
      $0 == v { print previous; exit }
      { previous = $0 }
    ')
    if [[ -n "$prev" && "$prev" != "$target_version" ]]; then
      echo "$prev"
      return
    fi
  fi

  # Fallback: latest tag reachable from HEAD.
  latest=$(git describe --tags --abbrev=0 --match 'v[0-9]*' HEAD 2>/dev/null | sed 's/^v//' || true)
  if [[ -n "$latest" && "$latest" != "$target_version" ]]; then
    echo "$latest"
    return
  fi

  echo ""
}

# If no previous version provided, try to detect from git tags
if [[ -z "$PREV_VERSION" ]]; then
  PREV_VERSION=$(detect_previous_version "$VERSION")
  if [[ -z "$PREV_VERSION" ]]; then
    echo "⚠️  Could not detect previous version, assuming this is first release"
  fi
fi

echo "Updating CHANGELOG.md for version $VERSION (previous: $PREV_VERSION)"

# Create anchor ID (1.0.8 -> v1-0-8)
ANCHOR="v${VERSION//./-}"

# Determine range end. For historical releases with an existing tag, use that tag.
if [[ -n "$RANGE_END_OVERRIDE" ]]; then
  RANGE_END="$RANGE_END_OVERRIDE"
elif git rev-parse "v${VERSION}" >/dev/null 2>&1; then
  RANGE_END="v${VERSION}"
else
  RANGE_END="HEAD"
fi

# Use tag commit date for historical releases, otherwise today's date.
if [[ "$RANGE_END" == "v${VERSION}" ]]; then
  DATE=$(git log -1 --format=%cs "$RANGE_END")
else
  DATE=$(date +%Y-%m-%d)
fi

# Generate commit log for the release window.
if [[ -n "$PREV_VERSION" ]] && git rev-parse "v${PREV_VERSION}" >/dev/null 2>&1; then
  COMMITS=$(git log "v${PREV_VERSION}..${RANGE_END}" --pretty=format:"- %s" --no-merges)
else
  # Fallback for first release when no prior tag exists.
  COMMITS=$(git log --pretty=format:"- %s" --no-merges | head -20)
fi

# Exclude release-changelog housekeeping commit(s) from release notes.
COMMITS=$(echo "$COMMITS" | grep -Ev "^- (chore|docs): update changelog for v${VERSION}$" || true)

# Categorize commits by conventional commit type
ADDED=$(echo "$COMMITS" | grep -E "^- feat(\([^)]+\))?!?:" | sed -E 's/^- feat(\([^)]+\))?!?: /- /' || true)
CHANGED=$(echo "$COMMITS" | grep -E "^- (chore|refactor|perf|style)(\([^)]+\))?!?:" | sed -E 's/^- (chore|refactor|perf|style)(\([^)]+\))?!?: /- /' || true)
FIXED=$(echo "$COMMITS" | grep -E "^- fix(\([^)]+\))?!?:" | sed -E 's/^- fix(\([^)]+\))?!?: /- /' || true)
OTHER=$(echo "$COMMITS" | grep -Ev "^- (feat|fix|chore|refactor|perf|style|docs|test|ci|build)(\([^)]+\))?!?:" || true)

# Create temp file for new entry
ENTRY_FILE=$(mktemp)

# Build changelog entry
cat > "$ENTRY_FILE" <<EOF
<a id="$ANCHOR"></a>
## [$VERSION] - $DATE
EOF

if [[ -n "$ADDED" ]]; then
  cat >> "$ENTRY_FILE" <<EOF
### Added
$ADDED
EOF
fi

if [[ -n "$CHANGED" ]]; then
  cat >> "$ENTRY_FILE" <<EOF
### Changed
$CHANGED
EOF
fi

if [[ -n "$FIXED" ]]; then
  cat >> "$ENTRY_FILE" <<EOF
### Fixed
$FIXED
EOF
fi

# If no categorized commits, add a generic Changed section
if [[ -z "$ADDED" && -z "$CHANGED" && -z "$FIXED" ]]; then
  cat >> "$ENTRY_FILE" <<EOF
### Changed
- Maintenance and documentation updates.
EOF
fi

# Add any other commits that don't follow conventional format
if [[ -n "$OTHER" ]]; then
  cat >> "$ENTRY_FILE" <<EOF
### Other
$OTHER
EOF
fi

# Create temp file for new CHANGELOG
TEMP_FILE=$(mktemp)

# Read CHANGELOG.md and insert new entry after [Unreleased]
awk '
  /^## \[Unreleased\]/ {
    print $0
    print ""
    while ((getline line < "'"$ENTRY_FILE"'") > 0) {
      print line
    }
    next
  }
  { print }
' CHANGELOG.md > "$TEMP_FILE"

# Update reference links at bottom
# Extract repository URL from existing links
REPO_URL=$(grep '\[Unreleased\]:' CHANGELOG.md | sed -E 's|.*https://github.com/([^/]+/[^/]+)/.*|\1|' || echo "dariuszparys/coralph")

# Build new reference links
NEW_UNRELEASED_LINK="[Unreleased]: https://github.com/${REPO_URL}/compare/v${VERSION}...HEAD"
NEW_VERSION_LINK="[${VERSION}]: https://github.com/${REPO_URL}/compare/v${PREV_VERSION}...v${VERSION}"

# Replace reference links section
awk -v new_unreleased="$NEW_UNRELEASED_LINK" -v new_version="$NEW_VERSION_LINK" '
  /^\[Unreleased\]:/ {
    print new_unreleased
    print new_version
    in_links=1
    next
  }
  in_links && /^\[/ {
    print
    next
  }
  in_links && !/^\[/ {
    in_links=0
  }
  !in_links { print }
' "$TEMP_FILE" > "${TEMP_FILE}.2"

mv "${TEMP_FILE}.2" CHANGELOG.md
rm -f "$TEMP_FILE" "$ENTRY_FILE"

echo "✅ Updated CHANGELOG.md with version $VERSION"
echo "📝 Entry preview:"
cat <<EOF

<a id="$ANCHOR"></a>
## [$VERSION] - $DATE
EOF
if [[ -n "$ADDED" ]]; then
  echo "### Added"
  echo "$ADDED"
fi
if [[ -n "$CHANGED" ]]; then
  echo "### Changed"
  echo "$CHANGED"
fi
if [[ -n "$FIXED" ]]; then
  echo "### Fixed"
  echo "$FIXED"
fi
if [[ -n "$OTHER" ]]; then
  echo "### Other"
  echo "$OTHER"
fi
