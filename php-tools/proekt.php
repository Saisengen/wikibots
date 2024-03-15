<?php
$wiki = $_GET["wiki"];
if (empty($wiki))
{ $wiki = 0; }
if ($wiki == 0)
{
$title = 'Страницы проектов по дате последней правки';
$slider = '';
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$q = 'select count(*) as num from ruwiki_p.page where page_namespace IN (104,105)'; // and page_title NOT LIKE "%/%"';
$query = 'select p.page_namespace, p.page_title, DATE_FORMAT(r.rev_timestamp, "%Y-%m-%dT%H:%i:%s") as rev_timestamp from ruwiki_p.page as p, ruwiki_p.revision as r where p.page_namespace IN (104,105) and p.page_latest = r.rev_id order by r.rev_timestamp';
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$all = mysql_query($q) or die(mysql_error()); 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
while ($row=mysql_fetch_array($all)) { 
	foreach ($row as $key => $value)
		{
			$allnumber = $value; 
		}
	}
$number = mysql_num_rows($res); 
$np = floor($allnumber/$n);
$timed = $etime - $stime;
$ns_array = explode(",", $ns);

echo "<p>Всего: $allnumber строк, показано $number. (выполнено за $timed сек.)</p>";
echo '<table class="tablesorter" border="1">
<thead><tr><th>№</th><th>Название</th><th style="word-wrap: normal">Последняя правка</th></tr></thead><tbody>'; 
$i = $n*$p;
  while ($row=mysql_fetch_array($res)) { 
	 echo '<tr class="tool">';
	 foreach ($row as $key => $value)
	 {
		$ns = "";
		if ($row['page_namespace']) {
			switch ($row['page_namespace']) {
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
		}
		if (preg_match("/\d{1,3}/", $key) == 0)
		{ 
				// it's better to use htmlspecialchars()
				$search  = array(   '<',    '>',      '"',  '\'',   '?',     '&');
				$replace = array('%3C', '%3E', '%22', '%27', '%3F', '%26');
				if (preg_match("/page_title/", $key))
				{
					echo "<td>".++$i."</td><td><a href=\"http://ru.wikipedia.org/wiki/". $ns ."".str_replace($search, $replace, $value)."\">".$ns."".str_replace('_', ' ', $value)."</a> (<a href=\"http://ru.wikipedia.org/w/index.php?title=". $ns ."".str_replace($search, $replace, $value)."&action=history\"><u>history</u></a>)</td>"; 
				}
				else if (preg_match("/rev_timestamp/", $key))
				{
					$search  = array('<', '>');
					$replace = array('&lt;', '&gt;');
					echo '<td style="word-wrap: normal">'.str_replace($search, $replace, $value).'</td>';
				}
		}
	}
    echo "</tr>\n"; 
  }
echo "</tbody></table>";  
echo $menu;
mysql_close($db);
//echo "<br><b>Connection closed</b>";
require_once("../skeleton_2.php");
}
else // если нужен викитекст
{
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$q = 'select count(*) as num from ruwiki_p.page where page_namespace IN (104,105)'; // and page_title NOT LIKE "%/%"';
$query = 'select p.page_namespace, p.page_title, DATE_FORMAT(r.rev_timestamp, "%Y-%m-%dT%H:%i:%s") as rev_timestamp from ruwiki_p.page as p, ruwiki_p.revision as r where p.page_namespace IN (104,105) and p.page_latest = r.rev_id order by r.rev_timestamp';
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$all = mysql_query($q) or die(mysql_error()); 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
while ($row=mysql_fetch_array($all)) { 
	foreach ($row as $key => $value)
		{
			$allnumber = $value; 
		}
	}
$number = mysql_num_rows($res); 
$np = floor($allnumber/$n);
$timed = $etime - $stime;
$ns_array = explode(",", $ns);
echo '<html>
  <head><meta charset="utf-8" /></head><body>{| class="wikitable sortable" width="100%"><br />
!! № !! Название !! Последняя правка<br />'; 
$i = $n*$p;
  while ($row=mysql_fetch_array($res)) { 
	 echo '|-<br />';
	 foreach ($row as $key => $value)
	 {
		$ns = "";
		if ($row['page_namespace']) {
			switch ($row['page_namespace']) {
		case 104: $ns = "Проект:"; break;
		case 105: $ns = "Обсуждение_проекта:"; break;
			}
		}
		if (preg_match("/\d{1,3}/", $key) == 0)
		{ 
				// it's better to use htmlspecialchars()
				$search  = array(   '<',    '>',      '"',  '\'',   '?',     '&');
				$replace = array('%3C', '%3E', '%22', '%27', '%3F', '%26');
				if (preg_match("/page_title/", $key))
				{
					echo "||".++$i."|| [[". $ns ."".str_replace($search, $replace, $value)."]]"; 
				}
				else if (preg_match("/rev_timestamp/", $key))
				{
					$search  = array('<', '>');
					$replace = array('&lt;', '&gt;');
					echo '||'.str_replace($search, $replace, $value).'<br />';
				}
		}
	}
  }
echo "|}</body></html>";
}
?>
