<?php
	$title = "LangLinks - Wikimedia Toolserver";
	header('Content-Type: text/html; charset=utf-8');
	require_once("../skeleton_up.php");
	$ts_pw = posix_getpwuid(posix_getuid());
	$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
	$toollang = $_GET['lang'];
	$from = $_GET['from'];
	$to = $_GET['to'];
	$num = $_GET['num'];
	if (!($num)) {$num = 20;}
	$db = mysql_connect('sql.toolserver.org', $ts_mycnf['user'], $ts_mycnf['password']);
	unset($ts_mycnf, $ts_pw);
	mysql_select_db('toolserver', $db) or die(mysql_error()); ;
	$query = "select dbname, lang, domain from toolserver.wiki where family = 'wikipedia' AND is_closed = 0 order by dbname"; 
	$res = mysql_query($query) or die(mysql_error()); 
	mysql_close($db);
?>
<form method="GET">
<p><b> 
<?php 
	switch ($toollang)
		{ 
			case 'ru': echo 'Страницы из'; break;
			case 'fr': echo 'Articles de '; break;
			case 'de': echo 'Artikel von '; break;
			case 'en': default: echo 'Articles from ';
		}
	echo '<select name="from" size="1">';
	$ar = '<select name="to" size="1">';
	while ($row=mysql_fetch_array($res)) 
	{ 
		$selected = ($from == $row['dbname']) ? 'selected="selected" ' : '';
		if ($from == $row['dbname']) 
		{
			$iw = $row['lang']; // for links in result html
		}
		$domain = (strlen($row['domain']) > 1) ? $row['domain'] : $row['dbname'];
		$selected_to = ($to == $row['lang']) ? 'selected="selected" ' : '';
		echo '<option '.$selected.'value="'.$row['dbname'].'">'.$domain.'</option>';
		$ar = $ar.'<option '.$selected_to.'value="'.$row['lang'].'">'.$domain.'</option>';
	}
	echo '</select>';
	$textarea = '<input type="text" name="num" size="2" maxlength="2" value="'.$num.'">';
	switch ($toollang)
		{ 
			case 'ru': echo 'с '.$textarea.'</input> и более интервиками, но без интервики на '; break;
			case 'fr': echo 'avec '.$textarea.'</input> ou plus langlinks, mais sans aucun à '; break;
			case 'de': echo 'mit '.$textarea.'</input> oder mehr langlinks, aber ohne zu '; break;
			case 'en': default: echo 'with '.$textarea.'</input> and more langlinks, but without anyone to ';
		}
	echo $ar.'</select>';
	echo '<input type="hidden" name="lang" value="'.$toollang.'">';
?>
</b></p>
<p><input type="submit" value="OK"></p>
</form>
<?php
if ($from)
{
	if ($to)
	{
		$stime = time();
		$ts_pw = posix_getpwuid(posix_getuid());
		$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/.my.cnf");
		$db = mysql_connect(str_replace('_', '-', $from).'.rrdb.toolserver.org', $ts_mycnf['user'], $ts_mycnf['password']);
		unset($ts_mycnf, $ts_pw);
		mysql_select_db($from, $db) or die(mysql_error()); ;
		$query = "select p.page_title, ll.num as langlinks_num from ".$from.".page as p, (select ll_from, count(ll_from) as num from ".$from.".langlinks where ll_from NOT IN (select ll_from from ".$from.".langlinks where ll_lang = '".$to."') group by ll_from) as ll where p.page_id = ll.ll_from AND p.page_namespace = 0 AND ll.num > ".$num." order by ll.num desc /* LIMIT:1200 */"; 
		$res = mysql_query($query) or die(mysql_error()); 
		$etime = time();
		$number = mysql_num_rows($res); 
		$timed = $etime - $stime;
		echo "<p>Total: $number rows. (done in $timed sec.)</p>";
		echo "<table class='wikitable sortable' width='auto'>"; 
		while ($row=mysql_fetch_array($res)) 
		{
			echo "<tr>";
			if ($th == 0) 
			{
				foreach ($row as $key => $value)
				{
					if (preg_match("/\d{1,3}/", $key) == 0)
						{ 
							echo "<th>".$key."</th>"; 
						}
					$th++;
				}
				echo "</tr>\n<tr>";
			}
			foreach ($row as $key => $value)
			{
				$ns = "";
				if (preg_match("/\d{1,3}/", $key) == 0)
				{ 
					$search  = array('<', '>', '"', '\'');
					$replace = array('&lt;', '&gt;', '&quot;', '&lsquo;');
					if (preg_match("/[^n][^o]_title/", $key))
					{
						echo "<td><a href=\"http://".$iw.".wikipedia.org/wiki/". $ns ."".str_replace($search, $replace, $value)."\">".$ns."".$value."</a></td>"; 
					}
					else
					{
						$search  = array('<', '>');
						$replace = array('&lt;', '&gt;');
						echo "<td>".str_replace($search, $replace, $value)."</td>";
					}
				}
			}
			echo "</tr>\n"; 
		}
		echo "</table>"; 
		mysql_close($db);
		echo "<br><b>Connection closed</b>";
	}
}
require_once("../skeleton_bottom.php");
?>
