# wikibots
Wikipedia bots written by ruwiki user MBH.

Bots in "incubator" folder was written by user Dmitry89 and edited by MBH after his retirement.

All in "rv" folder are written by user Iluvatar.

## Adding a new project to a solutions file
From your development environment (with dotnet 8 installed https://dotnet.microsoft.com/en-us/download/dotnet):
```shell
dotnet sln add  path/to/the/<project-name>.csproj
```

## Deploying on toolforge
There's three steps here:

### Push your changes to the repository
```shell
git commit -m 'awesome improvements'
git push origin main
```

### Build the new image

```shell
local> ssh login.toolforge.org
myuser@toolforge> become mbh
mbh@toolforge> toolforge build start https://github.com/Saisengen/wikibots
... wait for it to build
```

### Restart the webservice
```shell
mbh@toolforge> toolforge webservice buildservice restart
```

## Setting up credentials
You can set the credentials by creating an environment variable in toolforge like:
```shell
cat p | toolforge envvars add WIKIBOT_CREDENTIALS
```
