#!/bin/bash
set -e

# Check if the DEFINES environment variable is set
if [[ -z "$DEFINES" ]]; then
  echo "Error: The DEFINES environment variable is not set."
  exit 1
fi

# Convert the DEFINES environment variable into an array
IFS=', ' read -r -a defines <<< "$DEFINES"

# Temporary marker to identify added lines
marker="### TEMP DEFINES ###"

# Read the list of staged files from pre-commit
staged_files=("$@")

# Check if we are running on macOS or Linux
if [[ "$(uname)" == "Darwin" ]]; then
  SED_CMD="gsed"  # Use gsed on macOS
else
  SED_CMD="sed"   # Use sed on Linux
fi

# Process only staged files
echo "Processing staged files..."
for file in "${staged_files[@]}"; do
  # Skip non-C# files
  if [[ "$file" != *.cs ]]; then
    continue
  fi

  # Check if the file has a BOM (EF BB BF at the start of the file) using xxd
  bom_present=false
  bom_hex=$(xxd -p -l 3 "$file")
  if [[ "$bom_hex" == "efbbbf" ]]; then
    bom_present=true
  fi

  # If BOM is present, remove it temporarily
  if $bom_present; then
    # Remove BOM bytes (first 3 bytes) using tail and save content to temporary file
    tail -c +4 "$file" > "$file.tmp" && mv "$file.tmp" "$file"
  fi

  # Add defines only if marker is not already present
  if ! grep -q "$marker" "$file"; then
    # Add each define line separately using sed to insert at the top
    for define in "${defines[@]}"; do
      $SED_CMD -i "1i\\#define $define" "$file"
    done
    # Insert marker to identify where defines were added
    $SED_CMD -i "1i\\$marker" "$file"
  fi

  # If BOM was present, reinsert it at the beginning of the file
  if $bom_present; then
    # Reinsert BOM at the start of the file
    { echo -n -e '\xEF\xBB\xBF'; cat "$file"; } > "$file.tmp" && mv "$file.tmp" "$file"
  fi
done

# Run dotnet format
echo "Running dotnet format..."
dotnet format whitespace --folder --include "$@"

# Remove only the added defines
echo "Removing temporary defines..."
for file in "${staged_files[@]}"; do
  # Skip non-C# files
  if [[ "$file" != *.cs ]]; then
    continue
  fi

  # Check if the file has a BOM (EF BB BF at the start of the file) using xxd
  bom_present=false
  bom_hex=$(xxd -p -l 3 "$file")
  if [[ "$bom_hex" == "efbbbf" ]]; then
    bom_present=true
  fi

  # Use a temporary file to preserve BOM while modifying
  tmp_file="$file.tmp"
  # Remove the marker and added defines, ensuring the BOM stays intact
  $SED_CMD "/$marker/,+${#defines[@]}d" "$file" > "$tmp_file" && mv "$tmp_file" "$file"

  # If BOM was present, reinsert it at the beginning of the file
  if $bom_present; then
    # Reinsert BOM at the start of the file
    { echo -n -e '\xEF\xBB\xBF'; cat "$file"; } > "$file.tmp" && mv "$file.tmp" "$file"
  fi
done

echo "Pre-commit hook completed!"
