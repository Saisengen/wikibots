#!/usr/bin/python
# -*- coding: utf-8 -*-
import asyncio
import time
import json
import discord
from discord.ext import commands
from discord.ui import Button, View
import requests
import logging
import pymysql
import toolforge
import configparser

DEBUG = {"enable": False, "ID": 1237345748778221649, "SQL": {"user": "s55857", "pass": "", "port": 4711}}

config_bot = configparser.ConfigParser()
config_path = "config-py.ini" if DEBUG["enable"] is True else "configs/config-py.ini"
config_bot.read(config_path)

TOKEN = config_bot["MAIN"]["bot_token"]
BEARER_TOKEN = config_bot["MAIN"]["bearer_token"]
DEBUG["SQL"]["pass"] = config_bot["MAIN"]["DB_pass"]


# Целевой сервер, ID каналов с фидами, ID бота, ID ботов-источников, ID канала с командами,
# ID сообщения со списком откатывающих, ID канала с источником, список админов для команд.
CONFIG = {"SERVER": [1044474820089368666], "IDS": [1219273496371396681, 1212498198200062014], "BOT": 1225008116048072754,
          "SOURCE_BOTS": [1237362558046830662], "BOTCOMMANDS": 1212507148982947901,
          "ROLLBACKERS": 1237790591044292680, "SOURCE": 1237345566950948867,
          "ADMINS": [352826965494988822, 512545053223419924, 223219998745821194]}
if DEBUG["enable"]:
    CONFIG["IDS"].append(DEBUG["ID"])
USER_AGENT = {"User-Agent": "D-V; iluvatar@tools.wmflabs.org; python3.12; requests"}
Intents = discord.Intents.default()
Intents.members, Intents.message_content = True, True
discord.Intents.all()
allowed_mentions = discord.AllowedMentions(roles=True)
client = commands.Bot(intents=Intents, command_prefix="/")

select_options = {
    "1": ['Неконструктивная правка', 'очевидно ошибочная правка', 'акт [[Вікіпедія:Вандалізм|вандалізму]]'],
    "2": ['Нет АИ', 'добавление сомнительного содержимого [[ВП:ПРОВ|без источников]] или [[ВП:ОРИСС|оригинального исследования]]', 'додавання [[ВП:ОД|оригінального дослідження]] або сумнівної інформації [[ВП:В|без джерел]]'],
    "3": ['Порча вики-разметки', 'порча [[ВП:Викиразметка|викиразметки]] статьи', 'псування [[Вікірозмітка|вікірозмітки]] статті'],
    "4": ['Спам', 'добавление [[ВП:ВС|ненужных / излишних ссылок]] или спам', 'додавання [[ВП:УНИКАТИПОС|непотрібних / зайвих посилань]] або спам'],
    "5": ['Незначимый факт', 'отсутствует [[ВП:Значимость факта|энциклопедическая значимость]] факта', 'відсутня [[ВП:ЗВ|значущість]] факту'],
    "6": ['Переименование без КПМ', 'попытка переименования объекта по тексту без [[ВП:ПЕРЕ|переименования страницы]] или иное сомнит. переименование. Воспользуйтесь [[ВП:КПМ|специальной процедурой]].', 'перейменування по тексту без перейменування сторінки.'],
    "7": ['Тестовая правка', 'экспериментируйте в [[ВП:Песочница|песочнице]]', 'експерементуйте в [[Вікіпедія:Пісочниця|пісочниці]]'],
    "8": ['Удаление содержимого', 'необъяснённое удаление содержимого страницы', 'видалення вмісту сторінки'],
    "9": ['Орфография, пунктуация', 'добавление орфографических или пунктуационных ошибок', 'додавання орфографічних або пунктуаційних помилок'],
    "10": ['Не на языке проекта', 'добавление содержимого не на русском языке', 'додавання вмісту не українською мовою'],
    "11": ['Удаление шаблонов', 'попытка необоснованного удаления служебных или номинационных шаблонов', 'спроба необґрунтованого видалення службових або номінаційних шаблонів'],
    "12": ['Личное мнение', '[[ВП:НЕФОРУМ|изложение личного мнения]] об объекте статьи. Википедия не является [[ВП:НЕФОРУМ|форумом]] или [[ВП:НЕТРИБУНА|трибуной]]', 'виклад особистої думки про об\'єкт статті. [[ВП:НЕТРИБУНА|Вікіпедія — не трибуна]]'],
    "13": ['Комментарии в статье', 'добавление комментариев в статью. Комментарии и пометки оставляйте на [[Talk:$7|странице обсуждения]] статьи', 'додавання коментарів до статті. Коментарі та позначки залишайте на [[Сторінка обговорення:$1|сторінці обговорення]] статті'],
    "14": ['своя причина', '', ''],
    "15": ['Закрыть', '', '']
}
options = []
for option in select_options:
    options.append(discord.SelectOption(label=select_options[option][0], value=str(option)))
select_component = discord.ui.Select(placeholder="Выбор причины отмены", min_values=1, max_values=1, options=options, custom_id="sel1")
undo_prefix = ["отмена правки [[Special:Contribs/$author|$author]] по запросу [[u:$actor|$actor]]:", "скасовано останнє редагування [[Special:Contribs/$author|$author]] за запитом [[User:$actor|$actor]]:"]


class Reason(discord.ui.Modal, title='Причина'):
    res = discord.ui.TextInput(custom_id="edt1", label="Причина отмены", min_length=2, max_length=255, placeholder="введите причину", required=True, style=discord.TextStyle.short)

    async def on_submit(self, interaction: discord.Interaction):
        pass


def get_trigger(embed: discord.Embed) -> str:
    color = str(embed.color)
    if color == "#ff0000":
        return "patterns"
    elif color == "#ffff00":
        return "LW"
    elif color == "#ff00ff":
        return "ORES"
    elif color == "#00ff00":
        return "tags"
    else:
        return "unknown"


def send_to_db(actor: str, action_type: str, trigger: str):
    try:
        if DEBUG["enable"]:
            conn = pymysql.connections.Connection(user=DEBUG["SQL"]["user"],port=DEBUG["SQL"]["port"],password=DEBUG["SQL"]["pass"],database="s55857__rv",host='127.0.0.1')
        else:
            conn = toolforge.toolsdb("s55857__rv")
        with conn.cursor() as cur:
            cur.execute(f"SELECT name FROM ds_antivandal WHERE name=%s;", actor)
            res = cur.fetchall()
            if action_type in ["rollbacks", "undos", "approves"]:
                if len(res) == 0:
                    cur.execute(f"INSERT INTO ds_antivandal (name, {action_type}, {trigger}) VALUES (%s, 1, 1);", actor)
                else:
                    cur.execute(f"UPDATE ds_antivandal SET {action_type} = {action_type}+1, {trigger} = {trigger}+1 WHERE name = %s;", actor)
            conn.commit()
            if action_type == "approves":
                cur.execute(f"UPDATE ds_antivandal_false SET {trigger} = {trigger}+1 WHERE result = 'stats';")
                conn.commit()
        conn.close()
    except Exception as e:
        print(f"send_to_db error 1: {e}")
        return False


def get_from_db(is_all: bool = True, actor: str = None):
    try:
        if DEBUG["enable"]:
            conn = pymysql.connections.Connection(user=DEBUG["SQL"]["user"],port=DEBUG["SQL"]["port"],password=DEBUG["SQL"]["pass"],database="s55857__rv",host='127.0.0.1')
        else:
            conn = toolforge.toolsdb("s55857__rv")
        with conn.cursor() as cur:
            i_res = False
            triggers_false = False
            if is_all:
                cur.execute(f"SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES), SUM(tags) FROM ds_antivandal")
                r = cur.fetchall()
                cur.execute("SELECT name, SUM(rollbacks) + SUM(undos) + SUM(approves) AS am FROM ds_antivandal GROUP BY name ORDER BY am DESC LIMIT 5;")
                r2 = cur.fetchall()
                i_res = []
                for i in r2:
                    i_res.append(f"{i[0]}: {i[1]}")
                i_res = "\n".join(i_res)
                cur.execute("SELECT patterns, LW, ORES, tags FROM ds_antivandal_false WHERE result = 'stats';")
                r3 = cur.fetchall()
                triggers_false = f"Ложные триггеры: паттерны — {r3[0][0]}, LW — {r3[0][1]}, ORES — {r3[0][2]}, теги — {r3[0][3]}."
            else:
                cur.execute(f"SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES), SUM(tags) FROM ds_antivandal WHERE name=%s;", actor)
                r = cur.fetchall()
            conn.close()
            if len(r) > 0:
                return {"rollbacks": r[0][0], "undos": r[0][1], "approves": r[0][2], "total": i_res, "patterns": r[0][3],
                        "LW": r[0][4], "ORES": r[0][5], "tags": r[0][6], "triggers": triggers_false}
            else:
                return {"rollbacks": 0, "undos": 0, "approves": 0, "patterns": 0, "LW": 0, "ORES": 0, "tags": 0}
    except Exception as e:
        print(f"get_from_db error 1: {e}")
        return False


def delete_from_db(actor: str):
    try:
        if DEBUG["enable"]:
            conn = pymysql.connections.Connection(user=DEBUG["SQL"]["user"], port=DEBUG["SQL"]["port"], password=DEBUG["SQL"]["pass"], database="s55857__rv", host='127.0.0.1')
        else:
            conn = toolforge.toolsdb("s55857__rv")
        with conn.cursor() as cur:
            cur.execute(f"DELETE FROM ds_antivandal WHERE name='{actor}';")
            conn.commit()
        conn.close()
    except Exception as e:
        print(f"delete_from_db error 1: {e}")
        return False


@client.tree.context_menu(name="Поприветствовать")
async def welcome_user(inter: discord.Interaction, message: discord.Message):
    if inter.user.id in CONFIG["ADMINS"]:
        try:
            await inter.response.defer()
            await inter.followup.send(content=f"Приветствуем, <@{message.author.id}>! Если вы желаете получить доступ к остальным каналам "
                                              f"сервера, сообщите, пожалуйста, имя вашей учётной записи в проектах Викимедиа.")
        except Exception as e:
            print(f"welcome_user error 1: {e}")
    else:
        try:
            await inter.response.defer(ephemeral=True)
            await inter.followup.send(content=f"К сожалению, у вас нет разрешения на выполнение данной команды.")
        except Exception as e:
            print(f"welcome_user error 2: {e}")


@client.tree.command(name="rollback_help")
async def rollback_help(inter: discord.Interaction):
    """Список команд бота.
     """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"rollback_help 1: {e}")
    else:
        try:
            await inter.followup.send(content=f"/rollback_help — список команд бота.\n"
                                              f"/rollback_clear — очистка фид-каналов от всех сообщений бота.\n"
                                              f"/rollbackers — список участников, кому разрешены действия через бот.\n"
                                              f"/add_rollbacker — добавить участника в список тех, кому разрешены действия через бот.\n"
                                              f"/remove_rollbacker — удалить участника из списка тех, кому разрешены действия через бот.\n"
                                              f"/rollback_stats_all — статистика откатов через бот.\n"
                                              f"/rollback_stats — статистика действий участника через бот.\n"
                                              f"/rollback_stats_delete — удалить всю статистику действий участника через бот.\n"
                                              f"По вопросам работы бота обращайтесь к <@352826965494988822>.", ephemeral=True)
        except Exception as e:
            print(f"rollback_help 2: {e}")


@client.tree.command(name="rollback_stats_all")
async def rollback_stats_all(inter: discord.Interaction):
    """Просмотреть статистику откатов и отмен через бот.
     """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"rollback_stats_all 1: {e}")
    else:
        r = get_from_db(is_all=True)
        if r and len(r):
            try:
                await inter.followup.send(content=f"Через бот совершено: откатов — {r['rollbacks']}, отмен — {r['undos']}, "
                                                  f"одобрений ревизий — {r['approves']}.\n"
                                                  f"Наибольшее количество действий совершили:\n{r['total']}\n"
                                                  f"Действий по типам причин: паттерны — {r['patterns']}, ORES — {r['ORES']}, "
                                                  f"LW — {r['LW']}, метки — {r['tags']}.\n"
                                                  f"{r['triggers']}", ephemeral=True)
            except Exception as e:
                print(f"rollback_stats_all 2: {e}")


async def rollback_stats(inter: discord.Interaction, wiki_name: str):
    """Просмотреть статистику откатов и отмен через бот.

    Parameters
    -----------
    wiki_name: str
        Имя участника в вики
     """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"rollback_stats 1: {e}")
    else:
        r = get_from_db(is_all=False, actor=wiki_name)
        if r and len(r):
            try:
                if r["rollbacks"] is None:
                    await inter.followup.send(
                        content=f"Данный участник не совершал действий через бот.", ephemeral=True)
                else:
                    await inter.followup.send(content=f"Через бот участник {wiki_name} совершил действий: {r['rollbacks']+r['undos']+r['approves']},\n"
                                                      f"из них: откатов — {r['rollbacks']}, отмен — {r['undos']}, "
                                                      f"одобрений ревизий — {r['approves']}.\n"
                                                      f"Действий по типам причин: паттерны — {r['patterns']}, ORES — {r['ORES']},"
                                                      f" LW — {r['LW']}, метки — {r['tags']}.", ephemeral=True)
            except Exception as e:
                print(f"rollback_stats 2: {e}")


# noinspection PyUnresolvedReferences
@client.tree.command(name="rollback_stats_delete")
async def rollback_stats_delete(inter: discord.Interaction, wiki_name: str):
    """Удалить статистку откатов и отмен конкретного участника через бот.

    Parameters
    -----------
    wiki_name: str
        Имя участника в вики
     """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"rollback_stats_delete 1: {e}")
    else:
        if inter.user.id in CONFIG["ADMINS"]:
            delete_from_db(wiki_name)
            try:
                await inter.followup.send(
                    content=f"Статистика участника удалена, убедитесь в этом через соответствующую команду.",
                    ephemeral=True)
            except Exception as e:
                print(f"rollback_stats_delete 3: {e}")
        else:
            try:
                await inter.followup.send(content=f"К сожалению, у вас нет разрешения "
                                                  f"на выполнение данной комманды. Обратитесь к участнику <@{223219998745821194}> или <@{352826965494988822}>.",
                                          ephemeral=True)
            except Exception as e:
                print(f"rollback_stats_delete 4: {e}")


@client.tree.command(name="last_metro")
async def last_metro(inter: discord.Interaction):
    """Узнать время последнего запуска бота #metro.
     """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"Metro error 1: {e}")
    else:
        try:
            metro = requests.get(url="https://iluvatarbot.toolforge.org/metro/", headers=USER_AGENT).text.split("<br>")[
                0].replace("Задание запущено", "Последний запуск задания:")
            await inter.followup.send(content=metro, ephemeral=True)
        except Exception as e:
            print(f"Metro error 2: {e}")


@client.tree.command(name="rollback_clear")
async def rollback_clear(inter: discord.Interaction):
    """Очистка каналов с фидами от сообщений бота.
     """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"Clear feed error 1: {e}")
    else:
        if inter.user.id in CONFIG["ADMINS"]:
            try:
                await inter.followup.send(content=f"Очистка каналов начата.", ephemeral=True)
            except Exception as e:
                print(f"Clear feed error 2: {e}")
            for ID in CONFIG["IDS"]:
                channel = client.get_channel(ID)
                messages = channel.history(limit=100000)
                async for msg in messages:
                    if msg.author.id == CONFIG["BOT"]:
                        try:
                            await msg.delete()
                        except Exception as e:
                            print(f"Clear feed error 3: {e}")
                        time.sleep(1.0)
        else:
            try:
                await inter.followup.send(content=f"К сожалению, у вас нет разрешения "
                                                  f"на выполнение данной комманды. "
                                                  f"Обратитесь к участнику <@{223219998745821194}>.", ephemeral=True)
            except Exception as e:
                print(f"Clear feed error 4: {e}")


@client.tree.command(name="rollbackers")
async def rollbackers(inter: discord.Interaction):
    """Просмотра списка участников, кому разрешён откат и отмена через бот.
     """
    try:
        await inter.response.defer(ephemeral=True)
        msg_rights = await client.get_channel(CONFIG["BOTCOMMANDS"]).fetch_message(CONFIG["ROLLBACKERS"])
        rights_content = json.loads(msg_rights.content.replace("`", "")).values()
    except Exception as e:
        print(f"Rollbackers list error 1: {e}")
    else:
        try:
            await inter.followup.send(content=f"Откаты и отмены через бота разрешены участникам `{', '.join(rights_content)}`.\nДля запроса права или отказа от него обратитесь к участнику <@{223219998745821194}>.", ephemeral=True)
        except Exception as e:
            print(f"Rollbackers list error 2: {e}")


@client.tree.command(name="add_rollbacker")
async def add_rollbacker(inter: discord.Interaction, discord_name: discord.User, wiki_name: str):
    """Добавление участника в список тех, кому разрешён откат и отмена ботом.

    Parameters
    -----------
    discord_name: discord.User
        Участник Discord
    wiki_name: str
        Имя участника в вики
    """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"Add rollbacker error 1: {e}")
    if inter.user.id in CONFIG["ADMINS"]:
        if "@" not in wiki_name:
            try:
                msg_rights = await client.get_channel(CONFIG["BOTCOMMANDS"]).fetch_message(CONFIG["ROLLBACKERS"])
                rights_content = json.loads(msg_rights.content.replace("`", ""))
            except Exception as e:
                print(f"Add rollbacker error 2: {e}")
            else:
                if str(discord_name.id) not in rights_content:
                    rights_content[str(discord_name.id)] = wiki_name
                    try:
                        await msg_rights.edit(content=json.dumps(rights_content))
                        await inter.followup.send(content=f"Участник {wiki_name} добавлен в список откатывающих.", ephemeral=True)
                    except Exception as e:
                        print(f"Add rollbacker error 3: {e}")
                else:
                    try:
                        await inter.followup.send(content=f"Участник {wiki_name} уже присутствует в списке откатывающих.", ephemeral=True)
                    except Exception as e:
                        print(f"Add rollbacker error 4: {e}")

    else:
        try:
            await inter.followup.send(content=f"К сожалению, у вас нет разрешения на выполнение данной команды. Обратитесь к участнику <@{223219998745821194}>.", ephemeral=True)
        except Exception as e:
            print(f"Add rollbacker error 4: {e}")


@client.tree.command(name="remove_rollbacker")
async def remove_rollbacker(inter: discord.Interaction, wiki_name: str):
    """Удаление участника из списка тех, кому разрешён откат и отмена ботом.

    Parameters
    -----------
    wiki_name: str
        Имя участника в вики
    """
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f"Remove rollbacker error 1: {e}")
    if inter.user.id in CONFIG["ADMINS"]:
        try:
            msg_rights = await client.get_channel(CONFIG["BOTCOMMANDS"]).fetch_message(CONFIG["ROLLBACKERS"])
            rights_content = json.loads(msg_rights.content.replace("`", ""))
        except Exception as e:
            print(f"Remove rollbacker error 2: {e}")
        else:
            right_copy = rights_content.copy()
            for k in right_copy:
                if rights_content[k] == wiki_name:
                    del rights_content[k]
            if right_copy != rights_content:
                try:
                    await msg_rights.edit(content=json.dumps(rights_content))
                except Exception as e:
                    print(f"Remove rollbacker error 3: {e}")
                else:
                    await inter.followup.send(content=f"Участник {wiki_name} убран из списка откатывающих.", ephemeral=True)
            else:
                try:
                    await inter.followup.send(content=f"Участника {wiki_name} не было в списке откатывающих.", ephemeral=True)
                except Exception as e:
                    print(f"Remove rollbacker error 4: {e}")

    else:
        try:
            await inter.followup.send(content=f"К сожалению, у вас нет разрешения на выполнение данной команды. Обратитесь к участнику <@{223219998745821194}>.", ephemeral=True)
        except Exception as e:
            print(f"Remove rollbacker error 4: {e}")


def do_rollback(embed, actor, action_type="rollback", reason=""):
    diff_url = embed.url
    title = embed.title
    lang = "ru" if "ru.wikipedia.org" in diff_url else "uk"
    rev_id = diff_url.replace(f"https://{lang}.wikipedia.org/w/index.php?diff=", "") if "diff" in diff_url else diff_url.replace(f"https://{lang}.wikipedia.org/w/index.php?oldid=", "")
    data = {"action": "query", "prop": "revisions", "rvslots": "*", "rvprop": "ids|tags", "rvlimit": 500,
            "rvendid": rev_id, "rvexcludeuser": embed.author.name, "titles": title, "format": "json", "utf8": 1}
    try:
        r = requests.post(url=f"https://{lang}.wikipedia.org/w/api.php", data=data,
                          headers=USER_AGENT)
        if r.status_code == 404:
            return ["Такой страницы уже не существует.", f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"]
        r = r.json()
        page_id = list(r["query"]["pages"].keys())[0]
    except Exception as e:
        print(f"rollback error 1: {e}")
    else:
        if "-1" in r["query"]["pages"]:
            return ["Такой страницы уже не существует", f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"]
        if "revisions" in r["query"]["pages"][page_id] and "mw-rollback" in \
                r["query"]["pages"][page_id]["revisions"][-1]["tags"]:
            return ["Правки уже были откачены.", f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"]
        if "revisions" in r["query"]["pages"][page_id] and len(r["query"]["pages"][page_id]["revisions"]) > 0:
            return ["Правки данного пользователя не являются последними, действие невозможно", ""]
        data = {"action": "query", "prop": "revisions", "rvslots": "*", "rvprop": "ids|timestamp", "rvlimit": 500,
                "rvendid": rev_id, "rvuser": embed.author.name, "titles": title, "format": "json", "utf8": 1}
        try:
            r = requests.post(url=f"https://{lang}.wikipedia.org/w/api.php", data=data,
                              headers=USER_AGENT).json()
        except Exception as e:
            print(f"rollback error 2: {e}")
        else:
            if "-1" in r["query"]["pages"]:
                return ["Такой страницы уже не существует.", f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"]
            if "revisions" in r["query"]["pages"][page_id] and len(r["query"]["pages"][page_id]["revisions"]) > 0:
                rev_id = r["query"]["pages"][page_id]["revisions"][0]["revid"]
            api_url = f"https://{lang}.wikipedia.org/w/api.php"
            headers = {"Authorization": f"Bearer {BEARER_TOKEN}", "User-Agent": "Reimu; iluvatar@tools.wmflabs.org"}

            if action_type == "rollback":
                comment_body_uk = f"відкинуто редагування [[Special:Contribs/$2|$2]] за запитом [[User:{actor}|{actor}]]"
                comment_body_ru = f"откат правок [[Special:Contribs/$2|$2]] по запросу [[u:{actor}|{actor}]]"
                comment = comment_body_ru if lang == "ru" else comment_body_uk
                try:
                    rollback_token = \
                        requests.get(f'{api_url}?format=json&action=query&meta=tokens&type=rollback', headers=headers).json()["query"]["tokens"]["rollbacktoken"]
                except Exception as e:
                    print(f"rollback error 3: {e}")
                else:
                    data = {"action": "rollback", "format": "json", "title": title, "user": embed.author.name, "utf8": 1, "watchlist": "nochange", "summary": comment, "token": rollback_token}
                    try:
                        r = requests.post(url=f"https://{lang}.wikipedia.org/w/api.php", data=data, headers=headers).json()
                    except Exception as e:
                        print(f"rollback error 4: {e}")
                    else:
                        return [r["error"]["info"],
                                f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"] if "error" in r else [
                            "Success",
                            f"[{title}](<https://{lang}.wikipedia.org/w/index.php?diff={r['rollback']['revid']}>)"]

            else:
                data = {"action": "query", "prop": "revisions", "rvslots": "*", "rvprop": "ids|user", "rvlimit": 1,
                        "rvstartid": rev_id, "rvexcludeuser": embed.author.name, "titles": title, "format": "json", "utf8": 1}
                try:
                    r = requests.post(url=f"https://{lang}.wikipedia.org/w/api.php", data=data, headers=USER_AGENT).json()
                    check_revs = len(r["query"]["pages"][page_id]["revisions"])
                    if check_revs > 0:
                        parent_id = r["query"]["pages"][page_id]["revisions"][0]["revid"]
                        last_author = r["query"]["pages"][page_id]["revisions"][0]["user"]
                except Exception as e:
                    print(f"rollback error 5: {e}")
                else:
                    if check_revs == 0:
                        return ["Все версии принадлежат одному участнику", f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"]
                    try:
                        edit_token = requests.get(f'{api_url}?format=json&action=query&meta=tokens&type=csrf', headers=headers).json()["query"]["tokens"]["csrftoken"]
                    except Exception as e:
                        print(f"rollback error 6: {e}")
                    else:
                        reason = reason.replace("$author", embed.author.name).replace("$lastauthor", last_author)
                        data = {"action": "edit", "format": "json", "title": title, "undo": rev_id, "undoafter": parent_id,
                                "watchlist": "nochange", "nocreate": 1, "summary": reason, "token": edit_token, "utf8": 1}
                        try:
                            r = requests.post(url=f"https://{lang}.wikipedia.org/w/api.php", data=data, headers=headers).json()
                        except Exception as e:
                            print(f"rollback error 7: {e}")
                        else:
                            return [r["error"]["info"], f"[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(' ', '_')}>) (ID: {rev_id})"] if "error" in r else ["Success",
                                f"[{title}](<https://{lang}.wikipedia.org/w/index.php?diff={r['edit']['newrevid']}>)"]


def get_view(embed: discord.Embed, disable: bool = False) -> View:
    btn1 = Button(emoji="⏮️", label="", style=discord.ButtonStyle.danger, custom_id="btn1", disabled=disable)
    btn2 = Button(emoji="👍🏻", label="", style=discord.ButtonStyle.green, custom_id="btn2", disabled=disable)
    btn3 = Button(emoji="↪️", label="", style=discord.ButtonStyle.blurple, custom_id="btn3", disabled=disable)
    btn4 = Button(emoji="💩", label="", style=discord.ButtonStyle.green, custom_id="btn4", disabled=disable)
    view = View()
    view.add_item(btn1)
    view.add_item(btn3)
    view.add_item(btn2)
    view.add_item(btn4)
    return view


@client.event
async def on_interaction(inter):
    if "custom_id" not in inter.data:
        return
    if inter.data["custom_id"] != "sel1":
        try:
            await inter.response.defer()
        except Exception as e:
            print(f"On_Interaction error 0.1: {e}")
    else:
        if inter.data["values"][0] != "14":
            try:
                await inter.response.defer()
            except Exception as e:
                print(f"On_Interaction error 0.2: {e}")
    if inter.channel.id in CONFIG["IDS"]:
        try:
            msg_rights = await client.get_channel(CONFIG["BOTCOMMANDS"]).fetch_message(CONFIG["ROLLBACKERS"])
            msg_rights = json.loads(msg_rights.content.replace("`", ""))
        except Exception as e:
            print(f"On_Interaction error 1: {e}")
        else:
            if str(inter.user.id) not in msg_rights:
                try:
                    await inter.followup.send(content=f"К сожалению, у вас нет разрешение на выполнение откатов и отмен через бот. Обратитесь к участнику <@{223219998745821194}>.", ephemeral=True)
                except Exception as e:
                    print(f"On_Interaction error 2: {e}")
                finally:
                    return
            actor = msg_rights[str(inter.user.id)]
            msg = inter.message
            channel = client.get_channel(CONFIG["SOURCE"])

            v = False
            if "components" in inter.data and len(inter.data["components"]) > 0 and "components" in \
                    inter.data["components"][0] and len(inter.data["components"][0]["components"]) > 0 and \
                    inter.data["components"][0]["components"][0]["custom_id"] == "edt1":
                v = inter.data["components"][0]["components"][0]["value"]
            base_view = get_view(msg.embeds[0])
            if inter.data["custom_id"] == "sel1" or v is not False:
                lang_selector = 1
                if "uk.wikipedia.org" in msg.embeds[0].url:
                    lang_selector = 2

                if inter.data["custom_id"] == "sel1":
                    selected = inter.data["values"][0]
                    reason = f"{undo_prefix[lang_selector-1].replace('$actor', actor)} {select_options[selected][lang_selector].replace('$1', msg.embeds[0].title)}"
                    try:
                        await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                    except Exception as e:
                        print(f"On_Interaction TextEdit error 1: {e}")
                    if selected == "14":
                        try:
                            await inter.response.send_modal(Reason())
                        except Exception as e:
                            print(f"On_Interaction TextEdit error 2: {e}")
                        return
                    if selected == "15":
                        try:
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                        except Exception as e:
                            print(f"On_Interaction TextEdit error 3: {e}")
                        return
                else:
                    reason = f"{undo_prefix[lang_selector-1].replace('$actor', actor)} {v}"
                r = do_rollback(msg.embeds[0], actor, action_type="undo", reason=reason)
                try:
                    if r[0] == "Success":
                        await channel.send(content=f"{actor} отменил правку {r[1]}.")
                        send_to_db(actor, "undos", get_trigger(msg.embeds[0]))
                        await msg.delete()
                    else:
                        if "уже не существует" in r[0]:
                            new_embed = discord.Embed(color=msg.embeds[0].color, title="Страница была удалена.")
                            await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                        elif "уже были откачены" in r[0]:
                            new_embed = discord.Embed(color=msg.embeds[0].color, title="Правки уже были откачены.")
                            await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                        elif "версии принадлежат" in r[0]:
                            msg.embeds[0].set_footer(text=f"Отмена не удалась: все версии страницы принадлежат одному участнику.")
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                        else:
                            if r[1] != "":
                                msg.embeds[0].set_footer(text=f"Действие не удалось: {r[0]}, {r[1]}.")
                                await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                            else:
                                msg.embeds[0].set_footer(text=f"Действие не удалось: {r[0]}.")
                                await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                except Exception as e:
                    print(f"On_Interaction error 3: {e}")
            if inter.data["custom_id"] == "btn1":
                if len(msg.embeds) > 0:
                    r = do_rollback(msg.embeds[0], actor)
                    try:
                        if r[0] == "Success":
                            await inter.message.delete()
                            await channel.send(content=f"{actor} откатил правку {r[1]}.")
                            send_to_db(actor, "rollbacks", get_trigger(msg.embeds[0]))
                        else:
                            if "уже не существует" in r[0]:
                                new_embed = discord.Embed(color=msg.embeds[0].color, title="Страница была удалена.")
                                await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                            elif "уже были откачены" in r[0]:
                                new_embed = discord.Embed(color=msg.embeds[0].color, title="Правки уже были откачены.")
                                await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                            else:
                                if r[1] != "":
                                    msg.embeds[0].set_footer(text=f"Действие не удалось: {r[0]}, {r[1]}.")
                                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                                else:
                                    msg.embeds[0].set_footer(text=f"Действие не удалось: {r[0]}.")
                                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                    except Exception as e:
                        print(f"On_Interaction error 6: {e}")
            if inter.data["custom_id"] == "btn2":
                try:
                    await inter.message.delete()
                    await channel.send(
                        content=f"{actor} одобрил [правку](<{msg.embeds[0].url}>).")
                    send_to_db(actor, "approves", get_trigger(msg.embeds[0]))
                except Exception as e:
                    print(f"On_Interaction error 5: {e}")
            if inter.data["custom_id"] == "btn3":
                view = View()
                view.add_item(select_component)
                try:
                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=view)
                except Exception as e:
                    print(f"On_Interaction error 4: {e}")
            if inter.data["custom_id"] == "btn4":
                try:
                    await inter.message.delete()
                    await channel.send(
                        content=f"{actor} отметил [правку](<{msg.embeds[0].url}>) как плохую, но уже отменённую.")
                    send_to_db(actor, "approves", get_trigger(msg.embeds[0]))
                except Exception as e:
                    print(f"On_Interaction error 6: {e}")

@client.event
async def on_message(msg):
    if msg.author.id not in CONFIG["SOURCE_BOTS"]:
        try:
            await client.process_commands(msg)
        except Exception as e:
            print(f"On_Message error 1: {e}")
        return
    if msg.channel.id != CONFIG["SOURCE"]:
        try:
            await client.process_commands(msg)
        except Exception as e:
            print(f"On_Message error 2: {e}")
        return
    if len(msg.embeds) > 0:
        # не откачена ли
        lang = "ru" if "ru.wikipedia.org" in msg.embeds[0].url else "uk"
        rev_id = msg.embeds[0].url.replace(f"https://{lang}.wikipedia.org/w/index.php?", "").replace("oldid=", "").replace("diff=", "")
        try:
            data = {"action": "query", "prop": "revisions", "rvslots": "*", "rvprop": "ids|tags", "rvlimit": 500,
                    "rvendid": rev_id, "rvexcludeuser": msg.embeds[0].author.name, "titles": msg.embeds[0].title, "format": "json", "utf8": 1}
            r = requests.post(url=f"https://{lang}.wikipedia.org/w/api.php", data=data, headers=USER_AGENT)
            if r.status_code == 404:
                try:
                    await msg.delete()
                except Exception as e:
                    print(f"On_Message error 3: {e}")
            r = r.json()
            page_id = list(r["query"]["pages"].keys())[0]
        except Exception as e:
            print(f"On_Message error 4: {e}")
        else:
            if ("-1" in r["query"]["pages"]) or ("revisions" in r["query"]["pages"][page_id] and "mw-rollback" in
                                                 r["query"]["pages"][page_id]["revisions"][-1]["tags"]):
                try:
                    await msg.delete()
                except Exception as e:
                    print(f"On_Message error 5: {e}")
                return

        channel_new_id = 1212498198200062014 if "ru.wikipedia.org" in msg.embeds[0].url else 1219273496371396681
        if DEBUG["enable"]:
            channel_new_id = DEBUG["ID"]
        channel_new = client.get_channel(channel_new_id)
        try:
            new_message = await channel_new.send(embed=msg.embeds[0], view=get_view(msg.embeds[0], True))
        except Exception as e:
            print(f"On_Message error 6: {e}")
        else:
            try:
                await msg.delete()
            except Exception as e:
                print(f"On_Message error 7: {e}")
            finally:
                try:
                    await asyncio.sleep(3)
                    await new_message.edit(embed=new_message.embeds[0], view=get_view(new_message.embeds[0]))
                except Exception as e:
                    print(f"On_Message error 8: {e}")


@client.event
async def on_ready():
    try:
        for server in client.guilds:
            if server.id not in CONFIG["SERVER"]:
                guild = discord.utils.get(client.guilds, id=server.id)
                await guild.leave()
        await client.tree.sync()
        await client.change_presence(status=discord.Status.online, activity=discord.Game("pyCharm"))

        print("Просмотр пропущенных записей лога")
        channel = client.get_channel(CONFIG["SOURCE"])
        messages = channel.history(limit=50, oldest_first=False)
        async for msg in messages:
            if len(msg.embeds) > 0:
                await on_message(msg)
        print("Бот запущен")
    except Exception as e:
        print(f"On_Ready error 1: {e}")


@client.event
async def on_guild_join(guild):
    try:
        if guild.id not in CONFIG["SERVER"]:
            await guild.leave()
    except Exception as e:
        print(f"on_server_join 1: {e}")


client.run(token=TOKEN, reconnect=True, log_level=logging.WARN)
