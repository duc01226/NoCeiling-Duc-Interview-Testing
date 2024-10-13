using BenchmarkDotNet.Running;
using Easy.Platform.Benchmark;

BenchmarkRunner.Run<CheckDiffBenchmarkExecutor>();
BenchmarkRunner.Run<DeepCloneBenchmarkExecutor>();
