<?php
	//ini_set('display_errors',1);
	//error_reporting(E_ALL);
	$title = "Filemove log";
	$slider = '';
	require_once("../skeleton_1.php");
	$ts_pw = posix_getpwuid(posix_getuid());
	$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
	$lang = $_GET['lang'];
	$from = $_GET['from'];
	$p = $_GET["p"];
	$n = $_GET["n"];
	if (empty($p))
		{ $p = 0; }
	if (empty($n))
		{ $n = 100; }
	$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
	unset($ts_mycnf, $ts_pw);
	mysql_select_db('meta_p', $db) or die(mysql_error()); ;
	$query = "select dbname, lang, url from wiki where family = 'wikipedia' and is_closed = 0 and has_wikidata = 1 order by dbname";
	$res = mysql_query($query) or die(mysql_error()); 
	mysql_close($db);
?>
<form method="GET">
<p><b> 
<?php 
	echo ($lang != "ru" ? "Choose project:" : "Выберите проект:").'<select name="from" size="1">';
	while ($row=mysql_fetch_array($res)) 
	{ 
		$selected = ($from == $row['dbname']) ? 'selected="selected" ' : '';
		if ($from == $row['dbname']) 
		{
			$iw = $row['lang']; // for links in result html
		}
		echo '<option '.$selected.'value="'.$row['dbname'].'">'.$row['dbname'].'</option>';
	}
	echo '</select>';
	echo '<input type="hidden" name="lang" value="'.$lang.'">';
?>
</b></p>

<p><input type="submit" value="OK"></p>
</form>
<?php
if ($from)
{
		$stime = time();
		$ts_pw = posix_getpwuid(posix_getuid());
		$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/.my.cnf");
		$db = mysql_connect($from.'.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
		unset($ts_mycnf, $ts_pw);
		mysql_select_db($from.'_p', $db) or die(mysql_error());
		$start = ($n * $p);
		
		$query = "select log_timestamp, log_user_text, log_title, log_comment, log_params from logging where log_type = 'move' AND log_namespace = '6' ORDER BY log_timestamp DESC limit ".$start.",".$n." /* LIMIT: 600 */"; 
		/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
		//$all = mysql_query($q) or die(mysql_error()); 
		$res = mysql_query($query) or die(mysql_error()); 
		$etime = time();
		/*while ($row=mysql_fetch_array($all)) { 
			foreach ($row as $key => $value)
			{
				$allnumber = $value; 
			}
		}*/
		$number = mysql_num_rows($res); 
		//$np = floor($allnumber/$n);
		$timed = $etime - $stime;
echo '<p>'.($lang != "ru" ? "Total: " : "Всего: ").$number.($lang != "ru" ? " shown. Done in " : " строк. Выполнено за ").$timed.($lang != "ru" ? " sec." : " сек.").'</p>';
$menu = '<p><b>'.($lang != "ru" ? "Show: " : "Показать: ").'</b> ';
	// первые
if ($p < 1)
	$menu = $menu.'first | ';
else
	$menu = $menu.'<a href="filemoves.php?from='.$from.'&p=0&n='.$n.'&lang='.$lang.'">first</a> | ';
if($p != 0)
	$menu = $menu.'<a href="filemoves.php?from='.$from.'&p='.($p-1).'&n='.$n.'&lang='.$lang.'">prev.</a> | ';
else
	$menu = $menu.'prev. | ';
if ($n-1 < $number)
	$menu = $menu.'<a href="filemoves.php?from='.$from.'&p='.($p+1).'&n='.$n.'&lang='.$lang.'">next</a> ';
else
	$menu = $menu.'next ';
	$menu = $menu.'('.$n.' entries) ';
	$menu = $menu.'(by <a href="filemoves.php?from='.$from.'&p='.$p.'&n=20&lang='.$lang.'">20</a> | ';
	$menu = $menu.'<a href="filemoves.php?from='.$from.'&p='.$p.'&n=50&lang='.$lang.'">50</a> | ';
	$menu = $menu.'<a href="filemoves.php?from='.$from.'&p='.$p.'&n=100&lang='.$lang.'">100</a> | ';
	$menu = $menu.'<a href="filemoves.php?from='.$from.'&p='.$p.'&n=500&lang='.$lang.'">500</a>) </p>';
echo $menu;
		echo '<table class="tablesorter" border="1">
		<thead><tr>
		<th>'.($lang != "ru" ? "Timestamp" : "Дата").'</th>
		<th>'.($lang != "ru" ? "User" : "Участник").'</th>
		<th>'.($lang != "ru" ? "File" : "Файл").'</th>
		<th>'.($lang != "ru" ? "Comment" : "Комментарий").'</th>
		<th>'.($lang != "ru" ? "Parameters" : "Параметры").'</th>
		</tr></thead><tbody>'; 
		while ($row=mysql_fetch_array($res)) 
		{
			echo '<tr class="tool">';
			foreach ($row as $key => $value)
			{
				$ns = "";
				if (preg_match("/\d{1,3}/", $key) == 0)
				{ 
					$search  = array('<', '>', '"', '\'', ';');
					$replace = array('&lt;', '&gt;', '&quot;', '%27', '; ');
					if (preg_match("/[^n][^o]_title/", $key))
					{
						echo "<td><div style=\"word-wrap: break-word; width: 200px;\"><a href=\"http://".$iw.".wikipedia.org/wiki/File:".str_replace($search, $replace, $value)."\">File:".str_replace('_', ' ', $value)."</a></div></td>"; 
					}
					else if (preg_match("/[^n][^o]_user_text/", $key))
					{
						echo "<td><div style=\"word-wrap: break-word; width: 100px;\"><a href=\"http://".$iw.".wikipedia.org/wiki/User:".str_replace($search, $replace, $value)."\">".str_replace('_', ' ', $value)."</a></div></td>"; 
					}
					else if (preg_match("/log_params/", $key))
					{
						$search  = array('<', '>', ';');
						$replace = array('&lt;', '&gt;', '; ');
						echo "<td><div style=\"word-wrap: break-word; width: 200px;\">".str_replace($search, $replace, $value)."</div></td>";
					}
					else
					{
						$search  = array('<', '>', ';');
						$replace = array('&lt;', '&gt;', '; ');
						echo "<td><div style=\"word-wrap: break-word; width: 120px;\">".str_replace($search, $replace, $value)."</div></td>";
					}
				}
			}
			echo "</tr>\n"; 
		}
		echo "</table>"; 
		echo $menu;
		mysql_close($db);
		//echo "<br><b>Connection closed</b>";
}
require_once("../skeleton_2.php");
?>
