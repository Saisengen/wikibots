<?php
$bot = $_GET["bot"];
$slider = '';
$title = 'Orphaned-fairuse file';
if (!$bot) {
require_once("../skeleton_1.php");
}
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
 
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$th = 0;
$query = "select i.il_from, p.page_title, IF(LENGTH(m.cl_from) > 0, 'YES', NULL) as marked from ruwiki_p.categorylinks AS c JOIN ruwiki_p.page AS p ON c.cl_from = p.page_id LEFT JOIN ruwiki_p.imagelinks AS i ON p.page_title = i.il_to LEFT JOIN (select cl_from from ruwiki_p.categorylinks where cl_to = 'Файлы:Неиспользуемые_несвободные:Все') as m ON m.cl_from = c.cl_from where c.cl_to = 'Файлы:Несвободные' AND p.page_namespace = '6' AND i.il_from IS NULL"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
if (!$bot) {
echo "<p>Total: $number rows. (done in $timed sec.)</p>";
  echo '<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Orphaned-fairuse file</th>
                  <th>Marked by {{ofud}}</th>
                </tr>
              </thead><tbody>'; 
  while ($row=mysql_fetch_array($res)) { 
	 foreach ($row as $key => $value)
	 {
		if (strlen($row['marked']) > 0) 
		{
			$marked = " style='background-color:#E0FFFF;' ";
			}
			else
			{
			$marked = '';
			}
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/[^n][^o]_title/", $key))
		{
			echo "<tr class=\"tool\"><td".$marked."><a href=\"http://ru.wikipedia.org/wiki/File:".str_replace($search, $replace, $value)."\">File:".str_replace('_', ' ', $value)."</a></td>"; 
		}
		else if (preg_match("/marked/", $key))
		{
			echo "<td".$marked.">".$value."</td>"; 
		}
	}
	echo "</tr>\n"; 
}
echo "</table>"; 
mysql_close($db);
require_once("../skeleton_2.php");
}
else
{
  echo "<ol>";
while ($row=mysql_fetch_array($res)) { 
 foreach ($row as $key => $value)
 {
	if (strlen($row['marked']) > 0) 
	{
	}
	else
	{
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/[^n][^o]_title/", $key))
		{
			echo "<li>File:".str_replace('_', ' ', $value)."</li>"; 
		}
	}
  }
}
echo "</ol>"; 
}
?>

