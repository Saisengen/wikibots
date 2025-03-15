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
CONFIG = {'SERVER': [1044474820089368666],
          'IDS': [1212498198200062014, 1219273496371396681, 1342471984671625226,
                                       1348216089825509450, 1348216377789382656],
          'BOT': 1225008116048072754, 'SOURCE_BOTS': [1237362558046830662, 1299324425878900818],
          'BOTCOMMANDS': 1212507148982947901, 'ROLLBACKERS': 1237790591044292680, 'SOURCE': 1237345566950948867,
          'ADMINS': [352826965494988822, 512545053223419924, 223219998745821194]}
USER_AGENT = {'User-Agent': 'D-V; iluvatar@tools.wmflabs.org; python3.11'}
ALLOWED_USERS = {}
Intents = discord.Intents.default()
Intents.members, Intents.message_content = True, True
discord.Intents.all()
allowed_mentions = discord.AllowedMentions(roles=True)
client = commands.Bot(intents=Intents, command_prefix='/')

logger = logging.getLogger()
logger.setLevel(logging.DEBUG)
logging_handler = logging.FileHandler(filename='logs/antvand.log', encoding='utf-8', mode='w')
logging_handler.setLevel(logging.DEBUG)
console_handler = logging.StreamHandler()
console_handler.setLevel(logging.DEBUG)
formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
logging_handler.setFormatter(formatter)
console_handler.setFormatter(formatter)
logger.addHandler(logging_handler)
logger.addHandler(console_handler)

select_options_undo = {'1':  ['Нарушение АП',
                              '[[ВП:КОПИВИО|копирование текста из несвободных источников]]',
                              '[[ВП:АП|копіювання тексту з невільних джерел]]',
                              '[[ВП:КОПІВІА|капіраванне тэксту з несвабодных крыніц]]'],
                       '2':  ['Нет АИ',
                              'добавление сомнительного содержимого [[ВП:ПРОВ|без источников]] или '
                              '[[ВП:ОРИСС|оригинального исследования]]',
                              'додавання [[ВП:ОД|оригінального дослідження]] або сумнівної інформації [[ВП:В|без '
                              'джерел]]', 'дабаўленне [[ВП:НУДА|ўласнага даследвання]] або сумніўнай інфармацыі '
                                          '[[ВП:ПРАВ|без крыніц]]'],
                       '3':  ['Порча вики-разметки', 'порча [[ВП:Викиразметка|викиразметки]] статьи',
                              'псування [[Вікірозмітка|вікірозмітки]] статті', 'псаванне [[Вікіпедыя:Вікіразметка|'
                                                                               'вікіразметкі]] артыкула'],
                       '4':  ['Спам', 'добавление [[ВП:ВС|ненужных / излишних ссылок]] или спам',
                              'додавання [[ВП:УНИКАТИПОС|непотрібних / зайвих посилань]] або спам',
                              'дабаўленне непатрэбных / залішніх спасылак або спам'],
                       '5':  ['Незначимый факт', 'отсутствует [[ВП:Значимость факта|энциклопедическая значимость]] факта',
                              'відсутня [[ВП:ЗВ|значущість]] факту', 'адсутнічае [[ВП:КЗ|значнасць]] факта'],
                       '6':  ['Переименование без КПМ',
                              'попытка переименования объекта по тексту без [[ВП:ПЕРЕ|переименования страницы]] или иное '
                              'сомнит. переименование. Воспользуйтесь [[ВП:КПМ|специальной процедурой]]',
                              'перейменування по тексту без перейменування сторінки',
                              'перайменаванне ў тэксце без перайменавання артыкула. Карыстайцесь [[ВП:Да перайменавання|'
                              'адмысловай старонкай]]'],
                       '7':  ['Подлог источника',
                              '[[ВП:ПОДИСТ|изменение информации, подтверждённой источником, без его изменения]]',
                              '[[ВП:ВАНД|заміна інформації, підтвердженої джерелом, без зміни джерела]]',
                              '[[ВП:ПРАВ|змяненне пацверджанай інфармацыі без замены крыніцы]]'],
                       '8':  ['Удаление содержимого', 'необъяснённое удаление содержимого страницы',
                              'видалення вмісту сторінки', 'выдаленне змесціва старонкі без тлумачэння'],
                       '9':  ['Орфография, пунктуация', 'добавление орфографических или пунктуационных ошибок',
                              'додавання орфографічних або пунктуаційних помилок', 'дабаўленне арфаграфічных або '
                                                                                   'пунктуацыйных памылак'],
                       '10': ['Не на языке проекта', 'добавление содержимого не на русском языке',
                              'додавання вмісту не українською мовою', 'дабаўленне змесціва не на беларускай мове'],
                       '11': ['Удаление шаблонов', 'попытка необоснованного удаления служебных или номинационных '
                                                   'шаблонов',
                              'спроба необґрунтованого видалення службових або номінаційних шаблонів',
                              'спроба неабгрунтаванага выдалення службовых або намінацыйных шаблонаў'],
                       '12': ['Личное мнение',
                              '[[ВП:НЕФОРУМ|изложение личного мнения]] об объекте статьи. Википедия не является '
                              '[[ВП:НЕФОРУМ|форумом]] или [[ВП:НЕТРИБУНА|трибуной]]',
                              'виклад особистої думки про об\'єкт статті. [[ВП:НЕТРИБУНА|Вікіпедія — не трибуна]]',
                              'выказванне асабістага меркавання аб аб\'екце артыкула. [[ВП:ЧНЗВ|Вікіпедыя не '
                              'з\'яўляецца форумам або трыбунай]]'],
                       '13': ['Комментарии в статье', 'добавление комментариев в статью. Комментарии и пометки '
                                                      'оставляйте на [[Talk:$1|странице обсуждения]] статьи',
                              'додавання коментарів до статті. Коментарі та позначки залишайте на [[Сторінка '
                              'обговорення:$1|сторінці обговорення]] статті',
                              'дабаўленне каментароў у артыкул. Каментары і паметкі пакідайце на адмысловай '
                              '[[Размовы:$1|старонцы размоў]]'],
                       '14': ['Ненейтральный стиль',
                              'добавление текста в [[ВП:НТЗ|ненейтральном]] или [[ВП:СТИЛЬ|рекламном]] стиле',
                              'додавання тексту в [[ВП:НТЗ|ненейтральному]] або [[ВП:СТИЛЬ|рекламному]] стилі',
                              'дабаўленне тэксту ў [[ВП:НПГ|ненейтральным]] або '
                              '[[Вікіпедыя:Чым не з’яўляецца Вікіпедыя#Вікіпедыя — не трыбуна|рэкламным]] стылі'],
                       '15': ['НЕГУЩА',
                              '[[ВП:НЕГУЩА|описание ещё не случившихся возможных событий]]',
                              '[[ВП:ПРОРОК|опис можливих подій, які ще не відбулися]]',
                              '[[Вікіпедыя:Чым не з’яўляецца Вікіпедыя#'
                              'Вікіпедыя — не кававая гушча|апісанне падзей, якія яшчэ не здарыліся]]'],
                       '16': ['Троллинг', 'троллинг', 'тролінг', 'тролінг'],
                       '17': ['своя причина', '', '', ''],  # не менять название пункта без изменения в callback
                       '18': ['Закрыть', '', '', '']  # не менять название пункта без изменения в callback
                       }

options_undo, options_rfd = [], []
for option, index in select_options_undo.items():
    options_undo.append(SelectOption(label=index[0], value=str(option)))

select_options_rfd = {
    '1': ['Бессвязное содержимое', '{{уд-бессвязно}}', '{{Db-nonsense}}', '{{хв|Бессэнсоўнае змесціва}}'],
    '2': ['Вандализм', '{{уд-ванд}}', '{{Db-vand}}', '{{хв|Вандалізм}}'],
    '3': ['Тестовая страница', '{{уд-тест}}', '{{Db-test}}', '{{хв|Тэставая старонка}}'],
    '4': ['Реклама / спам', '{{уд-реклама}}', '{{Db-spam}}', '{{хв|Рэклама або спам}}'],
    '5': ['Пустая статья', '{{{уд-пусто}}', '{{Db-nocontext}}', '{{хв|Пустая старонка}}'],
    '6': ['На иностранном языке', '{{уд-иностр}}', '{{Db-lang}}', '{{хв|На замежнай мове}}'],
    '7': ['Нет значимости', '{{уд-нз}}', '{{Db-nn}}', '{{хв|Няма значнасці}}'],
    '8': ['Форк', '{{db-fork|$1}}', '{{db-duplicate|$1}}', '{{хв|Дублікат артыкула [[$1]]}}'], # не менять название пункта без изменения в callback
    '9': ['Нецелевое использование СО', '{{db-badtalk}}', '{{db-reason|Нецільове використання сторінки обговорення}}', '{{хв|Немэтавае выкарыстоўванне старонкі размоваў}}'],
    '10': ['своя причина', '{{delete|$1}}', '{{db-reason|$1}}', '{{хв|$1}}'],  # не менять название пункта без изменения в callback
    '11': ['Закрыть', '', '', '']  # не менять название пункта без изменения в callback
}
for option, index in select_options_rfd.items():
    options_rfd.append(SelectOption(label=index[0], value=str(option)))


select_component_undo = Select(placeholder='Выбор причины отмены', min_values=1, max_values=1, options=options_undo,
                               custom_id='select_component_undo')
select_component_undo.callback = select_component_undo
select_component_rfd = Select(placeholder='Выбор причины КБУ', min_values=1, max_values=1, options=options_rfd,
                              custom_id='select_component_rfd')

undo_prefix = ['Бот: отмена правки [[Special:Contribs/$author|$author]] по запросу [[User:$actor|$actor]]:',
               'скасовано останнє редагування [[Special:Contribs/$author|$author]] за запитом [[User:$actor|$actor]]:',
               'Бот: адкат праўкі [[Special:Contribs/$author|$author]] па запыце [[User:$actor|$actor]]:']
rfd_summary = ['Бот: Номинация на КБУ по запросу [[User:$actor|$actor]]',
               'Номінація на швидке вилучення за запитом [[User:$actor|$actor]]',
               'Бот: намінацыя на хуткае выдаленне па запыце [[User:$actor|$actor]]']
rollback_summary = ['Бот: откат правок [[Special:Contribs/$2|$2]] по запросу [[User:$1|$1]]',
                    'Бот: відкинуто редагування [[Special:Contribs/$2|$2]] за запитом [[User:$1|$1]]',
                    'Бот: хуткі адкат правак [[Special:Contribs/$2|$2]] па запыце [[User:$1|$1]]']

class ReasonUndo(Modal, title='Причина'):
    """Строка ввода причины отмены."""
    res = TextInput(custom_id='menu_undo', label='Причина отмены', min_length=2, max_length=255,
                         placeholder='введите причину', required=True, style=discord.TextStyle.short)


    async def on_submit(self, interaction: discord.Interaction):
        await interaction_defer(interaction, '0.1')
        if not await check_rights(interaction):
            return
        actor, msg, channel = get_data(interaction)
        lang_selector = get_lang_number(get_lang(msg.embeds[0].url))

        reason = f'{undo_prefix[lang_selector].replace("$actor", actor)} {self.children[0].value}'

        r = await do_rollback(msg.embeds[0], actor, action_type='undo', reason=reason)
        await result_undo_handler(r, interaction)


class ReasonRFD(Modal, title='Причина'):
    """Строка ввода номинации на удаления."""
    res = TextInput(custom_id='menu_rfd', label='Причина КБУ', min_length=2, max_length=255,
                    placeholder='введите причину', required=True, style=discord.TextStyle.short)

    def __init__(self, template=None):
        super().__init__()
        self.template = template

    async def on_submit(self, interaction: discord.Interaction):
        await interaction_defer(interaction, '0.2')
        if not await check_rights(interaction):
            return
        actor, msg, channel = get_data(interaction)
        lang_selector = get_lang_number(get_lang(msg.embeds[0].url))
        summary = rfd_summary[lang_selector].replace('$actor', actor)
        r = await do_rfd(msg.embeds[0], rfd=self.template.replace('$1', self.children[0].value), summary=summary)
        await result_rfd_handler(r, interaction)


async def result_rfd_handler(r, interaction: discord.Interaction) -> None:
    actor, msg, channel = get_data(interaction)
    try:
        if r == [] or r[1] == '':
            msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}.')
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
        if r[0] == 'Success':
            await channel.send(content=f'{actor} номинировал {r[1]} на КБУ.')
            await send_to_db(actor, 'rfd', get_trigger(msg.embeds[0]))
            await msg.delete()
            return
        if 'не существует' in r[0]:
            new_embed = Embed(color=msg.embeds[0].color, title='Страница была удалена.')
            await interaction.message.edit(embed=new_embed, view=None, delete_after=12.0)
        else:
            msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}, {r[1]}.')
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
    except Exception as e:
        logging.error(f'Error 1.0: {e}')


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
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
        else:
            if r[1] != '':
                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}, {r[1]}.')
            else:
                msg.embeds[0].set_footer(text=f'Действие не удалось: {r[0]}.')
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
    except Exception as e:
        logging.error(f'Error 2.0: {e}')


def get_view_undo() -> View:
    res = Select(placeholder="Причина?", max_values=1, min_values=1, options=options_undo, custom_id='undo_select')

    async def callback(interaction: discord.Interaction):
        if not await check_rights(interaction):
            return
        selected = select_options_undo[list(interaction.data.values())[0][0]]
        actor, msg, channel = get_data(interaction)
        lang = get_lang(msg.embeds[0].url)
        try:
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
        except Exception as e:
            logging.error(f'Error 3.0: {e}')

        if selected[0] == 'своя причина':
            try:
                await interaction.response.send_modal(ReasonUndo())
            except Exception as e:
                logging.error(f'Error 4.0: {e}')
            return

        await interaction_defer(interaction, '0.3')
        if selected[0] == 'Закрыть':
            return

        lang_selector = get_lang_number(lang)
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
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
        except Exception as e:
            logging.error(f'Error 5.0: {e}')

        lang_selector = get_lang_number(lang)
        summary = rfd_summary[lang_selector].replace('$actor', actor)
        rfd_reason = selected[lang_selector + 1]

        if selected[0] == 'своя причина' or selected[0] == 'Форк':
            try:
                await interaction.response.send_modal(ReasonRFD(template=rfd_reason))
            except Exception as e:
                logging.error(f'Error 6.0: {e}')
            return

        await interaction_defer(interaction, '0.4')
        if selected[0] == 'Закрыть':
            return

        r = await do_rfd(msg.embeds[0], rfd=rfd_reason, summary=summary)
        await result_rfd_handler(r, interaction)


    res.callback = callback
    view = View(timeout=None)
    return view.add_item(res)


def get_lang(url: str) -> str:
    """Получение кода языкового раздела из ссылки."""
    if 'ru.wikipedia.org' in url:
        return 'ru'
    elif 'uk.wikipedia.org' in url:
        return 'uk'
    elif 'be.wikipedia.org' in url:
        return 'be'
    elif 'wikidata.org' in url:
        return 'wd'
    elif 'commons.wikimedia.org' in url:
        return 'c'


def get_domain(lang: str) -> str:
    """Получение домена языкового раздела из кода."""
    if lang == 'wd':
        return 'wikidata.org'
    elif lang == 'c':
        return 'commons.wikimedia.org'
    else:
        return lang + '.wikipedia.org'

def get_lang_number(lang: str) -> int:
    """Получение индекса раздела."""
    return {'ru': 0, 'uk': 1, 'be': 2, 'wd': 3, 'c': 3}[lang]


def get_trigger(embed: Embed) -> str:
    """Получение причины реакции по цвету."""
    triggers_dict = {'#ff0000': 'patterns', '#ffff00': 'LW', '#ff00ff': 'ORES', '#00ff00': 'tags',
                     '#0000ff': 'replaces', '#ff8000': 'LW', '#00ffff': 'replaces'}
    return 'unknown' if (color:=str(embed.color)) not in triggers_dict else triggers_dict[color]


async def check_rights(interaction: discord.Interaction) -> bool:
    if str(interaction.user.id) not in ALLOWED_USERS:
        try:
            await interaction.followup.send(
                content='К сожалению, у вас нет разрешение на выполнение откатов и отмен через бот. Обратитесь к '
                        f'участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            logging.error(f'Error 7.0: {e}')
            return False
        else:
            return False
    return True


def get_data(interaction: discord.Interaction):
    actor = ALLOWED_USERS[str(interaction.user.id)]
    msg = interaction.message
    channel = client.get_channel(CONFIG['SOURCE'])
    return actor, msg, channel


def get_view_buttons(embed: Embed = None, disable: bool = False) -> View:
    """Формирование набора компонентов."""
    if embed is not None and not 'wikipedia' in embed.url:
        return View(timeout=None)

    revert_disabled = True if embed is not None and 'ilu=' not in embed.url else disable
    btn_rollback = Button(emoji='⏮️', style=discord.ButtonStyle.danger, custom_id="btn_rollback",
                          disabled=revert_disabled)
    btn_rfd = Button(emoji='🗑️', style=discord.ButtonStyle.danger, custom_id="btn_rfd", disabled=disable)
    btn_undo = Button(emoji='↪️', style=discord.ButtonStyle.blurple, custom_id="btn_undo",
                      disabled=revert_disabled)
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
                    await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_buttons(embed=msg.embeds[0]))
        except Exception as e:
            logging.error(f'Error 8.0: {e}')

    async def rfd_handler(interaction: discord.Interaction):
        await interaction_defer(interaction, '0.6')
        if not await check_rights(interaction):
            return
        msg = interaction.message
        try:
            await msg.edit(content=msg.content, embed=msg.embeds[0], view=get_view_rfd())
        except Exception as e:
            logging.error(f'Error 9.0: {e}')

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
            logging.error(f'Error 10.0: {e}')

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
            logging.error(f'Error 11.0: {e}')

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
            logging.error(f'Error 12.0: {e}')


    btn_rollback.callback = rollback_handler
    btn_rfd.callback = rfd_handler
    btn_undo.callback = undo_handler
    btn_good.callback = good_handler
    btn_bad.callback = bad_handler

    view_buttons = View(timeout=None)
    [view_buttons.add_item(i) for i in [btn_rollback, btn_rfd, btn_undo, btn_good, btn_bad]]
    return view_buttons


async def interaction_defer(interaction: discord.Interaction, error_description: str) -> None:
    try:
        await interaction.response.defer(ephemeral=True)
    except Exception as e:
        logging.error(f'Error {error_description}: {e}')


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
        logging.error(f'Error 13.0: {e}')


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
        logging.error(f'Error 14.0: {e}')
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
        logging.error(f'Error 15.0: {e}')


@client.tree.context_menu(name='Поприветствовать')
async def welcome_user(interaction: discord.Interaction, message: discord.Message):
    """Шаблонное приветствие пользователя."""
    await interaction_defer(interaction, '0.10')
    if interaction.user.id not in CONFIG['ADMINS']:
        try:
            await interaction.followup.send(content='К сожалению, у вас нет разрешения на выполнение данной команды.')
        except Exception as e:
            logging.error(f'Error 16.0: {e}')
        return
    try:
        await interaction.followup.send(content=f'Приветствуем, <@{message.author.id}>! Если вы желаете получить '
                                                'доступ к остальным каналам сервера, сообщите, пожалуйста, имя вашей '
                                                'учётной записи в проектах Викимедиа.')
    except Exception as e:
        logging.error(f'Error 17.0: {e}')


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
            logging.error(f'Error 18.0: {e}')
        return
    session = aiohttp.ClientSession(headers=USER_AGENT)
    try:
        await session.get(url='https://rv.toolforge.org/online.php?send=1&action=restart&name=antclr'
                              f'&token={os.environ["BOT_TOKEN"]}')
        await interaction.followup.send(content='Запрос отправлен.', ephemeral=True)
    except Exception as e:
        logging.error(f'Error 19.0: {e}')
    finally:
        await session.close()


@client.tree.command(name='rollback_help')
async def rollback_help(interaction: discord.Interaction):
    """Список команд бота."""
    await interaction_defer(interaction, '0.12')
    try:
        await interaction.followup.send(content='/rollback_help — список команд бота.\n'
                                                '/rollback_clear — очистка фид-каналов от всех сообщений бота.\n'
                                                '/rollbackers — список участников, кому разрешены действия через бот.\n'
                                                '/add_rollbacker — разрешить участнику действия через бот.\n'
                                                '/remove_rollbacker — запретить участника действия через бот.\n'
                                                '/rollback_stats_all — статистика откатов через бот.\n'
                                                '/rollback_stats — статистика действий участника через бот.\n'
                                                '/rollback_stats_delete — удалить всю статистику действий участника.\n\n'
                                                'По вопросам работы бота обращайтесь к <@352826965494988822>.',
                                        ephemeral=True)
    except Exception as e:
        logging.error(f'Error 20.0: {e}')


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
        logging.error(f'Error 21.0: {e}')


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
        logging.error(f'Error 22.0: {e}')


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
            logging.error(f'Error 23.0: {e}')
        return

    await delete_from_db(wiki_name)
    try:
        await interaction.followup.send(content='Статистика участника удалена, убедитесь в этом через соответствующую '
                                                'команду.', ephemeral=True)
    except Exception as e:
        logging.error(f'Error 24.0: {e}')


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
        logging.error(f'Error 25.0: {e}')
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
            logging.error(f'Error 26.0: {e}')
        return

    try:
        await interaction.followup.send(content='Очистка каналов начата.', ephemeral=True)
    except Exception as e:
        logging.error(f'Error 27.0: {e}')
    for channel_id in CONFIG['IDS']:
        channel = client.get_channel(channel_id)
        messages = channel.history(limit=100000)
        async for msg in messages:
            if msg.author.id == CONFIG['BOT']:
                try:
                    await msg.delete()
                    await asyncio.sleep(1.5)
                except Exception as e:
                    logging.error(f'Error 28.0: {e}')
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
        logging.error(f'Error 29.0: {e}')


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
    if interaction.user.id not in CONFIG['ADMINS'] or interaction.user.id == discord_name.id:
        try:
            await interaction.followup.send(content=f'К сожалению, у вас нет разрешения на выполнение данной команды. '
                                              f'Обратитесь к участнику <@{223219998745821194}>.', ephemeral=True)
        except Exception as e:
            logging.error(f'Error 30.0: {e}')
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
            logging.error(f'Error 31.0: {e}')
            return
    try:
        await interaction.followup.send(content=add_rollbacker_result, ephemeral=True)
    except Exception as e:
        logging.error(f'Error 32.0: {e}')


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
            logging.error(f'Error 33.0: {e}')
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
            logging.error(f'Error 34.0: {e}')
            return
    try:
        await interaction.followup.send(content=remove_rollbacker_result, ephemeral=True)
    except Exception as e:
        logging.error(f'Error 35.0: {e}')


async def do_rollback(embed, actor, action_type='rollback', reason=''):
    """Выполнение отката или отмены правки."""
    diff_url = embed.url
    title = embed.title
    lang = get_lang(diff_url)
    rev_id = diff_url.split('ilu=')[1]
    session = aiohttp.ClientSession(headers=USER_AGENT)
    try:
        r = await revision_check(f'https://{get_domain(lang)}/w/api.php', rev_id, title, session)
    except Exception as e:
        logging.error(f'Error 36.0: {e}')
        await session.close()
    else:
        if not r:
            r = await flagged_check(f'https://{get_domain(lang)}/w/api.php', title, rev_id, session)
        if r:
            await session.close()
            return ['Такой страницы уже не существует, правки были откачены или страница отпатрулирована.',
                    f'[{title}](<https://{get_domain(lang)}/wiki/{title.replace(" ", "_")}>) (ID: {rev_id})']
    data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'ids|timestamp', 'rvlimit': 500,
            'rvendid': rev_id, 'rvuser': get_name_from_embed(lang, embed.author.url), 'titles': title,
            'format': 'json', 'utf8': 1, 'uselang': 'ru'}
    try:
        r = await session.post(url=f'https://{get_domain(lang)}/w/api.php', data=data)
        r = await r.json()
    except Exception as e:
        logging.error(f'Error 37.0: {e}')
        await session.close()
    else:
        if '-1' in r['query']['pages']:
            await session.close()
            return ['Такой страницы уже не существует.',
                    f'[{title}](<https://{get_domain(lang)}/wiki/{title.replace(" ", "_")}>) (ID: {rev_id})']
        page_id = str(list(r['query']["pages"].keys())[0])
        if 'revisions' in r['query']['pages'][page_id] and len(r['query']['pages'][page_id]['revisions']) > 0:
            rev_id = r['query']['pages'][page_id]['revisions'][0]['revid']
            api_url = f'https://{get_domain(lang)}/w/api.php'
            headers = {'Authorization': f'Bearer {BEARER_TOKEN}', 'User-Agent': 'Reimu; iluvatar@tools.wmflabs.org'}
            session_with_auth = aiohttp.ClientSession(headers=headers)

            if action_type == 'rollback' and lang == 'be':  # в bewiki нет флага откатывающего
                action_type = 'undo'
                reason = rollback_summary[get_lang_number(lang)].replace('$1', actor).replace('$2', get_name_from_embed(lang, embed.author.url))

            if action_type == 'rollback':
                rollback_comment = rollback_summary[get_lang_number(lang)].replace('$1', actor)
                try:
                    r_token = await session_with_auth.get(f'{api_url}?format=json&action=query&meta=tokens'
                                                          f'&type=rollback')
                    rollback_token = await r_token.json()
                    rollback_token = rollback_token['query']['tokens']['rollbacktoken']
                except Exception as e:
                    await session_with_auth.close()
                    await session.close()
                    logging.error(f'Error 38.0: {e}')
                else:
                    data = {'action': 'rollback', 'format': 'json', 'title': title,
                            'user': get_name_from_embed(lang, embed.author.url), 'utf8': 1, 'watchlist': 'nochange',
                            'summary': rollback_comment, 'token': rollback_token, 'uselang': 'ru'}
                    try:
                        r = await session_with_auth.post(url=f'https://{get_domain(lang)}/w/api.php', data=data)
                        r = await r.json()
                    except Exception as e:
                        logging.error(f'Error 39.0: {e}')
                    else:
                        return [r['error']['info'],
                                f'[{title}](<https://{get_domain(lang)}/wiki/{title.replace(" ", "_")}>) '
                                f'(ID: {rev_id})'] if 'error' in r else [
                            'Success',
                            f'[{title}](<https://{get_domain(lang)}/w/index.php?diff={r["rollback"]["revid"]}>)']
                    finally:
                        await session.close()
                        await session_with_auth.close()
            else:
                data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'ids|user', 'rvlimit': 1,
                        'rvstartid': rev_id, 'rvexcludeuser': get_name_from_embed(lang, embed.author.url),
                        'titles': title, 'format': 'json', 'utf8': 1, 'uselang': 'ru'}
                try:
                    r = await session.post(url=f'https://{get_domain(lang)}/w/api.php', data=data)
                    r = await r.json()
                    check_revs = len(r['query']['pages'][page_id]['revisions'])
                except Exception as e:
                    logging.error(f'Error 40.0: {e}')
                else:
                    if check_revs == 0:
                        await session_with_auth.close()
                        await session.close()
                        return ['Все версии принадлежат одному участнику', f'[{title}]'
                                                                           f'(<https://{get_domain(lang)}/wiki/'
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
                        logging.error(f'Error 41.0: {e}')
                    else:
                        reason = reason.replace('$author', get_name_from_embed(lang, embed.author.url)).replace(
                            '$lastauthor', last_author)
                        data = {'action': 'edit', 'format': 'json', 'title': title, 'undo': rev_id,
                                'undoafter': parent_id, 'watchlist': 'nochange', 'nocreate': 1, 'summary': reason,
                                'token': edit_token, 'utf8': 1, 'uselang': 'ru'}
                        try:
                            r = await session_with_auth.post(url=f'https://{get_domain(lang)}/w/api.php', data=data)
                            r = await r.json()
                        except Exception as e:
                            logging.error(f'Error 42.0: {e}')
                        else:
                            if ('error' not in r and 'edit' in r and 'newrevid' not in r['edit'] and
                                    'revid' not in r['edit']):
                                logging.debug(r)
                                return  # debug
                            return [r['error']['info'], f'[{title}](<https://{get_domain(lang)}'
                                                        f'/wiki/{title.replace(" ", "_")}>) '
                                                        f'(ID: {rev_id})'] if 'error' in r else \
                                ['Success', f'[{title}](<https://{get_domain(lang)}/w/index.php?diff='
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
    return unquote(link.replace(f'https://{get_domain(lang)}/wiki/special:contribs/', ''))


async def do_rfd(embed: Embed, rfd: str, summary: str) -> list[str]:
    """Номинация на быстрое удаление."""
    diff_url, title = embed.url, embed.title
    lang = get_lang(diff_url)
    api_url = f'https://{get_domain(lang)}/w/api.php'
    headers = {'Authorization': f'Bearer {BEARER_TOKEN}', 'User-Agent': 'Reimu; iluvatar@tools.wmflabs.org'}

    session = aiohttp.ClientSession(headers=headers)
    try:
        r = await session.get(url=f'{api_url}?format=json&action=query&meta=tokens&type=csrf')
        edit_token = await r.json()
        edit_token = edit_token['query']['tokens']['csrftoken']
    except Exception as e:
        await session.close()
        logging.error(f'Error 43.0: {e}')
    else:
        payload = {'action': 'edit', 'format': 'json', 'title': title, 'prependtext': f'{rfd}\n\n', 'token': edit_token,
                   'utf8': 1, 'nocreate': 1, 'summary': summary, 'uselang': 'ru'}
        try:
            r = await session.post(url=api_url, data=payload)
            r = await r.json()
        except Exception as e:
            logging.error(f'Error 44.0: {e}')
            await session.close()
            return []
        else:
            if 'error' not in r and 'edit' in r and 'newrevid' not in r['edit'] and 'revid' not in r['edit']:
                logging.debug(r)
                return []  # debug
            return [r['error']['info'], f'[{title}](<https://{get_domain(lang)}/wiki/{title.replace(" ", "_")}>) '
                                        f'(ID: {title})'] if 'error' in r \
                else ['Success', f'[{title}](<https://{get_domain(lang)}/w/index.php?diff={r["edit"]["newrevid"]}>)',
                      title]
    return []


@client.event
async def on_message(msg):
    """Получение нового сообщения."""
    if len(msg.embeds) == 0:
        return
    if msg.author.id not in CONFIG['SOURCE_BOTS']:
        try:
            await client.process_commands(msg)
        except Exception as e:
            logging.error(f'Error 45.0: {e}')
        return
    if msg.channel.id != CONFIG['SOURCE']:
        try:
            await client.process_commands(msg)
        except Exception as e:
            logging.error(f'Error 46.0: {e}')
        return

    lang = get_lang(msg.embeds[0].url)
    rev_id = msg.embeds[0].url.split('diff=')[1] if 'ilu=' not in msg.embeds[0].url else\
        msg.embeds[0].url.split('ilu=')[1]

    # не откачена ли
    session = aiohttp.ClientSession(headers=USER_AGENT)
    is_reverted = await revision_check(f'https://{get_domain(lang)}/w/api.php', rev_id, msg.embeds[0].title, session)
    if not is_reverted:
        is_reverted = await flagged_check(f'https://{get_domain(lang)}/w/api.php', msg.embeds[0].title, rev_id, session)
    await session.close()
    if is_reverted:
        try:
            await msg.delete()
            return
        except Exception as e:
            logging.error(f'Error 47.0: {e}')
    channel_new_id = CONFIG['IDS'][get_lang_number(lang)]
    channel_new = client.get_channel(channel_new_id)
    try:
        new_message = await channel_new.send(embed=msg.embeds[0],
                                             view=get_view_buttons(embed=msg.embeds[0],disable=True))
        logging.debug(f'sleep error 1: {new_message.embeds[0].title}')
    except Exception as e:
        logging.error(f'Error 48.0: {e}')
    else:
        logging.debug(f'sleep error 2: {new_message.embeds[0].title}')
        try:
            await msg.delete()
        except Exception as e:
            logging.error(f'Error 49.0: {e}')
        finally:
            try:
                logging.debug(f'sleep error 3: {new_message.embeds[0].title}')
                await asyncio.sleep(3)
                await new_message.edit(embed=new_message.embeds[0],
                                       view=get_view_buttons(embed=new_message.embeds[0]))
                logging.debug(f'sleep error 4: {new_message.embeds[0].title}')
            except Exception as e:
                logging.error(f'Error 50.0: {e}')


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

        logging.info('Просмотр пропущенных записей лога')
        channel = client.get_channel(CONFIG['SOURCE'])
        messages = channel.history(limit=50, oldest_first=False)
        async for msg in messages:
            if len(msg.embeds) > 0:
                await on_message(msg)
        logging.info('Бот запущен')
    except Exception as e:(
        logging.error(f'Error 51.0: {e}'))


@client.event
async def on_guild_join(guild):
    """Событие входа бота на новый сервер."""
    try:
        if guild.id not in CONFIG['SERVER']:
            await guild.leave()
    except Exception as e:
        logging.error(f'Error 52.0: {e}')

client.run(token=TOKEN, reconnect=True, log_handler=logging_handler)
