using BenchmarkDotNet.Attributes;

namespace Easy.Platform.Benchmark;

[MemoryDiagnoser(false)]
public class DeepCloneBenchmarkExecutor
{
    public static readonly CheckDiffObjectClass CheckDiffObject = new()
    {
        PropStr1 = "Test",
        PropDate1 = Clock.Now,
        PropNumber1 = 1,
        PropObj1 = new CheckDiffObjectClass
        {
            PropStr1 = "Test",
            PropDate1 = Clock.Now,
            PropNumber1 = 1,
            PropListObj1 = Enumerable.Range(0, 100)
                .Select(
                    p => new CheckDiffObjectClass
                    {
                        PropStr1 = "Test",
                        PropDate1 = Clock.Now,
                        PropNumber1 = p
                    })
                .ToList()
        },
        PropListObj1 = Enumerable.Range(0, 100)
            .Select(
                p => new CheckDiffObjectClass
                {
                    PropStr1 = "Test",
                    PropDate1 = Clock.Now,
                    PropNumber1 = p
                })
            .ToList()
    };

    [Benchmark]
    public object DeepCloneJsonSerialization()
    {
        return CheckDiffObject.DeepClone();
    }

    [Benchmark]
    public object DeepCloneReflection()
    {
        return CheckDiffObject.DeepCloneUseReflection();
    }

    public class CheckDiffObjectClass
    {
        public string PropStr1 { get; set; }
        public DateTime PropDate1 { get; set; }
        public int PropNumber1 { get; set; }
        public CheckDiffObjectClass PropObj1 { get; set; }
        public List<CheckDiffObjectClass> PropListObj1 { get; set; }
    }
}
