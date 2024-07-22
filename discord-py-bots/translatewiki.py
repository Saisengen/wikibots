import time
import toolforge
import pymysql
import requests
import configparser

API = 'https://translatewiki.net/w/api.php'
DS_URL = 'https://discord.com/api/webhooks/'
tools = ['Swviewer', 'Convenient-discussions']
langs = ['ru', 'uk', 'be', 'be-tarask', 'en', 'qqq']
namespaces = "1206|1207"  # wikimedia, wikimedia talk. Можно добавить выделенные пространства

results, cont, timestamp_new = [], "", ""

DEBUG = {"enable": False, "SQL": {"user": "s55857", "pass": "", "port": 4711}}

config_bot = configparser.ConfigParser()
config_path = "config-py.ini" if DEBUG["enable"] is True else "configs/config-py.ini"
config_bot.read(config_path)
DEBUG["SQL"]["pass"] = config_bot["MAIN"]["DB_pass"]
TOKEN = config_bot["MAIN"]["TW_token"]

try:
    if DEBUG["enable"]:
        conn = pymysql.connections.Connection(user=DEBUG["SQL"]["user"], port=DEBUG["SQL"]["port"],
                                              password=DEBUG["SQL"]["pass"], database="s55857__rv", host='127.0.0.1')
    else:
        conn = toolforge.toolsdb("s55857__rv")
    with conn.cursor() as cur:
        cur.execute(f"SELECT timestmp FROM TW LIMIT 1;")
        res = cur.fetchall()
        timestamp = res[0][0]
except Exception as e:
    print(e)
    exit()


def sender(edit) -> None:
    if edit['type'] == 'new':
        url = f'https://translatewiki.net/?oldid={edit["revid"]}'
        color = 0xff0008
    else:
        url = f'https://translatewiki.net/?oldid={edit["old_revid"]}&diff={edit["revid"]}'
        color = 0x7cfc00
    payload = {"content": "", "tts": False, "embeds": [
        {"type": "rich", "title": edit['title'], "description": f"User:{edit['user']}", "color": color,
         "url": url}]}
    requests.post(url=f"{DS_URL}{TOKEN}", json=payload)


while cont is not None:
    data = {"action": "query", "format": "json", "list": "recentchanges", "utf8": 1, "rcend": timestamp,
            "rcdir": "older", "rcnamespace": "1206|1207", "rcprop": "title|ids|user|timestamp", "rclimit": 500,
            "rctype": "edit|new"}
    if cont != "":
        data["rccontinue"] = cont
    r = requests.post(url=API, data=data).json()
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
        for tool in tools:
            if ((f'Wikimedia:{tool}-' in page['title'] or f'Wikimedia talk:{tool}-' in page['title']) and
                    page['title'].split('/')[1] in langs):
                sender(page)
    # Для выделенных пространств
    else:
        if page['title'].split('/') in langs:
            sender(page)

# сохранение timestamp_new
try:
    if DEBUG["enable"]:
        conn = pymysql.connections.Connection(user=DEBUG["SQL"]["user"], port=DEBUG["SQL"]["port"],
                                              password=DEBUG["SQL"]["pass"], database="s55857__rv", host='127.0.0.1')
    else:
        conn = toolforge.toolsdb("s55857__rv")
    with conn.cursor() as cur:
        cur.execute(f"UPDATE TW SET timestmp = '{timestamp_new}';")
        conn.commit()
        conn.close()
except Exception as e:
    print(f"send_to_db error 1: {e}")
