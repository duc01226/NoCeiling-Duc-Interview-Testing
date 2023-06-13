docker network create platform-example-app-network

set ASPNETCORE_ENVIRONMENT=Development.Docker
set __TEXT_SNIPPET_API_HOST__=http://localhost:5001
set AutomationTestSettings__AppNameToOrigin__TextSnippetApp=http://localhost:4001
set AutomationTestSettings__RemoteWebDriverUrl=http://localhost:4444/wd/hub
set RandomThrowExceptionForTesting=true

docker compose -f platform-example-app.docker compose.yml -f platform-example-app.docker compose.override.yml -p easyplatform-example kill
docker compose -f platform-example-app.docker compose.yml -f platform-example-app.docker compose.override.yml -p easyplatform-example up --remove-orphans --detach
pause
