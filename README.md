# wikibots
Wikipedia bots written by ruwiki user MBH.

Bots in "incubator" folder was written by ruwiki user Dmitry89 and redacted by MBH after his retirement.


## Deploying on toolforge

There's two steps here, once you pushed your changes to the repository in github, login to `login.toolforge.org`:

```shell
local> ssh login.toolforge.org
myuser@toolforge> become mbh
mbh@toolforge>
```

### Build all the binaries
```shell
mbh@toolforge> toolforge build start https://github.com/Saisengen/wikibots
... wait for it to build

mbh@toolforge> toolforge jobs run --image tool-mbh/tool-mbh:latest --command "recompile" --mount=all recompile
```

That will leave all the newly compiled binaries under `public_html/cgi-bin`.

### Build a single binaries
You can build only one of the binaries if you pass the name as an argument:

```shell
mbh@toolforge> toolforge build start https://github.com/Saisengen/wikibots
... wait for it to build

mbh@toolforge> toolforge jobs run --image tool-mbh/tool-mbh:latest --command "recompile" --mount=all recompile thanks-stats
```

### Starting the webservice
```shell
mbh@toolforge> toolforge webservice buildservice restart
```