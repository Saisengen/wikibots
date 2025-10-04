"""Модуль автоудаляемых сообщений."""

from typing import Literal
from discord import Client, Message
from antivand_utils import get_cursor, to_thread, now
import antivand_mover

@to_thread
def db_store_ad_message(msg_ids: str) -> None:
    """Сохранение данных об автоудаляемом сообщении в БД."""
    msg_timestamp = int(now().timestamp())
    with get_cursor() as cur:
        cur.execute('INSERT INTO ad (msg_ids, msg_timestamp) VALUES (%s, %s)', (msg_ids, msg_timestamp))

@to_thread
def db_get_expired_ads() -> list[dict[Literal['channel_id', 'message_id'], int]]:
    """Получение списка просроченных автоудаляемых сообщений и их удаление из БД."""
    result_out = []
    timestamp = int(now().timestamp()) - 86400
    with get_cursor() as cur:
        cur.execute('SELECT msg_ids, msg_timestamp FROM ad WHERE msg_timestamp < %s', timestamp)
        for line in cur.fetchall():
            channel_id, message_id = line[0].split('|')
            result_out.append({'channel_id': int(channel_id), 'message_id': int(message_id)})
        cur.execute('SELECT @@GLOBAL.read_only')
        if not cur.fetchone()[0]:
            cur.execute('DELETE FROM ad WHERE msg_timestamp < %s', timestamp)
    return result_out

async def loop(client: Client) -> None:
    """Удаление старых автоудаляемых сообщений."""
    ads = await db_get_expired_ads()
    for ad in ads:
        try:
            msg = await client.get_channel(ad['channel_id']).fetch_message(ad['message_id'])
            await msg.delete()
        except Exception as e:
            pass

async def on_message(client: Client, msg: Message) -> None:
    """Обработка автоудаляемого сообщения."""
    if not msg.content.startswith('/ad ') and not msg.content.startswith('/ad\n') and msg.content != '/ad':
        return
    msg.content = msg.content[4:]
    msgs = await antivand_mover.move(client, msg, msg.channel)
    for new_msg in msgs[msg.id]:
        await db_store_ad_message(f'{new_msg.channel.id}|{new_msg.id}')
