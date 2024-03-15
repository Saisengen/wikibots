<?php
$title = 'Поиск пустых страниц';
$slider = '';
require_once("../skeleton_1.php");
$p = $_GET["p"];
$n = $_GET["n"];
$ns = $_GET["ns"];
if (empty($p))
{ $p = 0; }
if (empty($n))
{ $n = 100; }
if (empty($ns))
{ $ns = 0; }
$ts_pw = posix_getpwuid(posix_getuid());
$ts_mycnf = parse_ini_file($ts_pw['dir'] . "/replica.my.cnf");
$stime = time();
$db = mysql_connect('ruwiki.labsdb', $ts_mycnf['user'], $ts_mycnf['password']);
unset($ts_mycnf, $ts_pw);
mysql_select_db('ruwiki_p', $db) or die(mysql_error()); ;
$start = ($n * $p);
$q = 'select count(*) as num from ruwiki_p.page where page_namespace IN ('.$ns.') AND page_len < 1';
$query = 'select page_namespace, page_title, page_len from ruwiki_p.page where page_namespace IN ('.$ns.') AND page_len < 1 limit '.$start.','.$n;
/* Выполнить запрос. Если произойдет ошибка - вывести ее. */ 
$all = mysql_query($q) or die(mysql_error()); 
$res = mysql_query($query) or die(mysql_error()); 
$etime = time();
while ($row=mysql_fetch_array($all)) { 
	foreach ($row as $key => $value)
		{
			$allnumber = $value; 
		}
	}
$number = mysql_num_rows($res); 
$np = floor($allnumber/$n);
$timed = $etime - $stime;
$ns_array = explode(",", $ns);
?>
<script type='text/javascript'>
function union_chbox() {
// определим переменную
values="";
for (i = 0; i<24; i++) {
// если чекбокс выбран
if (document.getElementById('box'+i).checked) {
// записываем в переменную
values = values+document.getElementById('box'+i).value+",";
}}
values = values.substring(0, values.length - 1);
// записываем значения в обычный инпут
document.getElementById('values_str').value = values;
return (true);
}
</script >
<FORM style="border:inset 1px gray; padding: 5px; width:700px; align:left; margin-top:10px;" METHOD="GET">
Пространство имён (default &mdash; 0): <br />
<table border="0"><tr><td>
<INPUT TYPE="checkbox" <?php if (in_array("0", $ns_array)) echo 'checked="checked" '; ?> VALUE="0" id="box0" onClick="return union_chbox()"> 0: Статьи<br />
<INPUT TYPE="checkbox" <?php if (in_array("2", $ns_array)) echo 'checked="checked" '; ?> VALUE="2" id="box2" onClick="return union_chbox()"> 2: Участник<br />
<INPUT TYPE="checkbox" <?php if (in_array("4", $ns_array)) echo 'checked="checked" '; ?> VALUE="4" id="box4" onClick="return union_chbox()"> 4: Википедия<br />
<INPUT TYPE="checkbox" <?php if (in_array("6", $ns_array)) echo 'checked="checked" '; ?> VALUE="6" id="box6" onClick="return union_chbox()"> 6: Файл<br />
<INPUT TYPE="checkbox" <?php if (in_array("8", $ns_array)) echo 'checked="checked" '; ?> VALUE="8" id="box8" onClick="return union_chbox()"> 8: MediaWiki<br />
<INPUT TYPE="checkbox" <?php if (in_array("10", $ns_array)) echo 'checked="checked" '; ?> VALUE="10" id="box10" onClick="return union_chbox()"> 10: Шаблон<br />
</td><td>
<INPUT TYPE="checkbox" <?php if (in_array("12", $ns_array)) echo 'checked="checked" '; ?> VALUE="12" id="box12" onClick="return union_chbox()"> 12: Справка<br />
<INPUT TYPE="checkbox" <?php if (in_array("14", $ns_array)) echo 'checked="checked" '; ?> VALUE="14" id="box14" onClick="return union_chbox()"> 14: Категория<br />
<INPUT TYPE="checkbox" <?php if (in_array("100", $ns_array)) echo 'checked="checked" '; ?> VALUE="100" id="box16" onClick="return union_chbox()"> 100: Портал<br />
<INPUT TYPE="checkbox" <?php if (in_array("102", $ns_array)) echo 'checked="checked" '; ?> VALUE="102" id="box18" onClick="return union_chbox()"> 102: Инкубатор<br />
<INPUT TYPE="checkbox" <?php if (in_array("104", $ns_array)) echo 'checked="checked" '; ?> VALUE="104" id="box20" onClick="return union_chbox()"> 104: Проект<br />
<INPUT TYPE="checkbox" <?php if (in_array("106", $ns_array)) echo 'checked="checked" '; ?> VALUE="106" id="box22" onClick="return union_chbox()"> 106: Арбитраж<br />
</td><td>
<INPUT TYPE="checkbox" <?php if (in_array("1", $ns_array)) echo 'checked="checked" '; ?> VALUE="1" id="box1" onClick="return union_chbox()"> 1: Обсуждение статьи<br />
<INPUT TYPE="checkbox" <?php if (in_array("3", $ns_array)) echo 'checked="checked" '; ?> VALUE="3" id="box3" onClick="return union_chbox()"> 3: Обсуждение участника<br />
<INPUT TYPE="checkbox" <?php if (in_array("5", $ns_array)) echo 'checked="checked" '; ?> VALUE="5" id="box5" onClick="return union_chbox()"> 5: Обсуждение Википедии<br />
<INPUT TYPE="checkbox" <?php if (in_array("7", $ns_array)) echo 'checked="checked" '; ?> VALUE="7" id="box7" onClick="return union_chbox()"> 7: Обсуждение файла<br />
<INPUT TYPE="checkbox" <?php if (in_array("9", $ns_array)) echo 'checked="checked" '; ?> VALUE="9" id="box9" onClick="return union_chbox()"> 9: Обсуждение MediaWiki<br />
<INPUT TYPE="checkbox" <?php if (in_array("11", $ns_array)) echo 'checked="checked" '; ?> VALUE="11" id="box11" onClick="return union_chbox()"> 11: Обсуждение шаблона<br />
</td><td>
<INPUT TYPE="checkbox" <?php if (in_array("13", $ns_array)) echo 'checked="checked" '; ?> VALUE="13" id="box13" onClick="return union_chbox()"> 13: Обсуждение справки<br />
<INPUT TYPE="checkbox" <?php if (in_array("15", $ns_array)) echo 'checked="checked" '; ?> VALUE="15" id="box15" onClick="return union_chbox()"> 15: Обсуждение категории<br />
<INPUT TYPE="checkbox" <?php if (in_array("101", $ns_array)) echo 'checked="checked" '; ?> VALUE="101" id="box17" onClick="return union_chbox()"> 101: Обсуждение портала<br />
<INPUT TYPE="checkbox" <?php if (in_array("103", $ns_array)) echo 'checked="checked" '; ?> VALUE="103" id="box19" onClick="return union_chbox()"> 103: Обсуждение Инкубатора<br />
<INPUT TYPE="checkbox" <?php if (in_array("105", $ns_array)) echo 'checked="checked" '; ?> VALUE="105" id="box21" onClick="return union_chbox()"> 105: Обсуждение проекта<br />
<INPUT TYPE="checkbox" <?php if (in_array("107", $ns_array)) echo 'checked="checked" '; ?> VALUE="107" id="box23" onClick="return union_chbox()"> 107: Обсуждение арбитража<br />
</td></tr>
</table>
<INPUT TYPE="hidden" NAME="ns" id="values_str" value=""> 
<INPUT TYPE="hidden" NAME="p" VALUE="<?php echo $p; ?>"> 
<INPUT TYPE="hidden" NAME="n" VALUE="<?php echo $n; ?>"> 
<INPUT TYPE="submit" VALUE="OK" onClick="return union_chbox()"> 
</FORM>
<?php
echo "<p>Всего: $allnumber строк, показано $number. (выполнено за $timed сек.)</p>";
$menu = '<p><b>Посмотреть:</b> ';
	// первые
if ($p < 1)
	$menu = $menu.'первые | ';
else
	$menu = $menu.'<a href="?ns='.$ns.'&p=0&n='.$n.'">первые</a> | ';
	// последние
if ($p < $np)
	$menu = $menu.'<a href="?ns='.$ns.'&p='.$np.'&n='.$n.'">последние</a> | ';
else
	$menu = $menu.'последние | ';
if($p != 0)
	$menu = $menu.'<a href="?ns='.$ns.'&p='.($p-1).'&n='.$n.'">пред.</a> | ';
else
	$menu = $menu.'пред. | ';
if ($p < $np)
	$menu = $menu.'<a href="?ns='.$ns.'&p='.($p+1).'&n='.$n.'">след.</a> ';
else
	$menu = $menu.'след. ';
	$menu = $menu.'('.$n.' строк) ';
	$menu = $menu.'(по <a href="?ns='.$ns.'&p='.$p.'&n=20">20</a> | ';
	$menu = $menu.'<a href="?ns='.$ns.'&p='.$p.'&n=50">50</a> | ';
	$menu = $menu.'<a href="?ns='.$ns.'&p='.$p.'&n=100">100</a> | ';
	$menu = $menu.'<a href="?ns='.$ns.'&p='.$p.'&n=500">500</a>) </p>';
echo $menu;
echo '<table class="tablesorter" border="1">
<thead><tr><th>№</th><th>Название</th><th>Размер</th></tr></thead><tbody>'; 
$i = $n*$p;
  while ($row=mysql_fetch_array($res)) { 
	 echo '<tr class="tool">';
	 foreach ($row as $key => $value)
	 {
		$ns = "";
		if ($row['page_namespace']) {
			switch ($row['page_namespace']) {
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
		}
		if (preg_match("/\d{1,3}/", $key) == 0)
		{ 
				// it's better to use htmlspecialchars()
				$search  = array(   '<',    '>',      '"',  '\'',   '?',     '&');
				$replace = array('%3C', '%3E', '%22', '%27', '%3F', '%26');
				if (preg_match("/page_title/", $key))
				{
					echo "<td>".++$i."</td><td><a href=\"http://ru.wikipedia.org/wiki/". $ns ."".str_replace($search, $replace, $value)."\">".$ns."".str_replace('_', ' ', $value)."</a> (<a href=\"http://ru.wikipedia.org/w/index.php?title=". $ns ."".str_replace($search, $replace, $value)."&action=history\"><u>history</u></a>)</td>"; 
				}
				else if (preg_match("/page_len/", $key))
				{
					$search  = array('<', '>');
					$replace = array('&lt;', '&gt;');
					echo "<td>".str_replace($search, $replace, $value)."</td>";
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
