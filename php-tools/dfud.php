<?php
ini_set('display_errors',1);
error_reporting(E_ALL);
$title = 'Несвободные файлы в статьях о ныне живущих';
$slider = '';
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$th = 0;
$query = 'select DISTINCT il.il_to from ruwiki_p.categorylinks c1 JOIN ruwiki_p.imagelinks il ON c1.cl_from = il.il_from JOIN ruwiki_p.page p ON p.page_title = il.il_to JOIN ruwiki_p.categorylinks c2 ON p.page_id = c2.cl_from where c1.cl_to = "Википедия:Персоналии_без_указания_даты_смерти,_в_том_числе_—_ныне_живущие" or c1.cl_to = "Ныне_живущие" AND c2.cl_to = "Файлы:Несвободные"'; 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
echo "<p>Total: $number rows. (done in $timed sec.)</p>";
  echo '<table><tr><th>Disputed-fairuse file</th></tr><tr>'; 
  while ($row=mysql_fetch_array($res)) { 
	 foreach ($row as $key => $value)
	 {
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/il_to/", $key))
		{
			echo "<td># [[:<a href=\"http://ru.wikipedia.org/wiki/File:".str_replace($search, $replace, $value)."\">File:".str_replace('_', ' ', $value)."</a>]]</td>"; 
		}
	}
	echo "</tr>\n"; 
}
echo "</table>"; 
mysql_close($db);
require_once("../skeleton_2.php");
?>
