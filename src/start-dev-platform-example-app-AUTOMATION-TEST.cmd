dotnet test ./PlatformExampleApp/PlatformExampleApp.Test/PlatformExampleApp.Test.csproj

REM Test BDD Chrome
set AutomationTestSettings__WebDriverType=Chrome
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj

REM Test BDD Firefox
set AutomationTestSettings__WebDriverType=Firefox
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj

REM Test BDD Edge
set AutomationTestSettings__WebDriverType=Edge
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
pause
