#!/bin/bash

DEST_DIR="/layers/heroku_php/wikibots/public_html"

[[ -d "$DEST_DIR/cgi-bin" ]] || {
    echo "Unable to find cgi-bin directory at $DEST_DIR, something went wrong."
    exit 1
}

exec 1>>$TOOL_DATA_DIR/access.log
exec 2>>$TOOL_DATA_DIR/error.log

python \
    -m http.server \
    --directory="$DEST_DIR" \
    --cgi \
    "$@" \
    "${PORT:-8000}"

