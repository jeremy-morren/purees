// See https://aka.ms/new-console-template for more information


using BenchmarkDotNet.Running;
using PureES.Extensions.Benchmarks;

BenchmarkRunner.Run<InMemoryEventStorePersistenceBenchmarks>();