#!/bin/sh
echo "Configuring container..."

echo "__text_Snippet_Api_Host__=>$textSnippetApiHost"
grep -rl __text_Snippet_Api_Host__ /usr/share/nginx/html | xargs sed -i 's@__text_Snippet_Api_Host__@'"$textSnippetApiHost"'@g'

echo "done."

nginx -g "daemon off;"