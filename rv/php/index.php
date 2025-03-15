<?php
header("Access-Control-Allow-Origin: ''");
header("Access-Control-Allow-Methods:  ''");
header("Content-Security-Policy: default-src 'none'");
require_once 'components/authentication.php';
require_once 'components/network.php';
?>

    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title>AVMonitor</title>

    <meta name='viewport' content='width=device-width, initial-scale=1, shrink-to-fit=no'>
    <meta name="application-name" content="AVMonitor">
    <meta name="author" content="Iluvatar, MBH">
    <meta name="description" content="Быстрый откат подозрительных правок.">
    <meta name="keywords" content="rollback, patrolling wikipedia, recent changes">
    <meta name="msapplication-TileColor" content="#808d9f">

    <link rel="stylesheet" href="styles/styles.css">

<?php
if (!isset($_GET['response']) && (!isset($_SESSION['tokenKey']) || $_SESSION['tokenKey'] === "")) {
echo "
    <div id='login-page-base' class='login-base secondary-cont' style='display: none'>
    <div class='login-card'>
        <div style='text-align: center;'>
            <span class='fs-xl custom-lang' style='font-weight: bold;'>Добро пожаловать!</span>
            <a id='abtn' class='i-btn__accent accent-hover custom-lang' style='margin: 16px 0; color: var(--tc-accent) !important; padding: 0 24px; text-decoration: none !important;' href='https://rv.toolforge.org/auth'>Логин</a>
            <span id='login-r' class='fs-xs custom-lang' style='width: 80%'>Требуется глобальный флаг откатывающего или флаги в рувики / укрвки.</span>
            <span id='login-d' class='fs-xs' style='margin-top: 3px; width: 80%'><div id='ld1' style='display: inline'></div><div id='ld2' style='display: inline' onclick='openPO()'></div><div id='ld3' style='display: inline'></div></span>
        </div>
        <div>
            <span class='fs-xs'>Brought to you by <a rel='noopener noreferrer' target='_blank' href='https://meta.wikimedia.org/wiki/User:Iluvatar'>Iluvatar</a>, <a rel='noopener noreferrer' target='_blank' href='https://ru.wikipedia.org/wiki/User:MBH'>MBH</a></span>
        </div>
    </div>
</div>
";
}
else {
    if (!isset($_GET['response'])) {
        header('Location: https://ru.wikipedia.org/wiki/Служебная:Вклад/Рейму_Хакурей');
        exit();
    }
}

if ($_GET['response'] === "rights") {
    echo "У вас нет тех.права на откат ни в рувики, ни в укрвики.";
    exit();
}

if ($_GET['response'] === "rights_online") {
    echo "У вас нет прав для управления задачами.";
    exit();
}

if ($_GET['response'] === "success") {
    header('Location: https://ru.wikipedia.org/wiki/Служебная:Вклад/Рейму_Хакурей');
    exit();
}
