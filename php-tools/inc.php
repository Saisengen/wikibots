<?php
ini_set('display_errors',1);
error_reporting(E_ALL);
$title = "Актуальные страницы Инкубатора";
$slider = '';
require_once("../slider.php");
require_once("../skeleton_1.php");
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
 
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
// добавим стили
$content = '<style type="text/css">
	td.dt_act { background-color: #CCFF99;}
	td.dt_warn { background-color: #FFCC99;}
    td.ex_new { background-color: #CCFFFF;}
	td.ex_warn { background-color: #FFFFCC;}
	td.ex_old { background-color: #FFCCCC;}
    </style>';

$query = "SELECT c.page_namespace as ns, c.page_title, c.rev_timestamp created, c.rev_user_text creator, l.rev_timestamp last_edit, l.rev_user_text last_editor, ROUND((UNIX_TIMESTAMP(l.rev_timestamp) - UNIX_TIMESTAMP(c.rev_timestamp))/(60*60*24)) as edited, ROUND((UNIX_TIMESTAMP(NOW()) - UNIX_TIMESTAMP(c.rev_timestamp))/(60*60*24)) as exist, ROUND((UNIX_TIMESTAMP(NOW()) - UNIX_TIMESTAMP(l.rev_timestamp))/(60*60*24)) as downtime FROM (SELECT revision.rev_page, page.page_namespace, page.page_title, revision.rev_timestamp, revision.rev_user, revision.rev_user_text FROM `revision`, `page` WHERE page_namespace = 102 AND `page_id` = `rev_page` GROUP BY rev_page) as c, (SELECT revision.rev_page, page.page_namespace, page.page_title, revision.rev_timestamp, revision.rev_user, revision.rev_user_text FROM (SELECT revision.rev_page as t_id, MAX(revision.rev_timestamp) as t_time FROM `revision`, `page` WHERE page_namespace = 102 AND `page_id` = `rev_page` GROUP BY rev_page) as temp, `revision`, `page` WHERE page_namespace = 102 AND `page_id` = `rev_page` AND `page_id` = `t_id` AND t_time = rev_timestamp GROUP BY rev_page ORDER BY rev_timestamp DESC) as l WHERE c.rev_page = l.rev_page GROUP BY c.rev_page ORDER BY downtime DESC "; 
/* Выполнить запрос.Если произойдет ошибка - вывести ее.*/ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$new_ns = '<table class="tablesorter" border="1">
              <thead>
                <tr>
                  <th>Название</th>
                  <th>Создано</th>
                  <th>Автор</th>
				  <th>Посл.правка</th>
				  <th>Посл.автор</th>
				  <th>Время работы</th>
				  <th>Время жизни</th>
				  <th>Время простоя</th>
                </tr>
              </thead><tbody>'; 
// счетчики
$dt_w = 0;
$ex_w = 0;
$ex_o = 0;
for ($ty = 0; $ty < 23; $ty++)
{ 
	$dt[$ty] = 0;
	$ex[$ty] =0; 
}
// таблица
while ($row=mysql_fetch_array($res)) 
{ 
	$new_ns = $new_ns.'<tr class="tool">';
	// получаем отслеживаемые значения
	$ex1 = $row["exist"];
	$dt1 = $row["downtime"];
	// формируем массив статистических данных
	for ($ik = 0; $ik < 23; $ik++ )
	{
		if ($dt1 > (20 + 5*$ik)) // параметры сравнения 20,25,30,35,40...
		{
			$dt[$ik]++;
		}
		if ($ex1 > (20 + 5*$ik))
		{
			$ex[$ik]++;
		}
	}
	 foreach ($row as $key => $value)
	 {
	 $ns = "";
	 if ($row['ns']) {
		switch ($row['ns']) {
		case 102: $ns = "Инкубатор:"; break;
		case 103: $ns = "Обсуждение Инкубатора:"; break;
			}
		}
		if (preg_match("/\d{1,3}/", $key) == 0)
		{ 
			if ($key != 'ns')
			{ 
				$search  = array('<', '>', '"', '\'');
				$replace = array('&lt;', '&gt;', '&quot;', '%27');
				if (preg_match("/[^n][^o]_title/", $key))
				{
					$new_ns = $new_ns."<td><a href=\"http://ru.wikipedia.org/wiki/".$ns ."".str_replace($search, $replace, $value)."\">".$ns."".str_replace('_', ' ', $value)."</a> (<a href=\"http://ru.wikipedia.org/w/index.php?title=".$ns ."".str_replace($search, $replace, $value)."&action=history\">history</a>)</td>"; 
				}
				else if (preg_match("/(creator|editor)/", $key))
				{
					$new_ns = $new_ns."<td><a href=\"http://ru.wikipedia.org/wiki/User_talk:".str_replace($search, $replace, $value)."\">".$value."</a> (<a href=\"http://ru.wikipedia.org/wiki/Special:Contributions/".str_replace($search, $replace, $value)."\">c</a>)</td>"; 
				}
				else if (preg_match("/exist/", $key))
				{
					$search  = array('<', '>');
					$replace = array('&lt;', '&gt;');
					if ($ex1 > 120)
					{
						$ex_o++;
						$new_ns = $new_ns."<td class='ex_old'>".str_replace($search, $replace, $value)."</td>";
					}
					else if ($ex1 > 90)
					{
						$ex_w++;
						$new_ns = $new_ns."<td class='ex_warn'>".str_replace($search, $replace, $value)."</td>";
					}
					else if ($ex1 < 5)
					{
						$ex_w++;
						$new_ns = $new_ns."<td class='ex_new'>".str_replace($search, $replace, $value)."</td>";
					}
					else
						$new_ns = $new_ns."<td>".str_replace($search, $replace, $value)."</td>";
				}
				else if (preg_match("/downtime/", $key))
				{
					if ($dt1 > 20)
					{
						$dt_w++;
						$new_ns = $new_ns."<td class='dt_warn'>".str_replace($search, $replace, $value)."</td>";
					}
					else if ($dt1 < 5)
					{
						$dt_w++;
						$new_ns = $new_ns."<td class='dt_act'>".str_replace($search, $replace, $value)."</td>";
					}
					else
						$new_ns = $new_ns."<td>".str_replace($search, $replace, $value)."</td>";
				}
				else
				{
					$search  = array('<', '>');
					$replace = array('&lt;', '&gt;');
					$new_ns = $new_ns."<td>".str_replace($search, $replace, $value)."</td>";
				}
			}
		}
	}
    $new_ns = $new_ns."</tr>\n\n"; 
  }
$new_ns = $new_ns."</tbody></table>"; 
mysql_close($db);
$content = $content.'<p>Всего: '.$number.' страниц. (выполнено за '.$timed.' сек.)</p>
 <div id="slider">
 <div class="header" id="1-header">Статистика</div>
 <div class="content" id="1-content">
 <div class="text"><table class="tablesorter" border="1">
              <thead><tr><th>Более (дней)</th><th>Без правок</th><th>От даты создания</th></tr></thead><tbody>';
for ($ik = 0; $ik < 23; $ik++ )
{
	$abc = 20 + 5*$ik;
	$content = $content.'<tr class="tool"><td>'.$abc.'</td><td>'.$dt[$ik].'</td><td>'.$ex[$ik].'</td></tr>';
}
$content = $content."</tbody></table></div></div></div>";
$content = $content.$new_ns;
echo $content;
require_once("../skeleton_2.php");
?>
