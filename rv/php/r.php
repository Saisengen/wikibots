<?php
header('Content-Type: text/html; charset=utf-8');
header("Access-Control-Allow-Origin: ''");
header("Access-Control-Allow-Methods:  ''");
header("Content-Security-Policy: default-src 'none'");

require_once 'components/authentication.php';
require_once 'components/network.php';

set_error_handler("callback_error");


if (!isset($_SESSION['tokenKey']) || $_SESSION['tokenKey'] === "") {

    if (isset($_COOKIE["AVM-auth"])) {
        $cookies = $_COOKIE["AVM-auth"];
        $obj = json_decode($cookies);
        if (isset($obj->user)) {
            $_SESSION['user'] = $obj->user;
            $_SESSION['expire'] = $obj->expire;
            $_SESSION['tokenKey'] = authentication::tokens_decrypt($obj->token);
            $_SESSION['refresh_token'] = $obj->refresh_token;
            $_SESSION['wikis'] = $obj->wikis;
        }
    }
    if (!isset($_SESSION['tokenKey'])) {
        session_write_close();
        header('Location: https://rv.toolforge.org/auth/index.php');
        exit();
    }
}

if (!isset($_SERVER['PATH_INFO']) || !isset($_SERVER['HTTP_REFERER']))
    exit();

try {
    $r = explode('/', $_SERVER['PATH_INFO']);
    $pre_lang = $_SERVER['HTTP_REFERER'];
    if (str_contains($pre_lang, "https://ru.wikipedia.org/"))
        $lang = "ru";
    if (str_contains($pre_lang, "https://ru.wikipedia.org/"))
        $lang = "uk";
    $revid = $r[1];
    # $lang = "ru";

    $API_url = "https://ru.wikipedia.org/w/api.php";
    if ($lang === "uk")
        str_replace("https://ru.", "https://uk.", $API_url);
    $target_url = str_replace("api.php", "index.php", $API_url) . "?diff=" . $revid;
    $data = ["action" => "query", "format" => "json", "prop" => "revisions", "revids" =>  $revid, "rvprop" => "user|ids",
        "utf8" => 1];
    $r =  network::make_API_call(domain: $API_url, params: $data, method: "POST", with_auth: false);
    if ($r["status"] === "successful") {
        $content = json_decode($r["content"]);
        $content = $content->query->pages;
        $pageid = array_keys((array)$content)[0];
        $user = $content->$pageid->revisions[0]->user;
        if ($pageid > 0) {
            $target_url = str_replace("api.php", "index.php", $API_url) . "?curid=" . $pageid . "&action=history";
            $data = ["action" => "query", "format" => "json", "prop" => "revisions", "pageids" => $pageid,
                "rvprop" => "user|ids|tags", "rvlimit" => "500", "rvendid" => "137700965", "rvdir" => "older",
                "rvexcludeuser" => $user, "utf8" => 1];
            $r = network::make_API_call(domain: $API_url, params: $data, method: "POST", with_auth: false);
            if ($r["status"] === "successful") {
                $content = json_decode($r["content"]);
                $content = $content->query->pages;
                $rb_check = true;
                if (in_array("revisions", (array)$content->$pageid)) {
                    $keys = array_keys((array)$content->$pageid->revisions);
                    $last_key = array_pop($keys);
                    if (in_array("tags", (array)$content->$pageid->revisions[$last_key]))
                        if (in_array("mw-rollback", (array)$content->$pageid->revisions[$last_key]->tags))
                            $rb_check = false;
                }
                if (!$rb_check) {
                    header('Location: ' . $target_url);
                    exit();
                } else {
                    $data = $params3 = ["action" => "query", "meta" => "tokens", "type" => "rollback",
                        "format" => "json"];
                    $r = network::make_API_call(domain: $API_url, params: $data);
                    if ($r["status"] === "successful") {
                        $content = json_decode($r["content"]);
                        $rollbacktoken = $content->query->tokens->rollbacktoken;
                        $data = ["action" => "rollback", "format" => "json", "pageid" => $pageid,
                            "user" => $user, "utf8" => 1, "token" => $rollbacktoken];
                        $r = network::make_API_call(domain: $API_url, params: $data);
                        header('Location: ' . $target_url);
                        exit();
                    }
                }
            }
        }
    }
    header('Location: ' . $target_url);
    exit();
} catch (Exception $e) {
    header('Location: ' . $target_url);
    exit();
}

function callback_error($errno, $errstr, $lerror) {
    echo $errstr . $lerror;
}
