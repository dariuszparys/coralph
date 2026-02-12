#!/bin/bash
set -euo pipefail

# Update CHANGELOG.md with release or unreleased entries.
# Usage:
#   ./update-changelog.sh <version> [previous-version] [range-end-override]
#   ./update-changelog.sh --unreleased

VERSION_ARG="${1:-}"
PREV_VERSION_ARG="${2:-}"
RANGE_END_OVERRIDE="${3:-}"

ADDED=""
CHANGED=""
FIXED=""
OTHER=""

usage() {
  echo "Usage: $0 <version> [previous-version] [range-end-override]"
  echo "   or: $0 --unreleased"
  echo "Examples:"
  echo "  $0 1.2.0"
  echo "  $0 v1.2.0 1.1.0"
  echo "  $0 --unreleased"
}

strip_v_prefix() {
  local value="$1"
  echo "${value#v}"
}

detect_previous_version() {
  local target_version="$1"
  local known_versions=""
  local prev=""
  local latest=""

  # Prefer semver ordering from existing tags.
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

get_repo_slug() {
  local repo_slug

  repo_slug=$(grep '^\[Unreleased\]:' CHANGELOG.md 2>/dev/null | sed -E 's|.*https://github.com/([^/]+/[^/]+)/.*|\1|' || true)
  if [[ -z "$repo_slug" || "$repo_slug" == "[Unreleased]:" ]]; then
    echo "dariuszparys/coralph"
    return
  fi

  echo "$repo_slug"
}

filter_housekeeping_commits() {
  local commits="$1"

  # Exclude changelog housekeeping commits from generated notes.
  echo "$commits" | grep -Ev "^- (chore|docs): update changelog for v?[0-9]+\.[0-9]+\.[0-9]+$" || true
}

categorize_commits() {
  local commits="$1"

  ADDED=$(echo "$commits" | grep -E "^- feat(\([^)]+\))?!?:" | sed -E 's/^- feat(\([^)]+\))?!?: /- /' || true)
  CHANGED=$(echo "$commits" | grep -E "^- (chore|refactor|perf|style)(\([^)]+\))?!?:" | sed -E 's/^- (chore|refactor|perf|style)(\([^)]+\))?!?: /- /' || true)
  FIXED=$(echo "$commits" | grep -E "^- fix(\([^)]+\))?!?:" | sed -E 's/^- fix(\([^)]+\))?!?: /- /' || true)
  OTHER=$(echo "$commits" | grep -Ev "^- (feat|fix|chore|refactor|perf|style|docs|test|ci|build)(\([^)]+\))?!?:" || true)
}

append_sections_to_file() {
  local output_file="$1"
  local include_fallback="$2"

  if [[ -n "$ADDED" ]]; then
    cat >> "$output_file" <<EOF_ADDED
### Added
$ADDED
EOF_ADDED
  fi

  if [[ -n "$CHANGED" ]]; then
    cat >> "$output_file" <<EOF_CHANGED
### Changed
$CHANGED
EOF_CHANGED
  fi

  if [[ -n "$FIXED" ]]; then
    cat >> "$output_file" <<EOF_FIXED
### Fixed
$FIXED
EOF_FIXED
  fi

  if [[ -n "$OTHER" ]]; then
    cat >> "$output_file" <<EOF_OTHER
### Other
$OTHER
EOF_OTHER
  fi

  if [[ "$include_fallback" == "true" && -z "$ADDED" && -z "$CHANGED" && -z "$FIXED" && -z "$OTHER" ]]; then
    cat >> "$output_file" <<EOF_FALLBACK
### Changed
- Maintenance and documentation updates.
EOF_FALLBACK
  fi
}

replace_unreleased_body() {
  local body_file="$1"
  local temp_file
  temp_file=$(mktemp)

  awk -v body_file="$body_file" '
    BEGIN { in_unreleased=0; found=0 }
    /^## \[Unreleased\]/ {
      print
      print ""
      while ((getline line < body_file) > 0) {
        print line
      }
      in_unreleased=1
      found=1
      next
    }
    {
      if (in_unreleased) {
        if ($0 ~ /^<a id="/ || $0 ~ /^## \[/ || $0 ~ /^\[/) {
          in_unreleased=0
        } else {
          next
        }
      }
      print
    }
    END {
      if (!found) {
        exit 2
      }
    }
  ' CHANGELOG.md > "$temp_file"

  mv "$temp_file" CHANGELOG.md
}

remove_existing_release_entry() {
  local version="$1"
  local anchor="$2"
  local temp_file
  temp_file=$(mktemp)

  awk -v version="$version" -v anchor="$anchor" '
    BEGIN { skip=0 }
    {
      if ($0 == "<a id=\"" anchor "\"></a>" || $0 ~ "^## \\[" version "\\] - ") {
        skip=1
        next
      }
      if (skip && ($0 ~ /^<a id="/ || $0 ~ /^## \[/ || $0 ~ /^\[/)) {
        skip=0
      }
      if (!skip) {
        print
      }
    }
  ' CHANGELOG.md > "$temp_file"

  mv "$temp_file" CHANGELOG.md
}

insert_release_entry_after_unreleased() {
  local entry_file="$1"
  local temp_file
  temp_file=$(mktemp)

  awk -v entry_file="$entry_file" '
    BEGIN { in_unreleased=0; inserted=0; found=0 }
    /^## \[Unreleased\]/ {
      found=1
      in_unreleased=1
      print
      next
    }
    {
      if (in_unreleased && !inserted && ($0 ~ /^<a id="/ || $0 ~ /^## \[/ || $0 ~ /^\[/)) {
        print ""
        while ((getline line < entry_file) > 0) {
          print line
        }
        inserted=1
        in_unreleased=0
      }
      print
    }
    END {
      if (!found) {
        exit 2
      }
      if (found && !inserted) {
        print ""
        while ((getline line < entry_file) > 0) {
          print line
        }
      }
    }
  ' CHANGELOG.md > "$temp_file"

  mv "$temp_file" CHANGELOG.md
}

update_reference_links() {
  local new_unreleased_link="$1"
  local new_version_link="${2:-}"
  local version="${3:-}"
  local temp_file
  temp_file=$(mktemp)

  awk -v new_unreleased="$new_unreleased_link" -v new_version="$new_version_link" -v version="$version" '
    BEGIN { in_links=0; inserted=0 }
    /^\[Unreleased\]:/ {
      print new_unreleased
      if (new_version != "") {
        print new_version
      }
      in_links=1
      inserted=1
      next
    }
    {
      if (in_links) {
        if ($0 ~ /^\[/) {
          if ($0 ~ /^\[Unreleased\]:/) {
            next
          }
          if (version != "" && $0 ~ ("^\\[" version "\\]:")) {
            next
          }
          print
          next
        }
        in_links=0
      }
      print
    }
    END {
      if (!inserted) {
        print new_unreleased
        if (new_version != "") {
          print new_version
        }
      }
    }
  ' CHANGELOG.md > "$temp_file"

  mv "$temp_file" CHANGELOG.md
}

release_targets_head() {
  local range_end="$1"
  local range_sha=""
  local head_sha=""

  range_sha=$(git rev-parse "$range_end" 2>/dev/null || true)
  head_sha=$(git rev-parse HEAD)

  [[ -n "$range_sha" && "$range_sha" == "$head_sha" ]]
}

if [[ -z "$VERSION_ARG" ]]; then
  usage
  exit 1
fi

if [[ "$VERSION_ARG" == "--unreleased" || "$VERSION_ARG" == "unreleased" ]]; then
  echo "Updating CHANGELOG.md unreleased section"

  LATEST_TAG=$(git describe --tags --abbrev=0 --match 'v[0-9]*' HEAD 2>/dev/null || true)
  if [[ -n "$LATEST_TAG" ]]; then
    COMMITS=$(git log "${LATEST_TAG}..HEAD" --pretty=format:"- %s" --no-merges)
  else
    COMMITS=$(git log --pretty=format:"- %s" --no-merges | head -20)
  fi

  COMMITS=$(filter_housekeeping_commits "$COMMITS")
  categorize_commits "$COMMITS"

  UNRELEASED_BODY_FILE=$(mktemp)
  : > "$UNRELEASED_BODY_FILE"
  append_sections_to_file "$UNRELEASED_BODY_FILE" "false"
  replace_unreleased_body "$UNRELEASED_BODY_FILE"
  rm -f "$UNRELEASED_BODY_FILE"

  REPO_SLUG=$(get_repo_slug)
  if [[ -n "$LATEST_TAG" ]]; then
    NEW_UNRELEASED_LINK="[Unreleased]: https://github.com/${REPO_SLUG}/compare/${LATEST_TAG}...HEAD"
  else
    EXISTING_UNRELEASED_LINK=$(grep '^\[Unreleased\]:' CHANGELOG.md 2>/dev/null || true)
    if [[ -n "$EXISTING_UNRELEASED_LINK" ]]; then
      NEW_UNRELEASED_LINK="$EXISTING_UNRELEASED_LINK"
    else
      NEW_UNRELEASED_LINK="[Unreleased]: https://github.com/${REPO_SLUG}/commits/main"
    fi
  fi

  update_reference_links "$NEW_UNRELEASED_LINK"

  echo "Updated CHANGELOG.md unreleased section"
  exit 0
fi

VERSION=$(strip_v_prefix "$VERSION_ARG")
PREV_VERSION=$(strip_v_prefix "$PREV_VERSION_ARG")

if [[ -z "$PREV_VERSION" ]]; then
  PREV_VERSION=$(detect_previous_version "$VERSION")
  if [[ -z "$PREV_VERSION" ]]; then
    echo "Warning: Could not detect previous version, assuming first release"
  fi
fi

echo "Updating CHANGELOG.md for version $VERSION (previous: $PREV_VERSION)"

ANCHOR="v${VERSION//./-}"

if [[ -n "$RANGE_END_OVERRIDE" ]]; then
  RANGE_END="$RANGE_END_OVERRIDE"
elif git rev-parse "v${VERSION}" >/dev/null 2>&1; then
  RANGE_END="v${VERSION}"
else
  RANGE_END="HEAD"
fi

if [[ "$RANGE_END" == "v${VERSION}" ]]; then
  DATE=$(git log -1 --format=%cs "$RANGE_END")
else
  DATE=$(date +%Y-%m-%d)
fi

if [[ -n "$PREV_VERSION" ]] && git rev-parse "v${PREV_VERSION}" >/dev/null 2>&1; then
  COMMITS=$(git log "v${PREV_VERSION}..${RANGE_END}" --pretty=format:"- %s" --no-merges)
else
  COMMITS=$(git log --pretty=format:"- %s" --no-merges | head -20)
fi

COMMITS=$(filter_housekeeping_commits "$COMMITS")
categorize_commits "$COMMITS"

ENTRY_FILE=$(mktemp)
cat > "$ENTRY_FILE" <<EOF_ENTRY
<a id="$ANCHOR"></a>
## [$VERSION] - $DATE
EOF_ENTRY
append_sections_to_file "$ENTRY_FILE" "true"

remove_existing_release_entry "$VERSION" "$ANCHOR"
insert_release_entry_after_unreleased "$ENTRY_FILE"

if release_targets_head "$RANGE_END"; then
  EMPTY_UNRELEASED_BODY_FILE=$(mktemp)
  : > "$EMPTY_UNRELEASED_BODY_FILE"
  replace_unreleased_body "$EMPTY_UNRELEASED_BODY_FILE"
  rm -f "$EMPTY_UNRELEASED_BODY_FILE"
fi

REPO_SLUG=$(get_repo_slug)
NEW_UNRELEASED_LINK="[Unreleased]: https://github.com/${REPO_SLUG}/compare/v${VERSION}...HEAD"
if [[ -n "$PREV_VERSION" ]]; then
  NEW_VERSION_LINK="[${VERSION}]: https://github.com/${REPO_SLUG}/compare/v${PREV_VERSION}...v${VERSION}"
else
  NEW_VERSION_LINK="[${VERSION}]: https://github.com/${REPO_SLUG}/releases/tag/v${VERSION}"
fi
update_reference_links "$NEW_UNRELEASED_LINK" "$NEW_VERSION_LINK" "$VERSION"

rm -f "$ENTRY_FILE"

echo "Updated CHANGELOG.md with version $VERSION"
echo "Entry preview:"
cat <<EOF_PREVIEW

<a id="$ANCHOR"></a>
## [$VERSION] - $DATE
EOF_PREVIEW
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
