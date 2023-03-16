namespace Easy.Platform.Common.Extensions.WhenCases;

public static class AsyncWhenCaseExtension
{
    public static Task<WhenCase<TSource, TTarget>> When<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        params WhenCase<TSource, TTarget>.CaseItem[] cases)
    {
        return sourceWhenCaseTask.Then(
            sourceWhenCase => cases.Aggregate(
                sourceWhenCase,
                (currentWhenCase, nextCase) => currentWhenCase.When(nextCase)));
    }

    public static Task<WhenCase<TSource, TTarget>> When<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<TSource, bool> @case,
        Func<TSource, TTarget> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.When(@case, then));
    }

    public static Task<WhenCase<TSource, TTarget>> When<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<bool> @case,
        Func<TSource, TTarget> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.When(@case, then));
    }

    public static Task<WhenCase<TSource, TTarget>> When<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<TSource, bool> @case,
        Func<TSource, Task<TTarget>> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.When(@case, then));
    }

    public static async Task<WhenCase<TSource, TTarget>> When<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<bool> @case,
        Func<TSource, Task<TTarget>> then)
    {
        var sourceWhenCase = await sourceWhenCaseTask;
        return sourceWhenCase.When(@case, then);
    }

    public static Task<WhenCase<TSource>> When<TSource>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        params WhenCase<TSource, object>.CaseItem[] cases)
    {
        return sourceWhenCaseTask.Then(
            sourceWhenCase => cases.Aggregate(
                sourceWhenCase,
                (currentWhenCase, nextCase) => currentWhenCase.When(nextCase)));
    }

    public static Task<WhenCase<TSource>> When<TSource>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        Func<TSource, bool> @case,
        Action<TSource> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.When(@case, then));
    }

    public static Task<WhenCase<TSource>> When<TSource>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        Func<TSource, bool> @case,
        Action then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.When(@case, then));
    }

    public static Task<WhenCase<TSource>> When<TSource>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        Func<bool> @case,
        Action<TSource> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.When(@case, then));
    }

    public static Task<WhenCase<TSource, TTarget>> WhenIs<TSource, TTarget, TSourceIs>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<TSourceIs, TTarget> then) where TSourceIs : class
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.WhenIs(then));
    }

    public static Task<WhenCase<TSource>> WhenIs<TSource, TSourceIs>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        Action<TSourceIs> then) where TSourceIs : class
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.WhenIs(then));
    }

    public static Task<WhenCase<TSource, TTarget>> WhenValue<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        TSource @case,
        Func<TSource, TTarget> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.WhenValue(@case, then));
    }

    public static Task<WhenCase<TSource>> WhenValue<TSource>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        TSource @case,
        Action<TSource> then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.WhenValue(@case, then));
    }

    public static Task<WhenCase<TSource>> WhenValue<TSource>(
        this Task<WhenCase<TSource>> sourceWhenCaseTask,
        TSource @case,
        Action then)
    {
        return sourceWhenCaseTask.Then(sourceWhenCase => sourceWhenCase.WhenValue(@case, then));
    }

    public static Task<WhenCase<TSource, TTarget>> Else<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<TSource, TTarget> then)
    {
        return sourceWhenCaseTask.Then(_ => _.Else(then));
    }

    public static Task<WhenCase<TSource>> Else<TSource>(this Task<WhenCase<TSource>> sourceWhenCaseTask, Action then)
    {
        return sourceWhenCaseTask.Then(_ => _.Else(then));
    }

    public static Task<WhenCase<TSource, TTarget>> Else<TSource, TTarget>(
        this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask,
        Func<TSource, Task<TTarget>> then)
    {
        return sourceWhenCaseTask.Then(_ => _.Else(then));
    }

    public static Task<WhenCase<TSource>> Else<TSource>(this Task<WhenCase<TSource>> sourceWhenCaseTask, Func<Task> then)
    {
        return sourceWhenCaseTask.Then(_ => _.Else(then));
    }

    public static Task<TTarget> Execute<TSource, TTarget>(this Task<WhenCase<TSource, TTarget>> sourceWhenCaseTask)
    {
        return sourceWhenCaseTask.Then(_ => _.ExecuteAsync());
    }
}
