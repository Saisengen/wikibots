#!/bin/bash

if [[ "$TOOL_DATA_DIR" == "" ]]; then
    echo "Starting dev environment"
    DEST_DIR="/tmp/compilation"
else
    DEST_DIR="$TOOL_DATA_DIR/public_html"
fi

cd "$DEST_DIR/cgi-bin" || exit
python \
    -m http.server \
    --directory="$DEST_DIR" \
    --cgi \
    "$@" \
    "${PORT:-8000}" 