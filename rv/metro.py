import toolforge
import requests
import time
import os

URL_WIKI = 'https://ru.wikipedia.org/wiki/Участник:IKhitron/metro.json?action=raw'
API_WIKI = 'https://ru.wikipedia.org/w/api.php'
USER_AGENT = {'User-Agent': 'metro; iluvatar@tools.wmflabs.org; python3.12; requests'}
CONTENT_RESULT = ''

r = requests.post(url=URL_WIKI, timeout=30.0).json()
moscow_page = r['Moscow']['page']
moscow_template = r['Moscow']['template']

moscow_list_1 = ["'{}'".format(i.replace("'", "\\'").replace("_", " ")) for i in r['Moscow']["lists"][0]]
moscow_list_2 = ["'{}'".format(i.replace("'", "\\'").replace("_", " ")) for i in r['Moscow']["lists"][1]]

SPb_page = r['SPb']["page"]
SPb_template = r['SPb']["template"]
SPb_list_1 = ["'{}'".format(i.replace("'", "\\'").replace("_", " ")) for i in r['SPb']["lists"][0]]
SPb_list_2 = ["'{}'".format(i.replace("'", "\\'").replace("_", " ")) for i in r['SPb']["lists"][1]]

moscow_list_1_str = f"({', '.join(moscow_list_1)})" if len(moscow_list_1) > 0 else "('пустой список')"
moscow_list_2_str = f"({', '.join(moscow_list_2)})" if len(moscow_list_2) else "('пустой список')"

SPb_list_1_str = f"({', '.join(SPb_list_1)})" if len(SPb_list_1) > 0 else "('пустой список')"
SPb_list_2_str = f"({', '.join(SPb_list_2)})" if len(SPb_list_2) > 0 else "('пустой список')"


def list_chunks(x: list[str], n: int):
    for i in range(0, len(x), n):
        yield x[i:i + n]


def check_page_exists(pages: list[str], description: str):
    pages_parts = list(list_chunks(pages, 50))
    pages_not_exists = []
    for part in pages_parts:
        part = '|'.join([p[1:-1] for p in part])

        data = {'action': 'query', 'format': 'json', 'prop': 'info', 'titles': part, 'utf8': 1}
        r2 = requests.post(url=API_WIKI, data=data, headers=USER_AGENT, timeout=30.0).json()
        pages_not_exists += [r2['query']['pages'][p]['title'] for p in r2['query']['pages'] if int(p) < 0]
    if len(pages_not_exists) > 0:
        content = (f'В списке [{description}](<https://w.wiki/A3rg>) есть отсутствующие в проекте статьи: '
                   f'{", ".join(pages_not_exists)}.')
        return content + '\n'
    else:
        return ''


def sql_calls(page: int, template: int, pages_list_1: str, pages_list_2: str, description: str):
    sql_1 = f"""
    select replace(lt_title, '_', ' ') as 'a' from pagelinks join linktarget
    on pl_target_id = lt_id
    where pl_from = {page}
    and not lt_namespace
    and not lt_title in
    (select page_title from page
     where page_id in
     (select tl_from from templatelinks
      where tl_target_id = {template})
     and not page_namespace)
    and not exists
    (select * from page
     where page_title = lt_title
     and page_namespace = lt_namespace
     and page_is_redirect
     and replace(page_title, '_', ' ') in {pages_list_1});
     """

    sql_2 = f"""
    select replace(page_title, '_', ' ') as 'a' from page
    where replace(page_title, '_', ' ') in {pages_list_1}
    and not page_namespace
    and not (page_is_redirect
             and exists
             (select * from pagelinks join linktarget
              on pl_target_id = lt_id
              where pl_from = {page}
              and not lt_namespace
              and lt_title = page_title));
    """

    sql_3 = f"""
    select replace(page_title, '_', ' ') as a from page
    where page_id in
    (select tl_from from templatelinks
     where tl_target_id = {template})
    and not page_namespace
    and not page_title in
    (select lt_title from pagelinks join linktarget
     where pl_target_id = lt_id
     and pl_from = {page}
     and not lt_namespace)
    and not replace(page_title, '_', ' ') in {pages_list_2};
    """

    sql_4 = f"""
    select replace(page_title, '_', ' ') as a from page
    where replace(page_title, '_', ' ') in {pages_list_2}
    and not page_namespace
    and (not page_id in
    (select tl_from from templatelinks
     where tl_target_id = {template})
    or page_title in
    (select lt_title from pagelinks join linktarget
     where pl_target_id = lt_id
     and pl_from = {page}
     and not lt_namespace));
    """

    sql_result = ''
    sql_result_short = ''
    target = 'https://quarry.wmcloud.org/query/75785' if 'Москва' in description else \
        'https://quarry.wmcloud.org/query/80731'
    conn = toolforge.connect('ruwiki_p')
    with conn.cursor() as cur:
        cur.execute(sql_1)
        res = cur.fetchall()
        if len(res) > 0:
            res = ', '.join([i[0].decode('utf-8') for i in res]).replace('_', ' ')
            sql_result += f'Выдача по запросу [{description}-1]({target}) (Без шаблона) не пуста: {res}.\n'
            sql_result_short += f'Выдача по запросу [{description}-1](<{target}>) (Без шаблона) не пуста.\n'

        cur.execute(sql_2)
        res = cur.fetchall()
        if len(res) > 0:
            res = ', '.join([i[0].decode('utf-8') for i in res]).replace('_', ' ')
            sql_result += f'Выдача по запросу [{description}-2]({target}) (Исключения шаблона) не пуста: {res}.\n'
            sql_result_short += f'Выдача по запросу [{description}-2]({target}) (Исключения шаблона) не пуста.\n'

        cur.execute(sql_3)
        res = cur.fetchall()
        if len(res) > 0:
            res = ', '.join([i[0].decode('utf-8') for i in res]).replace('_', ' ')
            sql_result += f'Выдача по запросу [{description}-3]({target}) (Без ссылки) не пуста: {res}.\n'
            sql_result_short += f'Выдача по запросу [{description}-3]({target}) (Без ссылки) не пуста.\n'

        cur.execute(sql_4)
        res = cur.fetchall()
        if len(res) > 0:
            res = ', '.join([i[0].decode('utf-8') for i in res]).replace('_', ' ')
            sql_result += f'Выдача по запросу [{description}-4]({target}) (Исключения ссылки) не пуста: {res}.\n'
            sql_result_short += f'Выдача по запросу [{description}-4]({target}) (Исключения ссылки) не пуста.\n'

    conn.close()
    return [sql_result, sql_result_short]


CONTENT_RESULT += f'{check_page_exists(moscow_list_1, "Москва-1")}'
CONTENT_RESULT += f'{check_page_exists(moscow_list_2, "Москва-2")}'
CONTENT_RESULT += f'{check_page_exists(SPb_list_1, "Санкт-Петербург-1")}'
CONTENT_RESULT += f'{check_page_exists(SPb_list_2, "Санкт-Петербург-2")}'

pre_content = ''
res1 = sql_calls(moscow_page, moscow_template, moscow_list_1_str, moscow_list_2_str, 'Москва')
res2 = sql_calls(SPb_page, SPb_template, SPb_list_1_str, SPb_list_2_str, 'Санкт-Петербург')
pre_content += CONTENT_RESULT + res1[0] + res2[0]

if len(pre_content + '<@&1220507429830131844>') > 2000:
    pre_content = ''
    pre_content += CONTENT_RESULT + res1[1] + res2[1]

if pre_content != '':
    pre_content += '<@&1220507429830131844>'
    requests.post(url=f'{os.environ["DISCORD_URL_WEBHOOK"]}{os.environ["METRO_WEBHOOK"]}',
                  json={'content': pre_content, 'tts': False}, timeout=30.0)

f = open('public_html/metro/index.php', 'r', encoding='utf-8')
fl = f.read().split('\n')
f.close()
header = fl[:11]
body = fl[11:20]
body.insert(0, '\necho time_ago(new DateTime("@" . ' + str(int(time.time())) + ')) . "<br>";')

cont = '\n'.join(header) + '\n'.join(body)
f = open('public_html/metro/index.php', 'w', encoding='utf-8')
f.write(cont)
f.close()
