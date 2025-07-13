"""Взаимодействие с БД."""

import asyncio
from pymysql.connections import Connection
from pymysql.cursors import Cursor
from toolforge import toolsdb
from functools import wraps
from contextlib import contextmanager
from threading import Lock
from typing import Callable, Coroutine, Generator, Literal
from datetime import datetime, timezone
from discord import Embed
from antivand_config import DEBUG, DB_CREDITS

lock = Lock()
conn = Connection(**DB_CREDITS) if DEBUG else toolsdb(DB_CREDITS['database'])

def now() -> datetime:
    return datetime.now(timezone.utc)

@contextmanager
def get_cursor() -> Generator[Cursor, None, None]:
    """Получение курсора БД и перезапуск соединения, если требуется."""
    try:
        lock.acquire()
        conn.ping()
        yield conn.cursor()
    finally:
        lock.release()

def to_thread(func: Callable) -> Coroutine:
    @wraps(func)
    async def wrapper(*args, **kwargs):
        return await asyncio.to_thread(func, *args, **kwargs)
    return wrapper

@to_thread
def db_record_action(actor: str, action_type: str, embed: Embed, bad: bool = False) -> None:
    """Запись действия в БД."""
    triggers_dict = {'#ff0000': 'patterns', '#ffff00': 'LW', '#ff00ff': 'ORES', '#00ff00': 'tags', '#ffffff': 'replaces', '#ff8000': 'LW', '#00ffff': 'replaces'}
    color = str(embed.color)
    trigger = triggers_dict[color] if color in triggers_dict else 'unknown'
    with get_cursor() as cur:
        cur.execute('SELECT name FROM ds_antivandal WHERE name=%s;', actor)
        res = cur.fetchone()
        if res:
            cur.execute(f'UPDATE ds_antivandal SET {action_type} = {action_type}+1, {trigger} = {trigger}+1 WHERE name = %s;', actor)
        else:
            cur.execute(f'INSERT INTO ds_antivandal (name, {action_type}, {trigger}) VALUES (%s, 1, 1);', actor)
        if bad:
            cur.execute(f'UPDATE ds_antivandal_false SET {trigger} = {trigger}+1 WHERE result = "stats";')
        conn.commit()

@to_thread
def db_get_stats(actor: str = None) -> dict[Literal['rollbacks', 'undos', 'approves', 'rfd', 'total', 'patterns', 'LW', 'ORES', 'tags', 'replaces', 'false_triggers'], int | list[tuple[str, int]] | list[int]]:
    """Получение статистики из БД."""
    with get_cursor() as cur:
        if not actor:
            cur.execute('SELECT SUM(rollbacks), SUM(undos), SUM(approves), SUM(patterns), SUM(LW), SUM(ORES), SUM(tags), SUM(rfd), SUM(replaces) FROM ds_antivandal;')
            r = cur.fetchone()
            cur.execute('SELECT name, rollbacks + undos + approves + rfd AS am FROM ds_antivandal WHERE name != "service_account" ORDER BY am DESC LIMIT 5;')
            total = cur.fetchall()
            cur.execute('SELECT patterns, LW, ORES, tags, replaces FROM ds_antivandal_false WHERE result = "stats";')
            false_triggers = cur.fetchone()
        else:
            cur.execute('SELECT rollbacks, undos, approves, patterns, LW, ORES, tags, rfd, replaces FROM ds_antivandal WHERE name=%s;', actor)
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
        cur.execute('DELETE FROM ds_antivandal WHERE name=%s;', actor)
        conn.commit()

@to_thread
def db_store_ad_message(msg_ids: str) -> None:
    """Сохранение данных об автоудаляемом сообщении в БД."""
    msg_timestamp = int(now().timestamp())
    with get_cursor() as cur:
        cur.execute(f'INSERT INTO ad (msg_ids, msg_timestamp) VALUES ("{msg_ids}", {msg_timestamp});')
        conn.commit()

@to_thread
def db_get_expired_ads() -> list[dict[Literal['channel_id', 'message_id'], int]]:
    """Получение списка просроченных автоудаляемых сообщений и их удаление из БД."""
    result_out = []
    timestamp = int(now().timestamp()) - 86400
    with get_cursor() as cur:
        cur.execute(f'SELECT msg_ids, msg_timestamp FROM ad WHERE msg_timestamp < {timestamp};')
        for line in cur.fetchall():
            channel_id, message_id = line[0].split('|')
            result_out.append({'channel_id': int(channel_id), 'message_id': int(message_id)})
        cur.execute(f'DELETE FROM ad WHERE msg_timestamp < {timestamp};')
        conn.commit()
    return result_out

@to_thread
def db_fetch_users() -> dict[int, str]:
    """Получение списка доверенных участников с их айди Дискорда из БД."""
    with get_cursor() as cur:
        cur.execute('SELECT antivand_users.* FROM antivand_users LEFT JOIN ds_antivandal ON antivand_users.wiki_name = ds_antivandal.name ORDER BY ds_antivandal.rollbacks + ds_antivandal.undos + ds_antivandal.approves + ds_antivandal.rfd DESC, antivand_users.wiki_name ASC;')
        return dict(cur.fetchall())

@to_thread
def db_update_users(action: Literal['add', 'remove'], discord_id: int | None, wiki_name: str) -> None:
    """Обновление списка доверенных участников в БД."""
    with get_cursor() as cur:
        if action == 'add':
            cur.execute(f'INSERT INTO antivand_users (discord_id, wiki_name) VALUES ({discord_id}, %s);', wiki_name)
        else:
            cur.execute(f'DELETE FROM antivand_users WHERE wiki_name=%s', wiki_name)
        conn.commit()
