"""Модуль антивандальной системы."""

import json
import re
import logging
from asyncio import sleep
from urllib.parse import unquote
from aiohttp import ClientSession
from typing import Literal, Coroutine
from discord import SelectOption, Client, Embed, Message, Interaction, User, ButtonStyle, ChannelType, TextChannel
from discord.app_commands import CommandTree, Group, Choice, autocomplete
from discord.ui import Button, button, View, Select, select, TextInput, Modal
from antivand_utils import BOT_NAME, HEADERS, WAB2_HEADERS, DOMAINS, SOURCE_CHANNEL, STREAM_CHANNELS, ADMINS, BOT, SOURCE_BOT, USERS_PAGE, UPDATE_USERS_SUMMARY, LOG_TEMPLATES, SUMMARY, REASONS, now, get_cursor, to_thread, set_session, admin

USERS = []
DISCORD_TO_WIKI = {}

users = Group(name='users', description='Команды, затрагивающие список доверенных пользователей.')
stats = Group(name='stats', description='Команды, затрагивающие статистику действий через антивандальную систему')

template_regexp = re.compile(r'{{(.+)\|(.+)}}')
author_regexp = re.compile(r'https://\w+\.\w+\.org/wiki/special:contribs/(\S+)')
title_regexp = re.compile(r'\[hist\]\(<https://\w+\.\w+\.org/wiki/special:history/(\S+)>\)')

session, api_session, wab2_session = None, None, None

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

def get_globuser_link(user: str) -> str:
    """Получение викиссылки на глобальную учётную запись участника."""
    return f'[[Special:CentralAuth/{user}|{user}]]'

def get_contribs_link(user: str) -> str:
    """Получение викиссылки на вклад участника."""
    return f'[[Special:Contribs/{user}|{user}]]'

def get_page_link(embed: Embed, rev_id: int = 0) -> str:
    """Получение Markdown-ссылки на страницу или версию страницы для использования в Дискорде."""
    url = f'{get_domain(get_lang(embed.url))}/w/index.php?diff={rev_id}' if rev_id else embed.url
    return f'[{embed.title}](<{url}>)'

def get_embed_data(embed: Embed) -> tuple[str | None, str | None, str | None, str | None]:
    """Получение данных о правке из эмбеда."""
    diff_url = embed.url
    if not diff_url:
        return None, None, None, None
    lang = get_lang(diff_url)
    title = unquote(title_regexp.search(embed.description).group(1))
    author = unquote(author_regexp.fullmatch(embed.author.url).group(1))
    rev_id = diff_url.split('ilu=')[1] if 'ilu' in diff_url else None
    return lang, title, author, rev_id

async def request(func: Coroutine, type: Literal['text', 'JSON'] = 'text'):
    """Сетевой запрос, повторяемый несколько раз в случае ошибки."""
    for _ in range(5):
        try:
            res = await func()
            if not res.ok:
                raise Exception(str(res.status))
            return await (res.json() if type == 'JSON' else res.text())
        except Exception as e:
            if _ == 4:
                raise e
            await sleep(1)

async def api_request(lang: str, method: Literal['GET', 'POST'] = 'POST', **kwargs) -> dict:
    """Запрос к API раздела."""
    url = f'{get_domain(lang)}/w/api.php'
    params = {key: value for key, value in kwargs.items() if value is not None} | {'assertuser': BOT_NAME, 'errorformat': 'plaintext', 'errorlang': 'ru', 'format': 'json', 'formatversion': 2}
    return await request(lambda: api_session.get(url, params=params) if method == 'GET' else api_session.post(url, data=params), 'JSON')

async def get_page_text(lang: str, title: str) -> str:
    """Получение викитекста страницы."""
    return await request(lambda: session.get(f'{get_domain(lang)}/w/index.php', params={'title': title, 'action': 'raw'}))

async def get_user(discord_id: int) -> str:
    """Получение имени участника в Википедии по айди Дискорда."""
    if discord_id not in DISCORD_TO_WIKI:
        wiki_id = await request(lambda: wab2_session.get(f'https://wikiauthbot-ng.toolforge.org/whois/{discord_id}'))
        r = await api_request('m', action='query', meta='globaluserinfo', guiid=wiki_id)
        if r.get('errors'):
            DISCORD_TO_WIKI[discord_id] = ''
        else:
            DISCORD_TO_WIKI[discord_id] = r['query']['globaluserinfo']['name']
    return DISCORD_TO_WIKI[discord_id]

async def fetch_users() -> None:
    """Получение списка доверенных участников из внешних источников."""
    global USERS
    USERS = await db_sort_users(json.loads(await get_page_text('m', USERS_PAGE)))

async def update_users(action: Literal['add', 'remove'], user: str, admin: str) -> None:
    """Обновление списка доверенных участников во внешних источниках."""
    token = (await api_request('m', 'GET', action='query', meta='tokens', type='csrf'))['query']['tokens']['csrftoken']
    summary = UPDATE_USERS_SUMMARY[action].format(get_globuser_link(user), get_globuser_link(admin))
    r = await api_request('m', action='edit', title=USERS_PAGE, text=json.dumps(sorted(USERS)), summary=summary, token=token)
    if r.get('errors'):
        raise Exception(r['errors'][0]['text'])

@to_thread
def db_record_action(actor: str, action_type: str, embed: Embed, bad: bool = False) -> None:
    """Запись действия в БД."""
    triggers_dict = {'#ff0000': 'patterns', '#ffff00': 'LW', '#ff00ff': 'ORES', '#00ff00': 'tags', '#ffffff': 'replaces', '#ff8000': 'LW', '#00ffff': 'replaces'}
    color = str(embed.color)
    trigger = triggers_dict[color] if color in triggers_dict else 'unknown'
    with get_cursor() as cur:
        cur.execute('SELECT name FROM ds_antivandal WHERE name=%s', actor)
        res = cur.fetchone()
        if res:
            cur.execute(f'UPDATE ds_antivandal SET {action_type} = {action_type}+1, {trigger} = {trigger}+1 WHERE name = %s', actor)
        else:
            cur.execute(f'INSERT INTO ds_antivandal (name, {action_type}, {trigger}) VALUES (%s, 1, 1)', actor)
        if bad:
            cur.execute(f'UPDATE ds_antivandal_false SET {trigger} = {trigger}+1 WHERE result = "stats"')

@to_thread
def db_get_stats(actor: str | None, limit: int) -> dict[Literal['rollbacks', 'undos', 'approves', 'rfd', 'total', 'patterns', 'LW', 'ORES', 'tags', 'replaces', 'false_triggers'], int | list[tuple[str, int]] | list[int]]:
    """Получение статистики из БД."""
    with get_cursor() as cur:
        if not actor:
            cur.execute('SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES), SUM(tags), SUM(rfd), SUM(replaces) FROM ds_antivandal')
            r = cur.fetchone()
            cur.execute('SELECT name, rollbacks + undos + approves + rfd AS am FROM ds_antivandal WHERE name != "service_account" ORDER BY am DESC LIMIT %s', limit or 100)
            total = cur.fetchall()
            cur.execute('SELECT patterns, LW, ORES, tags, replaces FROM ds_antivandal_false WHERE result = "stats"')
            false_triggers = cur.fetchone()
        else:
            cur.execute('SELECT rollbacks, undos, approves, patterns, LW, ORES, tags, rfd, replaces FROM ds_antivandal WHERE name=%s', actor)
            r = cur.fetchone()
            total = []
            false_triggers = []
    if r:
        return {'rollbacks': r[0], 'undos': r[1], 'approves': r[2], 'rfd': r[7], 'total': total, 'patterns': r[3], 'LW': r[4], 'ORES': r[5], 'tags': r[6], 'replaces': r[8], 'false_triggers': false_triggers}
    return {'rollbacks': 0, 'undos': 0, 'approves': 0, 'rfd': 0, 'patterns': 0, 'LW': 0, 'ORES': 0, 'tags': 0, 'replaces': 0}

@to_thread
def db_remove_user(actor: str) -> None:
    """Удаление статистики пользователя из БД."""
    with get_cursor() as cur:
        cur.execute('DELETE FROM ds_antivandal WHERE name=%s', actor)

@to_thread
def db_sort_users(users: list[str]) -> dict[int, str]:
    """Получение списка доверенных участников, отсортированного по количеству действий."""
    with get_cursor() as cur:
        cur.execute('SELECT name FROM ds_antivandal ORDER BY rollbacks + undos + approves + rfd DESC, name ASC')
        sorted_users = [row[0] for row in cur.fetchall()]
        return [user for user in sorted_users if user in users] + [user for user in users if not user in sorted_users]

async def check_rights(_: View, interaction: Interaction) -> bool:
    """Проверка, что пользователь может пользоваться антивандальной системой."""
    if await get_user(interaction.user.id) not in USERS:
        await interaction.response.send_message(ephemeral=True, content=f'К сожалению, у вас нет прав на использование антивандальной системы. Обратитесь к участнику <@{ADMINS[0]}>, если желаете их получить.')
        return False
    return True

async def check_edit(embed: Embed) -> str | None:
    """Проверка, не была ли правка уже обработана."""
    lang, title, author, rev_id = get_embed_data(embed)
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

async def do_wiki_action(embed: Embed, actor: str, action: Literal['rollback', 'rfd', 'undo'], reason: str | None = None) -> int | str:
    """Обработка правки внутри Википедии."""
    if result := await check_edit(embed):
        return result

    lang, title, author, _ = get_embed_data(embed)
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
    if code == 'assertnameduser':
        return 'Действие невозможно — ошибка при аутентификации бота. Обратитесь к участнику Well very well.'
    if code in ['readonly', 'noapiwrite']:
        return 'Действие невозможно — правки в разделе отключены.'
    if code in ['hookaborted', 'spamdetected'] or 'abusefilter' in code:
        info = f': {error['abusefilter']['description']} (ID {error['abusefilter']['id']})' if 'abusefilter' in error else ''
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

    def __init__(self, embed: Embed = None):
        super().__init__(timeout=None)
        self.rollback.disabled = self.undo.disabled = embed and 'ilu=' not in embed.url

    @button(emoji='⏮️', style=ButtonStyle.danger, custom_id='btn_rollback')
    async def rollback(self, interaction: Interaction, _: Button):
        await do_action(interaction, 'rollback')

    @button(emoji='🗑️', style=ButtonStyle.danger, custom_id='btn_rfd')
    async def rfd(self, interaction: Interaction, _: Button):
        await interaction.response.defer() # TODO: почему если это убрать, то вызываемая панель сначала отключена?
        await interaction.message.edit(view=ReasonSelectView('rfd'))

    @button(emoji='↪️', style=ButtonStyle.blurple, custom_id='btn_undo')
    async def undo(self, interaction: Interaction, _: Button):
        await interaction.response.defer() # TODO: почему если это убрать, то вызываемая панель сначала отключена?
        await interaction.message.edit(view=ReasonSelectView('undo'))

    @button(emoji='👍🏻', style=ButtonStyle.green, custom_id='btn_good')
    async def good(self, interaction: Interaction, _: Button):
        await do_action(interaction, 'good')

    @button(emoji='💩', style=ButtonStyle.green, custom_id='btn_bad')
    async def bad(self, interaction: Interaction, _: Button):
        await do_action(interaction, 'bad')

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
        reason = REASONS[self.type][selected].get(get_lang(embed.url), '')

        await interaction.message.edit(view=MainView(embed))

        if selected == 'своя причина' or selected == 'Форк':
            modal = CustomReasonModal(self.type, reason if self.type == 'rfd' else None)
            await interaction.response.send_modal(modal)
            return
        if selected == 'закрыть':
            await interaction.response.defer()
            return

        await do_action(interaction, self.type, reason)

class CustomReasonModal(Modal, title='Причина'):
    """Поле ввода причины отмены или номинации на КБУ."""
    reason = TextInput(placeholder='Причина...', label='Причина', min_length=2, max_length=400)

    def __init__(self, type: Literal['rfd', 'undo'], template: str | None):
        super().__init__()
        self.type = type
        self.template = template or '$1'

    async def on_submit(self, interaction: Interaction):
        await do_action(interaction, self.type, self.template.replace('$1', str(self.reason)))

async def do_action(interaction: Interaction, action: Literal['rollback', 'rfd', 'undo', 'good', 'bad'], reason: str | None = None) -> None:
    """Действие через систему."""
    await interaction.response.defer()

    actor = await get_user(interaction.user.id)
    msg = interaction.message
    embed = msg.embeds[0]

    await msg.edit(embed=embed.remove_footer(), view=MainView(embed))

    result = 0 if action in ['good', 'bad'] else await do_wiki_action(embed, actor, action, reason)

    if isinstance(result, str):
        if 'невозмож' in result:
            await msg.edit(embed=embed.set_footer(text=result), view=MainView(embed))
        else:
            await msg.edit(embed=Embed(color=embed.color, title=result), view=None, delete_after=5)
        return
    
    log = interaction.client.get_channel(SOURCE_CHANNEL)
    await log.send(content=LOG_TEMPLATES[action].format(actor, get_page_link(embed, result)))
    await db_record_action(actor, action if action == 'rfd' else 'approves' if action in ['good', 'bad'] else action + 's', embed, bad=action == 'good')
    await msg.delete()

async def wiki_name(interaction: Interaction, current: str) -> list[Choice[str]]:
    """Автокомплит возможных имён участников."""
    return [Choice(name=user, value=user) for user in USERS if user.lower().startswith(current.lower())][:25]

@stats.command(name='list')
@autocomplete(wiki_name=wiki_name)
async def stats_list(interaction: Interaction, wiki_name: str | None = None, limit: int = 5) -> None:
    """Просмотреть статистику действий через антивандальную систему.

    Parameters
    -----------
    wiki_name: str | None
        Имя участника в Википедии. Если не указано, выдаётся общая статистика
    limit: int | None
        Размер топа участников (по умолчанию 5, 0 = все)
    """
    await interaction.response.defer(ephemeral=True)
    r = await db_get_stats(wiki_name, limit)
    if len(r) == 0:
        return
    if not wiki_name:
        total = '\n'.join(f'{row[0]}: {row[1]}' for row in r['total'])
        false_triggers = r['false_triggers']
        patterns = f'{false_triggers[0] / (r['patterns'] - 172) * 100:.3f}'
        lw = f'{false_triggers[1] / (r['LW'] - 1061) * 100:.3f}'
        ores = f'{false_triggers[2] / (r['ORES'] - 1431) * 100:.3f}'
        tags = f'{false_triggers[3] / (r['tags'] - 63) * 100:.3f}'
        replaces = f'{false_triggers[4] / r['replaces'] * 100:.3f}'
        await interaction.followup.send(ephemeral=True, content=
            f'Через систему совершено действий: откатов — {r['rollbacks']}, отмен — {r['undos']}, одобрений/отклонений правок — {r['approves']}, номинаций на КБУ — {r['rfd']}.\n'
            f'Наибольшее количество действий совершили:\n{total}\n'
            f'Действий по типам причин: паттерны — {r['patterns']}, ORES — {r['ORES']}, LW — {r['LW']}, теги — {r['tags']}, замены — {r['replaces']}.\n'
            f'Ложные триггеры, c 21.07.2024: паттерны — {false_triggers[0]} ({patterns} %), LW — {false_triggers[1]} ({lw} %), ORES — {false_triggers[2]} ({ores} %), теги — {false_triggers[3]} ({tags} %), замены — {false_triggers[4]} ({replaces} %).')
    elif r['rollbacks'] + r['undos'] + r['rfd'] + r['approves'] > 0:
        await interaction.followup.send(ephemeral=True, content=
            f'Через систему участник {wiki_name} совершил действий: {r['rollbacks'] + r['undos'] + r['rfd'] + r['approves']},\n'
            f'из них: откатов — {r['rollbacks']}, отмен — {r['undos']}, одобрений/отклонений правок — {r['approves']}, номинаций на КБУ — {r['rfd']}.\n'
            f'Действий по типам причин, за всё время: паттерны — {r['patterns']}, замены — {r['replaces']}, ORES — {r['ORES']}, LW — {r['LW']}, теги — {r['tags']}.')
    else:
        await interaction.followup.send(ephemeral=True, content='Данный участник не совершал действий через систему.')

@stats.command(name='remove')
@autocomplete(wiki_name=wiki_name)
@admin
async def stats_remove(interaction: Interaction, wiki_name: str) -> None:
    """Удалить статистку действий участника через антивандальную систему.

    Parameters
    -----------
    wiki_name: str
        Имя участника в Википедии
    """
    await interaction.response.defer(ephemeral=True)
    await db_remove_user(wiki_name)
    await interaction.followup.send(ephemeral=True, content='Статистика участника удалена.')

@admin
async def clear_feed(interaction: Interaction) -> None:
    """Очистка эмбедов антивандальной системы."""
    await interaction.response.send_message(ephemeral=True, content='Очистка каналов начата.')
    for channel_id in [SOURCE_CHANNEL, *STREAM_CHANNELS.values()]:
        channel = client.get_channel(channel_id)
        messages = channel.history(limit=1000)
        async for msg in messages:
            if msg.author.id not in [BOT, SOURCE_BOT] or len(msg.embeds) == 0:
                continue
            await msg.delete()

@users.command(name='list')
async def users_list(interaction: Interaction) -> None:
    """Просмотр списка участников, которые могут использовать антивандальную систему."""
    await interaction.response.send_message(ephemeral=True, content=f'Антивандальной системой могут пользоваться участники {', '.join(USERS)}.\nДля запроса права или отказа от него обратитесь к участнику <@{ADMINS[0]}>.')

@users.command(name='add')
@admin
async def users_add(interaction: Interaction, user: User) -> None:
    """Добавление участника в список тех, кто может использовать антивандальную систему.

    Parameters
    -----------
    user: User
        Участник
    """
    if interaction.user.id == user.id:
        return
    await interaction.response.defer(ephemeral=True)
    wiki_name = await get_user(user.id)
    if wiki_name in USERS:
        await interaction.followup.send(ephemeral=True, content=f'Участник {wiki_name} уже присутствует в списке доверенных.')
        return
    USERS.append(wiki_name)
    admin_name = await get_user(interaction.user.id)
    await update_users('add', wiki_name, admin_name)
    await interaction.followup.send(ephemeral=True, content=f'Участник {wiki_name} добавлен в список доверенных.')

@users.command(name='remove')
@autocomplete(wiki_name=wiki_name)
@admin
async def users_remove(interaction: Interaction, wiki_name: str) -> None:
    """Удаление участника из списка тех, кто может использовать антивандальную систему.

    Parameters
    -----------
    wiki_name: str | None
        Имя участника в Википедии
    """
    await interaction.response.defer(ephemeral=True)
    if wiki_name not in USERS:
        await interaction.followup.send(ephemeral=True, content=f'Участника {wiki_name} не было в списке доверенных.')
        return
    USERS.remove(wiki_name)
    admin_name = await get_user(interaction.user.id)
    await update_users('remove', wiki_name, admin_name)
    await interaction.followup.send(ephemeral=True, content=f'Участник {wiki_name} убран из списка доверенных.')

async def loop(client: Client) -> None:
    """Фоновый цикл антивандальной системы."""
    await fetch_users()
    for channel_id in STREAM_CHANNELS.values():
        messages = client.get_channel(channel_id).history(limit=20)
        async for msg in messages:
            if msg.author.id == BOT and len(msg.embeds) > 0 and await check_edit(msg.embeds[0]):
                await msg.delete()

async def on_message(client: Client, msg: Message) -> None:
    """Получение нового эмбеда."""
    if len(msg.embeds) == 0 or msg.author.id != SOURCE_BOT or msg.channel.id != SOURCE_CHANNEL:
        return
    embed = msg.embeds[0]
    if embed.url is None:
        return
    if await check_edit(embed):
        await msg.delete()
        return
    stream = client.get_channel(STREAM_CHANNELS[get_lang(embed.url)])
    await stream.send(embed=embed, view=MainView(embed))
    await msg.delete()

async def on_ready(client: Client, tree: CommandTree) -> None:
    """Запуск модуля."""
    tree.add_command(users)
    tree.add_command(stats)
    tree.command()(clear_feed)

    client.add_view(MainView())
    client.add_view(ReasonSelectView(type='rfd'))
    client.add_view(ReasonSelectView(type='undo'))

    global session, api_session, wab2_session
    session = ClientSession(headers={key: value for key, value in HEADERS.items() if key != 'Authorization'})
    api_session = ClientSession(headers=HEADERS)
    wab2_session = ClientSession(headers=WAB2_HEADERS)
    await set_session(session)

    await fetch_users()

    logging.warning('Просмотр пропущенных записей лога')
    messages = client.get_channel(SOURCE_CHANNEL).history(limit=500)
    async for msg in messages:
        if len(msg.embeds) > 0:
            await on_message(client, msg)
