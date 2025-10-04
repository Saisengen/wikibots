"""Бот сервера Русской Википедии."""

import logging
from discord import Client, Message, Interaction, Intents, AllowedMentions, Status, Game, Guild
from discord.app_commands import CommandTree
from discord.ext import tasks
from antivand_utils import DISCORD_TOKEN, SERVERS, ADMINS, now

import antivand_kaput
import antivand_ad
import antivand_metro
import antivand_mover

client = Client(intents=Intents(guilds=True, members=True, expressions=True, guild_messages=True, guild_reactions=True, message_content=True), allowed_mentions=AllowedMentions.none())
tree = CommandTree(client)

@tree.error
async def on_error(interaction: Interaction, error: Exception) -> None:
    """Обработчик ошибок в командах."""
    if 'autocomplete' not in str(error):
        raise error

@tree.command()
async def help(interaction: Interaction) -> None:
    """Список команд бота."""
    await interaction.response.send_message(ephemeral=True, content=
        '</help:1388457659484733501> — список команд бота.\n'
        '</clear_feed:1419287560110342229> — очистка каналов от всех сообщений бота.\n'
        '</last_metro:1320698385241739311> — время последнего запуска бота <#1220480407796187330>.\n\n'
        '</users list:1419287560110342226> — список участников, кому разрешены действия через бота.\n'
        '</users add:1419287560110342226> — разрешить участнику действия через бота.\n'
        '</users remove:1419287560110342226> — запретить участнику действия через бота.\n\n'
        '</stats list:1419287560110342227> — статистика действий через бота.\n'
        '</stats remove:1419287560110342227> — удалить статистику действий участника.\n\n'
        '/ad (текстовая команда) — отправить сообщение, которое автоматически удалится через сутки.\n\n'
        '«Перенести» (команда в контекстном меню) — перенести одно или несколько сообщений в другой канал или ветку.\n\n'
        f'По вопросам работы бота обращайтесь к <@{ADMINS[0]}>.')

@tasks.loop(seconds=15.0)
async def loop(client: Client) -> None:
    """Фоновый цикл."""
    start = now()

    await antivand_ad.loop(client)
    await antivand_kaput.loop(client)

    end = now()
    if start.timestamp() % 3600 < 15: # first iteration in an hour
        print(f'{start.strftime(time_format)} - loop iteration {loop.current_loop:04} finished in {min(round((end - start).seconds), 99):02} seconds - {end.strftime(time_format)}', flush=True)

@loop.after_loop
async def loop_error() -> None:
    """Обработка ошибок в фоновом цикле."""
    loop._task.add_done_callback(lambda _: loop._task.exception()) # just ignore exception
    loop.restart(client)

@client.event
async def on_message(msg: Message) -> None:
    """Получение нового сообщения."""
    await antivand_ad.on_message(client, msg)
    await antivand_kaput.on_message(client, msg)

@client.event
async def on_ready() -> None:
    """Запуск бота."""
    for guild in client.guilds:
        if guild.id not in SERVERS:
            await guild.leave()
    await client.change_presence(status=Status.online, activity=Game('pyCharm'))
    logging.warning('Запуск фоновых задач')
    loop.start(client)
    logging.warning('Запуск модулей')
    await antivand_kaput.on_ready(client, tree)
    await antivand_metro.on_ready(client, tree)
    await antivand_mover.on_ready(client, tree)
    logging.warning('Синхронизация команд')
    await tree.sync()
    logging.warning('Бот запущен')

@client.event
async def on_guild_join(guild: Guild) -> None:
    """Присоединение бота к новому серверу."""
    if guild.id not in SERVERS:
        await guild.leave()

formatter = logging.Formatter(fmt='%(asctime)s - %(levelname)s - %(message)s')
time_format = formatter.default_time_format
client.run(token=DISCORD_TOKEN, log_formatter=formatter, log_level=logging.WARNING, root_logger=True)
