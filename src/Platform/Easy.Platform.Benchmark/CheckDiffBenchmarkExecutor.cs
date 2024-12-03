using BenchmarkDotNet.Attributes;

namespace Easy.Platform.Benchmark;

[MemoryDiagnoser(false)]
public class CheckDiffBenchmarkExecutor
{
    public static readonly CheckDiffObjectClass CheckDiffObject1;
    public static readonly CheckDiffObjectClass CheckDiffObject2;

    static CheckDiffBenchmarkExecutor()
    {
        CheckDiffObject1 = new CheckDiffObjectClass
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
                    .ToList()!
            },
            PropListObj1 = Enumerable.Range(0, 100)
                .Select(
                    p => new CheckDiffObjectClass
                    {
                        PropStr1 = "Test",
                        PropDate1 = Clock.Now,
                        PropNumber1 = p
                    })
                .ToList()!
        };
        CheckDiffObject2 =
            CheckDiffObject1.DeepClone().With(p => { p.PropListObj1[10] = null; });
    }

    [Benchmark]
    public bool CheckDiffJsonSerialization()
    {
        return CheckDiffObject1.ToJson() != CheckDiffObject2.ToJson();
    }

    [Benchmark]
    public bool CheckDiffReflection()
    {
        return CheckDiffObject1.IsValuesDifferent(CheckDiffObject2);
    }

    public class CheckDiffObjectClass
    {
        public string PropStr1 { get; set; }
        public DateTime PropDate1 { get; set; }
        public int PropNumber1 { get; set; }
        public CheckDiffObjectClass PropObj1 { get; set; }
        public List<CheckDiffObjectClass?> PropListObj1 { get; set; }
    }
}
