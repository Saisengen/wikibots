# бот для уведомления о новых запросах помощи от новичков:
# https://ru.wikipedia.org/wiki/Категория:Википедия:Требуется_помощь

from time import sleep
import os
import requests
from urllib.parse import quote

URL = "https://ru.wikipedia.org/w/api.php"
WEBHOOK = f'https://discord.com/api/webhooks/{os.environ["HELP_CALLER_WEBHOOK"]}'
USER_AGENT = {'User-Agent': 'newbie_help_caller; iluvatar@tools.wmflabs.org; python3.11'}


def posting(title: str) -> None:
    data = {'content': '', 'embeds': [{'type': 'rich', 'title': 'Запрос помощи от новичка',
                                       'description': title, 'color': 0xFF8000,
                                       'url': f'https://ru.wikipedia.org/wiki/{quote(title)}'}]}
    try:
        pass
        requests.post(WEBHOOK, json=data, timeout=30)
    except Exception as e:
        print(f"Error 1.0: {e}")


while True:
    # На Тулфорже невозможно простым способом записать переменные окружерния из процесса
    f = open('py/other/newbie_help_current.txt', 'r+')
    TIMESTAMP, PAGE_ID = f.readlines()[0].split('|')
    f.close()
    print(PAGE_ID)

    PARAMS = {"cmdir": "desc", "format": "json", "list": "categorymembers", "action": "query", "utf8": 1,
              "cmtitle": "Категория:Википедия:Требуется помощь", "cmsort": "timestamp", "cmprop": "title|timestamp|ids",
              "cmend": TIMESTAMP}

    try:
        r = requests.get(url=URL, params=PARAMS, headers=USER_AGENT).json()
        pages = r["query"]["categorymembers"]
    except Exception as e:
        print(f"Error 1.1: {e}")
    else:
        for page in pages:
            print(page)
            if str(page['pageid']) == PAGE_ID and page["timestamp"] == TIMESTAMP:
                continue

            f = open('py/other/newbie_help_current.txt', 'w+')
            f.write(f'{page["timestamp"]}|{page["pageid"]}')
            f.close()

            posting(page["title"])
    finally:
        sleep(300)  # 5 min
