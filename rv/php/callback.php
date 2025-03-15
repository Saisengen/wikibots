<?php
require_once '../../vendor/autoload.php';
require_once '../components/authentication.php';
header("Content-type: application/json; charset=utf-8");

if (isset($_GET['code'])) {
    authentication::auth_begin($_GET['code']);
    $username = authentication::get_username();
    $wikis = ['ruwiki' => false, 'ukwiki' => false];
    $post_data = ['action' => 'query', 'meta' => 'globaluserinfo', 'guiprop' => 'groups|merged|editcount', 'guiuser' => $username, 'utf8' => 1, 'format' => 'json'];
    $r = network::make_API_call(authentication::$AUTH_API, $post_data, 'POST', false);
    if ($r["status"] === "successful") {
        $r = json_decode($r["content"]);
        $gui_groups = $r->query->globaluserinfo->groups;
        if (count(array_intersect(['global-rollbacker', 'global-sysop', 'steward'], $gui_groups)) > 0)
            $wikis = ['ruwiki' => true, 'ukwiki' => true];
        else {
            foreach ($r->query->globaluserinfo->merged as $account) {
                $account_info = (array)$account;
                if (in_array($account_info['wiki'], ['ruwiki', 'ukwiki']))
                    if (array_key_exists('groups', $account_info))
                        if (count(array_intersect($account_info['groups'], ['rollbacker', 'sysop'])) > 0)
                            $wikis[$account_info['wiki']] = true;
            }
        }
        if ($wikis['ruwiki'] === true || $wikis['ukwiki'] === true) {
            $_SESSION['user'] = $username;
            $_SESSION['wikis'] = $wikis;
            $token = authentication::tokens_crypt($_SESSION['tokenKey']);
            $cookie_json = json_encode(["user" => $username, "wikis" => $wikis, "token" => $token, "refresh_token" => $_SESSION['refresh_token'], "expire" => $_SESSION['expire']]);
            setcookie("AVM-auth", $cookie_json, time() + 60 * 60 * 24 * 31, "/", "rv.toolforge.org", TRUE, TRUE);
            session_write_close();
            header("Location: https://rv.toolforge.org/?response=success");
            exit();
        }
        else {
            $_SESSION = Array();
            session_write_close();
            header("Location: https://rv.toolforge.org/?response=rights");
            exit();
        }
    }
}
