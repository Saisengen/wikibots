#!/bin/bash
set -o nounset
set -o errexit
set -o pipefail
shopt -s nullglob

LAYER_DIR="/layers/heroku_php/wikibots"
PUBLIC_HTML="$LAYER_DIR/public_html"
PROJECTS=(
    web-services/*
    cluster-analysis/*
)


export_to_html_dir() {
    echo "#################################################"
    echo "## Export start ##"
    local dest_dir="${1?}"
    mkdir -p "$dest_dir/cgi-bin"

    # Static files
    for file in "index.html" "favicon.ico" "favicon-32x32.png"; do
        echo "Gathering index page"
        cp "web-services/$file" "$dest_dir/"
    done

    # Compiled files
    for project in "${PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            echo "## Gathering $project"
            cp -a "$project"/bin/Release/*/linux-x64/publish/* "$dest_dir/cgi-bin/"
            cp "$project"/*.html "$dest_dir/cgi-bin/"
        fi
    done

    echo "## Export end ##"
    echo "#################################################"
}


build_all() {
    echo "#################################################"
    echo "## Building start ##"
    local dotnet_build="dotnet publish --self-contained --runtime linux-x64"
    for solutions_file in $(find . -iname \*.sln); do
        echo "#################################################"
        echo "## Compiling $solutions_file ##"
        $dotnet_build "$solutions_file"
    done
    echo "## Building end ##"
    echo "#################################################"
}


populate_procfile() {
    # This adds an entry in the procfile for each of the binaries built
    # so they can be used in jobs
    for executable in $(find "$PUBLIC_HTML" -type f -executable -print); do
        # skip files with extensions
        if ! [[ "$executable" == *.* ]]; then
            echo "Creating Procfile entry point ${executable##*/}"
            echo "${executable##*/}: $executable" >> Procfile
        fi
    done
}


add_buildpack_layer_config() {
    cat >>"$LAYER_DIR.toml" <<EOL
[types]
launch = true
build = false
cache = false
EOL
}


main() {
    build_all
    export_to_html_dir "$PUBLIC_HTML"
    populate_procfile
    add_buildpack_layer_config
}


main "$@"
