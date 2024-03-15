<?php
$user = $_GET["user"];
$title = 'Откаты пользователей';
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
 mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$th = 0;
$query = "SELECT page_namespace, page_title, rev_id, rev_user_text, rev_timestamp, rev_comment FROM user, page, revision WHERE user_name = '".$user."' AND rev_user = user_id AND rev_comment LIKE '%|откат]] правок%' AND rev_page = page_id"; 
 // $query = "SELECT page_namespace, page_title, rev_id, rev_user_text, rev_timestamp, rev_comment FROM page, revision WHERE rev_user = '471055' AND rev_comment LIKE '%|откат]] правок%' AND rev_page = page_id"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
echo '<form method="GET">Открыть откаты участника <input type="text" name="user" size="40" value="'.$user.'">.<br/><input type="submit" value="OK"></form>';
echo '<p>Обратите внимание, что для участников с большим количеством правок запрос может выполняться несколько минут.</p>';
if ($user <> '')
{ 
echo '<p>На этой странице представлены откаты участника '.$user.'.</p>';
echo '<p>Total: '.$number.' rows. (done in '.$timed.' sec.)</p>';
echo '<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Page</th>
				  <th>Diff</th>
				  <th>User</th>
				  <th>Data</th>
				  <th>Comment</th>
                </tr>
              </thead><tbody>'; 
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	echo "<tr>";
	foreach ($row as $key => $value)
	{	
		$search  = array('<', '>', '"', '\'');
		$replace = array('&lt;', '&gt;', '&quot;', '%27');
		if (preg_match("/page_namespace/", $key))
		{
			switch ($value) {
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
		}
		else if (preg_match("/rev_id/", $key))
		{
			echo "<td><a href=\"http://ru.wikipedia.org/?diff=".$value."\">".$value."</a></td>"; 
		}
		else if (preg_match("/page_title/", $key))
		{
			echo "<td><a href=\"http://ru.wikipedia.org/wiki/".$namespace.str_replace($search, $replace, $value)."\">".$namespace.str_replace('_', ' ', $value)."</a></td>"; 
		}
		else 
		{
			echo "<td>".$value."</td>"; 
		}
	}
	echo "</tr>";
}
echo "</table>";

mysql_close($db);
}
require_once("../skeleton_2.php");
?>
