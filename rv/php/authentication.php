<?php
use Defuse\Crypto\Crypto;
use Defuse\Crypto\Key;
require_once __DIR__ . '/../../vendor/autoload.php';
require_once __DIR__ . '/network.php';

session_name('AVM');
session_start();

class authentication {
    static string $AUTH_API = "https://meta.wikimedia.org/w/api.php";

    static function auth_start() {
        header("Location: " . getenv('OAUTH_URL') . "/authorize?response_type=code&client_id=" .
            getenv('OAUTH_CLIENT_KEY'));
        exit();
    }

    static function auth_begin($code): string {
        $post_data = ['grant_type' => 'authorization_code', 'code' => $code, 'client_id' => getenv('OAUTH_CLIENT_KEY'),
            'client_secret' => getenv('OAUTH_CLIENT_SECRET')];
        $r = network::make_API_call(getenv('OAUTH_URL') . "/access_token", $post_data, "POST",
            false, false);
        if ($r["status"] === "successful") {
            $r = json_decode($r["content"]);
            $_SESSION['tokenKey'] = $r->access_token;
            $_SESSION['refresh_token'] = $r->refresh_token;
            $_SESSION['expire'] = strtotime(gmdate("M d Y H:i:s", time())) + $r->expires_in - 30;  // - 1 min
            return "successful";
        }
        else
            return $r["code"] . ": " . $r["message"];
    }

    static function pickup_bearer_token(): string {
        if (strtotime(gmdate("M d Y H:i:s", time())) <= $_SESSION['expire'])
            return $_SESSION['tokenKey'];
        $post_data = ['grant_type' => 'refresh_token', 'refresh_token' => $_SESSION['refresh_token'],
            'client_id' => getenv('OAUTH_CLIENT_KEY'), 'client_secret' => getenv('OAUTH_CLIENT_SECRET')];
        $r = network::make_API_call(getenv('OAUTH_URL') . "/access_token", $post_data, "POST",
            false, false);
        if ($r["status"] === "successful") {
            $r = json_decode($r["content"]);

            $token = authentication::tokens_crypt($r->access_token);
            $_SESSION['tokenKey'] =  $r->access_token;
            $_SESSION['refresh_token'] = $r->refresh_token;
            $_SESSION['expire'] = strtotime(gmdate("M d Y H:i:s", time())) + ($r->expires_in-30);  // 1 min

            $cookie_json = json_encode(["user" => $_SESSION['user'], "wikis" => $_SESSION['wikis'], "token" => $token,
                "refresh_token" => $r->refresh_token, "expire" => $_SESSION['expire']]);
            setcookie("AVM-auth", $cookie_json, time() + 60 * 60 * 24 * 31, "/", "rv.toolforge.org", TRUE, TRUE);
            return $r->access_token;
        }
        else
            return $r["code"] . ": " . $r["message"];
    }

    static function get_username(): string {
        $r = network::make_API_call(getenv('OAUTH_URL') . "/resource/profile", [], "GET",
            true, 'json');
        if ($r["status"] === "successful") {
            $r = json_decode($r["content"]);
            return $r->username;
        }
        else
            return $r["code"] . ": " . $r["message"];
    }

    static function tokens_crypt($token): string | bool {
        $key = bin2hex(random_bytes(32));

        $ciphertext1 = openssl_encrypt($token, "AES-128-ECB", $key);

        $ts_pw = posix_getpwuid(posix_getuid());
        $ts_mycnf = parse_ini_file("/data/project/rv/replica.my.cnf");
        $db = new PDO("mysql:host=tools.labsdb;dbname=s55857__rv;charset=utf8", $ts_mycnf['user'], $ts_mycnf['password']);
        unset($ts_mycnf, $ts_pw);

        $q = $db->prepare('SELECT name FROM credits WHERE name = :user;');
        $q->execute(array(':user' => $_SESSION['user']));
        $r = json_encode($q->fetchAll(PDO::FETCH_ASSOC));
        if (count(json_decode($r)) === 0)
            $q = $db->prepare('INSERT INTO credits (name, skey) VALUES (:name, :key)');
        else
            $q = $db->prepare('UPDATE credits SET skey = :key WHERE name = :name');
        $q->execute(array(':name' => $_SESSION['user'], ':key' => $key));

        $db = null;
        return $ciphertext1;
    }

    static function tokens_decrypt($token): string | bool {
        $ts_pw = posix_getpwuid(posix_getuid());
        $ts_mycnf = parse_ini_file("/data/project/rv/replica.my.cnf");
        $db = new PDO("mysql:host=tools.labsdb;dbname=s55857__rv;charset=utf8", $ts_mycnf['user'], $ts_mycnf['password']);
        unset($ts_mycnf, $ts_pw);

        $q = $db->prepare('SELECT * FROM credits WHERE name = :user;');
        $q->execute(array(':user' => $_SESSION['user']));
        $db = null;

        $r = json_decode(json_encode($q->fetchAll(PDO::FETCH_ASSOC)));
        if (count($r) > 0) {
            $key = $r[0]->skey;
            $access = openssl_decrypt($token, "AES-128-ECB", $key);
            return $access;
        }
        else
            return false;
    }
}
