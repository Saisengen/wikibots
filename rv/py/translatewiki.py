"""Проверка изменений в переводах отслеживаемых проектов тна Translatewiki.net"""

import os
import time
import toolforge
import pymysql
import requests

API = 'https://translatewiki.net/w/api.php'
USER_AGENT = {'User-Agent': 'TW; iluvatar@tools.wmflabs.org; python3.11'}
TOOLS = ['Swviewer', 'Convenient-discussions']
LANGS = ['ru', 'uk', 'be', 'be-tarask', 'en', 'qqq']
NAMESPACES = '1206|1207'  # wikimedia, wikimedia talk. Можно добавить выделенные пространства

results, cont, timestamp_new = [], '', ''
DEBUG = {'ENABLE': False, 'port': 4711}
DB_CREDITS = {'user': os.environ['TOOL_TOOLSDB_USER'], 'port': DEBUG['port'], 'host': '127.0.0.1',
              'password': os.environ['TOOL_TOOLSDB_PASSWORD'], 'database': f'{os.environ["TOOL_TOOLSDB_USER"]}__rv'}


def sender(edit) -> None:
    """
    Отправка сообщпения в Discord.
    """
    if edit['type'] == 'new':
        url = f'https://translatewiki.net/?oldid={edit["revid"]}'
        color = 0xff0008
    else:
        url = f'https://translatewiki.net/?oldid={edit["old_revid"]}&diff={edit["revid"]}'
        color = 0x7cfc00
    payload = {'content': '', 'tts': False, 'embeds': [
        {'type': 'rich', 'title': edit['title'], 'description': f'User:{edit["user"]}', 'color': color, 'url': url}]}
    requests.post(url=f'{os.environ["DISCORD_URL_WEBHOOK"]}{os.environ["TW_WEBHOOK"]}', json=payload, headers=USER_AGENT, timeout=30.0)

try:
    conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG['ENABLE'] else (
        toolforge.toolsdb(DB_CREDITS['database']))
    with conn.cursor() as cur:
        cur.execute('SELECT timestmp FROM TW LIMIT 1')
        res = cur.fetchall()
        timestamp = res[0][0]
except Exception as e:
    print(e)
else:
    while cont is not None:
        data = {'action': 'query', 'format': 'json', 'list': 'recentchanges', 'utf8': 1, 'rcend': timestamp,
                'rcdir': 'older', 'rcnamespace': NAMESPACES, 'rcprop': 'title|ids|user|timestamp', 'rclimit': 500,
                'rctype': 'edit|new'}
        if cont != '':
            data['rccontinue'] = cont
        r = requests.post(url=API, data=data, headers=USER_AGENT, timeout=30.0).json()
        results += r['query']['recentchanges']
        if cont == '':
            timestamp_new = r['query']['recentchanges'][0]['timestamp']
        cont = None if 'continue' not in r else r['continue']['rccontinue']
        time.sleep(5)

    for page in results:
        if page['timestamp'] == timestamp:
            continue
        # Для пространств wikimedia и wikimedia talk
        if page['ns'] in [1206, 1207]:
            for tool in TOOLS:
                if ((f'Wikimedia:{tool}-' in page['title'] or f'Wikimedia talk:{tool}-' in page['title']) and
                        page['title'].split('/')[1] in LANGS):
                    sender(page)
        # Для выделенных пространств
        else:
            if page['title'].split('/') in LANGS:
                sender(page)

    # сохранение timestamp_new
    try:
        conn = pymysql.connections.Connection(**DB_CREDITS) if DEBUG['ENABLE'] else (
            toolforge.toolsdb(DB_CREDITS['database']))
        with conn.cursor() as cur:
            cur.execute(f'UPDATE TW SET timestmp = "{timestamp_new}";')
            conn.commit()
            conn.close()
    except Exception as e:
        print(f'send_to_db error 1: {e}')
