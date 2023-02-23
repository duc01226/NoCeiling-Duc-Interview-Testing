docker network create platform-example-app-network

set ASPNETCORE_ENVIRONMENT=Development.Docker
set __TEXT_SNIPPET_API_HOST__=http://text-snippet-api
set __TEXT_SNIPPET_API_HOST__=http://host.docker.internal:5001
set __TEXT_SNIPPET_API_HOST__=http://localhost:5001
set AppNameToOrigin__TextSnippetApp=http://text-snippet-webspa
set AppNameToOrigin__TextSnippetApp=http://host.docker.internal:4001
set AppNameToOrigin__TextSnippetApp=http://localhost:4001
set RemoteWebDriverUrl=http://selenium-hub:4444/wd/hub
set RemoteWebDriverUrl=http://host.docker.internal:4444/wd/hub
set RemoteWebDriverUrl=http://localhost:4444/wd/hub
set WebDriverType=Chrome
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example kill
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example up --remove-orphans --detach
pause
