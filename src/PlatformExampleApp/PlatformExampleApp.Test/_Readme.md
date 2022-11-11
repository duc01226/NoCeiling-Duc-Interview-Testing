Each TestCase class file will run in parallel
Each IClassFixture is a new instance for each TestCase class file (by SCOPED DI), but is used same instance for each test case method using the same fixture

Each test case method in a class run in an order. 
Each time a new instance of the TestClass will be created. 
So that we could call reset driver fixture in the constructor.
Same Driver like running in a same browser. You may want to reset cookies and others things before start new test cases

IF we use dependency injection with singleton, the driver will be reused by all of test classes
