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
    for file in web-services/*html web-services/*ico; do
        echo "Gathering static file $file"
        cp "$file" "$dest_dir/"
    done

    # Compiled files
    for project in "${PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            if [[ -d "$project/bin/Release" ]]; then
                echo "## Gathering $project"
                cp -a "$project"/bin/Release/*/linux-x64/publish/* "$dest_dir/cgi-bin/"
                cp "$project"/*.html "$dest_dir/cgi-bin/"
            else
                echo "Unable to find release for project $project, did you forget to add it to the solutions file?"
                echo "   dotnet sln add $project"
            fi
        fi
    done

    echo "## Export end ##"
    echo "#################################################"
}


build_all() {
    echo "#################################################"
    echo "## Building start ##"
    local dotnet_build="dotnet publish --self-contained --runtime linux-x64"
    echo "#################################################"
    echo "## Compiling all.sln ##"
    $dotnet_build all.sln
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
