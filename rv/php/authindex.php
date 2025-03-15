<?php
require_once '../../vendor/autoload.php';
require_once '../components/authentication.php';
header("Content-type: application/json; charset=utf-8");

if (!isset($_GET['action'])) {
    authentication::auth_start();
    exit();
}

if (isset($_GET["action"]) && $_GET["action"] == "unlogin") {
    $_SESSION = Array();
    session_write_close();
    setcookie("AVM-auth", null, time() - 1, "/", "rv.toolforge.org", TRUE, TRUE);
    echo json_encode(["result" => "success"]);
}
