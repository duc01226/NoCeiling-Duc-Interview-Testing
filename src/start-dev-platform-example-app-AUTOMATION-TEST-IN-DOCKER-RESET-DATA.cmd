docker network create platform-example-app-network

set ASPNETCORE_ENVIRONMENT=Development.Docker
set textSnippetApiHost=http://text-snippet-api
set textSnippetApiHost=http://host.docker.internal:5001
set textSnippetApiHost=http://localhost:5001
set AppNameToOrigin__TextSnippetApp=http://text-snippet-webspa
set AppNameToOrigin__TextSnippetApp=http://host.docker.internal:4001
set AppNameToOrigin__TextSnippetApp=http://localhost:4001
set RemoteWebDriverUrl=http://selenium-hub:4444/wd/hub
set RemoteWebDriverUrl=http://host.docker.internal:4444/wd/hub
set RemoteWebDriverUrl=http://localhost:4444/wd/hub
set WebDriverType=Chrome
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example kill
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example rm -f
docker volume prune -f
REM Do up/kill/up again because up the first time to create volume. It may have i/o errors for some container when container try to init right away. So we kill and up again
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example up --remove-orphans --detach
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example kill
REM Waiting for the volume i/o stable
timeout 15
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example up --remove-orphans --detach
pause
