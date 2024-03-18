#!/bin/bash

DEST_DIR="${DEST_DIR:-/layers/heroku_php/wikibots/public_html}"

[[ -d "$DEST_DIR/cgi-bin" ]] || {
    echo "Unable to find cgi-bin directory at $DEST_DIR, something went wrong."
    exit 1
}

## Set the logs to tool dir if mounted with NFS
## Otherwise they will be sent to stdout/stderr
[[ "$TOOL_DATA_DIR" != "" ]] && {
    # shellcheck disable=SC2093
    exec 1>> "$TOOL_DATA_DIR/access.log"
    exec 2>> "$TOOL_DATA_DIR/error.log"
}

sillyserver="
# copied mostly from https://github.com/python/cpython/blob/main/Lib/http/server.py#L646
import sys
import http.server
import socketserver

# Overwrite the stderr/stdout logging
class LoggingCGIHandler(http.server.CGIHTTPRequestHandler):
    def log_request(self, code='-', size='-'):
        log = self.log_message
        if isinstance(code, http.server.HTTPStatus):
            code = code.value
            if code >= 400:
                log = self.log_error

        log('\"%s\" %s %s',
            self.requestline, str(code), str(size))

    def log_error(self, format, *args):
        message = format % args
        sys.stderr.write('%s - - [%s] %s\n' %
                         (self.address_string(),
                          self.log_date_time_string(),
                          message.translate(self._control_char_table)))

    def log_message(self, format, *args):
        message = format % args
        sys.stdout.write('%s - - [%s] %s\n' %
                         (self.address_string(),
                          self.log_date_time_string(),
                          message.translate(self._control_char_table)))


class SillyServer(http.server.ThreadingHTTPServer):
    def finish_request(self, request, client_address):
        self.RequestHandlerClass(request, client_address, self,
                                    directory='"$DEST_DIR"')

http.server.test(
    HandlerClass=LoggingCGIHandler,
    ServerClass=SillyServer,
    port='"${PORT:-8000}"',
    bind='0.0.0.0',
    protocol='HTTP/1.1',
)
"

# -u is for unbuffered
python \
    -u \
    -c "$sillyserver" \
    "$@"

