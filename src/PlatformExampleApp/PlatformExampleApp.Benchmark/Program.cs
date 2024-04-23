using BenchmarkDotNet.Running;
using PlatformExampleApp.Benchmark;

var summary = BenchmarkRunner.Run<QueryBenchmarkExecutor>();
