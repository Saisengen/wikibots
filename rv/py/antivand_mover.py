"""Модуль переноса сообщений."""

from typing import Literal
from io import BytesIO
from math import ceil
from asyncio import sleep
from discord import Client, Message, Webhook, Interaction, Thread, MessageType, MessageReferenceType, Embed, Member, StickerFormatType, File, HTTPException, AllowedMentions, MessageSnapshot, SelectOption, ChannelType
from discord.reaction import ReactionType # TODO: temp hack until discord.py 2.6.4
from discord.abc import Messageable
from discord.ui import Modal, Select, ChannelSelect, Label, TextInput
from discord.app_commands import CommandTree
from discord.utils import MISSING # TODO: temp hack until discord.py 2.6.4
from antivand_utils import get_session

# TODO: голосования и голосовые

# TODO: вебхуки не могут отвечать на сообщения (https://github.com/discord/discord-api-docs/discussions/3282) и отправлять стикеры (https://github.com/discord/discord-api-docs/discussions/4441). сейчас вместо этого генерируется соответствующий эмбед или стикер отправляется как изображение

# monkeypatching + memory leak
map = {}
def get_author(self):
    return map.get(id(self))
def set_author(self, value):
    map[id(self)] = value
MessageSnapshot.author = property(get_author, set_author)

class MoveError(Exception):
    """Ошибка при переносе сообщения."""
    def __init__(self, message: str, msg: Message):
        super().__init__(message)
        self.msg = msg

def formatnum(num: int) -> str:
    """Форматирование строки с количеством сообщений."""
    if num % 100 >= 10 and num % 100 < 20:
        return f'{num} сообщений'
    return f'{num} ' + ['сообщений', 'сообщение', 'сообщения', 'сообщения', 'сообщения', 'сообщений', 'сообщений', 'сообщений', 'сообщений', 'сообщений'][num % 10]

async def move_msg(client: Client, msg: Message, wh: Webhook, dst: Messageable, msgs: dict[int, list[Message]], authors: set[Member]) -> None:
    """Перенос конкретного сообщения."""
    if msg.poll:
        raise MoveError('Сообщение является голосованием', msg)
    if msg.flags.voice:
        raise MoveError('Сообщение является голосовым', msg)

    if msg.type not in [MessageType.default, MessageType.reply]:
        return

    orig_msg = msg
    if msg.reference and msg.reference.type == MessageReferenceType.forward:
        try:
            msg = await client.get_channel(msg.reference.channel_id).fetch_message(msg.reference.message_id)
        except Exception as e:
            author = msg.author
            msg = msg.message_snapshots[0]
            msg.author = author
            msg.content = '↪ Переслано:\n' + msg.content

    reply_embed = MISSING
    if msg.type == MessageType.reply:
        orig = msg.reference.resolved
        if isinstance(orig, Message):
            orig_orig = orig
            description = ''
            if orig.reference and orig.reference.type == MessageReferenceType.forward:
                try:
                    orig = await client.get_channel(orig.reference.channel_id).fetch_message(orig.reference.message_id)
                except Exception as e:
                    author = orig.author
                    orig = orig.message_snapshots[0]
                    orig.author = author
                    description = '↪️ '
            description += orig.content if len(orig.content) < 100 else orig.content[0:100] + '...'
            if len(orig.attachments) + len(orig.stickers) > 0:
                description += ' 🖼️'
            jump_msg = msgs[orig_orig.id][0] if orig_orig.id in msgs else orig_orig
            description = f'[Ответ:]({jump_msg.jump_url}) ' + description
            reply_embed = Embed(color=orig.author.color, description=description)
            reply_embed.set_author(name=orig.author.display_name, icon_url=orig.author.display_avatar.url)
        else:
            reply_embed = Embed(description='Ответ')

    all_files = []
    files_embedded = False
    for a in msg.attachments:
        if len(await a.read()) > 10 * 1024 ** 2:
            msg.content += '\n' + (f'||{a.proxy_url}||' if a.is_spoiler() else a.proxy_url)
            files_embedded = True
        else:
            all_files.append(await a.to_file(spoiler=a.is_spoiler()))
    for s in msg.stickers:
        s = await s.fetch()
        if s.format == StickerFormatType.lottie:
            raise MoveError('Сообщение включает в себя lottie-стикер', msg)
        format = {StickerFormatType.png: 'png', StickerFormatType.apng: 'apng', StickerFormatType.gif: 'gif'}[s.format]
        r = await get_session().get(url=s.url)
        r = await r.read()
        if len(r) <= 10 * 1024 ** 2:
            all_files.append(File(BytesIO(r), filename=s.name + '.' + format, description=s.description))

    non_reply_embeds = files_embedded or len(msg.embeds) > 0
    msgs[orig_msg.id] = []
    for i in range(max(ceil(len(msg.content) / 2000), 1)):
        last = (i + 1) * 2000 >= len(msg.content)
        content = msg.content[i * 2000 : (i + 1) * 2000] # TODO: возможно можно как-то делить сообщения чтоб не ломать например ссылки и другую разметку
        files = all_files if last else MISSING
        embed = reply_embed if last and not non_reply_embeds else MISSING
        suppress_embeds = not last or (not reply_embed and not non_reply_embeds)
        new_msg = await wh.send(content=content, username=msg.author.display_name, avatar_url=msg.author.display_avatar.url, files=files, embed=embed, suppress_embeds=suppress_embeds, thread=MISSING if not isinstance(dst, Thread) else dst, wait=True)
        msgs[orig_msg.id].append(new_msg)
    if non_reply_embeds and reply_embed:
        new_msg = await wh.send(content=MISSING, username=msg.author.display_name, avatar_url=msg.author.display_avatar.url, embed=reply_embed, thread=MISSING if not isinstance(dst, Thread) else dst, wait=True)
        msgs[orig_msg.id].append(new_msg)

    msg = orig_msg

    if isinstance(msg.author, Member):
        authors.add(msg.author)

    for r in msg.reactions:
        async for user in r.users(type=ReactionType.normal):
            if isinstance(user, Member):
                authors.add(user)
        async for user in r.users(type=ReactionType.burst):
            if isinstance(user, Member):
                authors.add(user)
        try:
            await msgs[orig_msg.id][-1].add_reaction(r)
        except HTTPException as e: # TODO: это происходит, когда эмодзи реакции был удалён с сервера. теоретически решаемо, если бот её сам загрузит, а потом удалит
            pass

async def move(client: Client, msg: Message, dst: Messageable, interaction: Interaction = None, count: int = 1, delete: bool = True, on_error: Literal['cancel', 'stop', 'skip'] = 'cancel') -> dict[int, list[Message]]:
    """Перенос сообщений в другой канал или ветку."""
    first_send = False
    async def send(content: str) -> None:
        try:
            await interaction.followup.send(ephemeral=True, content=content)
        except Exception as e:
            pass

    if interaction:
        await interaction.response.defer(ephemeral=True, thinking=True)
        print('move', interaction.user.id, msg.id, dst.id, count, delete, on_error, flush=True)

    src = msg.channel
    msgs = {}
    authors = set()
    webhook = await (dst if not isinstance(dst, Thread) else dst.parent).create_webhook(name='вагонетка с сообщениями')

    try:
        try:
            await move_msg(client, msg, webhook, dst, msgs, authors)
        except MoveError as e:
            print('MoveError:', e.msg.id, e.message, flush=True)
            if interaction:
                await send(f'Не удалось скопировать сообщение с ID {e.msg.id} ({e.message.lower()}), обратитесь к участнику <@512545053223419924>.')
            if on_error != 'skip':
                raise e
        async for m in src.history(limit=count - 1 if count != 0 else None, after=msg):
            try:
                await sleep(1)
                await move_msg(client, m, webhook, dst, msgs, authors)
            except MoveError as e:
                print('MoveError:', e.msg.id, e.message, flush=True)
                if interaction:
                    await send(f'Не удалось скопировать сообщение с ID {e.msg.id} ({e.message.lower()}), обратитесь к участнику <@512545053223419924>.')
                if on_error != 'skip':
                    raise e
        if interaction and authors:
            msg = await webhook.send(content=', '.join([f'<@{user.id}>' for user in authors]), allowed_mentions=AllowedMentions(users=True), thread=MISSING if not isinstance(dst, Thread) else dst, wait=True)
            await msg.delete()
    except MoveError as e:
        if on_error == 'cancel':
            for msglist in msgs.values():
                for msg in msglist:
                    await msg.delete()
            if interaction:
                await send('Перенос сообщений отменён.' if delete else 'Копирование сообщений отменено.')
        else:
            if delete:
                for id in msgs:
                    msg = await src.fetch_message(id)
                    await msg.delete()
            if interaction:
                await send(f'Перенос остановлен, было перенесено {formatnum(len(msgs))}.' if delete else f'Копирование остановлено, было скопировано {formatnum(len(msgs))}.')
    else:
        if delete:
            for id in msgs:
                msg = await src.fetch_message(id)
                await msg.delete()
        if interaction:
            await send(f'Перенос успешен, было перенесено {formatnum(len(msgs))}.' if delete else f'Копирование успешно, было скопировано {formatnum(len(msgs))}.')

    await webhook.delete()

    return msgs

class MoveModal(Modal, title='Перенести'):
    """Параметры переноса."""
    channel=Label(
        text='Канал/ветка',
        component=ChannelSelect(
            channel_types=[ChannelType.text, ChannelType.voice, ChannelType.news, ChannelType.stage_voice, ChannelType.news_thread, ChannelType.public_thread, ChannelType.private_thread]
        )
    )

    count=Label(
        text='Количество сообщений',
        description='0 — все сообщения ниже, 1 — только текущее, 2+ — соответствующее количество сообщений ниже.',
        component=TextInput(default='0')
    )

    type=Label(
        text='Действие',
        component=Select(options=[
            SelectOption(label='Перенести', description='После копирования сообщений в указанный канал удалить их из текущего.', default=True),
            SelectOption(label='Скопировать', description='Только скопировать сообщения в указанный канал.')
        ])
    )

    onerror=Label(
        text='При ошибке',
        description='В случае, если при переносе сообщения произошла ошибка.',
        component=Select(options=[
            SelectOption(label='Отменить', value='cancel', description='Процесс будет остановлен, скопированные сообщения в указанном канале будут удалены.', default=True),
            SelectOption(label='Остановить', value='stop', description='Процесс будет остановлен.'),
            SelectOption(label='Пропустить', value='skip', description='Сообщение будет пропущено, процесс будет продолжен.')
        ])
    )

    def __init__(self, message: Message):
        super().__init__(timeout=None)
        self.message = message
        self.channel.component.required = True # TODO: temp hack until discord.py 2.6.4

    async def on_submit(self, interaction: Interaction):
        await move(interaction.client, self.message, interaction.client.get_channel(int(self.channel.component.values[0])), interaction, int(self.count.component.value), self.type.component.values[0] == 'Перенести', self.onerror.component.values[0])

async def request_move(interaction: Interaction, message: Message):
    """Контекстная команда переноса."""
    if interaction.permissions.manage_messages:
        await interaction.response.send_modal(MoveModal(message))
    else:
        await interaction.response.send_message(ephemeral=True, content='Использование команды «Перенести» доступно только модераторам сервера.')

async def on_ready(_: Client, tree: CommandTree) -> None:
    """Запуск модуля."""
    tree.context_menu(name='Перенести')(request_move)
