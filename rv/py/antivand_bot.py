"""Анти-вандальный бот"""

import json
import re
import logging
from asyncio import sleep
from urllib.parse import unquote
from functools import wraps
from aiohttp import ClientSession
from typing import Literal, Callable, Coroutine

from discord import SelectOption, Client, Embed, Message, Interaction, User, Intents, AllowedMentions, ButtonStyle, Status, Game, ChannelType, TextChannel, Guild
from discord.app_commands import CommandTree, Group, Choice, autocomplete
from discord.ext import tasks
from discord.ui import Button, button, View, Select, select, TextInput, Modal

from antivand_config import HEADERS, DOMAINS, DISCORD_TOKEN, SERVERS, SOURCE_CHANNEL, STREAM_CHANNELS, ADMINS, BOT, SOURCE_BOT, USERS_PAGE, UPDATE_USERS_SUMMARY, LOG_TEMPLATES, SUMMARY, REASONS
from antivand_db import now, db_record_action, db_get_stats, db_remove_user, db_store_ad_message, db_get_expired_ads, db_fetch_users, db_update_users

USERS = {}

intents = Intents.default()
intents.message_content = True
client = Client(intents=intents, allowed_mentions=AllowedMentions.none())

tree = CommandTree(client)
users = Group(name='users', description='Команды, затрагивающие список доверенных пользователей.')
stats = Group(name='stats', description='Команды, затрагивающие статистику действий через бота')
tree.add_command(users)
tree.add_command(stats)

template_regexp = re.compile(r'{{(.+)\|(.+)}}')
author_regexp = re.compile(r'https://\w+\.\w+\.org/wiki/special:contribs/(\S+)')
title_regexp = re.compile(r'\[hist\]\(<https://\w+\.\w+\.org/wiki/special:history/(\S+)>\)')

session, api_session = None, None

def get_lang(url: str) -> str:
    """Получение кода языкового раздела из ссылки."""
    for lang, domain in DOMAINS.items():
        if url.startswith(f'https://{domain}.org'):
            return lang

def get_domain(lang: str) -> str:
    """Получение домена языкового раздела из кода."""
    return f'https://{DOMAINS[lang]}.org'

def get_user_link(user: str) -> str:
    """Получение викиссылки на участника."""
    return f'[[:User:{user}|{user}]]'

def get_contribs_link(user: str) -> str:
    """Получение викиссылки на вклад участника."""
    return f'[[Special:Contribs/{user}|{user}]]'

def get_page_link(embed: Embed, rev_id: int = 0) -> str:
    """Получение Markdown-ссылки на страницу или версию страницы для использования в Дискорде."""
    url = f'{get_domain(get_lang(embed.url))}/w/index.php?diff={rev_id}' if rev_id else embed.url
    return f'[{embed.title}](<{url}>)'

def get_interaction_data(interaction: Interaction) -> tuple[str, Message, Embed]:
    """Получение данных о действии."""
    actor = USERS[interaction.user.id]
    msg = interaction.message
    embed = msg.embeds[0]
    return actor, msg, embed

def get_embed_data(embed: Embed) -> tuple[str | None, str | None, str | None, str | None]:
    """Получение данных о правки."""
    diff_url = embed.url
    if not diff_url:
        return None, None, None, None
    lang = get_lang(diff_url)
    title = unquote(title_regexp.search(embed.description).group(1))
    author = unquote(author_regexp.fullmatch(embed.author.url).group(1))
    rev_id = diff_url.split('ilu=')[1] if 'ilu' in diff_url else None
    return lang, title, author, rev_id

async def check_rights(_: View, interaction: Interaction) -> bool:
    """Проверка, что пользователь может пользоваться ботом."""
    if interaction.user.id not in USERS:
        await interaction.response.send_message(ephemeral=True, content='К сожалению, у вас нет прав на использование бота. Обратитесь к участнику <@512545053223419924>, если желаете их получить.')
        return False
    return True

def admin(func: Callable) -> Coroutine:
    """Декоратор проверки, что пользователь является администратором бота."""
    @wraps(func)
    async def wrapper(interaction: Interaction, *args, **kwargs):
        if interaction.user.id in ADMINS:
            return await func(interaction, *args, **kwargs)
        await interaction.response.send_message(ephemeral=True, content='Для выполнения этой команды требуются права администратора бота.')
    return wrapper

async def api_request(lang: str, method: Literal['GET', 'POST'] = 'POST', **kwargs) -> dict:
    """Запрос к API раздела."""
    url = f'{get_domain(lang)}/w/api.php'
    params = {key: value for key, value in kwargs.items() if value is not None} | {'errorformat': 'plaintext', 'errorlang': 'ru', 'format': 'json', 'formatversion': 2}
    for _ in range(5):
        try:
            if method == 'GET':
                res = await api_session.get(url, params=params)
            else:
                res = await api_session.post(url, data=params)
            if not res.ok:
                raise Exception(str(res.status_code))
            return await res.json()
        except Exception as e:
            if _ == 4:
                raise e
            await sleep(1)

async def fetch_users() -> None:
    """Получение списка доверенных участников из внешних источников."""
    global USERS
    all_users = await db_fetch_users()
    r = await session.get(f'{get_domain("m")}/w/index.php', params={'title': USERS_PAGE, 'action': 'raw'})
    users = json.loads(await r.text())
    USERS = {discord_id: wiki_name for discord_id, wiki_name in all_users.items() if wiki_name in users}

async def update_users(action: Literal['add', 'remove'], user: str, admin: str) -> None:
    """Обновление списка доверенных участников во внешних источниках."""
    users = list(USERS.values())
    await db_update_users(action, list(USERS.keys())[users.index(user)] if action == 'add' else None, user)
    token = (await api_request('m', 'GET', action='query', meta='tokens', type='csrf'))['query']['tokens']['csrftoken']
    summary = UPDATE_USERS_SUMMARY[action].format(get_user_link(user), get_user_link(admin))
    r = await api_request('m', action='edit', title=USERS_PAGE, text=json.dumps(sorted(users)), summary=summary, token=token)
    if r.get('errors'):
        raise Exception(r['errors'][0]['text'])

async def check_edit(embed: Embed) -> str | None:
    """Проверка, не была ли правка уже обработана."""
    lang, title, author, rev_id = get_embed_data(embed)
    logging.error(f'{lang}, {title}, {author}, {rev_id}')
    if not lang:
        return

    r = await api_request(lang, action='query', prop='info|flagged', titles=title)
    page = r['query']['pages'][0]
    if 'missing' in page:
        return 'Страница уже была удалена.'
    # flagged нет без расширения
    if page.get('flagged', {}).get('stable_revid', 0) >= (int(rev_id) if rev_id else page['lastrevid']):
        return 'Страница уже была отпатрулирована.'

    timestamp = page['touched']
    if rev_id:
        r = await api_request(lang, action='query', prop='revisions', rvprop='tags|timestamp', revids=rev_id)
        if 'badrevids' in r['query']:
            return 'Правка уже была удалена.'
        page = r['query']['pages'][0]
        if 'missing' in page:
            return 'Страница уже была удалена.'
        if 'mw-reverted' in page['revisions'][0]['tags']: # у правки есть тег "отменено"
            return 'Правка уже была откачена или отменена.'
        timestamp = page['revisions'][0]['timestamp']

    r = await api_request(lang, action='query', list='recentchanges', rcprop='', rclimit=1, rcshow='patrolled', rctitle=title, rcend=timestamp)
    if 'errors' not in r and r['query']['recentchanges']:
        return 'Страница уже была отпатрулирована.'

    if not rev_id:
        return

    r = await api_request(lang, action='query', prop='revisions', rvprop='sha1', rvlimit=1, rvstartid=rev_id, rvexcludeuser=author, titles=title)
    page = r['query']['pages'][0]
    if 'missing' in page:
        return 'Страница уже была удалена.'
    if not 'revisions' in page: # авторы предыдущих правок скрыты или страница переименована
        return
    sha1 = page['revisions'][0].get('sha1') # хэш предыдущей правки, сделанной не целевым юзером
    if not sha1: # содержимое скрыто
        return

    # проверка, повторяется ли старый хэш после этой правки
    r = await api_request(lang, action='query', prop='revisions', rvprop='sha1', rvlimit='max', rvendid=rev_id, titles=title)
    page = r['query']['pages'][0]
    if 'missing' in page:
        return 'Страница уже была удалена.'
    if any(revision.get('sha1') == sha1 for revision in page['revisions']):
        return 'Правка уже была отменена.'

async def do_action(interaction: Interaction, action: Literal['rollback', 'rfd', 'undo'], reason: str | None = None) -> int | str:
    """Выполнение действия с правкой."""
    actor, _, embed = get_interaction_data(interaction)
    lang, title, author, _ = get_embed_data(embed)

    if result := await check_edit(embed):
        return result

    summary = SUMMARY[action][lang].format(get_contribs_link(author), get_user_link(actor), reason.replace('$1', title) if reason else None)
    timestamp = str(now()).replace('+00:00', 'Z')

    rollback = action == 'rollback' and lang not in ['be', 'c'] # TODO: в bewiki нет флага откатывающего, на commons ещё не дали
    token_type = 'rollback' if rollback else 'csrf'
    token = (await api_request(lang, 'GET', action='query', meta='tokens', type=token_type))['query']['tokens'][token_type + 'token']

    if rollback:
        r = await api_request(lang, action='rollback', title=title, user=author, summary=summary, watchlist='nochange', token=token)
        if not r.get('errors'):
            return r['rollback']['revid']
    elif action == 'rfd':
        prepend = True
        nocreate = True
        if lang == 'wd' and ':' not in title:
            reason = f'{{{{subst:rfd|{title}|{template_regexp.fullmatch(reason).group(2)}.}}}} <small>Posted by bot on the request of [{{{{fullurl:User:{actor}}}}} {actor}].</small> --~~~~'
            title = 'Wikidata:Requests for deletions'
            prepend = False
        elif 'merge' in reason:
            # probably implement merge logic here
            title = 'Talk:' + title
            nocreate = None

        r = await api_request(lang, action='edit', title=title, summary=summary, prependtext = reason + '\n\n' if prepend else None, appendtext = None if prepend else '\n\n' + reason, nocreate=nocreate, token=token)
        if not r.get('errors'):
            return r['edit']['newrevid']
    else:
        r = await api_request(lang, action='query', prop='revisions', rvprop='ids', rvlimit=1, rvuser=author, titles=title)
        page = r['query']['pages'][0]
        if 'missing' in page:
            return 'Страница уже была удалена.'
        if not 'revisions' in page:
            return 'Правка была скрыта.'
        new_id = page['revisions'][0]['revid']
        r = await api_request(lang, action='query', prop='revisions', rvprop='ids', rvlimit=1, rvstartid=new_id, rvexcludeuser=author, titles=title) # TODO: повторяется запрос из revision_check
        page = r['query']['pages'][0]
        if 'missing' in page:
            return 'Страница уже была удалена.'
        if not 'revisions' in page:
            return 'Отмена невозможна — последняя версия другого участника скрыта или страница была переименована.'
        old_id = page['revisions'][0]['revid']
        r = await api_request(lang, action='edit', title=title, undo=new_id, undoafter=old_id, summary=summary, starttimestamp=timestamp, nocreate=True, watchlist='nochange', token=token)
        if not r.get('errors'):
            if 'nochange' in r['edit']:
                return 'Правка уже была отменена.'
            return r['edit']['newrevid']

    error = r['errors'][0]
    code = error['code']
    if code in ['readonly', 'noapiwrite']:
        return 'Действие невозможно — правки в разделе отключены.'
    if code in ['hookaborted', 'spamdetected'] or 'abusefilter' in code:
        info = f': {error["abusefilter"]["description"]} (ID {error["abusefilter"]["id"]})' if 'abusefilter' in error else ''
        return f'Действие невозможно — отклонено фильтром злоупотреблений или другим расширением{info}.'
    if code in ['noimageredirect', 'noedit'] or 'denied' in code:
        return 'Действие невозможно — у бота не хватает прав.'
    if 'blocked' in code:
        return 'Действие невозможно — учётная запись бота была заблокирована.'
    if code == 'ratelimited':
        return 'Действие временно невозможно — правки рейтлимитированы.'
    if 'protected' in code:
        return 'Действие невозможно — страница была защищена.'
    if code in ['missingtitle', 'pagedeleted']:
        return 'Страница уже была удалена.'
    if code == 'alreadyrolled':
        return 'Откат невозможен — страница была отредактирована другим участником.'
    if code == 'notvisiblerev':
        return 'Откат невозможен — последняя версия другого участника скрыта.'
    if code in ['onlyauthor', 'revwrongpage']:
        return 'Действие невозможно — страница была переименована.'
    if code == 'editconflict':
        return 'Действие временно невозможно — произошёл конфликт редактирования.'
    if code == 'undofailure':
        return 'Отмена невозможна — промежуточные изменения несовместимы.'
    if code == 'nosuchrevid':
        return 'Правка уже была удалена.'
    return 'Действие невозможно: ' + error['text']

class MainView(View):
    """Главная панель управления."""
    interaction_check = check_rights

    def __init__(self, embed: Embed = None, disable: bool = False):
        super().__init__(timeout=None)
        self.rollback.disabled = self.undo.disabled = (embed and 'ilu=' not in embed.url) or disable
        self.rfd.disabled = self.good.disabled = self.bad.disabled = disable

    @button(emoji='⏮️', style=ButtonStyle.danger, custom_id='btn_rollback')
    async def rollback(self, interaction: Interaction, _: Button):
        await interaction.response.defer(ephemeral=True)
        r = await do_action(interaction, action='rollback')
        await result_handler(r, interaction, 'rollback')

    @button(emoji='🗑️', style=ButtonStyle.danger, custom_id='btn_rfd')
    async def rfd(self, interaction: Interaction, _: Button):
        await interaction.response.defer(ephemeral=True) # TODO: почему если это убрать, то вызываемая панель сначала отключена?
        await interaction.message.edit(view=ReasonSelectView(type='rfd'))

    @button(emoji='↪️', style=ButtonStyle.blurple, custom_id='btn_undo')
    async def undo(self, interaction: Interaction, _: Button):
        await interaction.response.defer(ephemeral=True) # TODO: почему если это убрать, то вызываемая панель сначала отключена?
        await interaction.message.edit(view=ReasonSelectView(type='undo'))

    @button(emoji='👍🏻', style=ButtonStyle.green, custom_id='btn_good')
    async def good(self, interaction: Interaction, _: Button):
        await result_handler(0, interaction, 'good')

    @button(emoji='💩', style=ButtonStyle.green, custom_id='btn_bad')
    async def bad(self, interaction: Interaction, _: Button):
        await result_handler(0, interaction, 'bad')

class ReasonSelectView(View):
    """Панель выбора причины отмены или номинации на КБУ."""
    interaction_check = check_rights

    def __init__(self, type: Literal['rfd', 'undo']):
        super().__init__(timeout=None)
        self.type = type
        self.select.options = [SelectOption(label=option) for option in REASONS[type]]
        self.select.custom_id = f'{type}_select'

    @select(placeholder='Причина?')
    async def select(self, interaction: Interaction, _: Select):
        embed = interaction.message.embeds[0]
        selected = list(interaction.data.values())[0][0]

        await interaction.message.edit(view=MainView(embed=embed, disable=True))

        reason = REASONS[self.type][selected].get(get_lang(embed.url), '')

        if selected == 'своя причина' or selected == 'Форк':
            modal = CustomReasonModal(type=self.type, template=reason if self.type == 'rfd' else None)
            await interaction.response.send_modal(modal)
            await sleep(1)
            await interaction.message.edit(view=MainView(embed=embed))
            return

        await interaction.response.defer(ephemeral=True)

        if selected == 'закрыть':
            await interaction.message.edit(view=MainView(embed=embed))
            return

        r = await do_action(interaction, action=self.type, reason=reason)
        await result_handler(r, interaction, self.type)

class CustomReasonModal(Modal, title='Причина'):
    """Поле ввода причины отмены или номинации на КБУ."""
    reason = TextInput(placeholder='Причина...', label='Причина', min_length=2, max_length=100)

    def __init__(self, type: Literal['rfd', 'undo'], template: str | None = None):
        super().__init__(timeout=60)
        self.type = type
        self.template = template

    async def on_submit(self, interaction: Interaction):
        await interaction.response.defer(ephemeral=True)
        reason = str(self.reason)
        if self.template:
            reason = self.template.replace('$1', reason)
        r = await do_action(interaction, action=self.type, reason=reason)
        await result_handler(r, interaction, self.type)

async def result_handler(result: int | str, interaction: Interaction, action: Literal['rollback', 'rfd', 'undo', 'good', 'bad']) -> None:
    """Обработчик результата действия через бота."""
    actor, msg, embed = get_interaction_data(interaction)
    log = client.get_channel(SOURCE_CHANNEL)
    if isinstance(result, int):
        await log.send(content=LOG_TEMPLATES[action].format(actor, get_page_link(embed, result)))
        await db_record_action(actor, action if action == 'rfd' else 'approves' if action in ['good', 'bad'] else action + 's', embed, bad=action == 'good')
        await msg.delete()
    elif 'невозмож' in result:
        await msg.edit(embed=embed.set_footer(text=result), view=MainView(embed=embed))
    else:
        await msg.edit(embed=Embed(color=embed.color, title=result), view=None, delete_after=5)

async def wiki_name(interaction: Interaction, current: str) -> list[Choice[str]]:
    """Автокомплит возможных имён участников."""
    return [Choice(name=user, value=user) for user in USERS.values() if user.lower().startswith(current.lower())][:25]

@tree.error
async def on_error(interaction: Interaction, error: Exception) -> None:
    """Обработчик ошибок в командах."""
    if 'autocomplete' not in str(error):
        raise error

@tree.command(name='help')
async def help(interaction: Interaction) -> None:
    """Список команд бота."""
    await interaction.response.send_message(ephemeral=True, content=
        '</help:1388457659484733501> — список команд бота.\n'
        '</clear_feed:1388459883858493526> — очистка каналов от всех сообщений бота.\n'
        '</last_metro:1320698385241739311> — время последнего запуска бота <#1220480407796187330>.\n\n'
        '</users:1388459883858493527> — список участников, кому разрешены действия через бота.\n'
        '</users_add:1388459883858493528> — разрешить участнику действия через бота.\n'
        '</users_remove:1388459883858493529> — запретить участнику действия через бота.\n\n'
        '</stats:1388459883858493523> — статистика всех действий через бота.\n'
        '</stats_user:1388459883858493524> — статистика действий участника через бота.\n'
        '</stats_user_remove:1388459883858493525> — удалить статистику действий участника.\n\n'
        'По вопросам работы бота обращайтесь к <@512545053223419924>.')

@stats.command(name='list')
@autocomplete(wiki_name=wiki_name)
async def stats_list(interaction: Interaction, wiki_name: str | None = None) -> None:
    """Просмотреть статистику действий через бота.

    Parameters
    -----------
    wiki_name: str | None
        Имя участника в Википедии
    """
    await interaction.response.defer(ephemeral=True)
    r = await db_get_stats(actor=wiki_name)
    if len(r) == 0:
        return
    if not wiki_name:
        total = '\n'.join(f'{row[0]}: {row[1]}' for row in r['total'])
        false_triggers = r['false_triggers']
        patterns = f'{false_triggers[0] / (r["patterns"] - 172) * 100:.3f}'
        lw = f'{false_triggers[1] / (r["LW"] - 1061) * 100:.3f}'
        ores = f'{false_triggers[2] / (r["ORES"] - 1431) * 100:.3f}'
        tags = f'{false_triggers[3] / (r["tags"] - 63) * 100:.3f}'
        replaces = f'{false_triggers[4] / r["replaces"] * 100:.3f}'
        await interaction.followup.send(ephemeral=True, content=
            f'Через бота совершено действий: откатов — {r["rollbacks"]}, отмен — {r["undos"]}, одобрений/отклонений правок — {r["approves"]}, номинаций на КБУ — {r["rfd"]}.\n'
            f'Наибольшее количество действий совершили:\n{total}\n'
            f'Действий по типам причин: паттерны — {r["patterns"]}, ORES — {r["ORES"]}, LW — {r["LW"]}, теги — {r["tags"]}, замены — {r["replaces"]}.\n'
            f'Ложные триггеры, c 21.07.2024: паттерны — {false_triggers[0]} ({patterns} %), LW — {false_triggers[1]} ({lw} %), ORES — {false_triggers[2]} ({ores} %), теги — {false_triggers[3]} ({tags} %), замены — {false_triggers[4]} ({replaces} %).')
    elif r['rollbacks'] > 0:
        await interaction.followup.send(ephemeral=True, content=
            f'Через бота участник {wiki_name} совершил действий: {r["rollbacks"] + r["undos"] + r["rfd"] + r["approves"]},\n'
            f'из них: откатов — {r["rollbacks"]}, отмен — {r["undos"]}, одобрений/отклонений правок — {r["approves"]}, номинаций на КБУ — {r["rfd"]}.\n'
            f'Действий по типам причин, за всё время: паттерны — {r["patterns"]}, замены — {r["replaces"]}, ORES — {r["ORES"]}, LW — {r["LW"]}, теги — {r["tags"]}.')
    else:
        await interaction.followup.send(ephemeral=True, content='Данный участник не совершал действий через бота.')

@stats.command(name='remove')
@autocomplete(wiki_name=wiki_name)
@admin
async def stats_user_remove(interaction: Interaction, wiki_name: str) -> None:
    """Удалить статистку действий участника через бота.

    Parameters
    -----------
    wiki_name: str
        Имя участника в Википедии
    """
    await interaction.response.defer(ephemeral=True)
    await db_remove_user(wiki_name)
    await interaction.followup.send(ephemeral=True, content='Статистика участника удалена.',)

@tree.command(name='last_metro')
async def last_metro(interaction: Interaction) -> None:
    """Узнать время последнего запуска бота #metro."""
    await interaction.response.defer(ephemeral=True)
    r = await session.get(url='https://rv.toolforge.org/metro/')
    r = await r.text()
    await interaction.followup.send(ephemeral=True, content=r.split('<br>')[0].replace('Задание запущено', 'Последний запуск задания:'))

@tree.command(name='clear_feed')
@admin
async def clear_feed(interaction: Interaction) -> None:
    """Очистка каналов от сообщений бота."""
    await interaction.response.send_message(ephemeral=True, content='Очистка каналов начата.')
    for channel_id in [SOURCE_CHANNEL, *STREAM_CHANNELS.values()]:
        channel = client.get_channel(channel_id)
        messages = channel.history(limit=1000)
        async for msg in messages:
            if msg.author.id not in [BOT, SOURCE_BOT] or len(msg.embeds) == 0:
                continue
            await msg.delete()
            await sleep(2)

@users.command(name='list')
async def users_list(interaction: Interaction) -> None:
    """Просмотр списка участников, которые могут использовать бота."""
    rights_content = ', '.join(f'<@{discord_id}> ({wiki_name})' for discord_id, wiki_name in USERS.items())
    await interaction.response.send_message(ephemeral=True, content=f'Действия через бота разрешены участникам {rights_content}.\nДля запроса права или отказа от него обратитесь к участнику <@512545053223419924>.')

@users.command(name='add')
@admin
async def users_add(interaction: Interaction, user: User, wiki_name: str) -> None:
    """Добавление участника в список тех, кто может использовать бота.

    Parameters
    -----------
    user: User
        Аккаунт в Discord
    wiki_name: str
        Имя участника в Википедии
    """
    if interaction.user.id == user.id:
        return
    await interaction.response.defer(ephemeral=True)
    global USERS
    if user.id in USERS:
        await interaction.followup.send(ephemeral=True, content=f'Участник {wiki_name} уже присутствует в списке доверенных.')
        return
    USERS[user.id] = wiki_name
    admin_name = USERS.get(interaction.user.id, '')
    await update_users('add', wiki_name, admin_name)
    await interaction.followup.send(ephemeral=True, content=f'Участник {wiki_name} добавлен в список доверенных.')

@users.command(name='remove')
@autocomplete(wiki_name=wiki_name)
@admin
async def users_remove(interaction: Interaction, wiki_name: str) -> None:
    """Удаление участника из списка тех, кто может использовать бота.

    Parameters
    -----------
    wiki_name: str
        Имя участника в Википедии
    """
    await interaction.response.defer(ephemeral=True)
    global USERS
    discord_ids = [key for key, value in USERS.items() if value == wiki_name]
    if len(discord_ids) == 0:
        await interaction.followup.send(ephemeral=True, content=f'Участника {wiki_name} не было в списке доверенных.')
        return
    admin_name = USERS.get(interaction.user.id, '')
    for discord_id in discord_ids:
        del USERS[discord_id]
    await update_users('remove', wiki_name, admin_name)
    await interaction.followup.send(ephemeral=True, content=f'Участник {wiki_name} убран из списка доверенных.')

@tasks.loop(seconds=30.0)
async def loop(client: Client) -> None:
    """Фоновый цикл."""
    await fetch_users()

    ads = await db_get_expired_ads()
    for ad in ads:
        msg = await client.get_channel(ad['channel_id']).fetch_message(ad['message_id'])
        await msg.delete()
        await sleep(3.0)

    for channel_id in STREAM_CHANNELS.values():
        messages = client.get_channel(channel_id).history(limit=20)
        async for msg in messages:
            if msg.author.id != BOT or len(msg.embeds) == 0:
                continue
            if await check_edit(msg.embeds[0]):
                await msg.delete()
                await sleep(3.0)

async def ad_message(msg: Message) -> None:
    """Обработка автоудаляемого сообщения."""
    for webhook in await msg.channel.webhooks():
        if webhook.name != 'ADMessages':
            continue
        attachments = []
        for attachment in msg.attachments:
            attachments.append(attachment.proxy_url)
        attachments = '\n' + '\n'.join(attachments) if len(attachments) > 0 else ''
        new_msg_wh = await webhook.send(content=f'{msg.content[4:] + attachments}', username=msg.author.display_name, avatar_url=msg.author.avatar.url, wait=True)
        await db_store_ad_message(f'{new_msg_wh.channel.id}|{new_msg_wh.id}')
    await msg.delete()

@client.event
async def on_message(msg: Message) -> None:
    """Получение нового сообщения."""
    if msg.content.startswith('/ad ') or msg.content == '/ad':
        await ad_message(msg)
        return
    if len(msg.embeds) == 0 or msg.author.id != SOURCE_BOT or msg.channel.id != SOURCE_CHANNEL:
        return
    embed = msg.embeds[0]
    if await check_edit(embed):
        await msg.delete()
        return
    stream = client.get_channel(STREAM_CHANNELS[get_lang(embed.url)])
    new_message = await stream.send(embed=embed, view=MainView(embed=embed, disable=True))
    await msg.delete()
    await sleep(3)
    await new_message.edit(view=MainView(embed=embed))

@client.event
async def on_ready() -> None:
    """Запуск бота."""
    for guild in client.guilds:
        if guild.id not in SERVERS:
            await guild.leave()
    await tree.sync()

    client.add_view(MainView())
    client.add_view(ReasonSelectView(type='rfd'))
    client.add_view(ReasonSelectView(type='undo'))

    await client.change_presence(status=Status.online, activity=Game('pyCharm'))

    global session, api_session
    api_session = ClientSession(headers=HEADERS)
    del HEADERS['Authorization']
    session = ClientSession(headers=HEADERS)

    await fetch_users()

    logging.warning('Запуск фоновых задач')
    loop.start(client)

    logging.warning('Включение ошибочно отключённых панелей')
    for channel_id in STREAM_CHANNELS.values():
        messages = client.get_channel(channel_id).history(limit=100)
        async for msg in messages:
            if msg.author.id != BOT or len(msg.embeds) == 0:
                continue
            view = View.from_message(msg)
            if len(view.children) == 5 and view.children[4].disabled:
                await msg.edit(view=MainView(embed=msg.embeds[0]))

    logging.warning('Просмотр пропущенных записей лога')
    messages = client.get_channel(SOURCE_CHANNEL).history(limit=500)
    async for msg in messages:
        if len(msg.embeds) > 0:
            await on_message(msg)

    logging.warning('Проверка наличия вебхуков')
    # создание вебхуков (для авто-удаляемых сообщений) во всех текстовых каналов, где ещё не созданы
    for wh_channel in client.get_all_channels():
        if wh_channel.type == ChannelType.text:
            webhooks = await wh_channel.webhooks()
            wh_check = False
            for webhook in webhooks:
                if webhook.name == 'ADMessages':
                    wh_check = True
            if not wh_check:
                wh = await wh_channel.create_webhook(name='ADMessages')
                logging.warning(wh)

    logging.warning('Бот запущен')

@client.event
async def on_guild_join(guild: Guild) -> None:
    """Присоединение бота к новому серверу."""
    if guild.id not in SERVERS:
        await guild.leave()

client.run(token=DISCORD_TOKEN, log_formatter=logging.Formatter(fmt='%(asctime)s - %(levelname)s - %(message)s'), log_level=logging.WARNING, root_logger=True)
