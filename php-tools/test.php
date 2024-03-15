<?php
$bot = $_GET["bot"];
$slider = '';
$title = 'DB request - Test';
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
$query = "select page_namespace, page_title from page, templatelinks where tl_namespace=2 and tl_title LIKE 'Box%' and tl_from = page_id group by page_id order by page_title"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$y = 0;
$n = 0;
$content = "<h3>Ссылки на юзербоксы {{User:Box}}:</h3> <br />".$query."<br /><p>Total: $number rows. (done in $timed sec.)</p><br />";
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	$ku[$y][0] = (string)$row['page_namespace'];
	$ku[$y][1] = (string)$row['page_title'];
    $y++;
}
//$content = $content.'<ol>';
for ($i = 0; $i < $y; $i++) 
{
    switch ($ku[$i][0]) 
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
	$aaa = $ns.$ku[$i][1];
	$content = $content.'<br /><a href="http://ru.wikipedia.org/wiki/'.$aaa.'">'.$aaa.'</a>';
}
//$content = $content.'</ol>'; 
echo $content; 
/**************************************************************************************************************/
$query = "select page_namespace, page_title from page, templatelinks where tl_namespace='10' and tl_title LIKE 'Gender_switchьзователь%' and tl_from = page_id group by page_id order by page_title"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$y = 0;
$n = 0;
$content = "<h3>Ошибки {{Gender_switchьзователь}}:</h3> <br />".$query." <br /><p>Total: $number rows. (done in $timed sec.)</p><br />";
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	$ku[$y][0] = (string)$row['page_namespace'];
	$ku[$y][1] = (string)$row['page_title'];
    $y++;
}
//$content = $content.'<ol>';
for ($i = 0; $i < $y; $i++) 
{
    switch ($ku[$i][0]) 
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
	$aaa = $ns.$ku[$i][1];
	$content = $content.'<br /><a href="http://ru.wikipedia.org/wiki/'.$aaa.'">'.$aaa.'</a>';
}
//$content = $content.'</ol>'; 
echo $content; 
/**************************************************************************************************************/
$query = "select page_namespace, page_title from page, templatelinks where tl_namespace='10' and tl_title LIKE 'Userbox~ruwiki%' and tl_from = page_id group by page_id order by page_title"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$y = 0;
$n = 0;
$content = "<h3>Ссылки на юзербоксы {{Userbox~ruwiki}}:</h3> <br />".$query." <br /><p>Total: $number rows. (done in $timed sec.)</p><br />";
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	$ku[$y][0] = (string)$row['page_namespace'];
	$ku[$y][1] = (string)$row['page_title'];
    $y++;
}
//$content = $content.'<ol>';
for ($i = 0; $i < $y; $i++) 
{
    switch ($ku[$i][0]) 
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
	$aaa = $ns.$ku[$i][1];
	$content = $content.'<br /><a href="http://ru.wikipedia.org/wiki/'.$aaa.'">'.$aaa.'</a>';
}
//$content = $content.'</ol>'; 
echo $content; 
/**************************************************************************************************************/
$query = "select page_namespace, page_title from page, templatelinks where tl_namespace='2' and tl_title LIKE 'Box~ruwiki%' and tl_from = page_id group by page_id order by page_title"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$y = 0;
$n = 0;
$content = "<h3>Ссылки на юзербоксы {{User:Box~ruwiki}}:</h3> <br />".$query." <br /><p>Total: $number rows. (done in $timed sec.)</p><br />";
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	$ku[$y][0] = (string)$row['page_namespace'];
	$ku[$y][1] = (string)$row['page_title'];
    $y++;
}
//$content = $content.'<ol>';
for ($i = 0; $i < $y; $i++) 
{
    switch ($ku[$i][0]) 
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
	$aaa = $ns.$ku[$i][1];
	$content = $content.'<br /><a href="http://ru.wikipedia.org/wiki/'.$aaa.'">'.$aaa.'</a>';
}
//$content = $content.'</ol>'; 
echo $content; 
/**************************************************************************************************************/
$query = "select page_namespace, page_title from page where page_namespace='2' and page_title LIKE 'Box~ruwiki%' and page_is_redirect = '1'"; 
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
$number = mysql_num_rows($res); 
$timed = $etime - $stime;
$y = 0;
$n = 0;
$content = "<h3>Редиректы {{User:Box~ruwiki}}:</h3> <br />".$query." <br /><p>Total: $number rows. (done in $timed sec.)</p><br />";
while ($row=mysql_fetch_array($res, MYSQL_ASSOC)) 
{ 
	$ku[$y][0] = (string)$row['page_namespace'];
	$ku[$y][1] = (string)$row['page_title'];
    $y++;
}
//$content = $content.'<ol>';
for ($i = 0; $i < $y; $i++) 
{
    switch ($ku[$i][0]) 
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
	$aaa = $ns.$ku[$i][1];
	$content = $content.'<br /><a href="http://ru.wikipedia.org/wiki/'.$aaa.'">'.$aaa.'</a>';
}
//$content = $content.'</ol>'; 
echo $content; 

mysql_close($db);
require_once("../skeleton_2.php");
?>
