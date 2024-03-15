<?php
$ns = $_GET["ns"];
$n = $_GET["n"];
// если не задан, пусть будет 4
if (!$ns) { $ns = 4; }
if (!$n) 
{ $nll = '';}
else { $nll = 'where count > '.($n-1); }
// для формирования ссылок
$namespace = '';
switch ($ns) {
		case 0: $namespace = ""; break;
		case 1: $namespace = "Обсуждение:"; break;
		case 2: $namespace = "Участник:"; break;
		case 3: $namespace = "Обсуждение_участника:"; break;
		case 4: $namespace = "Википедия:"; break;
		case 5: $namespace = "Обсуждение_Википедии:"; break;
		case 6: $namespace = "Файл:"; break;
		case 7: $namespace = "Обсуждение_файла:"; break;
		case 8: $namespace = "MediaWiki:"; break;
		case 9: $namespace = "Обсуждение_MediaWiki:"; break;		
		case 10: $namespace = "Шаблон:"; break;
		case 11: $namespace = "Обсуждение_шаблона:"; break;
		case 12: $namespace = "Справка:"; break;
		case 13: $namespace = "Обсуждение_справки:"; break;
		case 14: $namespace = "Категория:"; break;
		case 15: $namespace = "Обсуждение_категории:"; break;
		case 100: $namespace = "Портал:"; break;
		case 101: $namespace = "Обсуждение_портала:"; break;
		case 102: $namespace = "Инкубатор:"; break;
		case 103: $namespace = "Обсуждение_Инкубатора:"; break;
		case 104: $namespace = "Проект:"; break;
		case 105: $namespace = "Обсуждение_проекта:"; break;
		case 106: $namespace = "Арбитраж:"; break;
		case 107: $namespace = "Обсуждение_арбитража:"; break;
		case 828: $namespace = "Модуль:"; break;
		case 829: $namespace = "Обсуждение_модуля:"; break;
		}
$slider = '';
$title = 'Подстраницы';
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
 
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$th = 0;
$query = "select page_title, count from (select page_title, (count(*)-1) as count from (select page_namespace, substr(page_title,1, IF (locate('/', page_title) > 1, locate('/', page_title)-1, CHAR_LENGTH(page_title)) ) as page_title from ruwiki_p.page where page_namespace=".$ns." and page_is_redirect = 0) as a GROUP BY page_title) as b ".$nll.""; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
echo "<p>Параметр ns=x меняет пространство имен, параметр n=y задает минимальное число подстраниц (при n=1 - 1 подстраница, при n=2 - 2 подстраницы). <br />Например, <a href=\"http://tools.wmflabs.org/dibot/subpages.php?ns=4&n=1\">subpages.php?ns=4&n=1</a> - вернет все заголовки в пространстве Википедия хотя бы с одной подстраницей.</p>";
echo "<p>Total: $number rows. (done in $timed sec.)</p>";
  echo '<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Название</th>
				  <th>Список подстраниц</th>
				  <th>Кол-во подстраниц</th>
                </tr>
              </thead><tbody>'; 
  while ($row=mysql_fetch_array($res)) { 
	echo "<tr class=\"tool\">";
	 foreach ($row as $key => $value)
	 {
		
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/[^n][^o]_title/", $key))
		{
			echo "<td><a href=\"http://ru.wikipedia.org/wiki/".$namespace.str_replace($search, $replace, $value)."\">".$namespace.str_replace('_', ' ', $value)."</a></td><td><a href=\"http://ru.wikipedia.org/wiki/Служебная:Указатель_по_началу_названия/".$namespace.str_replace($search, $replace, $value)."\">Подстраницы</a></td>"; 
		}
		else if (preg_match("/count/", $key))
		{
			echo "<td>".$value."</td>"; 
		}
	}
	echo "</tr>\n"; 
}
echo "</table>"; 
mysql_close($db);
require_once("../skeleton_2.php");
?>
