#!/bin/bash

# Define your specific extension and commit ID
EXTENSION="png"
COMMIT_ID="1746d2e358722c57790dcac0c2949f158ac4792b"

# List files of the specific extension that were modified in the commit
FILES=$(git diff-tree --no-commit-id --name-only -r $COMMIT_ID | grep "\.$EXTENSION$")

for file in $FILES; do
  # Check out the previous version of the file
  git show $COMMIT_ID^:$file > "${file}_bump"
  # Rename the file by appending a suffix, here using "_bump". Adjust as needed.
  mv "${file}_bump" "$(dirname "$file")/$(basename "$file" .$EXTENSION)_bump.$EXTENSION"
done
