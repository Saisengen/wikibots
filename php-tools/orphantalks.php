<?php
$title = 'Страницы обсуждения без основной страницы';
$arxiv = $_GET["arxiv"];
$slider='';
if (empty($arxiv))
{ $arxiv = 0; }
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$query = 'SELECT page_title FROM page WHERE page_namespace="1" AND page_title NOT IN (SELECT page_title FROM page WHERE page_namespace="0") AND page_title NOT IN (SELECT page_title FROM page where page_namespace = "1" AND (page_title LIKE ("%Архив%") OR page_title LIKE ("%архив") OR page_title LIKE ("%Посредничеств%") OR page_title LIKE ("%/План") OR page_title LIKE ("%/to_do") OR page_title LIKE ("%/Шапка")));';
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$asd = ($arxiv > 0) ? ( '<p><a href="http://tools.wmflabs.org/dibot/orphantalks.php?arxiv=0">Выключить из запроса архивы</a></p>') : ('<p><a href="http://tools.wmflabs.org/dibot/orphantalks.php?arxiv=1">Включить в запрос архивы</a></p>');
//echo $asd;
echo "<p>Всего: $number строк (выполнено за $timed сек).</p>";
echo '<table class="tablesorter" border="1">
<thead><tr><th>№</th><th>Страница</th></tr></thead><tbody>'; 
$i = 0;
  while ($row=mysql_fetch_array($res)) { 
	 echo '<tr class="tool">';
	 foreach ($row as $key => $value)
	 {
		if (preg_match("/\d{1,3}/", $key) == 0)
		{ 
				// it's better to use htmlspecialchars()
				$search  = array(   '<',    '>',      '"',  '\'',   '?',     '&');
				$replace = array('%3C', '%3E', '%22', '%27', '%3F', '%26');
				if (preg_match("/page_title/", $key))
				{
					echo "<td>#</td><td>[[<a href=\"http://ru.wikipedia.org/wiki/Talk:".str_replace($search, $replace, $value)."\">Обсуждение:".str_replace('_', ' ', $value)."</a>]]</td>"; 
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
?>
