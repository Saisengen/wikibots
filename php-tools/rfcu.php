<?php
$all = '';
$all = $_GET["all"];
$title = ($all != '') ? 'Кандидаты на улучшение по дате номинаций' : 'Просроченные номинации к улучшение';
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$th = 0;
$purge = (time() - 86400*500);
$query = ($all != '') ? 'select p.page_namespace, p.page_title, c.cl_sortkey_prefix from ruwiki_p.categorylinks as c, ruwiki_p.page as p where c.cl_to LIKE "Статьи на улучшении%дней‎" and c.cl_from = p.page_id and UNIX_TIMESTAMP(c.cl_timestamp) > UNIX_TIMESTAMP("'.date("Y-m-d", $purge).' 00:00:00") order by c.cl_sortkey_prefix' : 'select p.page_namespace, p.page_title, c.cl_sortkey_prefix from ruwiki_p.categorylinks as c, ruwiki_p.page as p where c.cl_to LIKE "Статьи на улучшении более%дней‎" and c.cl_from = p.page_id and UNIX_TIMESTAMP(c.cl_timestamp) > UNIX_TIMESTAMP("'.date("Y-m-d", $purge).' 00:00:00") order by c.cl_sortkey_prefix';
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$y = 0;
$n = 0;
$content = '';
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	$ku[$y][0] = (string)$row['cl_sortkey_prefix'];
	$ku[$y][1] = (string)$row['page_title'];
    $ku[$y][2] = (string)$row['page_namespace'];
    $z[($ku[$y][0])] = 0;
    $y++;
}
for ($i = 0; $i < $y-1; $i++) 
{
    switch ($ku[$i][2]) 
    {
		case 0: $ns = ""; break;
		case 1: $ns = "Обсуждение:"; break;
		case 2: $ns = "Участник:"; break;
		case 3: $ns = "Обсуждение_участника:"; break;
		case 4: $ns = "Википедия:"; break;
		case 5: $ns = "Обсуждение_Википедии:"; break;
		case 6: $ns = "Файл:"; break;
		case 7: $ns = "Обсуждение_файла:"; break;
		case 8: $ns = "MediaWiki:"; break;
		case 9: $ns = "Обсуждение_MediaWiki:"; break;		
		case 10: $ns = "Шаблон:"; break;
		case 11: $ns = "Обсуждение_шаблона:"; break;
		case 12: $ns = "Справка:"; break;
		case 13: $ns = "Обсуждение_справки:"; break;
		case 14: $ns = "Категория:"; break;
		case 15: $ns = "Обсуждение_категории:"; break;
		case 100: $ns = "Портал:"; break;
		case 101: $ns = "Обсуждение_портала:"; break;
		case 102: $ns = "Инкубатор:"; break;
		case 103: $ns = "Обсуждение_Инкубатора:"; break;
		case 104: $ns = "Проект:"; break;
		case 105: $ns = "Обсуждение_проекта:"; break;
		case 106: $ns = "Арбитраж:"; break;
		case 107: $ns = "Обсуждение_арбитража:"; break;
		case 828: $ns = "Модуль:"; break;
		case 829: $ns = "Обсуждение_модуля:"; break;
	}
    $kunom[($ku[$i][0])][($z[($ku[$i][0])])] = $ns.$ku[$i][1];
    $z[($ku[$i][0])]++;
}
$content = $content.'<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Дата</th>
                  <th>Шт.</th>
                  <th>Номинации</th>
                </tr>
              </thead><tbody>'; 
foreach ($kunom as $date => $noms)
{
   $str = '';
   switch (date("n", strtotime($date)))
   {
    	case 1: $mm = "января"; break;
		case 2: $mm = "февраля"; break;
		case 3: $mm = "марта"; break;
		case 4: $mm = "апреля"; break;
		case 5: $mm = "мая"; break;
		case 6: $mm = "июня"; break;
		case 7: $mm = "июля"; break;
		case 8: $mm = "августа"; break;
		case 9: $mm = "сентября"; break;
		case 10: $mm = "октября"; break;
		case 11: $mm = "ноября"; break;
        case 12: $mm = "декабря"; break;
   }
   $date_rus = date("j ".$mm." Y", strtotime($date));
   $content = $content.'<tr class="tool"><td style="white-space: nowrap;"><a href="//ru.wikipedia.org/wiki/Википедия:К удалению/'.$date_rus.'">'.$date.'</a></td>';
   $daynumber = 0;
   foreach ($noms as $pages)
    { 
        $str = $str.'<a href="//ru.wikipedia.org/wiki/'.urlencode($pages).'">'.str_replace("_", " ", $pages).'</a> <sup><a class="redlink" href="//ru.wikipedia.org/wiki/Википедия:К удалению/'.$date_rus.'#'.str_replace("%",".", urlencode($pages)).'">перейти</a></sup> &bull; ';
		$daynumber++;
    }
    $str = '<td align="center">'.$daynumber.'</td><td>'.$str.'</td></tr>';
    $str1 = str_replace(" &bull; </td>", "</td>", $str);
    $content = $content.$str1;
	$n++;
}
$content = $content."</tbody></table>";

$plural = $n%10==1&&$n%100!=11?' день':($n%10>=2&&$n%10<=4&&($n%100<10||$n%100>=20)?' дня':' дней');
$content = ($all != '') ? '<p>На данной странице представлены <a href="//ru.wikipedia.org/wiki/Категория:Википедия:Кандидаты_на_удаление_по_дате_номинации">все кандидаты на удаление</a>, сортированные по дате номинации. Данные актуальны на момент загрузки страницы с поправкой на задержки в синхронизации базы данных.
<br /><br />Предполагается, что список корректен (нулевая правка не требуется, т.к. в категорию статья попадает сразу при номинации). Отсутствие той или иной страницы может быть вызвано удалением шаблона КУ из номинированной страницы.
<br /><br /><b>Всего: '.$number.' номинаций за '.$n.$plural.'. (выполнено за '.$timed.' сек.)</b></p>'.$content : '<p>На данной странице представлены <a href="//ru.wikipedia.org/wiki/Категория:Википедия:Просроченные_подведения_итогов_по_удалению_страниц">просроченные номинации к удалению</a>. Данные актуальны на момент загрузки страницы с поправкой на задержки в синхронизации базы данных.
<br /><br />Самые "свежие" просроченные номинации могут не появляться в списке (вероятно это связано с необходимостью сделать нулевую правку в статье, чтобы шаблон КУ обновил включение в категории).
<br /><br /><b>Всего просрочено: '.$number.' номинаций за '.$n.$plural.'. (выполнено за '.$timed.' сек.)</b></p>'.$content;
/*echo "<pre>"; 
print_r($kunom); 
echo "</pre>";*/
mysql_close($db);
echo $content;
require_once("../skeleton_2.php");
?>
