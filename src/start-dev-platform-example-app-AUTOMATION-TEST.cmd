REM Test BDD Chrome
set AutomationTestSettings__WebDriverType=Chrome
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
livingdoc test-assembly ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/AutomationTest.BravoTalents.BDD.dll -t ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/TestExecution.json --title=Test-Platform-Test-Snippet-Using-Chrome
start "" "./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/LivingDoc.html"

REM Test BDD Edge
set AutomationTestSettings__WebDriverType=Edge
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
livingdoc test-assembly ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/AutomationTest.BravoTalents.BDD.dll -t ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/TestExecution.json --title=Test-Platform-Test-Snippet-Using-Edge
start "" "./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/LivingDoc.html"

REM Test BDD Firefox
set AutomationTestSettings__WebDriverType=Firefox
dotnet test ./PlatformExampleApp/PlatformExampleApp.Test.BDD/PlatformExampleApp.Test.BDD.csproj
livingdoc test-assembly ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/AutomationTest.BravoTalents.BDD.dll -t ./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/TestExecution.json --title=Test-Platform-Test-Snippet-Using-Firefox
start "" "./PlatformExampleApp/PlatformExampleApp.Test.BDD/bin/Debug/net7.0/LivingDoc.html"

pause

