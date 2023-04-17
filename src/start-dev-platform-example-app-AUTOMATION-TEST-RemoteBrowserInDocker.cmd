docker network create platform-example-app-network

set ASPNETCORE_ENVIRONMENT=Development.Docker
set __TEXT_SNIPPET_API_HOST__=http://localhost:5001
set AutomationTestSettings__AppNameToOrigin__TextSnippetApp=http://localhost:4001
set AutomationTestSettings__RemoteWebDriverUrl=http://localhost:4444/wd/hub
set RandomThrowExceptionForTesting=true

docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example kill
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example up selenium-hub chrome1 chrome2 edge firefox --remove-orphans --detach
REM Waiting for the infrastructure started successfully
timeout 10
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example up text-snippet-api text-snippet-webspa --remove-orphans --detach
REM Waiting for the docker remote web browser started successfully
timeout 10

start "" "http://localhost:4444/ui"

REM Test BDD Chrome
set AutomationTestSettings__WebDriverType=Chrome
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
livingdoc test-assembly ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/PlatformExampleApp.Test.BDD.dll -t ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/TestExecution.json
start "" "./LivingDoc.html"

REM Test BDD Edge
set AutomationTestSettings__WebDriverType=Edge
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
livingdoc test-assembly ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/PlatformExampleApp.Test.BDD.dll -t ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/TestExecution.json
start "" "./LivingDoc.html"

REM Test BDD Firefox
set AutomationTestSettings__WebDriverType=Firefox
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
livingdoc test-assembly ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/PlatformExampleApp.Test.BDD.dll -t ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/TestExecution.json
start "" "./LivingDoc.html"

pause

