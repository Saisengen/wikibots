<?php
header("Access-Control-Allow-Origin: ''");
header("Access-Control-Allow-Methods:  ''");
// header("Content-Security-Policy: default-src 'self'; script-src tools-static.wmflabs.org rv.toolforge.org");

require_once 'components/authentication.php';
require_once 'components/network.php';
require_once __DIR__ . '/../vendor/autoload.php';
use GuzzleHttp\Client;

if ((!isset($_SESSION['user']) || $_SESSION['user'] == '') && !isset($_GET['token'])) {
        $_SESSION['online'] = 'auth';
        session_write_close();
        header('Location: https://rv.toolforge.org/auth/index.php');
        exit();
    }
if (($_SESSION['user'] !== 'Iluvatar' and $_SESSION['user'] !== 'MBH' and $_SESSION['user'] !== 'Well very well') && !isset($_GET['token'])) {
        session_write_close();
        header("Location: https://rv.toolforge.org/?response=rights_online");
        exit();
}

if (isset($_GET['token']) && ($_GET['token'] !== getenv('BOT_TOKEN') || !isset($_GET['action']) || $_GET['action'] !== 'restart')) {
        session_write_close();
        exit();
}
else {
    $TOOLFORGE_API = 'https://api.svc.tools.eqiad1.wikimedia.cloud:30003';
    $client = new Client(['timeout' => 20.0, 'verify' => false]);
    if (isset($_GET['get']) || isset($_GET['send']) || isset($_POST['send'])) {
        if (isset($_GET['get'])) {
            if (isset($_GET['jobs'])) {
                $response = $client->get($TOOLFORGE_API . '/jobs/v1/tool/rv/jobs/',
                    ['cert' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.crt',
                     'ssl_key' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.key']);
                echo $response->getBody()->getContents();
            }
            if (isset($_GET['file'])) {
                $jobs_file = yaml_parse_file('../jobs.yaml', -1);
                echo json_encode($jobs_file);
            }
        }
        if (isset($_GET['send']) && isset($_GET['action']) && isset($_GET['name'])) {
            if ($_GET['action'] === 'restart') {
                $response = $client->post($TOOLFORGE_API . '/jobs/v1/tool/rv/jobs/' . $_GET["name"] . '/restart',
                    ['cert' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.crt',
                     'ssl_key' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.key']);
                echo $response->getBody()->getContents();
            }
            if ($_GET['action'] === 'stop') {
                $response = $client->delete($TOOLFORGE_API . '/jobs/v1/tool/rv/jobs/' . $_GET["name"],
                    ['cert' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.crt',
                     'ssl_key' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.key']);
                echo $response->getBody()->getContents();
            }
        }
        if (isset($_POST['send']) && isset($_POST['action']) && $_POST['action'] == 'run') {
                unset($_POST['send']);
                unset($_POST['action']);
                $_POST['cmd'] = $_POST['command'];
                unset($_POST['command']);
                $_POST['imagename'] = $_POST['image'];
                $_POST['filelog'] = true;
                if (isset($_POST['filelog-stderr'])) {
                    $_POST['filelog_stderr'] = $_POST['filelog-stderr'];
                    unset($_POST['filelog-stderr']);
                }
                if (isset($_POST['filelog-stdout'])) {
                    $_POST['filelog_stdout'] = $_POST['filelog-stdout'];
                    unset($_POST['filelog-stdout']);
                }
                unset($_POST['image']);
                $response = $client->post($TOOLFORGE_API . '/jobs/v1/tool/rv/jobs/',
                    ['json' => $_POST, 'cert' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.crt',
                     'ssl_key' => getenv('TOOL_DATA_DIR') . '/.toolskube/client.key']);
                echo $response->getBody()->getContents();
       }
    }
    else {
?>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title>Монитор состояния</title>

    <meta name='viewport' content='width=device-width, initial-scale=1, shrink-to-fit=no'>
    <meta name="application-name" content="AVMonitor">
    <meta name="author" content="Iluvatar, MBH">
    <meta name="description" content="Монитор состояния">
    <meta name="keywords" content="rollback, patrolling wikipedia, recent changes">
    <meta name="msapplication-TileColor" content="#808d9f">

    <link rel="stylesheet" href="styles/stylesOn.css">
    <script type="text/javascript" src="//tools-static.wmflabs.org/cdnjs/ajax/libs/jquery/3.7.1/jquery.min.js"></script>
    <script type="text/javascript" src="//tools-static.wmflabs.org/cdnjs/ajax/libs/vue/3.4.35/vue.global.min.js"></script>

    <body lang='ru'>
        <div id='app'>
          <div style="display: flex; justify-content: center; align-items: center">
            <ul class="customList">
            <h3 style='text-align: center'>Задания в работе</h3>
            <li v-for="job in jobs" style="text-align: start;">
                <div :style="job.status_short == 'Running' ? { 'background': 'linear-gradient(to left, rgb(63 255 121) 0%, rgb(213 243 211), rgb(234 241 235)) !important'} : ''">
                Задание: {{ job.name }}<br>
                Расписание: <p v-if="job.hasOwnProperty('schedule')" style="display:inline">{{ job.schedule }}</p><p v-else style="display:inline">постоянно</p><br>
                Статус: <p v-if="job.status_short == 'Running'" style="display:inline">выполняется</p><p v-else style="display:inline">{{ job.status_short.replace('Last schedule time:', 'в планировщике, посл. ').replace('T', ' ').replace(':00Z', '').replace('Running for', 'запущено').replace('Waiting for scheduled time', 'ожид. перв. запуска') }}</p>
                </div>
                <button class="buttonStop" v-on:click="stop(job.name)" style="display:inline">Остановить</button> <button class="buttonRestart" v-on:click="restart(job.name)" style="display:inline">Перезапуск</button>
            </li>
            <br><br>
            <h3 v-if="file.length > 0" style="text-align: center">Остановленные задания</h3>
            <li v-for="stoppedJob in file" style="text-align: start;">
                <div>
                Задание: {{ stoppedJob.name }}<br>
                Расписание: <p v-if="stoppedJob.hasOwnProperty('schedule')" style="display:inline">{{ stoppedJob.schedule }}</p><p v-else style="display:inline">постоянно</p><br>
                Статус: остановлено
                </div>
                <button class="buttonRun" v-on:click="run(stoppedJob)" style="display:inline">Запустить</button>
            </li>
            </ul>

         </div>
        </div>
    </body>

<script>
  const { createApp, ref } = Vue

  createApp({
    data() {
      const title = ref('Hello vue!')
      var jobs = []
      var file = []
      return {jobs: jobs, title: title, file: file}
    },
    methods: {
      stop(value) {
        $.ajax({
             url: "online.php?send=1&action=stop&name=" + encodeURI(value),
             context: this,
         }).done(function( data ) {
              let res = JSON.parse(data);
              if (res.hasOwnProperty('messages') && Object.keys(res.messages).length == 0) {
                  setTimeout("alert('Готово. Страница будет перезагружена через 5 секунд.');", 1);
                  setTimeout(function(){
                      window.location.reload(1);
                  }, 5000);
              }
         });
      },
      restart(value) {
        $.ajax({
             url: "online.php?send=1&action=restart&name=" + encodeURI(value),
             context: this,
         }).done(function( data ) {
              let res = JSON.parse(data);
              if (res.hasOwnProperty('messages') && Object.keys(res.messages).length == 0) {
                  setTimeout("alert('Готово. Страница будет перезагружена через 5 секунд.');", 1);
                  setTimeout(function(){
                      window.location.reload(1);
                  }, 5000);
              }
         });
      },
      run(value) {
          let data = value;
          data.send = 1;
          data.action = 'run';
          $.ajax({
              url: 'online.php',
              method: 'post',
              data: data,
              success: function(data) {
	      let res = JSON.parse(data);
              if (res.hasOwnProperty('messages') && Object.keys(res.messages).length == 0) {
                  setTimeout("alert('Готово. Страница будет перезагружена через 5 секунд.');", 1);
                  setTimeout(function(){
                      window.location.reload(1);
                  }, 5000);
              }
              }
          });
      }
    },
    beforeMount() {
       $.ajax({
           url: "online.php?get=1&jobs=1",
           context: this,
       }).done(function( data ) {
            this.jobs = JSON.parse(data).jobs

            $.ajax({
              url: "online.php?get=1&file=1",
              context: this,
            }).done(function( data2 ) {
                console.log(this.jobs);
                this.file = '';
                let resultFile = [];
                let resFile = JSON.parse(data2)[0];
                for (var i=0; i < resFile.length; i++) {
                    let check = 1;
                    for (var n=0; n < this.jobs.length; n++) {
                       if (this.jobs[n].name == resFile[i].name)
                           check = 0;
                    }
                    if (check == 1)
                        resultFile.push(resFile[i]);
                }
                this.file = resultFile;
            });

       });
    }

  }).mount('#app')


    
</script>
















<?php
    }
}
?>
