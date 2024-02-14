#!/bin/bash


if [[ "$1" != "" ]]; then
    PROJECT="$1"
    if ! [[ -d "web-services/$PROJECT" ]]; then
        echo "Unable to find project $PROJECT"
        exit 1
    fi
else
    PROJECT="all"
fi

if [[ "$TOOL_DATA_DIR" == "" ]]; then
    DEST_DIR="/tmp/compilation" 
else
    DEST_DIR="$TOOL_DATA_DIR/public_html" 
fi
[[ -d "$DEST_DIR/cgi-bin" ]] || mkdir -p "$DEST_DIR/cgi-bin"

set -o nounset
set -o errexit
set -o pipefail

DOTNET_BUILD="dotnet publish --self-contained --runtime linux-x64"
if [[ "$PROJECT" == "all" ]]; then
    $DOTNET_BUILD web-services/web-services.sln
else
    $DOTNET_BUILD web-services/$PROJECT
fi

for file in "index.html" "favicon.ico" "favicon-32x32.png"; do
    echo "Gathering index page"
    cp "web-services/$file" "$DEST_DIR/"
done

for program in web-services/*; do
    if [[ -d "$program" ]]; then
        if [[ "$PROJECT" == "all" ]] || [[ "$program" =~ $PROJECT$ ]]; then
            echo "Gathering $program"
            cp -a "$program"/bin/Release/net8.0/linux-x64/publish/* "$DEST_DIR/cgi-bin/"
            cp "$program"/*.html "$DEST_DIR/cgi-bin/"
        fi
    fi
done

echo "Done compiling all the code, you can find the binaries and static code at $DEST_DIR"