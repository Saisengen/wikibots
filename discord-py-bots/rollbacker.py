"""Анти-вандальный бот"""

import asyncio
import os
import json
import logging
import time
import datetime
from urllib.parse import unquote
import discord
from discord import SelectOption, Embed
from discord.ext import commands
from discord.ui import Button, View, Select, TextInput, Modal
import pymysql
import toolforge
import aiohttp
from antivand_cleaner import revision_check, flagged_check


DEBUG = False
DB_CREDITS = {'user': os.environ['TOOL_TOOLSDB_USER'], 'port': 4711, 'host': '127.0.0.1',
              'password': os.environ['TOOL_TOOLSDB_PASSWORD'], 'database': f'{os.environ["TOOL_TOOLSDB_USER"]}__rv'}
TOKEN = os.environ['DISCORD_BOT_TOKEN']
BEARER_TOKEN = os.environ['BEARER_TOKEN']

# Целевой сервер, ID каналов с потоками, ID бота, ID ботов-источников, ID канала с командами,
# ID сообщения со списком откатывающих, ID канала с источником, список администраторов бота.
CONFIG = {'SERVER': [1044474820089368666], 'IDS': [1219273496371396681, 1212498198200062014],
          'BOT': 1225008116048072754, 'SOURCE_BOTS': [1237362558046830662, 1299324425878900818],
          'BOTCOMMANDS': 1212507148982947901, 'ROLLBACKERS': 1237790591044292680, 'SOURCE': 1237345566950948867,
          'ADMINS': [352826965494988822, 512545053223419924, 223219998745821194]}
USER_AGENT = {'User-Agent': 'D-V; iluvatar@tools.wmflabs.org; python3.11'}
STORAGE, ALLOWED_USERS = [], {}
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
    '14': ['своя причина', '', ''],  # не менять название пункта без изменения в callback
    '15': ['Закрыть', '', '']  # не менять название пункта без изменения в callback
}
options_undo, options_rfd = [], []
for option, index in select_options_undo.items():
    options_undo.append(SelectOption(label=index[0], value=str(option)))

select_options_rfd = {
    '1': ['Бессвязное содержимое', '{{уд-бессвязно}}', '{{Db-nonsense}}'],
    '2': ['Вандализм', '{{уд-ванд}}', '{{Db-vand}}'],
    '3': ['Тестовая страница', '{{уд-тест}}', '{{Db-test}}'],
    '4': ['Реклама / спам', '{{уд-реклама}}', '{{Db-spam}}'],
    '5': ['Пустая статья', '{{{уд-пусто}}', '{{Db-nocontext}}'],
    '6': ['На иностранном языке', '{{уд-иностр}}', '{{Db-lang}}'],
    '7': ['Нет значимости', '{{уд-нз}}', '{{Db-nn}}'],
    '8': ['своя причина', '', ''],  # не менять название пункта без изменения в callback
    '9': ['Закрыть', '', '']  # не менять название пункта без изменения в callback
}
for option, index in select_options_rfd.items():
    options_rfd.append(SelectOption(label=index[0], value=str(option)))


select_component_undo = Select(placeholder='Выбор причины отмены', min_values=1, max_values=1, options=options_undo,
                               custom_id='select_component_undo')
select_component_undo.callback = select_component_undo
select_component_rfd = Select(placeholder='Выбор причины КБУ', min_values=1, max_values=1, options=options_rfd,
                              custom_id='select_component_rfd')

undo_prefix = ['Бот: отмена правки [[Special:Contribs/$author|$author]] по запросу [[User:$actor|$actor]]:',
               'скасовано останнє редагування [[Special:Contribs/$author|$author]] за запитом [[User:$actor|$actor]]:']
rfd_summary = ['Бот: Номинация на КБУ по запросу [[User:$actor|$actor]]',
               'Номінація на швидке вилучення за запитом [[User:$actor|$actor]]']


class ReasonUndo(Modal, title='Причина'):
    """Строка ввода причины отмены."""
    res = TextInput(custom_id='menu_undo', label='Причина отмены', min_length=2, max_length=255,
                         placeholder='введите причину', required=True, style=discord.TextStyle.short)


    async def on_submit(self, interaction: discord.Interaction):
        await interaction_defer(interaction, '0.1')
        if not await check_rights(interaction):
            return
        actor, msg, channel = get_data(interaction)
        lang_selector = 0 if get_lang(msg.embeds[0].url) != 'uk' else 1

        reason = f'{undo_prefix[lang_selector].replace("$actor", actor)} {self.children[0].value}'

        r = await do_rollback(msg.embeds[0], actor, action_type='undo', reason=reason)
        await result_undo_handler(r, interaction)


class ReasonRFD(Modal, title='Причина'):
    """Строка ввода номинации на удаления."""
    res = TextInput(custom_id='menu_rfd', label='Причина КБУ', min_length=2, max_length=255,
                    placeholder='введите причину', required=True, style=discord.TextStyle.short)


    async def on_submit(self, interaction: discord.Interaction):
        await interaction_defer(interaction, '0.2')
        if not await check_rights(interaction):
            return
        actor, msg, channel = get_data(interaction)
        lang_selector = 0 if get_lang(msg.embeds[0].url) != 'uk' else 1
        summary = rfd_summary[lang_selector].replace('$actor', actor)
        r = await do_rfd(msg.embeds[0], rfd=self.children[0].value, summary=summary)
        await result_rfd_handler(r, interaction)


async def result_rfd_handler(r, interaction: discord.Interaction) -> None:
    actor, msg, channel = get_data(interaction)
    try:
        if r[0] == 'Success':
            await channel.send(content=f'{actor} номинировал {r[1]} на КБУ.')
            await send_to_db(actor, 'rfd', get_trigger(msg.embeds[0]))
            await msg.delete()
            return
        if 'не существует' in r[0]:
            new_embed = Embed(color=msg.embeds[0].color, title='Страница была удалена.')
            await interaction.message.edit(embed=new_embed, view=None, delete_after=12.0)
        else:
            if r[1] != '':
                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}, {r[1]}.')
            else:
                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}.')
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons())
    except Exception as e:
        print(f'Error 1.0: {e}')


async def result_undo_handler(r, interaction: discord.Interaction) -> None:
    actor, msg, channel = get_data(interaction)
    try:
        if r[0] == 'Success':
            await channel.send(content=f'{actor} выполнил отмену на странице {r[1]}.')
            await send_to_db(actor, 'undos', get_trigger(msg.embeds[0]))
            await msg.delete()
            return
        if 'были откачены' in r[0]:
            await send_to_db('service_account', 'undos', get_trigger(msg.embeds[0]))
            new_embed = Embed(color=msg.embeds[0].color, title='Страница была удалена, отпатрулирована или правки уже '
                                                               'были откачены.')
            await interaction.message.edit(embed=new_embed, view=None, delete_after=12.0)
        elif 'версии принадлежат' in r[0]:
            msg.embeds[0].set_footer(text='Отмена не удалась: все версии страницы принадлежат одному участнику.')
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons())
        else:
            if r[1] != '':
                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}, {r[1]}.')
            else:
                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}.')
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons())
    except Exception as e:
        print(f'Error 2.0: {e}')


def get_view_undo() -> View:
    res = Select(placeholder="Причина?", max_values=1, min_values=1, options=options_undo, custom_id='undo_select')

    async def callback(interaction: discord.Interaction):
        if not await check_rights(interaction):
            return
        selected = select_options_undo[list(interaction.data.values())[0][0]]
        actor, msg, channel = get_data(interaction)
        lang = get_lang(msg.embeds[0].url)
        try:
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons())
        except Exception as e:
            print(f'Error 3.0: {e}')

        if selected[0] == 'своя причина':
            try:
                await interaction.response.send_modal(ReasonUndo())
            except Exception as e:
                print(f'Error 4.0: {e}')
            return

        await interaction_defer(interaction, '0.3')
        if selected[0] == 'Закрыть':
            return

        lang_selector = 0 if lang != 'uk' else 1
        reason = (f'{undo_prefix[lang_selector].replace("$actor", actor)} '
                  f'{selected[lang_selector + 1].replace("$1", msg.embeds[0].title)}')
        r = await do_rollback(msg.embeds[0], actor, action_type='undo', reason=reason)
        await result_undo_handler(r, interaction)


    res.callback = callback
    view = View(timeout=None)
    return view.add_item(res)


def get_view_rfd() -> View:
    res = Select(placeholder="Причина?", max_values=1, min_values=1, options=options_rfd, custom_id='rfd_select')

    async def callback(interaction: discord.Interaction):
        if not await check_rights(interaction):
            return
        selected = select_options_rfd[list(interaction.data.values())[0][0]]
        actor, msg, channel = get_data(interaction)
        lang = get_lang(msg.embeds[0].url)
        try:
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons())
        except Exception as e:
            print(f'Error 5.0: {e}')

        if selected[0] == 'своя причина':
            try:
                await interaction.response.send_modal(ReasonRFD())
            except Exception as e:
                print(f'Error 6.0: {e}')
            return

        await interaction_defer(interaction, '0.4')
        if selected[0] == 'Закрыть':
            return

        lang_selector = 0 if lang != 'uk' else 1
        summary = rfd_summary[lang_selector].replace('$actor', actor)
        rfd_reason = selected[lang_selector + 1]
        r = await do_rfd(msg.embeds[0], rfd=rfd_reason, summary=summary)
        await result_rfd_handler(r, interaction)


    res.callback = callback
    view = View(timeout=None)
    return view.add_item(res)


def get_lang(url: str) -> str:
    """Получение кода языкового раздела из ссылки."""
    return 'ru' if 'ru.wikipedia.org' in url else 'uk'


def get_trigger(embed: Embed) -> str:
    """Получение причины реакции по цвету."""
    triggers_dict = {'#ff0000': 'patterns', '#ffff00': 'LW', '#ff00ff': 'ORES', '#00ff00': 'tags',
                     '#0000ff': 'replaces'}
    return 'unknown' if (color:=str(embed.color)) not in triggers_dict else triggers_dict[color]


async def check_rights(interaction: discord.Interaction) -> bool:
    if str(interaction.user.id) not in ALLOWED_USERS:
        try:
            await interaction.followup.send(
                content='К сожалению, у вас нет разрешение на выполнение откатов и отмен через бот. Обратитесь к '
                        f'участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Error 7.0: {e}')
            return False
        else:
            return False
    return True


def get_data(interaction: discord.Interaction):
    actor = ALLOWED_USERS[str(interaction.user.id)]
    msg = interaction.message
    channel = client.get_channel(CONFIG['SOURCE'])
    return actor, msg, channel


def get_view_buttons(disable: bool = False) -> View:
    """Формирование набора компонентов."""
    btn_rollback = Button(emoji='⏮️', style=discord.ButtonStyle.danger, custom_id="btn_rollback", disabled=disable)
    btn_undo = Button(emoji='↪️', style=discord.ButtonStyle.blurple, custom_id="btn_undo", disabled=disable)
    btn_rfd = Button(emoji='🗑️', style=discord.ButtonStyle.danger, custom_id="btn_rfd", disabled=disable)
    btn_good = Button(emoji='👍🏻', style=discord.ButtonStyle.green, custom_id="btn_good", disabled=disable)
    btn_bad = Button(emoji='💩', style=discord.ButtonStyle.green, custom_id="btn_bad", disabled=disable)


    async def rollback_handler(interaction: discord.Interaction):
        await interaction_defer(interaction, '0.5')
        if not await check_rights(interaction):
            return
        actor, msg, channel = get_data(interaction)
        if len(msg.embeds) == 0:
            return
        r = await do_rollback(msg.embeds[0], actor)
        try:
            if r[0] == 'Success':
                await interaction.message.delete()
                await channel.send(content=f'{actor} выполнил откат на странице {r[1]}.')
                await send_to_db(actor, 'rollbacks', get_trigger(msg.embeds[0]))
            else:
                if 'были откачены' in r[0]:
                    await send_to_db('service_account', 'rollbacks', get_trigger(msg.embeds[0]))
                    new_embed = Embed(color=msg.embeds[0].color, title='Страница была удалена, отпатрулирована или '
                                                                       'правки уже были откачены.')
                    await interaction.message.edit(embed=new_embed, view=None, delete_after=12.0)
                else:
                    footer_info = f'{r[0]}, {r[1]}' if r[1] != '' else f'{r[0]}'
                    if r[1] != '':
                        msg.embeds[0].set_footer(text=f'Действие не удалось: {footer_info}.')
                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons())
        except Exception as e:
            print(f'Error 8.0: {e}')

    async def rfd_handler(interaction: discord.Interaction):
        await interaction_defer(interaction, '0.6')
        if not await check_rights(interaction):
            return
        msg = interaction.message
        try:
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_rfd())
        except Exception as e:
            print(f'Error 9.0: {e}')

    async def undo_handler(interaction: discord.Interaction):
        await interaction_defer(interaction, '0.7')
        if not await check_rights(interaction):
            return
        view = View()
        view.add_item(select_component_undo)
        msg = interaction.message
        try:
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_undo())
        except Exception as e:
            print(f'Error 10.0: {e}')

    async def good_handler(interaction: discord.Interaction):
        await interaction_defer(interaction, '0.8')
        if not await check_rights(interaction):
            return
        try:
            actor, msg, channel = get_data(interaction)
            await interaction.message.delete()
            await channel.send(content=f'{actor} одобрил правку на странице '
                                       f'[{msg.embeds[0].title}](<{msg.embeds[0].url}>).')
            await send_to_db(actor, 'approves', get_trigger(msg.embeds[0]), bad=True)
        except Exception as e:
            print(f'Error 11.0: {e}')

    async def bad_handler(interaction: discord.Interaction):
        await interaction_defer(interaction, '0.9')
        if not await check_rights(interaction):
            return
        try:
            actor, msg, channel = get_data(interaction)
            await interaction.message.delete()
            await channel.send(
                content=f'{actor} отметил правку на странице [{msg.embeds[0].title}](<{msg.embeds[0].url}>) '
                        f'как неконструктивную, но уже отменённую.')
            await send_to_db(actor, 'approves', get_trigger(msg.embeds[0]))
        except Exception as e:
            print(f'Error 12.0: {e}')


    btn_rollback.callback = rollback_handler
    btn_rfd.callback = rfd_handler
    btn_undo.callback = undo_handler
    btn_good.callback = good_handler
    btn_bad.callback = bad_handler

    view_buttons = View(timeout=None)
    [view_buttons.add_item(i) for i in [btn_rollback, btn_undo, btn_rfd, btn_good, btn_bad]]
    return view_buttons


async def interaction_defer(interaction: discord.Interaction, error_description: str) -> None:
    try:
        await interaction.response.defer(ephemeral=True)
    except Exception as e:
        print(f'Error {error_description}: {e}')


async def send_to_db(actor: str, action_type: str, trigger: str, bad: bool = False) -> None:
    """Отправка в БД."""
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG else toolforge.toolsdb(DB_CREDITS['database'])
        with conn.cursor() as cur:
            if action_type in ['rollbacks', 'undos', 'approves', 'rfd']:
                cur.execute('SELECT name FROM ds_antivandal WHERE name=%s;', actor)
                res = cur.fetchall()
                if len(res) == 0:
                    cur.execute(f'INSERT INTO ds_antivandal (name, {action_type}, {trigger}) VALUES (%s, 1, 1);',
                                actor)
                else:
                    cur.execute(f'UPDATE ds_antivandal SET {action_type} = {action_type}+1, {trigger} = '
                                f'{trigger}+1 WHERE name = %s;', actor)
            conn.commit()
            if bad:
                cur.execute(f'UPDATE ds_antivandal_false SET {trigger} = {trigger}+1 WHERE result = "stats";')
                conn.commit()
        conn.close()
    except Exception as e:
        print(f'Error 13.0: {e}')


async def get_from_db(is_all: bool = True, actor: str = None):
    """Получение из БД."""
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG else toolforge.toolsdb(DB_CREDITS['database'])
        with conn.cursor() as cur:
            i_res = False
            triggers_false = False
            if is_all:
                cur.execute('SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES), '
                            'SUM(tags), SUM(rfd), SUM(replaces) FROM ds_antivandal')
                r = cur.fetchall()
                cur.execute('SELECT name, SUM(rollbacks) + SUM(undos) + SUM(approves) + SUM(rfd) + SUM(replaces) AS am '
                            'FROM ds_antivandal GROUP BY name ORDER BY am DESC LIMIT 5;')
                r2 = cur.fetchall()
                i_res = []
                for i in r2:
                    if i[0] != 'service_account':
                        i_res.append(f'{i[0]}: {i[1]}')
                i_res = '\n'.join(i_res)
                cur.execute('SELECT patterns, LW, ORES, tags, replaces FROM ds_antivandal_false '
                            'WHERE result = "stats";')
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
                                  f'LW — {r3[0][1]} ({lw} %), ORES — {r3[0][2]} ({ores} %), теги — {r3[0][3]} '
                                  f'({tags} %), замены — {r3[0][4]} ({replaces} %).')
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
        print(f'Error 14.0: {e}')
        return False


async def delete_from_db(actor: str) -> None:
    """Удаление из БД."""
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG else toolforge.toolsdb(DB_CREDITS['database'])
        with conn.cursor() as cur:
            cur.execute(f'DELETE FROM ds_antivandal WHERE name="{actor}";')
            conn.commit()
        conn.close()
    except Exception as e:
        print(f'Error 15.0: {e}')


@client.tree.context_menu(name='Поприветствовать')
async def welcome_user(interaction: discord.Interaction, message: discord.Message):
    """Шаблонное приветствие пользователя."""
    await interaction_defer(interaction, '0.10')
    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content='К сожалению, у вас нет разрешения на выполнение данной команды.')
        except Exception as e:
            print(f'Error 16.0: {e}')
        return
    try:
        await interaction.followup.send(content=f'Приветствуем, <@{message.author.id}>! Если вы желаете получить '
                                                'доступ к остальным каналам сервера, сообщите, пожалуйста, имя вашей '
                                                'учётной записи в проектах Викимедиа.')
    except Exception as e:
        print(f'Error 17.0: {e}')


@client.tree.command(name='rollback_restart_cleaner')
async def rollback_restart_cleaner(interaction: discord.Interaction):
    """Перезапуск бота, очищающего ленты."""
    await interaction_defer(interaction, '0.11')
    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content='К сожалению, у вас нет разрешения на выполнение данной команды. '
                                                    f'Обратитесь к участнику <@{223219998745821194}> или '
                                                    f'<@{352826965494988822}>.', ephemeral=True)
        except Exception as e:
            print(f'Error 18.0: {e}')
        return
    session = aiohttp.ClientSession(headers=USER_AGENT)
    try:
        await session.get(url='https://rv.toolforge.org/online.php?send=1&action=restart&name=antclr'
                              f'&token={os.environ["BOT_TOKEN"]}')
        await interaction.followup.send(content='Запрос отправлен.', ephemeral=True)
    except Exception as e:
        print(f'error 19.0: {e}')
    finally:
        await session.close()


@client.tree.command(name='rollback_help')
async def rollback_help(interaction: discord.Interaction):
    """Список команд бота."""
    await interaction_defer(interaction, '0.12')
    try:
        await interaction.followup.send(content="""/rollback_help — список команд бота.\n
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
        print(f'Error 20.0: {e}')


@client.tree.command(name='rollback_stats_all')
async def rollback_stats_all(interaction: discord.Interaction):
    """Просмотреть статистику откатов и отмен через бот."""
    await interaction_defer(interaction, '0.13')
    r = await get_from_db(is_all=True)
    if not r or not len(r):
        return
    try:
        await interaction.followup.send(content=f'Через бот совершено: откатов — {r["rollbacks"]}, '
                                          f'отмен — {r["undos"]}, одобрений ревизий — {r["approves"]}, '
                                          f'номинаций на КБУ — {r["rfd"]}.\n'
                                          f'Наибольшее количество действий совершили:\n{r["total"]}\n'
                                          f'Действий по типам причин: паттерны — {r["patterns"]}, '
                                          f'ORES — {r["ORES"]}, LW — {r["LW"]}, метки — {r["tags"]}, '
                                          f'замены — {r["replaces"]}.\n'
                                          f'{r["triggers"]}', ephemeral=True)
    except Exception as e:
        print(f'Error 21.0: {e}')


@client.tree.command(name='rollback_stats')
async def rollback_stats(interaction: discord.Interaction, wiki_name: str):
    """Просмотреть статистику откатов и отмен через бот.

    Parameters
    -----------
    wiki_name: str
        Имя участника в вики
     """
    await interaction_defer(interaction, '0.14')
    r = await get_from_db(is_all=False, actor=wiki_name)
    if not r or not len(r):
        return

    try:
        if r['rollbacks'] is None:
            await interaction.followup.send(content='Данный участник не совершал действий через бот.', ephemeral=True)
        else:
            await interaction.followup.send(content=f'Через бот участник {wiki_name} совершил действий: '
                                                    f'{r["rollbacks"] + r["undos"] + r["approves"]},\n'
                                                    f'из них: откатов — {r["rollbacks"]}, отмен — {r["undos"]}, '
                                                    f'одобрений ревизий — {r["approves"]}, '
                                                    f'номинаций на КБУ — {r["rfd"]}.\n'
                                                    'Действий по типам причин, за всё время: паттерны — '
                                                    f'{r["patterns"]}, замены — {r["replaces"]}, ORES — {r["ORES"]}, '
                                                    f'LW — {r["LW"]}, метки — {r["tags"]}.', ephemeral=True)
    except Exception as e:
        print(f'Error 22.0: {e}')


@client.tree.command(name='rollback_stats_delete')
async def rollback_stats_delete(interaction: discord.Interaction, wiki_name: str):
    """Удалить статистку откатов и отмен конкретного участника через бот.

    Parameters
    -----------
    wiki_name: str
        Имя участника в вики
     """
    await interaction_defer(interaction, '0.15')
    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content='К сожалению, у вас нет разрешения '
                                                    'на выполнение данной команды. Обратитесь к участнику '
                                                    f'<@{223219998745821194}> или <@{352826965494988822}>.',
                                            ephemeral=True)
        except Exception as e:
            print(f'Error 23.0: {e}')
        return

    await delete_from_db(wiki_name)
    try:
        await interaction.followup.send(content='Статистика участника удалена, убедитесь в этом через соответствующую '
                                                'команду.', ephemeral=True)
    except Exception as e:
        print(f'Error 24.0: {e}')


@client.tree.command(name='last_metro')
async def last_metro(interaction: discord.Interaction):
    """Узнать время последнего запуска бота #metro."""
    await interaction_defer(interaction, '0.16')
    session = aiohttp.ClientSession(headers=USER_AGENT)
    try:
        r = await session.get(url='https://rv.toolforge.org/metro/')
        r = await r.text()
        metro = r.split('<br>')[0].replace('Задание запущено', 'Последний запуск задания:')
        await interaction.followup.send(content=metro, ephemeral=True)
    except Exception as e:
        print(f'Error 25.0: {e}')
    finally:
        await session.close()


@client.tree.command(name='rollback_clear')
async def rollback_clear(interaction: discord.Interaction):
    """Очистка каналов с выдачей от сообщений бота."""
    await interaction_defer(interaction, '0.17')

    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content='К сожалению, у вас нет разрешения '
                                              'на выполнение данной команды. '
                                              f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Error 26.0: {e}')
        return

    try:
        await interaction.followup.send(content='Очистка каналов начата.', ephemeral=True)
    except Exception as e:
        print(f'Error 27.0: {e}')
    for channel_id in CONFIG['IDS']:
        channel = client.get_channel(channel_id)
        messages = channel.history(limit=100000)
        async for msg in messages:
            if msg.author.id == CONFIG['BOT']:
                try:
                    await msg.delete()
                    await asyncio.sleep(1.5)
                except Exception as e:
                    print(f'Error 28.0: {e}')
                time.sleep(1.0)


@client.tree.command(name='rollbackers')
async def rollbackers(interaction: discord.Interaction):
    """Просмотра списка участников, кому разрешён откат и отмена через бот."""
    await interaction_defer(interaction, '0.18')
    rights_content = ALLOWED_USERS.values()
    try:
        await interaction.followup.send(content=f'Откаты и отмены через бота разрешены участникам '
                                        f'`{", ".join(rights_content)}`.\nДля запроса права или отказа от него '
                                        f'обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
    except Exception as e:
        print(f'Error 29.0: {e}')


@client.tree.command(name='add_rollbacker')
async def add_rollbacker(interaction: discord.Interaction, discord_name: discord.User, wiki_name: str):
    """Добавление участника в список тех, кому разрешён откат и отмена ботом.

    Parameters
    -----------
    discord_name: discord.User
        Участник Discord
    wiki_name: str
        Имя участника в вики
    """
    await interaction_defer(interaction, '0.19')
    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content=f'К сожалению, у вас нет разрешения на выполнение данной команды. '
                                              f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Error 30.0: {e}')
        return

    global ALLOWED_USERS
    add_rollbacker_result = f'Участник {wiki_name} уже присутствует в списке откатывающих.'
    if str(discord_name.id) not in ALLOWED_USERS:
        add_rollbacker_result = f'Участник {wiki_name} добавлен в список откатывающих.'
        ALLOWED_USERS[str(discord_name.id)] = wiki_name

        try:
            msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
            await msg_rights.edit(content=json.dumps(ALLOWED_USERS))
        except Exception as e:
            print(f'Error 31.0: {e}')
            return
    try:
        await interaction.followup.send(content=add_rollbacker_result, ephemeral=True)
    except Exception as e:
        print(f'Error 32.0: {e}')


@client.tree.command(name='remove_rollbacker')
async def remove_rollbacker(interaction: discord.Interaction, wiki_name: str):
    """Удаление участника из списка тех, кому разрешён откат и отмена ботом.

    Parameters
    -----------
    wiki_name: str
        Имя участника в вики
    """
    await interaction_defer(interaction, '0.20')
    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content=f'К сожалению, у вас нет разрешения на выполнение данной команды. '
                                              f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            print(f'Error 33.0: {e}')
        return

    global ALLOWED_USERS
    right_copy = ALLOWED_USERS.copy()
    for k in right_copy:
        if ALLOWED_USERS[k] == wiki_name:
            del ALLOWED_USERS[k]
    remove_rollbacker_result = f'Участника {wiki_name} не было в списке откатывающих.'
    if right_copy != ALLOWED_USERS:
        remove_rollbacker_result = f'Участник {wiki_name} убран из списка откатывающих.'
        try:
            msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
            await msg_rights.edit(content=json.dumps(ALLOWED_USERS))
        except Exception as e:
            print(f'Error 34.0: {e}')
            return
    try:
        await interaction.followup.send(content=remove_rollbacker_result, ephemeral=True)
    except Exception as e:
        print(f'Error 35.0: {e}')


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
        print(f'Error 36.0: {e}')
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
        print(f'Error 37.0: {e}')
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
                    print(f'Error 38.0: {e}')
                else:
                    data = {'action': 'rollback', 'format': 'json', 'title': title,
                            'user': get_name_from_embed(lang, embed.author.url), 'utf8': 1, 'watchlist': 'nochange',
                            'summary': comment, 'token': rollback_token, 'uselang': 'ru'}
                    try:
                        r = await session_with_auth.post(url=f'https://{lang}.wikipedia.org/w/api.php', data=data)
                        r = await r.json()
                    except Exception as e:
                        print(f'Error 39.0: {e}')
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
                    print(f'Error 40.0: {e}')
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
                        print(f'Error 41.0: {e}')
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
                            print(f'Error 42.0: {e}')
                        else:
                            if ('error' not in r and 'edit' in r and 'newrevid' not in r['edit'] and
                                    'revid' not in r['edit']):
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


def get_name_from_embed(lang: str, link: str) -> str:
    """Получение имени пользователя из ссылки на вклад."""
    return unquote(link.replace(f'https://{lang}.wikipedia.org/wiki/special:contribs/', ''))


async def do_rfd(embed: Embed, rfd: str, summary: str):
    """Номинация на быстрое удаление."""
    diff_url, title = embed.url, embed.title
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
        print(f'Error 43.0: {e}')
    else:
        payload = {'action': 'edit', 'format': 'json', 'title': title, 'prependtext': f'{rfd}\n\n', 'token': edit_token,
                   'utf8': 1, 'nocreate': 1, 'summary': summary, 'uselang': 'ru'}
        try:
            r = await session.post(url=api_url, data=payload)
            r = await r.json()
        except Exception as e:
            print(f'Error 44.0: {e}')
        else:
            if 'error' not in r and 'edit' in r and 'newrevid' not in r['edit'] and 'revid' not in r['edit']:
                return print(r)  # debug
            return [r['error']['info'], f'[{title}](<https://{lang}.wikipedia.org/wiki/{title.replace(" ", "_")}>) '
                                        f'(ID: {title})'] if 'error' in r \
                else ['Success', f'[{title}](<https://{lang}.wikipedia.org/w/index.php?diff={r["edit"]["newrevid"]}>)',
                      title]
        finally:
            await session.close()


@client.event
async def on_message(msg):
    """Получение нового сообщения."""
    if len(msg.embeds) == 0:
        return
    if msg.author.id not in CONFIG['SOURCE_BOTS']:
        try:
            await client.process_commands(msg)
        except Exception as e:
            print(f'Error 45.0: {e}')
        return
    if msg.channel.id != CONFIG['SOURCE']:
        try:
            await client.process_commands(msg)
        except Exception as e:
            print(f'Error 46.0: {e}')
        return

    # предотвращение массовых уведомлений от территориальных замен
    global STORAGE
    STORAGE = [el for el in STORAGE if el['timestamp'] + 1800 >= datetime.datetime.now(datetime.UTC).timestamp()]
    lang = get_lang(msg.embeds[0].url)
    rev_id = msg.embeds[0].url.replace(f'https://{lang}.wikipedia.org/w/index.php?diff=', '')
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
            print(f'Error 47.0: {e}')
    channel_new_id = 1212498198200062014 if lang == 'ru' else 1219273496371396681
    channel_new = client.get_channel(channel_new_id)
    try:
        new_message = await channel_new.send(embed=msg.embeds[0],
                                             view=get_view_buttons(disable=True))
        STORAGE.append({'wiki': f'{lang}wiki', 'rev_id': rev_id, 'trigger': trigger, 'msg': new_message, 'timestamp':
            datetime.datetime.now(datetime.UTC).timestamp()})

    except Exception as e:
        print(f'Error 48.0: {e}')
    else:
        try:
            await msg.delete()
        except Exception as e:
            print(f'Error 49.0: {e}')
        finally:
            try:
                await asyncio.sleep(3)
                await new_message.edit(embed=new_message.embeds[0],
                                       view=get_view_buttons())
            except Exception as e:
                print(f'Error 50.0: {e}')


@client.event
async def on_ready():
    """Событие после запуска бота."""
    try:
        for server in client.guilds:
            if server.id not in CONFIG['SERVER']:
                guild = discord.utils.get(client.guilds, id=server.id)
                await guild.leave()
        client.add_view(get_view_buttons())
        client.add_view(get_view_rfd())
        client.add_view(get_view_undo())
        await client.tree.sync()
        await client.change_presence(status=discord.Status.online, activity=discord.Game('pyCharm'))
        global ALLOWED_USERS

        msg_rights = await client.get_channel(CONFIG['BOTCOMMANDS']).fetch_message(CONFIG['ROLLBACKERS'])
        ALLOWED_USERS = json.loads(msg_rights.content.replace('`', ''))

        print('Просмотр пропущенных записей лога')
        channel = client.get_channel(CONFIG['SOURCE'])
        messages = channel.history(limit=50, oldest_first=False)
        async for msg in messages:
            if len(msg.embeds) > 0:
                await on_message(msg)
        print('Бот запущен')
    except Exception as e:
        print(f'Error 51.0: {e}')


@client.event
async def on_guild_join(guild):
    """Событие входа бота на новый сервер."""
    try:
        if guild.id not in CONFIG['SERVER']:
            await guild.leave()
    except Exception as e:
        print(f'Error 52.0: {e}')


client.run(token=TOKEN, reconnect=True, log_level=logging.WARN)
