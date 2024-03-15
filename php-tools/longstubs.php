<?php
	$title = "LongStubs - Wikimedia Toolserver";
	header('Content-Type: text/html; charset=utf-8');
	require_once("../skeleton_up.php");
	$ts_pw = posix_getpwuid(posix_getuid());
	$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
	$toollang = $_GET['lang'];
	$from = $_GET['from'];
	$cat = $_GET['cat'];
	$len = $_GET['len'];
	if (!($len)) {$len = 20000;}
	$db = mysql_connect('sql.toolserver.org', $ts_mycnf['user'], $ts_mycnf['password']);
	unset($ts_mycnf, $ts_pw);
	mysql_select_db('toolserver', $db) or die(mysql_error()); ;
	$query = "select dbname, lang, domain from toolserver.wiki where size > 10000 AND is_closed = 0 order by dbname"; 
	$res = mysql_query($query) or die(mysql_error()); 
	mysql_close($db);
?>
<form method="GET">
<p><b> 
<?php
	switch ($toollang)
		{ 
			case 'ru': echo 'Выберите проект: '; break;
			case 'en': default: echo 'Choose project ';
		}
	echo '<select name="from" size="1">';
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
	$cat_text = '<input type="text" name="cat" size="40" maxlength="300" value="'.$cat.'">';
	$len_text = '<input type="text" name="len" size="5" maxlength="5" value="'.$len.'">';
	switch ($toollang)
		{ 
			case 'ru': echo '. <br />Введите название категории без слова «Категория:» '.$cat_text.' и максимальный размер стаба (по умолчанию ~20кб): '.$len_text.'.'; break;
			case 'en': default: echo '. <br />Write category-name without „Category:“ '.$cat_text.' and max size of stub (default: ~20kb): '.$len_text.'.';
		}
		echo '<input type="hidden" name="lang" value="'.$toollang.'">';
?>
</b></p>

<p><input type="submit" value="OK"></p>
</form>
<?php
if ($from)
{
	if ($cat)
	{
		$stime = time();
		$ts_pw = posix_getpwuid(posix_getuid());
		$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/.my.cnf");
		$db = mysql_connect(str_replace('_', '-', $from).'.rrdb.toolserver.org', $ts_mycnf['user'], $ts_mycnf['password']);
		unset($ts_mycnf, $ts_pw);
		mysql_select_db($from, $db) or die(mysql_error());
		$query = "select page_id, page_namespace, page_title, page_len from page where page_id IN (select cl_from from categorylinks where cl_to = '".str_replace(' ', '_', $cat)."') AND page_len > ".$len." order by page_len desc /* LIMIT:1200 */"; 
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
