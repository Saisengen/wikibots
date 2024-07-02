#!/usr/bin/python
# -*- coding: utf-8 -*-

import re
import discord
from discord.ext import tasks
import requests
import logging
import configparser

config_bot = configparser.ConfigParser()
config_path = "configs/config-py.ini"
config_bot.read(config_path)

TOKEN = config_bot["MAIN"]["bot_token"]


# ID канала, ID эмодзи, ID целевого участника (бота), ID канала для команд.
CHANNEL = {"IDS": [1212498198200062014, 1219273496371396681],
           "EMOJI_IDS": [1209494470291226656, 1209494468169043988, 1209502325794668634],
           "BOTS": [1225008116048072754], "BOTCOMMANDS": 1212507148982947901}
USER_AGENT = {"User-Agent": "D-V; iluvatar@tools.wmflabs.org; python3.12; requests"}
Intents = discord.Intents.default()
Intents.members, Intents.message_content = True, True
discord.Intents.all()
allowed_mentions = discord.AllowedMentions(roles=True)
client = discord.Client(intents=Intents)


def flagged_check(url, title, rev_id):
    data = {"action": "query", "prop": "info|flagged", "titles": title, "format": "json", "utf8": 1}
    try:
        r3 = requests.post(url=url, data=data, headers=USER_AGENT).json()
    except Exception as e:
        print(e)
    else:
        if "query" in r3:
            if "pages" in r3["query"]:
                if len(r3["query"]['pages']) > 0:
                    page_id = list(r3["query"]['pages'].keys())[0]
                    if "flagged" in r3["query"]['pages'][page_id]:
                        if "stable_revid" in r3["query"]['pages'][page_id]['flagged']:
                            if r3["query"]['pages'][page_id]['flagged']['stable_revid'] >= int(rev_id):
                                return True
    return False


# Получение старых сообщений (задержка в минутах)
@tasks.loop(seconds=40.0)
async def get_messages():
    for ID in CHANNEL["IDS"]:
        try:
            channel = client.get_channel(ID)
        except Exception as e:
            print(e)
        else:
            try:
                messages = channel.history(limit=20, oldest_first=False)
                async for msg in messages:
                    if msg.author.id in CHANNEL["BOTS"] and len(msg.embeds) > 0 and\
                            ("oldid" in msg.embeds[0].url or "diff" in msg.embeds[0].url):
                        diff_url = msg.embeds[0].url
                        title = msg.embeds[0].title
                        if "oldid" in diff_url or "diff" in diff_url:
                            if "ru.wikipedia.org" in diff_url:
                                rev_id = diff_url.replace("https://ru.wikipedia.org/w/index.php?diff=", "") \
                                    if "diff" in diff_url else \
                                    diff_url.replace("https://ru.wikipedia.org/w/index.php?oldid=", "")
                            else:
                                rev_id = diff_url.replace("https://uk.wikipedia.org/w/index.php?diff=", "") \
                                    if "diff" in diff_url else \
                                    diff_url.replace("https://uk.wikipedia.org/w/index.php?oldid=", "")
                            url = re.search("(https://.*?.org)", diff_url).group(1)
                            data = {"action": "query", "prop": "revisions", "rvslots": "*", "rvprop": "tags|user",
                                    "revids": rev_id, "format": "json", "utf8": 1}
                            try:
                                r = requests.post(url=f"{url}/w/api.php", data=data, headers=USER_AGENT)
                                check = True if r.status_code == 404 else False
                                r = r.json()
                            except Exception as e:
                                print(e)
                            else:
                                if not check:
                                    if "badrevids" in r["query"]:
                                        check = True
                                    else:
                                        if "-1" in r["query"]["pages"] or "missing" in r["query"]["pages"]:
                                            check = True
                                        else:
                                            page_id = list(r["query"]["pages"].keys())[0]
                                            if "revisions" not in r["query"]["pages"][page_id]:
                                                check = True
                                            else:
                                                if "-1" in r["query"]["pages"][page_id]["revisions"] or \
                                                        len(r["query"]["pages"][page_id]["revisions"]) < 1:
                                                    check = True
                                                else:
                                                    if "mw-reverted" in \
                                                            r["query"]["pages"][page_id]["revisions"][0]["tags"]:
                                                        check = True
                                                    else:
                                                        data = {"action": "query", "prop": "revisions",
                                                                "rvslots": "*", "rvprop": "tags", "rvlimit": 500,
                                                                "rvendid": rev_id,
                                                                "titles": title, "format": "json", "utf8": 1}
                                                        if "user" in r["query"]["pages"][page_id]["revisions"][0]:
                                                            data["rvexcludeuser"] = \
                                                            r["query"]["pages"][page_id]["revisions"][0]["user"]
                                                        try:
                                                            r2 = requests.post(url=f"{url}/w/api.php", data=data,
                                                                               headers=USER_AGENT).json()
                                                            page_id = list(r2["query"]["pages"].keys())[0]
                                                        except Exception as e:
                                                            print(e)
                                                        else:
                                                            if "revisions" in r2["query"]["pages"][page_id]:
                                                                if "mw-rollback" in \
                                                                        r2["query"]["pages"][page_id]["revisions"][-1][
                                                                            "tags"]:
                                                                    check = True
                                                                else:
                                                                    check = flagged_check(f"{url}/w/api.php", title,
                                                                                          rev_id)
                                                            else:
                                                                check = flagged_check(f"{url}/w/api.php", title, rev_id)

                                if check:
                                    try:
                                        await msg.delete()
                                    except Exception as e:
                                        print(e)

            except Exception as e:
                print(e)


@client.event
async def on_ready():
    try:
        if not get_messages.is_running():
            await get_messages.start()
    except Exception as e:
        print(f'Error during get messages from Discord: {e}.')

client.run(token=TOKEN, reconnect=True, log_level=logging.WARN)
