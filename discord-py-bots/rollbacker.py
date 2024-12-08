"""Анти-вандальный бот"""

import asyncio
import os
import json
import logging
import time
import datetime
from urllib.parse import unquote, quote
import discord
import pymysql
import toolforge
from discord.ext import commands
from discord.ui import Button, View
import aiohttp
from antivand_cleaner import revision_check, flagged_check

DEBUG = {'ENABLE': False, 'ID': 1237345748778221649, 'port': 4711}
DB_CREDITS = {'user': os.environ['TOOL_TOOLSDB_USER'], 'port': DEBUG['port'], 'host': '127.0.0.1',
              'password': os.environ['TOOL_TOOLSDB_PASSWORD'], 'database': f'{os.environ["TOOL_TOOLSDB_USER"]}__rv'}

TOKEN = os.environ['DISCORD_BOT_TOKEN']
BEARER_TOKEN = os.environ['BEARER_TOKEN']

# Целевой сервер, ID каналов с фидами, ID бота, ID ботов-источников, ID канала с командами,
# ID сообщения со списком откатывающих, ID канала с источником, список админов для команд.
CONFIG = {'SERVER': [1044474820089368666], 'IDS': [1219273496371396681, 1212498198200062014],
          'BOT': 1225008116048072754, 'SOURCE_BOTS': [1237362558046830662, 1299324425878900818], 'BOTCOMMANDS': 1212507148982947901,
          'ROLLBACKERS': 1237790591044292680, 'SOURCE': 1237345566950948867,
          'ADMINS': [352826965494988822, 512545053223419924, 223219998745821194]}
USER_AGENT = {'User-Agent': 'D-V; iluvatar@tools.wmflabs.org; python3.11'}
STORAGE = []
Intents = discord.Intents.default()
Intents.members, Intents.message_content = True, True
discord.Intents.all()
allowed_mentions = discord.AllowedMentions(roles=True)
client = commands.Bot(intents=Intents, command_prefix='/')

select_options_undo = {
    '1': ['Неконструктивная правка', 'очевидно ошибочная правка', 'акт [[Вікіпедія:Вандалізм|вандалізму]]'],
    '2': ['Нет АИ',
          'добавление сомнительного содержимого [[ВП:ПРОВ|без источников]] или [[ВП:ОРИСС|оригинального исследования]]',
          'додавання [[ВП:ОД|оригінального дослідження]] або сумнівної інформації [[ВП:В|без джерел]]'],
    '3': ['Порча вики-разметки', 'порча [[ВП:Викиразметка|викиразметки]] статьи',
          'псування [[Вікірозмітка|вікірозмітки]] статті'],
    '4': ['Спам', 'добавление [[ВП:ВС|ненужных / излишних ссылок]] или спам',
          'додавання [[ВП:УНИКАТИПОС|непотрібних / зайвих посилань]] або спам'],
    '5': ['Незначимый факт', 'отсутствует [[ВП:Значимость факта|энциклопедическая значимость]] факта',
          'відсутня [[ВП:ЗВ|значущість]] факту'],
    '6': ['Переименование без КПМ',
          'попытка переименования объекта по тексту без [[ВП:ПЕРЕ|переименования страницы]] или иное сомнит. '
          'переименование. Воспользуйтесь [[ВП:КПМ|специальной процедурой]].', 'перейменування по тексту без '
                                                                               'перейменування сторінки.'],
    '7': ['Тестовая правка', 'экспериментируйте в [[ВП:Песочница|песочнице]]',
          'експерементуйте в [[Вікіпедія:Пісочниця|пісочниці]]'],
    '8': ['Удаление содержимого', 'необъяснённое удаление содержимого страницы', 'видалення вмісту сторінки'],
    '9': ['Орфография, пунктуация', 'добавление орфографических или пунктуационных ошибок',
          'додавання орфографічних або пунктуаційних помилок'],
    '10': ['Не на языке проекта', 'добавление содержимого не на русском языке',
           'додавання вмісту не українською мовою'],
    '11': ['Удаление шаблонов', 'попытка необоснованного удаления служебных или номинационных шаблонов',
           'спроба необґрунтованого видалення службових або номінаційних шаблонів'],
    '12': ['Личное мнение',
           '[[ВП:НЕФОРУМ|изложение личного мнения]] об объекте статьи. Википедия не является [[ВП:НЕФОРУМ|форумом]] или'
           ' [[ВП:НЕТРИБУНА|трибуной]]', 'виклад особистої думки про об\'єкт статті. [[ВП:НЕТРИБУНА|Вікіпедія — '
                                         'не трибуна]]'],
    '13': ['Комментарии в статье',
           'добавление комментариев в статью. Комментарии и пометки оставляйте на [[Talk:$7|странице обсуждения]] '
           'статьи', 'додавання коментарів до статті. Коментарі та позначки залишайте на '
                     '[[Сторінка обговорення:$1|сторінці обговорення]] статті'],
    '14': ['своя причина', '', ''],
    '15': ['Закрыть', '', '']
}
options_undo, options_rfd = [], []
for option, index in select_options_undo.items():
    options_undo.append(discord.SelectOption(label=index[0], value=str(option)))

select_options_rfd = {
    '1': ['Бессвязное содержимое', '{{уд-бессвязно}}', '{{Db-nonsense}}'],
    '2': ['Вандализм', '{{уд-ванд}}', '{{Db-vand}}'],
    '3': ['Тестовая страница', '{{уд-тест}}', '{{Db-test}}'],
    '4': ['Реклама / спам', '{{уд-реклама}}', '{{Db-spam}}'],
    '5': ['Пустая статья', '{{{уд-пусто}}', '{{Db-nocontext}}'],
    '6': ['На иностранном языке', '{{уд-иностр}}', '{{Db-lang}}'],
    '7': ['Нет значимости', '{{уд-нз}}', '{{Db-nn}}'],
    '8': ['своя причина', '', ''],
    '9': ['Закрыть', '', '']
}
for option, index in select_options_rfd.items():
    options_rfd.append(discord.SelectOption(label=index[0], value=str(option)))

select_component_undo = discord.ui.Select(placeholder='Выбор причины отмены', min_values=1, max_values=1,
                                          options=options_undo, custom_id='sel1')
select_component_rfd = discord.ui.Select(placeholder='Выбор причины КБУ', min_values=1, max_values=1,
                                         options=options_rfd, custom_id='sel2')
undo_prefix = ['отмена правки [[Special:Contribs/$author|$author]] по запросу [[User:$actor|$actor]]:',
               'скасовано останнє редагування [[Special:Contribs/$author|$author]] за запитом [[User:$actor|$actor]]:']
rfd_summary = ['Номинация на КБУ по запросу [[User:$actor|$actor]]',
               'Номінація на швидке вилучення за запитом [[User:$actor|$actor]]']


class ReasonUndo(discord.ui.Modal, title='Причина'):
    """Строка ввода причины отмены."""
    res = discord.ui.TextInput(custom_id='edt1', label='Причина отмены', min_length=2, max_length=255,
                               placeholder='введите причину', required=True, style=discord.TextStyle.short)

    async def on_submit(self, interaction: discord.Interaction):
        pass


class ReasonRFD(discord.ui.Modal, title='Причина'):
    """Строка ввода номинации на удаления."""
    res = discord.ui.TextInput(custom_id='edt2', label='Причина КБУ', min_length=2, max_length=255,
                               placeholder='введите причину', required=True, style=discord.TextStyle.short)

    async def on_submit(self, interaction: discord.Interaction):
        pass


def get_lang(url: str) -> str:
    """Получение кода языкового раздела из ссылки."""
    return 'ru' if 'ru.wikipedia.org' in url else 'uk'


def get_trigger(embed: discord.Embed) -> str:
    """Получение причины реакции по цвету."""
    color = str(embed.color)
    if color == '#ff0000':
        return 'patterns'
    if color == '#ffff00':
        return 'LW'
    if color == '#ff00ff':
        return 'ORES'
    if color == '#00ff00':
        return 'tags'
    if color == '#0000ff':
        return 'replaces'
    return 'unknown'


def send_to_db(actor: str, action_type: str, trigger: str, bad: bool = False) -> None:
    """Отправка в БД."""
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG['ENABLE'] else (
            toolforge.toolsdb(DB_CREDITS['database']))
        with conn.cursor() as cur:
            if action_type in ['rollbacks', 'undos', 'approves', 'rfd']:
                cur.execute('SELECT name FROM ds_antivandal WHERE name=%s;', actor)
                res = cur.fetchall()
                if len(res) == 0:
                    cur.execute(f'INSERT INTO ds_antivandal (name, {action_type}, {trigger}) VALUES (%s, 1, 1);', actor)
                else:
                    cur.execute(f'UPDATE ds_antivandal SET {action_type} = {action_type}+1, {trigger} = '
                                f'{trigger}+1 WHERE name = %s;', actor)
            conn.commit()
            if bad:
                cur.execute(f'UPDATE ds_antivandal_false SET {trigger} = {trigger}+1 WHERE result = "stats";')
                conn.commit()
        conn.close()
    except Exception as e:
        print(f'send_to_db error 1: {e}')


def get_from_db(is_all: bool = True, actor: str = None):
    """Получение из БД."""
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG['ENABLE'] else (
            toolforge.toolsdb(DB_CREDITS['database']))
        with conn.cursor() as cur:
            i_res = False
            triggers_false = False
            if is_all:
                cur.execute('SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES), '
                            'SUM(tags), SUM(rfd), SUM(replaces) FROM ds_antivandal')
                r = cur.fetchall()
                cur.execute('SELECT name, SUM(rollbacks) + SUM(undos) + SUM(approves) + SUM(rfd) + SUM(replaces) AS am FROM '
                            'ds_antivandal GROUP BY name ORDER BY am DESC LIMIT 5;')
                r2 = cur.fetchall()
                i_res = []
                for i in r2:
                    if i[0] != 'service_account':
                        i_res.append(f'{i[0]}: {i[1]}')
                i_res = '\n'.join(i_res)
                cur.execute('SELECT patterns, LW, ORES, tags, replaces FROM ds_antivandal_false WHERE result = "stats";')
                r3 = cur.fetchall()
                patterns = r[0][3] - 172
                patterns = 0 if patterns == 0 else float(f'{(r3[0][0]) / patterns * 100:.3f}')
                lw = r[0][4] - 1061
                lw = 0 if lw == 0 else float(f'{(r3[0][1]) / lw * 100:.3f}')
                ores = r[0][5] - 1431
                ores = 0 if ores == 0 else float(f'{(r3[0][2]) / ores * 100:.3f}')
                tags = r[0][6] - 63
                tags = 0 if tags == 0 else float(f'{(r3[0][3]) / tags * 100:.3f}')
                replaces = r[0][8] - 0
                replaces = 0 if replaces == 0 else float(f'{(r3[0][4]) / replaces * 100:.3f}')
                triggers_false = (f'Ложные триггеры, c 21.07.2024: паттерны — {r3[0][0]} ({patterns} %), '
                                  f'LW — {r3[0][1]} ({lw} %), ORES — {r3[0][2]} ({ores} %), теги — {r3[0][3]} ({tags} %), '
                                  f'замены — {r3[0][4]} ({replaces} %).')
            else:
                cur.execute('SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES),'
                            ' SUM(tags), SUM(rfd), SUM(replaces) FROM ds_antivandal WHERE name=%s;', actor)
                r = cur.fetchall()
            conn.close()
            if len(r) > 0:
                return {'rollbacks': r[0][0], 'undos': r[0][1], 'approves': r[0][2], 'rfd': r[0][7], 'total': i_res,
                        'patterns': r[0][3], 'LW': r[0][4], 'ORES': r[0][5], 'tags': r[0][6], 'replaces': r[0][8],
                        'triggers': triggers_false}
            return {'rollbacks': 0, 'undos': 0, 'approves': 0, 'rfd': 0, 'patterns': 0, 'LW': 0, 'ORES': 0, 'tags': 0,
                    'replaces': 0}
    except Exception as e:
        print(f'get_from_db error 1: {e}')
        return False


def delete_from_db(actor: str) -> None:
    """Удаление из БД."""
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG['ENABLE'] else (
            toolforge.toolsdb(DB_CREDITS['database']))
        with conn.cursor() as cur:
            cur.execute(f'DELETE FROM ds_antivandal WHERE name="{actor}";')
            conn.commit()
        conn.close()
    except Exception as e:
        print(f'delete_from_db error 1: {e}')


@client.tree.context_menu(name='Поприветствовать')
async def welcome_user(inter: discord.Interaction, message: discord.Message):
    """Шаблонное приветствие пользователя."""
    if inter.user.id in CONFIG['ADMINS']:
        try:
            await inter.response.defer()
            await inter.followup.send(content=f'Приветствуем, <@{message.author.id}>! Если вы желаете получить доступ '
                                              'к остальным каналам сервера, сообщите, пожалуйста, имя вашей учётной '
                                              f'записи в проектах Викимедиа.')
        except Exception as e:
            print(f'welcome_user error 1: {e}')
    else:
        try:
            await inter.response.defer(ephemeral=True)
            await inter.followup.send(content='К сожалению, у вас нет разрешения на выполнение данной команды.')
        except Exception as e:
            print(f'welcome_user error 2: {e}')


@client.tree.command(name='rollback_restart_cleaner')
async def rollback_restart_cleaner(inter: discord.Interaction):
    """Перезаапуск бота, очищающего ленты."""
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f'restart_cleaner 1: {e}')
    else:
        if inter.user.id in CONFIG['ADMINS']:
            session = aiohttp.ClientSession(headers=USER_AGENT)
            try:
                await session.get(url='https://rv.toolforge.org/online.php?send=1&action=restart&name=antclr'
                                      f'&token={os.environ["BOT_TOKEN"]}')
                await inter.followup.send(content='Запрос отправлен.', ephemeral=True)
            except Exception as e:
                print(f'restart_cleaner 2: {e}')
            finally:
                await session.close()
        else:
            try:
                await inter.followup.send(content='К сожалению, у вас нет разрешения на выполнение данной команды. '
                                                  f'Обратитесь к участнику <@{223219998745821194}> или '
                                                  f'<@{352826965494988822}>.',
                                          ephemeral=True)
            except Exception as e:
                print(f'restart_cleaner 3: {e}')


@client.tree.command(name='rollback_help')
async def rollback_help(inter: discord.Interaction):
    """Список команд бота."""
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f'rollback_help 1: {e}')
    else:
        try:
            await inter.followup.send(content="""/rollback_help — список команд бота.\n
                                              /rollback_clear — очистка фид-каналов от всех сообщений бота.\n
                                              /rollbackers — список участников, кому разрешены действия через бот.\n
                                              /add_rollbacker — разрешить участнику действия через бот.\n"
                                              /remove_rollbacker — запретить участника действия через бот.\n
                                              /rollback_stats_all — статистика откатов через бот.\n
                                              /rollback_stats — статистика действий участника через бот.\n
                                              /rollback_stats_delete — удалить всю статистику действий участника.\n
                                              По вопросам работы бота обращайтесь к <@352826965494988822>.""",
                                      ephemeral=True)
        except Exception as e:
            print(f'rollback_help 2: {e}')


@client.tree.command(name='rollback_stats_all')
async def rollback_stats_all(inter: discord.Interaction):
    """Просмотреть статистику откатов и отмен через бот."""
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f'rollback_stats_all 1: {e}')
    else:
        r = get_from_db(is_all=True)
        if r and len(r):
            try:
                await inter.followup.send(content=f'Через бот совершено: откатов — {r["rollbacks"]}, '
                                                  f'отмен — {r["undos"]}, одобрений ревизий — {r["approves"]}, '
                                                  f'номинаций на КБУ — {r["rfd"]}.\n'
                                                  f'Наибольшее количество действий совершили:\n{r["total"]}\n'
                                                  f'Действий по типам причин: паттерны — {r["patterns"]}, '
                                                  f'ORES — {r["ORES"]}, LW — {r["LW"]}, метки — {r["tags"]}, '
                                                  f'замены — {r["replaces"]}.\n'
                                                  f'{r["triggers"]}', ephemeral=True)
            except Exception as e:
                print(f'rollback_stats_all 2: {e}')


@client.tree.command(name='rollback_stats')
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
        print(f'rollback_stats 1: {e}')
    else:
        r = get_from_db(is_all=False, actor=wiki_name)
        if r and len(r):
            try:
                if r['rollbacks'] is None:
                    await inter.followup.send(
                        content='Данный участник не совершал действий через бот.', ephemeral=True)
                else:
                    await inter.followup.send(content=f'Через бот участник {wiki_name} совершил действий: '
                                                      f'{r["rollbacks"] + r["undos"] + r["approves"]},\n'
                                                      f'из них: откатов — {r["rollbacks"]}, отмен — {r["undos"]}, '
                                                      f'одобрений ревизий — {r["approves"]}, '
                                                      f'номинаций на КБУ — {r["rfd"]}.\n'
                                                      'Действий по типам причин, за всё время: паттерны — '
                                                      f'{r["patterns"]}, замены — {r["replaces"]}, ORES — {r["ORES"]}, '
                                                      f'LW — {r["LW"]}, метки — {r["tags"]}.', ephemeral=True)
            except Exception as e:
                print(f'rollback_stats 2: {e}')


# noinspection PyUnresolvedReferences
@client.tree.command(name='rollback_stats_delete')
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
        print(f'rollback_stats_delete 1: {e}')
    else:
        if inter.user.id in CONFIG['ADMINS']:
            delete_from_db(wiki_name)
            try:
                await inter.followup.send(content='Статистика участника удалена, убедитесь в этом через соответствующую '
                                                  'команду.', ephemeral=True)
            except Exception as e:
                print(f'rollback_stats_delete 3: {e}')
        else:
            try:
                await inter.followup.send(content='К сожалению, у вас нет разрешения '
                                                  'на выполнение данной команды. Обратитесь к участнику '
                                                  f'<@{223219998745821194}> или <@{352826965494988822}>.',
                                          ephemeral=True)
            except Exception as e:
                print(f'rollback_stats_delete 4: {e}')


@client.tree.command(name='last_metro')
async def last_metro(inter: discord.Interaction):
    """Узнать время последнего запуска бота #metro."""
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f'Metro error 1: {e}')
    else:
        session = aiohttp.ClientSession(headers=USER_AGENT)
        try:
            r = await session.get(url='https://rv.toolforge.org/metro/')
            r = await r.text()
            metro = r.split('<br>')[0].replace('Задание запущено', 'Последний запуск задания:')
            await inter.followup.send(content=metro, ephemeral=True)
        except Exception as e:
            print(f'Metro error 2: {e}')
        finally:
            await session.close()


@client.tree.command(name='rollback_clear')
async def rollback_clear(inter: discord.Interaction):
    """Очистка каналов с выдачей от сообщений бота."""
    try:
        await inter.response.defer(ephemeral=True)
    except Exception as e:
        print(f'Clear feed error 1: {e}')
    else:
        if inter.user.id in CONFIG['ADMINS']:
            try:
                await inter.followup.send(content='Очистка каналов начата.', ephemeral=True)
            except Exception as e:
                print(f'Clear feed error 2: {e}')
            for channel_id in CONFIG['IDS']:
                channel = client.get_channel(channel_id)
                messages = channel.history(limit=100000)
                async for msg in messages:
                    if msg.author.id == CONFIG['BOT']:
                        try:
                            await msg.delete()
                            await asyncio.sleep(1.5)
                        except Exception as e:
                            print(f'Clear feed error 3: {e}')
                        time.sleep(1.0)
        else:
            try:
                await inter.followup.send(content='К сожалению, у вас нет разрешения '
                                                  'на выполнение данной команды. '
                                                  f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
            except Exception as e:
                print(f'Clear feed error 4: {e}')


@client.tree.command(name='rollbackers')
async def rollbackers(inter: discord.Interaction):
    """Просмотра списка участников, кому разрешён откат и отмена через бот."""
    try:
        await inter.response.defer(ephemeral=True)
        msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
        rights_content = json.loads(msg_rights.content.replace('`', '')).values()
    except Exception as e:
        print(f'Rollbackers list error 1: {e}')
    else:
        try:
            await inter.followup.send(content=f'Откаты и отмены через бота разрешены участникам '
                                              f'`{", ".join(rights_content)}`.\nДля запроса права или отказа от него '
                                              f'обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Rollbackers list error 2: {e}')


@client.tree.command(name='add_rollbacker')
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
        print(f'Add rollbacker error 1: {e}')
    if inter.user.id in CONFIG['ADMINS']:
        if '@' not in wiki_name:
            try:
                msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
                rights_content = json.loads(msg_rights.content.replace('`', ''))
            except Exception as e:
                print(f'Add rollbacker error 2: {e}')
            else:
                if str(discord_name.id) not in rights_content:
                    rights_content[str(discord_name.id)] = wiki_name
                    try:
                        await msg_rights.edit(content=json.dumps(rights_content))
                        await inter.followup.send(content=f'Участник {wiki_name} добавлен в список откатывающих.',
                                                  ephemeral=True)
                    except Exception as e:
                        print(f'Add rollbacker error 3: {e}')
                else:
                    try:
                        await inter.followup.send(content=f'Участник {wiki_name} уже присутствует в списке '
                                                          f'откатывающих.', ephemeral=True)
                    except Exception as e:
                        print(f'Add rollbacker error 4: {e}')
    else:
        try:
            await inter.followup.send(content=f'К сожалению, у вас нет разрешения на выполнение данной команды. '
                                              f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Add rollbacker error 4: {e}')


@client.tree.command(name='remove_rollbacker')
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
        print(f'Remove rollbacker error 1: {e}')
    if inter.user.id in CONFIG['ADMINS']:
        try:
            msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
            rights_content = json.loads(msg_rights.content.replace('`', ''))
        except Exception as e:
            print(f'Remove rollbacker error 2: {e}')
        else:
            right_copy = rights_content.copy()
            for k in right_copy:
                if rights_content[k] == wiki_name:
                    del rights_content[k]
            if right_copy != rights_content:
                try:
                    await msg_rights.edit(content=json.dumps(rights_content))
                except Exception as e:
                    print(f'Remove rollbacker error 3: {e}')
                else:
                    await inter.followup.send(content=f'Участник {wiki_name} убран из списка откатывающих.',
                                              ephemeral=True)
            else:
                try:
                    await inter.followup.send(content=f'Участника {wiki_name} не было в списке откатывающих.',
                                              ephemeral=True)
                except Exception as e:
                    print(f'Remove rollbacker error 4: {e}')
    else:
        try:
            await inter.followup.send(content=f'К сожалению, у вас нет разрешения на выполнение данной команды. '
                                              f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Remove rollbacker error 4: {e}')


async def do_rollback(embed, actor, action_type='rollback', reason=''):
    """Выполнение отката или отмены правки."""
    diff_url = embed.url
    title = embed.title
    lang = get_lang(diff_url)
    rev_id = diff_url.replace(f'https://{lang}.wikipedia.org/w/index.php?diff=', '')
    session = aiohttp.ClientSession(headers=USER_AGENT)
    try:
        r = await revision_check(f'https://{lang}.wikipedia.org/w/api.php', rev_id, title, session)
    except Exception as e:
        print(f'rollback error 1.1: {e}')
        await session.close()
    else:
        if not r:
            r = await flagged_check(f'https://{lang}.wikipedia.org/w/api.php', title, rev_id, session)
        if r:
            await session.close()
            return ['Такой страницы уже не существует, правки были откачены или страница отпатрулирована.',
                    f'[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(" ", "_")}>) (ID: {rev_id})']
        data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'ids|timestamp', 'rvlimit': 500,
                'rvendid': rev_id, 'rvuser': get_name_from_embed(lang, embed.author.url), 'titles': title,
                'format': 'json', 'utf8': 1, 'uselang': 'ru'}
    try:
        r = await session.post(url=f'https://{lang}.wikipedia.org/w/api.php', data=data)
        r = await r.json()
    except Exception as e:
        print(f'rollback error 2: {e}')
        await session.close()
    else:
        if '-1' in r['query']['pages']:
            await session.close()
            return ['Такой страницы уже не существует.',
                    f'[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(" ", "_")}>) (ID: {rev_id})']
        page_id = str(list(r['query']["pages"].keys())[0])
        if 'revisions' in r['query']['pages'][page_id] and len(r['query']['pages'][page_id]['revisions']) > 0:
            rev_id = r['query']['pages'][page_id]['revisions'][0]['revid']
            api_url = f'https://{lang}.wikipedia.org/w/api.php'
            headers = {'Authorization': f'Bearer {BEARER_TOKEN}', 'User-Agent': 'Reimu; iluvatar@tools.wmflabs.org'}
            session_with_auth = aiohttp.ClientSession(headers=headers)

            if action_type == 'rollback':
                comment_body_uk = ('Бот: відкинуто редагування [[Special:Contribs/$2|$2]] за запитом '
                                   f'[[User:{actor}|{actor}]]')
                comment_body_ru = f'Бот: откат правок [[Special:Contribs/$2|$2]] по запросу [[u:{actor}|{actor}]]'
                comment = comment_body_ru if lang == 'ru' else comment_body_uk
                try:
                    r_token = await session_with_auth.get(f'{api_url}?format=json&action=query&meta=tokens'
                                                          f'&type=rollback')
                    rollback_token = await r_token.json()
                    rollback_token = rollback_token['query']['tokens']['rollbacktoken']
                except Exception as e:
                    await session_with_auth.close()
                    await session.close()
                    print(f'rollback error 3: {e}')
                else:
                    data = {'action': 'rollback', 'format': 'json', 'title': title,
                            'user': get_name_from_embed(lang, embed.author.url), 'utf8': 1, 'watchlist': 'nochange',
                            'summary': comment, 'token': rollback_token, 'uselang': 'ru'}
                    try:
                        r = await session_with_auth.post(url=f'https://{lang}.wikipedia.org/w/api.php', data=data)
                        r = await r.json()
                    except Exception as e:
                        print(f'rollback error 4: {e}')
                    else:
                        return [r['error']['info'],
                                f'[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(" ", "_")}>) '
                                f'(ID: {rev_id})'] if 'error' in r else [
                            'Success',
                            f'[{title}](<https://{lang}.wikipedia.org/w/index.php?diff={r["rollback"]["revid"]}>)']
                    finally:
                        await session.close()
                        await session_with_auth.close()
            else:
                data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'ids|user', 'rvlimit': 1,
                        'rvstartid': rev_id, 'rvexcludeuser': get_name_from_embed(lang, embed.author.url),
                        'titles': title, 'format': 'json', 'utf8': 1, 'uselang': 'ru'}
                try:
                    r = await session.post(url=f'https://{lang}.wikipedia.org/w/api.php', data=data)
                    r = await r.json()
                    check_revs = len(r['query']['pages'][page_id]['revisions'])
                except Exception as e:
                    print(f'rollback error 5: {e}')
                else:
                    if check_revs == 0:
                        await session_with_auth.close()
                        await session.close()
                        return ['Все версии принадлежат одному участнику', f'[{title}]'
                                                                           f'(<https://{lang}.wikipedia.org/wiki/'
                                                                           f'{title.replace(" ", "_")}>) (ID: '
                                                                           f'{rev_id})']
                    parent_id = r['query']['pages'][page_id]['revisions'][0]['revid']
                    last_author = r['query']['pages'][page_id]['revisions'][0]['user']
                    try:
                        r_token = await session_with_auth.get(f'{api_url}?format=json&action=query&meta=tokens'
                                                              f'&type=csrf')
                        edit_token = await r_token.json()
                        edit_token = edit_token['query']['tokens']['csrftoken']
                    except Exception as e:
                        await session.close()
                        await session_with_auth.close()
                        print(f'rollback error 6: {e}')
                    else:
                        reason = reason.replace('$author', get_name_from_embed(lang, embed.author.url)).replace(
                            '$lastauthor', last_author)
                        data = {'action': 'edit', 'format': 'json', 'title': title, 'undo': rev_id,
                                'undoafter': parent_id, 'watchlist': 'nochange', 'nocreate': 1, 'summary': reason,
                                'token': edit_token, 'utf8': 1, 'uselang': 'ru'}
                        try:
                            r = await session_with_auth.post(url=f'https://{lang}.wikipedia.org/w/api.php', data=data)
                            r = await r.json()
                        except Exception as e:
                            print(f'rollback error 7: {e}')
                        else:
                            if 'newrevid' not in r['edit'] and 'revid' not in r['edit']:
                                return print(r)  # debug
                            return [r['error']['info'], f'[{title}](<https://{lang}'
                                                        f'.wikipedia.org/wiki/{title.replace(" ", "_")}>) '
                                                        f'(ID: {rev_id})'] if 'error' in r else \
                                ['Success', f'[{title}](<https://{lang}.wikipedia.org/w/index.php?diff='
                                            f'{r["edit"]["newrevid"]}>)', title]
                        finally:
                            await session.close()
                            await session_with_auth.close()
                finally:
                    await session.close()
        else:
            await session.close()


def get_view(lang: str = 'ru', page: str = '', user: str = '', disable: bool = False) -> View:
    """Формирование набора компонентов."""
    btn1 = Button(emoji='⏮️', style=discord.ButtonStyle.danger, custom_id='btn1', disabled=disable, row=1)
    btn2 = Button(emoji='🗑️', style=discord.ButtonStyle.danger, custom_id='btn5', disabled=disable, row=1)
    btn5 = Button(emoji='↪️', style=discord.ButtonStyle.blurple, custom_id='btn3', disabled=disable, row=1)
    view = View()
    [view.add_item(i) for i in [btn1, btn2, btn5]]

    # if lang == 'ru':
    #     user, page = quote(user), quote(page)
    #     btn3 = Button(emoji='🔨', style=discord.ButtonStyle.url,
    #                   url=f'https://ru.wikipedia.org/w/index.php?action=edit&preload=User:Iluvatar/Preload&'
    #                       f'section=new&title=User:QBA-bot/Запросы_на_блокировку&preloadtitle=1%23%23%23%23{user}',
    #                   disabled=disable, row=2)
    #     btn4 = Button(emoji='🔒', style=discord.ButtonStyle.url, url=f'https://ru.wikipedia.org/w/index.php?'
    #                                                                 f'action=edit&preload=User:Iluvatar/Preload&'
    #                                                                 f'section=new&title=User:Iluvatar/Запросы_на_'
    #                                                                 f'полузащиту&preloadtitle=1%23%23%23%23{page}',
    #                   disabled=disable, row=2)
    #     [view.add_item(i) for i in [btn3, btn4]]

    btn6 = Button(emoji='👍🏻', style=discord.ButtonStyle.green, custom_id='btn2', disabled=disable,
                  row=1) #2 if lang == 'ru' else 1)
    btn7 = Button(emoji='💩', style=discord.ButtonStyle.green, custom_id='btn4', disabled=disable,
                  row=1) #2 if lang == 'ru' else 1)
    [view.add_item(i) for i in [btn6, btn7]]
    return view


def get_name_from_embed(lang: str, link: str) -> str:
    """Получение имени пользователя из ссылки на вклад."""
    return unquote(link.replace(f'https://{lang}.wikipedia.org/wiki/special:contribs/', ''))


async def do_rfd(embed: discord.Embed, rfd: str, summary: str):
    """Номинация на быстрое удаление."""
    diff_url = embed.url
    title = embed.title
    lang = get_lang(diff_url)
    api_url = f'https://{lang}.wikipedia.org/w/api.php'
    headers = {'Authorization': f'Bearer {BEARER_TOKEN}', 'User-Agent': 'Reimu; iluvatar@tools.wmflabs.org'}
    rfd = '{{delete|' + rfd + '}}' if '{{' not in rfd or '}}' not in rfd else rfd

    session = aiohttp.ClientSession(headers=headers)
    try:
        r = await session.get(url=f'{api_url}?format=json&action=query&meta=tokens&type=csrf')
        edit_token = await r.json()
        edit_token = edit_token['query']['tokens']['csrftoken']
    except Exception as e:
        await session.close()
        print(f'rfd error 1: {e}')
    else:
        payload = {'action': 'edit', 'format': 'json', 'title': title, 'prependtext': f'{rfd}\n\n', 'token': edit_token,
                   'utf8': 1, 'nocreate': 1, 'summary': summary, 'uselang': 'ru'}
        try:
            r = await session.post(url=api_url, data=payload)
            r = await r.json()
        except Exception as e:
            print(f'rfd error 2: {e}')
        else:
            if 'newrevid' not in r['edit'] and 'revid' not in r['edit']:
                return print(r)  # debug
            return [r['error']['info'], f'[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(" ", "_")}>) '
                                        f'(ID: {title})'] if 'error' in r \
                else ['Success', f'[{title}](<https://{lang}.wikipedia.org/w/index.php?diff={r["edit"]["newrevid"]}>)',
                      title]
        finally:
            await session.close()


@client.event
async def on_interaction(inter):
    """Событие получение отклика пользователя."""
    if 'custom_id' not in inter.data:
        return
    if inter.data['custom_id'] != 'sel1' and inter.data['custom_id'] != 'sel2':
        try:
            await inter.response.defer()
        except Exception as e:
            print(f'On_Interaction error 0.1: {e}')
    else:
        if (inter.data['custom_id'] == 'sel1' and inter.data['values'][0] != '14') or (inter.data['custom_id'] == 'sel2'
                                                                                       and inter.data['values'][0]
                                                                                       != '8'):
            try:
                await inter.response.defer()
            except Exception as e:
                print(f'On_Interaction error 0.2: {e}')
    if inter.channel.id in CONFIG['IDS']:
        try:
            msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
            msg_rights = json.loads(msg_rights.content.replace('`', ''))
        except Exception as e:
            print(f'On_Interaction error 1: {e}')
        else:
            if str(inter.user.id) not in msg_rights:
                try:
                    await inter.followup.send(content='К сожалению, у вас нет разрешение на выполнение откатов и отмен '
                                                      f'через бот. Обратитесь к участнику <@{223219998745821194}>.',
                                              ephemeral=True)
                except Exception as e:
                    print(f'On_Interaction error 2: {e}')
                return
            actor = msg_rights[str(inter.user.id)]
            msg = inter.message
            channel = client.get_channel(CONFIG['SOURCE'])

            undo_options_check, rfd_options_check = False, False
            if 'components' in inter.data and len(inter.data['components']) > 0 and 'components' in \
                    inter.data['components'][0] and len(inter.data['components'][0]['components']) > 0:
                if inter.data['components'][0]['components'][0]['custom_id'] in ['edt1', 'edt2']:
                    if inter.data['components'][0]['components'][0]['custom_id'] == 'edt1':
                        undo_options_check = inter.data['components'][0]['components'][0]['value']
                    else:
                        rfd_options_check = inter.data['components'][0]['components'][0]['value']
            lang = get_lang(msg.embeds[0].url)
            base_view = get_view(lang=lang, user=get_name_from_embed(lang, msg.embeds[0].author.url),
                                 page=msg.embeds[0].title)
            if inter.data['custom_id'] == 'sel2' or rfd_options_check is not False:
                lang_selector = 1
                if lang == 'uk':
                    lang_selector = 2
                summary = rfd_summary[lang_selector - 1].replace('$actor', actor)
                if inter.data['custom_id'] == 'sel2':
                    selected = inter.data['values'][0]
                    rfd_reason = select_options_rfd[selected][lang_selector]
                    try:
                        await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                    except Exception as e:
                        print(f'On_Interaction TextEdit error 1.1: {e}')
                    if selected == '8':
                        try:
                            await inter.response.send_modal(ReasonRFD())
                        except Exception as e:
                            print(f'On_Interaction TextEdit error 2.1: {e}')
                        return
                    if selected == '9':
                        try:
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                        except Exception as e:
                            print(f'On_Interaction TextEdit error 3.1: {e}')
                        return
                else:
                    rfd_reason = rfd_options_check
                r = await do_rfd(msg.embeds[0], rfd=rfd_reason, summary=summary)
                try:
                    if r[0] == 'Success':
                        await channel.send(content=f'{actor} номинировал {r[1]} на КБУ.')
                        send_to_db(actor, 'rfd', get_trigger(msg.embeds[0]))
                        await msg.delete()
                    else:
                        if 'уже не существует' in r[0]:
                            new_embed = discord.Embed(color=msg.embeds[0].color, title='Страница была удалена.')
                            await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                        else:
                            if r[1] != '':
                                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}, {r[1]}.')
                            else:
                                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}.')
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                except Exception as e:
                    print(f'On_Interaction error 3.2: {e}')
            if inter.data['custom_id'] == 'sel1' or undo_options_check is not False:
                lang_selector = 1
                if lang == 'uk':
                    lang_selector = 2

                if inter.data['custom_id'] == 'sel1':
                    selected = inter.data['values'][0]
                    reason = (f'{undo_prefix[lang_selector - 1].replace("$actor", actor)} '
                              f'{select_options_undo[selected][lang_selector].replace("$1", msg.embeds[0].title)}')
                    try:
                        await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                    except Exception as e:
                        print(f'On_Interaction TextEdit error 1: {e}')
                    if selected == '14':
                        try:
                            await inter.response.send_modal(ReasonUndo())
                        except Exception as e:
                            print(f'On_Interaction TextEdit error 2: {e}')
                        return
                    if selected == '15':
                        try:
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                        except Exception as e:
                            print(f'On_Interaction TextEdit error 3: {e}')
                        return
                else:
                    reason = f'{undo_prefix[lang_selector - 1].replace("$actor", actor)} {undo_options_check}'
                r = await do_rollback(msg.embeds[0], actor, action_type='undo', reason=reason)
                try:
                    if r[0] == 'Success':
                        await channel.send(content=f'{actor} выполнил отмену на странице {r[1]}.')
                        send_to_db(actor, 'undos', get_trigger(msg.embeds[0]))
                        await msg.delete()
                    else:
                        if 'были откачены' in r[0]:
                            send_to_db('service_account', 'undos', get_trigger(msg.embeds[0]))
                            new_embed = discord.Embed(color=msg.embeds[0].color, title='Страница была удалена, '
                                                                                       'отпатрулирована или правки уже '
                                                                                       'были откачены.')
                            await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                        elif 'версии принадлежат' in r[0]:
                            msg.embeds[0].set_footer(text='Отмена не удалась: все версии страницы принадлежат одному '
                                                          'участнику.')
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                        else:
                            if r[1] != '':
                                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}, {r[1]}.')
                            else:
                                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}.')
                            await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                except Exception as e:
                    print(f'On_Interaction error 3: {e}')
            if inter.data['custom_id'] == 'btn1':
                if len(msg.embeds) > 0:
                    r = await do_rollback(msg.embeds[0], actor)
                    try:
                        if r[0] == 'Success':
                            await inter.message.delete()
                            await channel.send(content=f'{actor} выполнил откат на странице {r[1]}.')
                            send_to_db(actor, 'rollbacks', get_trigger(msg.embeds[0]))
                        else:
                            if 'были откачены' in r[0]:
                                send_to_db('service_account', 'rollbacks', get_trigger(msg.embeds[0]))
                                new_embed = discord.Embed(color=msg.embeds[0].color, title='Страница была удалена, '
                                                                                       'отпатрулирована или правки уже '
                                                                                       'были откачены.')
                                await inter.message.edit(embed=new_embed, view=None, delete_after=12.0)
                            else:
                                footer_info = f'{r[0]}, {r[1]}' if r[1] != '' else f'{r[0]}'
                                if r[1] != '':
                                    msg.embeds[0].set_footer(text=f'Действие не удалось: {footer_info}.')
                                await msg.edit(content=msg.content, embed=msg.embeds[0], view=base_view)
                    except Exception as e:
                        print(f'On_Interaction error 6: {e}')
            if inter.data['custom_id'] == 'btn2':
                try:
                    await inter.message.delete()
                    await channel.send(content=f'{actor} одобрил [правку](<{msg.embeds[0].url}>) на странице '
                                               f'{msg.embeds[0].title}.')
                    send_to_db(actor, 'approves', get_trigger(msg.embeds[0]), bad=True)
                except Exception as e:
                    print(f'On_Interaction error 5: {e}')
            if inter.data['custom_id'] == 'btn3':
                view = View()
                view.add_item(select_component_undo)
                try:
                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=view)
                except Exception as e:
                    print(f'On_Interaction error 4: {e}')
            if inter.data['custom_id'] == 'btn5':
                view = View()
                view.add_item(select_component_rfd)
                try:
                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=view)
                except Exception as e:
                    print(f'On_Interaction error 4.1: {e}')
            if inter.data['custom_id'] == 'btn4':
                try:
                    await inter.message.delete()
                    await channel.send(
                        content=f'{actor} отметил [правку](<{msg.embeds[0].url}>) на странице {msg.embeds[0].title} '
                                f'как неконструктивную, но уже отменённую.')
                    send_to_db(actor, 'approves', get_trigger(msg.embeds[0]))
                except Exception as e:
                    print(f'On_Interaction error 6.1: {e}')


@client.event
async def on_message(msg):
    """Получение нового сообщения."""
    if msg.author.id not in CONFIG['SOURCE_BOTS']:
        try:
            await client.process_commands(msg)
        except Exception as e:
            print(f'On_Message error 1: {e}')
        return
    if msg.channel.id != CONFIG['SOURCE']:
        try:
            await client.process_commands(msg)
        except Exception as e:
            print(f'On_Message error 2: {e}')
        return

    global STORAGE
    STORAGE = [el for el in STORAGE if el['timestamp'] < datetime.datetime.now(datetime.UTC).timestamp() + 30]

    lang = get_lang(msg.embeds[0].url)
    rev_id = msg.embeds[0].url.replace(f'https://{lang}.wikipedia.org/w/index.php?diff=', '')

    if len(msg.embeds) > 0:
        trigger = get_trigger(msg.embeds[0])
        for el in STORAGE:
            if (el['wiki'] == f'{lang}wiki' and el['rev_id'] == rev_id and el['trigger'] == 'replaces' 
                    and trigger != 'replaces'):
                await asyncio.sleep(1.5)
                await el['msg'].delete()
            if el['wiki'] == f'{lang}wiki' and el['rev_id'] == rev_id and el[
                'trigger'] != 'replaces' and trigger == 'replaces':
                await asyncio.sleep(1.5)
                await msg.delete()
                return

        # не откачена ли
        session = aiohttp.ClientSession(headers=USER_AGENT)
        is_reverted = await revision_check(f'https://{lang}.wikipedia.org/w/api.php', rev_id, msg.embeds[0].title,
                                           session)
        if not is_reverted:
            is_reverted = await flagged_check(f'https://{lang}.wikipedia.org/w/api.php', msg.embeds[0].title, rev_id,
                                              session)
        await session.close()
        if is_reverted:
            try:
                await msg.delete()
                return
            except Exception as e:
                print(f'On_Message error 3: {e}')
        channel_new_id = 1212498198200062014 if lang == 'ru' else 1219273496371396681
        channel_new = client.get_channel(channel_new_id)
        try:
            new_message = await channel_new.send(embed=msg.embeds[0],
                                                 view=get_view(lang=lang, disable=True,
                                                               user=get_name_from_embed(lang, msg.embeds[0].author.url),
                                                               page=msg.embeds[0].title))
            STORAGE.append({'wiki': f'{lang}wiki', 'rev_id': rev_id, 'trigger': trigger, 'msg': new_message, 'timestamp':
                datetime.datetime.now(datetime.UTC).timestamp()})

        except Exception as e:
            print(f'On_Message error 4: {e}')
        else:
            try:
                await msg.delete()
            except Exception as e:
                print(f'On_Message error 5: {e}')
            finally:
                try:
                    await asyncio.sleep(3)
                    await new_message.edit(embed=new_message.embeds[0],
                                           view=get_view(lang=lang,
                                                         user=get_name_from_embed(lang, msg.embeds[0].author.url),
                                                         page=msg.embeds[0].title))
                except Exception as e:
                    print(f'On_Message error 6: {e}')


@client.event
async def on_ready():
    """Событие после запуска бота."""
    try:
        for server in client.guilds:
            if server.id not in CONFIG['SERVER']:
                guild = discord.utils.get(client.guilds, id=server.id)
                await guild.leave()
        await client.tree.sync()
        await client.change_presence(status=discord.Status.online, activity=discord.Game('pyCharm'))

        print('Просмотр пропущенных записей лога')
        channel = client.get_channel(CONFIG['SOURCE'])
        messages = channel.history(limit=50, oldest_first=False)
        async for msg in messages:
            if len(msg.embeds) > 0:
                await on_message(msg)
        print('Бот запущен')
    except Exception as e:
        print(f'On_Ready error 1: {e}')


@client.event
async def on_guild_join(guild):
    """Событие входа бота на новый сервер."""
    try:
        if guild.id not in CONFIG['SERVER']:
            await guild.leave()
    except Exception as e:
        print(f'on_server_join 1: {e}')


client.run(token=TOKEN, reconnect=True, log_level=logging.WARN)
