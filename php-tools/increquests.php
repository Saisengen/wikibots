<?php
$title = 'Запросы в Инкубаторе';
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error());
// $query = "select ptitle, puser, DATE_FORMAT(pdate, '%Y-%m-%dT%H:%i:%s') as pdate, tr.rev_user_text as tuser, DATE_FORMAT(tr.rev_timestamp, '%Y-%m-%dT%H:%i:%s') as tdate from ruwiki_p.revision as tr, ruwiki_p.page as tp, (select p.page_title as ptitle, r.rev_user_text as puser, r.rev_timestamp as pdate from ruwiki_p.categorylinks as cl, ruwiki_p.revision as r, ruwiki_p.page as p where cl.cl_to = 'Проект:Инкубатор:Запросы_на_проверку' and cl.cl_from = p.page_id and p.page_latest = r.rev_id) as a where tp.page_title = ptitle and tp.page_namespace = '103' and tp.page_latest = tr.rev_id"; 

// $query = "select ptitle, puser, DATE_FORMAT(pdate, '%Y-%m-%dT%H:%i:%s') as pdate, tr.rev_user_text as tuser, DATE_FORMAT(tr.rev_timestamp, '%Y-%m-%dT%H:%i:%s') as tdate from ruwiki_p.revision as tr, ruwiki_p.page as tp RIGHT JOIN (select p.page_title as ptitle, r.rev_user_text as puser, r.rev_timestamp as pdate from ruwiki_p.categorylinks as cl, ruwiki_p.revision as r, ruwiki_p.page as p where cl.cl_to = 'Проект:Инкубатор:Запросы_на_проверку' and cl.cl_from = p.page_id and p.page_latest = r.rev_id) as a ON tp.page_title = a.ptitle where tp.page_namespace = '103' and tp.page_latest = tr.rev_id"; 

$query = "select * from (select p.page_title as ptitle, r.rev_user_text as puser, DATE_FORMAT(r.rev_timestamp, '%Y-%m-%dT%H:%i:%s') as pdate from ruwiki_p.categorylinks as cl, ruwiki_p.revision as r, ruwiki_p.page as p where cl.cl_to = 'Проект:Инкубатор:Запросы_на_проверку' and cl.cl_from = p.page_id and p.page_latest = r.rev_id) as a LEFT JOIN (select tp.page_title as ttitle, tr.rev_user_text as tuser, DATE_FORMAT(tr.rev_timestamp, '%Y-%m-%dT%H:%i:%s') as tdate from ruwiki_p.revision as tr, ruwiki_p.page as tp where tp.page_namespace = '103' and tp.page_latest = tr.rev_id) as b ON a.ptitle = b.ttitle"; 


// DATE_FORMAT(r.rev_timestamp, "%Y-%m-%dT%H:%i:%s") */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
echo "<h3>Запросы на проверку</h3><p>Всего: $number запросов. (выполнено за $timed сек.)</p>";
  echo '<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Статья</th>
                  <th>Статья, посл.автор</th>
				  <th>Статья, посл.правка</th>
				  <th>Обс., посл.автор</th>
				  <th>Обс., посл.правка</th>
                </tr>
              </thead><tbody>'; 
  while ($row=mysql_fetch_array($res)) { 
	 foreach ($row as $key => $value)
	 {
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/ptitle/", $key))
		{
			echo "<tr class=\"tool\"><td><a href=\"http://ru.wikipedia.org/wiki/Инкубатор:".str_replace($search, $replace, $value)."\">Инкубатор:".str_replace('_', ' ', $value)."</a>(<a href=\"http://ru.wikipedia.org/wiki/Обсуждение_Инкубатора:".str_replace($search, $replace, $value)."\">обс.</a>)</td>"; 
		}
		else if (preg_match("/puser/", $key))
		{
			echo "<td><a href=\"http://ru.wikipedia.org/wiki/User:".$value."\">".$value."</a> (<a href=\"http://ru.wikipedia.org/wiki/User_talk:".$value."\">о</a> • <a href=\"http://ru.wikipedia.org/wiki/Special:Contributions/".$value."\">в</a>)</td>"; 
		}
		else if (preg_match("/pdate/", $key))
		{
			echo "<td>".$value."</td>"; 
		}
		else if (preg_match("/tuser/", $key))
		{
			if (!empty($value))
			echo "<td><a href=\"http://ru.wikipedia.org/wiki/User:".$value."\">".$value."</a> (<a href=\"http://ru.wikipedia.org/wiki/User_talk:".$value."\">о</a> • <a href=\"http://ru.wikipedia.org/wiki/Special:Contributions/".$value."\">в</a>)</td>";
			else
			echo "<td>не создана</td>";
		}
		else if (preg_match("/tdate/", $key))
		{
			if (!empty($value))
			echo "<td>".$value."</td>"; 
			else
			echo "<td>не создана</td>";
		}
	}
	echo '</tr>'; 
}
echo "</table>"; 

// ЗАПРОСЫ О ПОМОЩИ

$query = "select * from (select p.page_title as ptitle, r.rev_user_text as puser, DATE_FORMAT(r.rev_timestamp, '%Y-%m-%dT%H:%i:%s') as pdate from ruwiki_p.categorylinks as cl, ruwiki_p.revision as r, ruwiki_p.page as p where cl.cl_to = 'Проект:Инкубатор:Запросы_о_помощи' and cl.cl_from = p.page_id and p.page_latest = r.rev_id) as a LEFT JOIN (select tp.page_title as ttitle, tr.rev_user_text as tuser, DATE_FORMAT(tr.rev_timestamp, '%Y-%m-%dT%H:%i:%s') as tdate from ruwiki_p.revision as tr, ruwiki_p.page as tp where tp.page_namespace = '103' and tp.page_latest = tr.rev_id) as b ON a.ptitle = b.ttitle"; 


// DATE_FORMAT(r.rev_timestamp, "%Y-%m-%dT%H:%i:%s") */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
echo "<h3>Запросы о помощи</h3><p>Всего: $number запросов. (выполнено за $timed сек.)</p>";
  echo '<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Статья</th>
                  <th>Статья, посл.автор</th>
				  <th>Статья, посл.правка</th>
				  <th>Обс., посл.автор</th>
				  <th>Обс., посл.правка</th>
                </tr>
              </thead><tbody>'; 
  while ($row=mysql_fetch_array($res)) { 
	 foreach ($row as $key => $value)
	 {
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/ptitle/", $key))
		{
			echo "<tr class=\"tool\"><td><a href=\"http://ru.wikipedia.org/wiki/Инкубатор:".str_replace($search, $replace, $value)."\">Инкубатор:".str_replace('_', ' ', $value)."</a>(<a href=\"http://ru.wikipedia.org/wiki/Обсуждение_Инкубатора:".str_replace($search, $replace, $value)."\">обс.</a>)</td>"; 
		}
		else if (preg_match("/puser/", $key))
		{
			echo "<td><a href=\"http://ru.wikipedia.org/wiki/User:".$value."\">".$value."</a> (<a href=\"http://ru.wikipedia.org/wiki/User_talk:".$value."\">о</a> • <a href=\"http://ru.wikipedia.org/wiki/Special:Contributions/".$value."\">в</a>)</td>"; 
		}
		else if (preg_match("/pdate/", $key))
		{
			echo "<td>".$value."</td>"; 
		}
		else if (preg_match("/tuser/", $key))
		{
			if (!empty($value))
			echo "<td><a href=\"http://ru.wikipedia.org/wiki/User:".$value."\">".$value."</a> (<a href=\"http://ru.wikipedia.org/wiki/User_talk:".$value."\">о</a> • <a href=\"http://ru.wikipedia.org/wiki/Special:Contributions/".$value."\">в</a>)</td>";
			else
			echo "<td>не создана</td>";
		}
		else if (preg_match("/tdate/", $key))
		{
			if (!empty($value))
			echo "<td>".$value."</td>"; 
			else
			echo "<td>не создана</td>";
		}
	}
	echo '</tr>'; 
}
echo "</table>"; 

mysql_close($db);
require_once("../skeleton_2.php");
?>
