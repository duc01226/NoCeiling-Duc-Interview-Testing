#!/bin/sh
echo "Configuring container..."

echo "Environment variables"
echo "__TEXT_SNIPPET_API_HOST__ => $__TEXT_SNIPPET_API_HOST__"

echo "Environment variables replacement"
for i in `find /usr/share/nginx/html -name "*.js"`; do envsubst '${__TEXT_SNIPPET_API_HOST__}' < $i | sponge $i;done

echo "Done environment variables replacement"

echo "done."

nginx -g "daemon off;"
