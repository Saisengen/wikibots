<?php
require_once __DIR__ . '/../../vendor/autoload.php';
use GuzzleHttp\Client;

class network {
    static function make_API_call($domain, $params = [], $method = "POST", $with_auth = true, $content_type = 'json'): array {
        $headers = ['User-Agent' => 'SWViewer; swviewer.maintainers@toolforge.org; PHP, Guzzle 7.8'];
        if ($with_auth)
            $headers['Authorization'] = 'Bearer ' . authentication::pickup_bearer_token();
        //if ($content_type === 'json')
        //    $headers['Accept'] = 'application/json';
        $headers['Content-Type'] = 'application/x-www-form-urlencoded';
        $headers['Accept'] = 'application/x-www-form-urlencoded';
        try {
            $client = new Client(['timeout' => 20.0, 'verify' => false]);
            $response = ($method === 'POST') ? $client->post($domain, ['headers' => $headers, 'form_params' => $params]) : $client->get($domain, ['headers' => $headers]);
            return ["status"=>"successful", "code"=>$response->getStatusCode(),
                "content"=>$response->getBody()->getContents()];
        } catch (\GuzzleHttp\Exception\GuzzleException $e) {
            return ["status"=>"error", "code"=>$e->getCode(), "message"=>$e->getMessage()];
        }
    }
}
