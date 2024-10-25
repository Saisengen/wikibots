"""Поиск замен территориальной принадлежности"""

import datetime
import os
import time
import re
from urllib.parse import quote
import pywikibot
import requests


DISCORD_URL = f'{os.environ["DISCORD_URL_WEBHOOK"]}{os.environ["SERVICE_WEBHOOK"]}'
USER_AGENT = {'User-Agent': 'RUKR; iluvatar@tools.wmflabs.org; python3.11'}
whitelist = requests.get(url=f'https://swviewer.toolforge.org/php/getGlobals.php?'
                             f'ext_token={os.environ["SWVIEWER_BACKEND_TOKEN"]}&user=DS', headers=USER_AGENT, timeout=60).json()['users']
SUSPICIOUS = [['россия', 'украина'], ['россии', 'украине'], ['россии', 'украины'], ['российск', 'украинск'],
              ['росія', 'україна'], ['росії', 'україни'], ['російськи', 'українськи']]


def is_reverted(current_site, title: str, timestamp: str) -> bool:
    """Проверка, откачена ли уже правка. Проверка по хэшам ревизий, нет ли после неё правки
    с хэшем идущей перед ней правки.

    Args:
        :param current_site: pywikibot-сущность сайта
        :param title: название статьи
        :param timestamp: таймстамп правки в формате MediaWiki
    :return: True если правка откачена
    """
    parent_timestamp, sha1 = None, None
    for revision in pywikibot.Page(current_site, title).revisions(starttime=timestamp, total=2):
        parent_timestamp = revision['timestamp']
    for revision in pywikibot.Page(current_site, title).revisions(starttime=parent_timestamp, reverse=True):
        if sha1 is None:
            sha1 = revision['sha1']
        else:
            if sha1 is not None and sha1 == revision['sha1']:
                return True
    return False


def get_trigger_text(text: str, susp: str, type_process: str) -> str:
    """Возвращает оформленный и обрезанный текст с изменением территориальными принадлежностями.

    Args:
        :param text: текст фрагмента
        :param susp: территориальная принадлежность по триггеру
        :param type_process: тип изменения: добавлено (+) или убрано (-)
    :return: готовый текст для публикации
    """

    type_process = type_process if type_process != '-' else '\\-'
    if len(text) > 300:
        finded = re.findall('(.{0,100}' + susp + '.{0,100})', text, flags=re.MULTILINE | re.IGNORECASE)
        text = ' […] '.join(finded)
    return f'**{type_process}** {re.sub(susp, "`>>>>" + susp.upper() + "<<<<`", text, flags=re.IGNORECASE)}'


def change_check(site, susp, diff, change) -> None:
    """Поиск замен территориальной принадлежности.

    Args:
        :param site: pywikibot-сущность сайта
        :param susp: список возможно-подозрительных территориальных принадлежностей при замене
        :param diff: pywikibot-сущность (compare) разницы между двумя версиями
        :param change: pywikibot-сущность правки из метода recentchange
    """
    diff_parsed = pywikibot.diff.html_comparator(diff)
    deleted_lines, added_lines = diff_parsed['deleted-context'], diff_parsed['added-context']
    total_per_change = 0
    for index, deleted_line in enumerate(deleted_lines):
        if total_per_change == 2:  # не более двух выдач на правку, если замен больше
            break
        if len(added_lines) > index:
            deleted_lines_lower = set(map(lambda x: x.lower(), deleted_line.split()))
            added_lines_lower = set(map(lambda x: x.lower(), added_lines[index].split()))
            for sus_elem in susp + list(map(lambda x: x[::-1], susp)):
                if (len([i for i in deleted_lines_lower if sus_elem[0] in i]) > 0 and
                        len([i for i in deleted_lines_lower if sus_elem[1] in i]) == 0):
                    if (len([i for i in added_lines_lower if sus_elem[1] in i]) > 0 and
                            (len([i for i in added_lines_lower if sus_elem[0] in i]) == 0)):
                        if 'user' in change and change['user'] not in whitelist:
                            user_info = site['site'].users(change['user'])
                            for user in user_info:
                                user_info = user
                                break
                            if 'groups' not in user_info or (
                                    len(set(user_info['groups']) & {'sysop', 'bot', 'editor', 'autoreview'}) == 0 and
                                    user_info['editcount'] <= 1000):
                                if not is_reverted(site['site'], change['title'], change['timestamp']):
                                    # print(f'{site["site"].lang}: {change["title"]}:'
                                    #       f'\n{deleted_line}\n{added_lines[index]}')

                                    url = (f'https://{site["site"].lang}.wikipedia.org/w/index.php?diff='
                                           f'{change["revid"]}')
                                    description = (f'{get_trigger_text(deleted_line, sus_elem[0], "-")}\n\n'
                                                   f'{get_trigger_text(added_lines[index], sus_elem[1], "+")}')[:3500]
                                    data = {'content': '', 'embeds': [{'type': 'rich', 'title': change['title'],
                                                                       'author': {'name': change['user'],
                                                                                  'url': f'https://{site["site"].lang}.'
                                                                                         f'wikipedia.org/wiki/'
                                                                                         f'special:contribs/'
                                                                                         f'{quote(change["user"])}'},
                                                                       'description': description, 'color': 0x0000FF,
                                                                       'url': url}]}
                                    total_per_change += 1
                                    requests.post(DISCORD_URL, json=data, headers=USER_AGENT, timeout=30)
                                    break
                                # print(f'Уже отменено: {site["site"].lang}, {change["title"]}, {change["revid"]}')


def main() -> None:
    """Получение свежих правок циклом."""
    print('Бот запущен.')
    site_ru = pywikibot.site.APISite(code='ru', fam='wikipedia', user='IluvatarBot')
    site_uk = pywikibot.site.APISite(code='uk', fam='wikipedia', user='IluvatarBot')

    sites = [{
            'site': site_ru, 'end_time': site_ru.server_time() - datetime.timedelta(minutes=120), 'end_id': None,
            'end_id_prev': None}, {'site': site_uk, 'end_time': site_uk.server_time() - datetime.timedelta(minutes=120),
            'end_id': None, 'end_id_prev': None}]

    try:
        while True:
            for site in sites:
                changes = site['site'].recentchanges(namespaces=0, bot=False, patrolled=False, changetype='edit',
                    end=site['end_time'])
                site['end_time'], site['end_id'], site['end_id_prev'] = None, None, site['end_id']
                for change in changes:
                    if site['end_id'] is None and site['end_id'] is None:
                        site['end_time'], site['end_id'] = change['timestamp'], change['revid']
                    if change['revid'] == site['end_id_prev']:
                        continue
                    try:
                        html_diff = site['site'].compare(old=change['old_revid'], diff=change['revid'])
                    except Exception:
                        continue
                    change_check(site, SUSPICIOUS, html_diff, change)


            time.sleep(55)
    except KeyboardInterrupt:
        pass


if __name__ == '__main__':
    main()
