# wikibots
Wikipedia bots written by ruwiki user MBH.

Ruwiki's article incubator bots was initially written by user Dmitry89 and completely rewritten by MBH after his retirement. Category Pathfinder was originally written by MBH and rewritten by Adamant.pwn (its interface is written by Serhio Magpie). All in "rv" folder are written by users [Iluvatar](https://github.com/SiarheiGribov) and [Well very well](https://github.com/LeviPesin).

## Rebuilding webservices on Toolforge
```shell
toolforge build start https://github.com/Saisengen/wikibots
toolforge webservice buildservice restart
```

## Setting up credentials
```shell
cat p | toolforge envvars add CREDS
```
