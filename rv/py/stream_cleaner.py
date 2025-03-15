"""Очистка лент от уже отменённых правок и правок на удалённых страницах."""

import os
import asyncio
import logging
import discord
import aiohttp
import re
# import time
from discord.ext import tasks

logging_handler = logging.FileHandler(filename='logs/antclr.log', encoding='utf-8', mode='w')

domain_regexp = re.compile(r'(\w+\.wikipedia\.org|wikidata\.org|commons\.wikimedia\.org)')

async def flagged_check(url: str, title: str, rev_id: int, session: aiohttp.ClientSession) -> bool | None:
    """Проверка на наличие более новых проверенных ревизий."""
    data = {'action': 'query', 'prop': 'info|flagged', 'titles': title, 'format': 'json', 'utf8': 1}
    try:
        r = await session.post(url=url, data=data)
        r = await r.json()
        if 'query' not in r or 'pages' not in r['query'] or len(r['query']['pages']) == 0:
            return False
        page_id = list(r['query']['pages'].keys())[0]
        if 'flagged' not in r['query']['pages'][page_id] or 'stable_revid' \
                not in r['query']['pages'][page_id]['flagged']:
            return False
        if r['query']['pages'][page_id]['flagged']['stable_revid'] >= int(rev_id):
            return True
    except Exception as e:
        print(f'flagged_check 1: {e}')


async def revision_check(url: str, rev_id: int, title: str, session: aiohttp.ClientSession) -> bool | None:
    """Проверка ревизий на предмет отмены по хэшу."""

    # получаем информацию о странице - page_id, title, заодно теги, имя юзера и проверяем, не удалена ли страница
    data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'tags|user',
            'revids': rev_id, 'format': 'json', 'utf8': 1}
    try:
        r = await session.post(url=url, data=data)
        r_status = r.status
        r = await r.json()
    except Exception as e:
        print(f'revision_check 1: {e}')
        return False
    if 'query' not in r:
        print(r)
        print(data)
        return False
    if r_status == 404 or 'badrevids' in r['query'] or '-1' in r['query']['pages'] or 'missing' in r['query']['pages']:
        return True
    page = r['query']['pages'][str(list(r['query']['pages'].keys())[0])]
    if 'revisions' not in page or '-1' in page['revisions'] or len(page['revisions']) < 1 or \
            'mw-reverted' in page['revisions'][0]['tags']:
        return True
    # запрашиваем хэш предыдущей правки, сделанной не целевым юзером
    data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'sha1', 'rvlimit': 1, 'rvstartid': rev_id,
            'titles': title, 'format': 'json', 'utf8': 1}
    if 'user' in page['revisions'][0]:
        data['rvexcludeuser'] = page['revisions'][0]['user']
    try:
        r2 = await session.post(url=url, data=data)
        r2 = await r2.json()
        page_id = str(list(r2['query']['pages'].keys())[0])
        page = r2['query']['pages'][page_id]
    except Exception as e:
        print(f'revision_check 2: {e}')
        return False
    if 'revisions' in page and len(page['revisions']) > 0 and 'sha1' in page['revisions'][0]:
        sha1 = page['revisions'][0]['sha1']
        # Запрашиваем хэши всех правок до целевой ревизии
        # здесь можно было бы уже во втором запросе запросить все ревизии и третий не слать. Не уверен, что
        # предпочтительнее с т.з нагрузки на сервер: два запроса или один с неограниченным кол-вом ревизий
        data = {'action': 'query', 'prop': 'revisions', 'rvslots': '*', 'rvprop': 'sha1', 'rvlimit': 50,
                'rvendid': rev_id, 'format': 'json', 'utf8': 1, 'titles': title}
        try:
            r3 = await session.post(url=url, data=data)
            r3 = await r3.json()
        except Exception as e:
            print(f'revision_check 3: {e}')
            return False
        page = r3['query']['pages'][page_id]
        if 'revisions' in page and len(page['revisions']) > 0 and \
                len([i for i in page['revisions'] if 'sha1' in i and i['sha1'] == sha1]) > 0:
            return True


if __name__ == '__main__':
    # ID каналов и ID целевого участника (бота)
    CONFIG = {'IDS':
              [1212498198200062014, 1219273496371396681, 1342471984671625226, 1348216089825509450, 1348216377789382656],
              'BOTS': [1225008116048072754], 'TOKEN': os.environ['DISCORD_BOT_TOKEN']}
    USER_AGENT = {'User-Agent': 'D-V-C; iluvatar@tools.wmflabs.org; python3.11; requests'}
    Intents = discord.Intents.default()
    Intents.members, Intents.message_content = True, True
    discord.Intents.all()
    client = discord.Client(intents=Intents)


    # Получение старых сообщений (задержка в секундах)
    @tasks.loop(seconds=60.0)
    async def get_messages() -> None:
        """Функция запроса сообщений из лент"""

        session = aiohttp.ClientSession(headers=USER_AGENT)
        for channel_id in CONFIG['IDS']:
            try:
                channel = client.get_channel(channel_id)
            except Exception as e:
                print(f'get_messages 1: {e}')
                continue
            try:
                messages = channel.history(limit=30, oldest_first=False)
                async for msg in messages:
                    if msg.author.id in CONFIG['BOTS'] and len(msg.embeds) > 0:
                        domain = domain_regexp.search(msg.embeds[0].url).group()
                        rev_id = msg.embeds[0].url.split('diff=')[1] if 'ilu=' not in msg.embeds[0].url else (
                            msg.embeds[0].url.split('ilu='))[1]
                        api_url = f'https://{domain}/w/api.php'
                        status = await revision_check(api_url, rev_id, msg.embeds[0].title, session)
                        if not status:
                            status = await flagged_check(api_url, msg.embeds[0].title, rev_id, session)
                        if status:
                            try:
                                await msg.delete()
                                await asyncio.sleep(3.0)
                            except Exception as e:
                                print(f'get_messages 2: {e}')
            except Exception as e:
                print(f'get_messages 3: {e}')
        try:
            await session.close()
        except Exception as e:
            print(f'get_messages 4: {e}')


    @client.event
    async def on_ready() -> None:
        """Событие после запуска бота."""

        try:
            if not get_messages.is_running():
                await get_messages.start()
        except Exception as e:
            print(f'on_ready 1: {e}')


    client.run(token=CONFIG['TOKEN'], reconnect=True, log_handler=logging_handler)
