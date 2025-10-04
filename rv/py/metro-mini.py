"""Модуль метро."""

from discord import Interaction, Client
from discord.app_commands import CommandTree
from antivand_utils import get_session

async def last_metro(interaction: Interaction) -> None:
    """Узнать время последнего запуска бота #транспорт."""
    await interaction.response.defer(ephemeral=True)
    r = await get_session().get(url='https://rv.toolforge.org/metro/')
    r = await r.text()
    await interaction.followup.send(ephemeral=True, content=r.split('<br>')[0].replace('Задание запущено', 'Последний запуск задания:'))

async def on_ready(_: Client, tree: CommandTree) -> None:
    """Запуск модуля."""
    tree.command()(last_metro)
