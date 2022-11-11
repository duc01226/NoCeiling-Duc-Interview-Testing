namespace Easy.Platform.Common.Extensions.WhenCases;

public class WhenCase
{
}

/// <summary>
/// Support fix if/else code smell
/// References:
/// https://levelup.gitconnected.com/treat-if-else-as-a-code-smell-until-proven-otherwise-3bd2c4c577bf#:~:text=The%20If%2DElse%20statement%20is,and%20the%20need%20for%20refactoring.
/// </summary>
public class WhenCase<TSource, TTarget> : WhenCase
{
    protected WhenCase(TSource source)
    {
        Source = source;
    }

    public WhenCase(TSource source, params CaseItem[] cases) : this(source)
    {
        Cases = cases.ToList();
    }

    public WhenCase(TSource source, Func<TSource, bool> @case, Func<TSource, TTarget> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, Func<TSource, bool> @case, Func<TSource, Task<TTarget>> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, bool @case, Func<TSource, TTarget> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, bool @case, Func<TSource, Task<TTarget>> then) : this(source)
    {
        When(@case, then);
    }

    public TSource Source { get; init; }

    protected List<CaseItem> Cases { get; init; } = new();
    protected bool ElseCaseAdded { get; set; }

    public static implicit operator TTarget(WhenCase<TSource, TTarget> caseCase)
    {
        return caseCase.Execute();
    }

    public static Func<TSource, bool> WhenValueCaseBuilder(TSource @case)
    {
        return source => (source == null && @case == null) || source?.Equals(@case) == true;
    }

    public static WhenCase<TSource, TTarget> WhenValue(
        TSource source,
        TSource @case,
        Func<TSource, TTarget> then)
    {
        return new WhenCase<TSource, TTarget>(source).WhenValue(@case, then);
    }

    public static WhenCase<TSource, TTarget> WhenValue(
        TSource source,
        TSource @case,
        Func<TSource, Task<TTarget>> then)
    {
        return new WhenCase<TSource, TTarget>(source).WhenValue(@case, then);
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public static WhenCase<TSource, TTarget> WhenIs<TSourceIs>(
        TSource source,
        Func<TSourceIs, TTarget> then) where TSourceIs : class
    {
        return new WhenCase<TSource, TTarget>(source).WhenIs(then);
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public static WhenCase<TSource, TTarget> WhenIs<TSourceIs>(
        TSource source,
        Func<TSourceIs, Task<TTarget>> then) where TSourceIs : class
    {
        return new WhenCase<TSource, TTarget>(source).WhenIs(then);
    }

    public WhenCase<TSource, TTarget> WhenValue(TSource @case, Func<TSource, TTarget> then)
    {
        return WhenValue(@case, _ => Task.FromResult(then(_)));
    }

    public WhenCase<TSource, TTarget> WhenValue(TSource @case, Func<TSource, Task<TTarget>> then)
    {
        Cases.Add(new CaseItem(WhenValueCaseBuilder(@case), then));
        return this;
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public WhenCase<TSource, TTarget> WhenIs<TSourceIs>(Func<TSourceIs, TTarget> then)
        where TSourceIs : class
    {
        return WhenIs<TSourceIs>(_ => Task.FromResult(then(_)));
    }

    /// <summary>
    /// Ex: WhenIs[SomeTypeToCheckThatTheTargetObjTypeIsThisType]
    /// </summary>
    public WhenCase<TSource, TTarget> WhenIs<TSourceIs>(Func<TSourceIs, Task<TTarget>> then)
        where TSourceIs : class
    {
        Cases.Add(new CaseItem(source => source is TSourceIs, _ => then(_ as TSourceIs)));
        return this;
    }

    public WhenCase<TSource, TTarget> Else(Func<TSource, TTarget> then)
    {
        return Else(new CaseItem(true, then));
    }

    public WhenCase<TSource, TTarget> Else(Func<TSource, Task<TTarget>> then)
    {
        return Else(new CaseItem(true, then));
    }

    public WhenCase<TSource, TTarget> Else(CaseItem elseCaseItem)
    {
        EnsureCanAddElseCase();

        Cases.Add(elseCaseItem);
        ElseCaseAdded = true;
        return this;
    }

    public bool HasMatchedCase()
    {
        return Cases.Any(@case => @case.IsMatch(Source).Result);
    }

    public async Task<TTarget> ExecuteAsync()
    {
        foreach (var @case in Cases)
            if (await @case.IsMatch(Source))
                return await @case.CaseAction(Source);

        return default;
    }

    public TTarget Execute()
    {
        return ExecuteAsync().Result;
    }

    protected WhenCase<TSource, TTarget> EnsureCanAddElseCase()
    {
        if (ElseCaseAdded) throw new Exception("Else case has been added");

        return this;
    }

    public class CaseItem
    {
        public CaseItem(Func<TSource, bool> @case, Func<TSource, Task<TTarget>> then)
        {
            Match = @case;
            CaseAction = then;
        }

        public CaseItem(bool @case, Func<TSource, Task<TTarget>> then)
        {
            Match = p => @case;
            CaseAction = then;
        }

        public CaseItem(Func<TSource, bool> @case, Func<TSource, TTarget> then) : this(@case, _ => Task.FromResult(then(_)))
        {
        }

        public CaseItem(bool @case, Func<TSource, TTarget> then) : this(@case, _ => Task.FromResult(then(_)))
        {
        }

        public CaseItem(Func<TSource, Task<bool>> @case, Func<TSource, Task<TTarget>> then)
        {
            MatchAsync = @case;
            CaseAction = then;
        }

        public CaseItem(Task<bool> @case, Func<TSource, Task<TTarget>> then)
        {
            MatchAsync = p => @case;
            CaseAction = then;
        }

        public CaseItem(Func<TSource, Task<bool>> @case, Func<TSource, TTarget> then) : this(@case, _ => Task.FromResult(then(_)))
        {
        }

        public CaseItem(Task<bool> @case, Func<TSource, TTarget> then) : this(@case, _ => Task.FromResult(then(_)))
        {
        }

        public Func<TSource, bool> Match { get; init; }
        public Func<TSource, Task<bool>> MatchAsync { get; init; }
        public Func<TSource, Task<TTarget>> CaseAction { get; init; }

        public static implicit operator CaseItem(ValueTuple<Func<TSource, bool>, Func<TSource, Task<TTarget>>> caseItemInfo)
        {
            return new CaseItem(caseItemInfo.Item1, caseItemInfo.Item2);
        }

        public static implicit operator CaseItem(ValueTuple<bool, Func<TSource, Task<TTarget>>> caseItemInfo)
        {
            return new CaseItem(caseItemInfo.Item1, caseItemInfo.Item2);
        }

        public static implicit operator CaseItem(ValueTuple<Func<TSource, bool>, Func<TSource, TTarget>> caseItemInfo)
        {
            return new CaseItem(caseItemInfo.Item1, _ => Task.FromResult(caseItemInfo.Item2(_)));
        }

        public static implicit operator CaseItem(ValueTuple<bool, Func<TSource, TTarget>> caseItemInfo)
        {
            return new CaseItem(caseItemInfo.Item1, _ => Task.FromResult(caseItemInfo.Item2(_)));
        }

        public async Task<bool> IsMatch(TSource source)
        {
            return MatchAsync != null
                ? await MatchAsync(source)
                : Match(source);
        }
    }

    #region Async Case Constructor

    public WhenCase(TSource source, Func<TSource, Task<bool>> @case, Func<TSource, TTarget> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, Func<Task<bool>> @case, Func<TSource, TTarget> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, Func<TSource, Task<bool>> @case, Func<TSource, Task<TTarget>> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, Task<bool> @case, Func<TSource, TTarget> then) : this(source)
    {
        When(@case, then);
    }

    public WhenCase(TSource source, Task<bool> @case, Func<TSource, Task<TTarget>> then) : this(source)
    {
        When(@case, then);
    }

    #endregion

    #region When Sync Case

    public WhenCase<TSource, TTarget> When(CaseItem caseItem)
    {
        Cases.Add(caseItem);
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<TSource, bool> @case, Func<TSource, TTarget> then)
    {
        Cases.Add(new CaseItem(@case, then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<bool> @case, Func<TSource, TTarget> then)
    {
        Cases.Add(new CaseItem(_ => @case(), then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(bool @case, Func<TSource, TTarget> then)
    {
        Cases.Add(new CaseItem(_ => @case, then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<TSource, bool> @case, Func<TSource, Task<TTarget>> then)
    {
        Cases.Add(new CaseItem(@case, then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<bool> @case, Func<TSource, Task<TTarget>> then)
    {
        return When(_ => @case(), then);
    }

    public WhenCase<TSource, TTarget> When(bool @case, Func<TSource, Task<TTarget>> then)
    {
        return When(_ => @case, then);
    }

    #endregion

    #region When Async Case

    public WhenCase<TSource, TTarget> When(Func<TSource, Task<bool>> @case, Func<TSource, TTarget> then)
    {
        Cases.Add(new CaseItem(@case, then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<Task<bool>> @case, Func<TSource, TTarget> then)
    {
        Cases.Add(new CaseItem(_ => @case(), then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Task<bool> @case, Func<TSource, TTarget> then)
    {
        Cases.Add(new CaseItem(_ => @case, then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<TSource, Task<bool>> @case, Func<TSource, Task<TTarget>> then)
    {
        Cases.Add(new CaseItem(@case, then));
        return this;
    }

    public WhenCase<TSource, TTarget> When(Func<Task<bool>> @case, Func<TSource, Task<TTarget>> then)
    {
        return When(_ => @case(), then);
    }

    public WhenCase<TSource, TTarget> When(Task<bool> @case, Func<TSource, Task<TTarget>> then)
    {
        return When(_ => @case, then);
    }

    #endregion
}

public class WhenCase<TSource> : WhenCase<TSource, ValueTuple>
{
    public WhenCase(TSource source, params CaseItem[] cases) : base(source, cases)
    {
    }

    public WhenCase(TSource source, Func<TSource, bool> @case, Action<TSource> then) : base(source, @case, then.ToFunc())
    {
    }

    public WhenCase(TSource source, Func<TSource, bool> @case, Func<TSource, ValueTuple> then) : base(source, @case, then)
    {
    }

    public WhenCase(TSource source, bool @case, Action<TSource> then) : base(source, @case, then.ToFunc())
    {
    }

    public WhenCase(TSource source, bool @case, Action then) : base(source, @case, _ => then.ToFunc()())
    {
    }

    public WhenCase(TSource source, bool @case, Func<TSource, ValueTuple> then) : base(source, @case, then)
    {
    }

    public static WhenCase<TSource> WhenValue(
        TSource source,
        TSource @case,
        Action<TSource> then)
    {
        return new WhenCase<TSource>(source).WhenValue(@case, then);
    }

    public static WhenCase<TSource> WhenValue(
        TSource source,
        TSource @case,
        Action then)
    {
        return new WhenCase<TSource>(source).WhenValue(@case, then);
    }

    public static WhenCase<TSource> WhenValue(
        TSource source,
        TSource @case,
        Func<TSource, Task> then)
    {
        return new WhenCase<TSource>(source).WhenValue(@case, then);
    }

    public static WhenCase<TSource> WhenValue(
        TSource source,
        TSource @case,
        Func<Task> then)
    {
        return new WhenCase<TSource>(source).WhenValue(@case, then);
    }

    public static WhenCase<TSource> WhenIs<TSourceIs>(
        TSource source,
        Action<TSourceIs> then) where TSourceIs : class
    {
        return new WhenCase<TSource>(source).WhenIs(then);
    }

    public static WhenCase<TSource> WhenIs<TSourceIs>(
        TSource source,
        Func<TSourceIs, Task> then) where TSourceIs : class
    {
        return new WhenCase<TSource>(source).WhenIs(then);
    }

    public new WhenCase<TSource> When(CaseItem caseItem)
    {
        return (WhenCase<TSource>)base.When(caseItem);
    }

    public WhenCase<TSource> When(Func<TSource, bool> @case, Action<TSource> then)
    {
        return (WhenCase<TSource>)When(@case, then.ToFunc());
    }

    public WhenCase<TSource> When(Func<bool> @case, Action<TSource> then)
    {
        return (WhenCase<TSource>)When(_ => @case(), then.ToFunc());
    }

    public WhenCase<TSource> When(bool @case, Action<TSource> then)
    {
        return (WhenCase<TSource>)When(_ => @case, then.ToFunc());
    }

    public WhenCase<TSource> When(Func<TSource, bool> @case, Action then)
    {
        return When(@case, _ => then.ToFunc()());
    }

    public WhenCase<TSource> When(Func<TSource, bool> @case, Func<Task> then)
    {
        return When(@case, _ => then.ToAsyncFunc()().GetResult());
    }

    public WhenCase<TSource> When(Func<bool> @case, Func<Task> then)
    {
        return When(@case, _ => then.ToAsyncFunc()().GetResult());
    }

    public WhenCase<TSource> When(bool @case, Func<Task> then)
    {
        return When(@case, _ => then.ToAsyncFunc()().GetResult());
    }

    public WhenCase<TSource> WhenValue(TSource @case, Func<TSource, Task> then)
    {
        return (WhenCase<TSource>)base.WhenValue(@case, then.ToAsyncFunc());
    }

    public WhenCase<TSource> WhenValue(TSource @case, Func<Task> then)
    {
        return WhenValue(@case, _ => then.ToAsyncFunc()().GetResult());
    }

    public WhenCase<TSource> WhenValue(TSource @case, Action then)
    {
        return (WhenCase<TSource>)base.WhenValue(@case, _ => then.ToFunc()());
    }

    public WhenCase<TSource> WhenValue(TSource @case, Action<TSource> then)
    {
        return (WhenCase<TSource>)base.WhenValue(@case, source => then.ToFunc()(source));
    }

    public WhenCase<TSource> WhenIs<TSourceIs>(Func<Task> then) where TSourceIs : class
    {
        return WhenIs<TSourceIs>(_ => then.ToAsyncFunc()().GetResult());
    }

    public WhenCase<TSource> WhenIs<TSourceIs>(Action<TSourceIs> then) where TSourceIs : class
    {
        return (WhenCase<TSource>)WhenIs(then.ToFunc());
    }

    public WhenCase<TSource> WhenIs<TSourceIs>(Func<TSourceIs, Task> then)
        where TSourceIs : class
    {
        Cases.Add(new CaseItem(source => source is TSourceIs, _ => then(_ as TSourceIs).Then(() => ValueTuple.Create())));
        return this;
    }

    public WhenCase<TSource> Else(Action then)
    {
        return Else(_ => then.ToFunc()());
    }

    public WhenCase<TSource> Else(Action<TSource> then)
    {
        return Else(() => then.ToFunc()(Source));
    }

    public WhenCase<TSource> Else(Func<Task> then)
    {
        return Else(_ => then.ToAsyncFunc()());
    }

    #region Async Case Constructor

    public WhenCase(TSource source, Func<TSource, Task<bool>> @case, Action<TSource> then) : base(source, @case, then.ToFunc())
    {
    }

    public WhenCase(TSource source, Func<Task<bool>> @case, Action<TSource> then) : base(source, @case, then.ToFunc())
    {
    }

    public WhenCase(TSource source, Func<TSource, Task<bool>> @case, Action then) : base(source, @case, _ => then.ToFunc()())
    {
    }

    public WhenCase(TSource source, Func<TSource, Task<bool>> @case, Func<TSource, Task> then) : base(source, @case, then.ToAsyncFunc())
    {
    }

    public WhenCase(TSource source, Task<bool> @case, Action<TSource> then) : base(source, @case, then.ToFunc())
    {
    }

    public WhenCase(TSource source, Task<bool> @case, Action then) : base(source, @case, _ => then.ToFunc()())
    {
    }

    public WhenCase(TSource source, Task<bool> @case, Func<TSource, Task> then) : base(source, @case, then.ToAsyncFunc())
    {
    }

    #endregion
}
