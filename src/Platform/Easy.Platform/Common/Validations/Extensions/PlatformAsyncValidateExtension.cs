using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Validations.Extensions;

public static class PlatformAsyncValidateExtension
{
    public static Task<PlatformValidationResult<TValue>> ValidateAsync<TValue>(
        this TValue value,
        Func<TValue, Task<bool>> must,
        params PlatformValidationError[] errorMsgs)
    {
        return must(value).Then(mustValue => PlatformValidationResult<TValue>.Validate(value, mustValue, errorMsgs));
    }

    public static Task<PlatformValidationResult<TValue>> ValidateAsync<TValue>(
        this TValue value,
        Func<TValue, Task<bool>> must,
        PlatformValidationError errorMsg)
    {
        return must(value).Then(mustValue => PlatformValidationResult<TValue>.Validate(value, mustValue, errorMsg));
    }

    public static Task<PlatformValidationResult<TValue>> ValidateAsync<TValue>(
        this TValue value,
        Func<TValue, Task<bool>> must,
        Func<TValue, PlatformValidationError> errorMsg)
    {
        return must(value).Then(mustValue => PlatformValidationResult<TValue>.Validate(value, mustValue, errorMsg(value)));
    }

    public static Task<PlatformValidationResult<TValue>> ValidateAsync<TValue>(
        this TValue value,
        Func<Task<bool>> must,
        params PlatformValidationError[] errorMsgs)
    {
        return must().Then(mustValue => PlatformValidationResult<TValue>.Validate(value, mustValue, errorMsgs));
    }

    public static Task<PlatformValidationResult<TValue>> ValidateAsync<TValue>(
        this TValue value,
        Task<bool> must,
        params PlatformValidationError[] errorMsgs)
    {
        return must.Then(mustValue => PlatformValidationResult<TValue>.Validate(value, mustValue, errorMsgs));
    }

    /// <summary>
    /// Then validate and map to the next validation[T]. <br />
    /// Validation[T] => ThenValidateAsync Validation[T1] => Validation[T1]
    /// </summary>
    public static Task<PlatformValidationResult<TResult>> ThenValidateAsync<T, TResult>(
        this Task<PlatformValidationResult<T>> sourceValidationTask,
        Func<T, PlatformValidationResult<TResult>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.ThenValidate(nextValidation));
    }

    /// <summary>
    /// Then validate and map to the next validation[T]. <br />
    /// Validation[T] => ThenValidateAsync Validation[T1] => Validation[T1]
    /// </summary>
    public static Task<PlatformValidationResult<TResult>> ThenValidateAsync<T, TResult>(
        this Task<PlatformValidationResult<T>> sourceValidationTask,
        Func<T, Task<PlatformValidationResult<TResult>>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.ThenValidateAsync(nextValidation));
    }

    public static Task<PlatformValidationResult> AndAsync(
        this Task<PlatformValidationResult> sourceValidationTask,
        Func<PlatformValidationResult> nextValidation)
    {
        return sourceValidationTask.Then(p => p.And(nextValidation));
    }

    public static Task<PlatformValidationResult> AndAsync(
        this Task<PlatformValidationResult> sourceValidation,
        Func<Task<PlatformValidationResult>> nextValidation)
    {
        return sourceValidation.Then(p => p.AndAsync(nextValidation));
    }

    public static Task<PlatformValidationResult<TValue>> AndAsync<TValue>(
        this Task<PlatformValidationResult<TValue>> sourceValidationTask,
        Func<TValue, PlatformValidationResult<TValue>> nextValidation)
    {
        return sourceValidationTask.Then(p => p.And(nextValidation));
    }

    public static Task<PlatformValidationResult<TValue>> AndAsync<TValue>(
        this Task<PlatformValidationResult<TValue>> sourceValidation,
        Func<TValue, Task<PlatformValidationResult<TValue>>> nextValidation)
    {
        return sourceValidation.Then(p => p.AndAsync(nextValidation));
    }

    public static Task<T> EnsureValidAsync<T>(
        this Task<PlatformValidationResult<T>> sourceValidationTask)
    {
        return sourceValidationTask.Then(p => p.EnsureValid());
    }


    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        bool must,
        params PlatformValidationError[] errors)
    {
        return valueTask.Then(value => value.Validate(must, errors));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        Func<bool> must,
        params PlatformValidationError[] errors)
    {
        return valueTask.Then(value => value.Validate(must, errors));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> must,
        params PlatformValidationError[] errors)
    {
        return valueTask.Then(value => value.Validate(must, errors));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> must,
        Func<TValue, PlatformValidationError> errorMsg)
    {
        return valueTask.Then(value => value.Validate(must, errorMsg));
    }

    public static Task<PlatformValidationResult<List<T>>> ThenValidateFoundAllAsync<T>(
        this Task<List<T>> objectsTask,
        List<T> mustFoundAllItems,
        Func<List<T>, string> notFoundObjectsToErrorMsg)
    {
        return objectsTask.Then(p => p.ValidateFoundAll(mustFoundAllItems, notFoundObjectsToErrorMsg));
    }

    public static Task<PlatformValidationResult<List<T>>> ThenValidateFoundAllByAsync<T, TFoundBy>(
        this Task<List<T>> objectsTask,
        Func<T, TFoundBy> foundBy,
        List<TFoundBy> toFoundByObjects,
        Func<List<TFoundBy>, string> notFoundByObjectsToErrorMsg)
    {
        return objectsTask.Then(p => p.ValidateFoundAllBy(foundBy, toFoundByObjects, notFoundByObjectsToErrorMsg));
    }

    public static Task<PlatformValidationResult<T>> ThenValidateFoundAsync<T>(this Task<T?> objTask, string errorMsg = null)
    {
        return objTask.Then(p => p.ValidateFound(errorMsg));
    }

    public static Task<PlatformValidationResult<IEnumerable<T>>> ThenValidateFoundAsync<T>(this Task<IEnumerable<T>> objectsTask, string errorMsg = null)
    {
        return objectsTask.Then(p => p.ValidateFound(errorMsg));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, () => mustNot(value), errorMsgs));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        Func<TValue, bool> mustNot,
        Func<TValue, PlatformValidationError> errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, () => mustNot(value), errorMsgs(value)));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        Func<bool> mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, mustNot, errorMsgs));
    }

    public static Task<PlatformValidationResult<TValue>> ThenValidateNotAsync<TValue>(
        this Task<TValue> valueTask,
        bool mustNot,
        params PlatformValidationError[] errorMsgs)
    {
        return valueTask.Then(value => PlatformValidationResult<TValue>.ValidateNot(value, mustNot, errorMsgs));
    }

    public static Task<PlatformValidationResult<TR>> WaitValidThen<T, TR>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR> next)
    {
        return task.Then(val => val.Then(next));
    }

    public static Task<PlatformValidationResult<TR>> WaitValidThen<T, TR>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR>> nextTask)
    {
        return task.Then(val => val.ThenAsync(nextTask));
    }


    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2>>> task,
        Func<T1, T2, TR> next)
    {
        var taskResultVal = await task;
        return taskResultVal.Then(taskResult => next(taskResult.Item1, taskResult.Item2));
    }

    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, T3, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3>>> task,
        Func<T1, T2, T3, TR> next)
    {
        var taskResultVal = await task;
        return taskResultVal.Then(taskResult => next(taskResult.Item1, taskResult.Item2, taskResult.Item3));
    }

    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, T3, T4, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3, T4>>> task,
        Func<T1, T2, T3, T4, TR> next)
    {
        var taskResultVal = await task;
        return taskResultVal.Then(taskResult => next(taskResult.Item1, taskResult.Item2, taskResult.Item3, taskResult.Item4));
    }

    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, T3, T4, T5, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3, T4, T5>>> task,
        Func<T1, T2, T3, T4, T5, TR> next)
    {
        var taskResultVal = await task;
        return taskResultVal.Then(taskResult => next(taskResult.Item1, taskResult.Item2, taskResult.Item3, taskResult.Item4, taskResult.Item5));
    }


    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2>>> task,
        Func<T1, T2, Task<TR>> nextTask)
    {
        var taskResultVal = await task;
        return await taskResultVal.ThenAsync(taskResult => nextTask(taskResult.Item1, taskResult.Item2));
    }

    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, T3, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3>>> task,
        Func<T1, T2, T3, Task<TR>> nextTask)
    {
        var taskResultVal = await task;
        return await taskResultVal.ThenAsync(taskResult => nextTask(taskResult.Item1, taskResult.Item2, taskResult.Item3));
    }

    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, T3, T4, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3, T4>>> task,
        Func<T1, T2, T3, T4, Task<TR>> nextTask)
    {
        var taskResultVal = await task;
        return await taskResultVal.ThenAsync(taskResult => nextTask(taskResult.Item1, taskResult.Item2, taskResult.Item3, taskResult.Item4));
    }

    public static async Task<PlatformValidationResult<TR>> WaitValidThen<T1, T2, T3, T4, T5, TR>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3, T4, T5>>> task,
        Func<T1, T2, T3, T4, T5, Task<TR>> nextTask)
    {
        var taskResultVal = await task;
        return await taskResultVal.ThenAsync(taskResult => nextTask(taskResult.Item1, taskResult.Item2, taskResult.Item3, taskResult.Item4, taskResult.Item5));
    }


    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2>>> WaitValidThenWithAll<T, TR1, TR2>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2)
    {
        var tResultVal = await task;
        return tResultVal.Then<ValueTuple<T, TR1, TR2>>(tResult => (tResult, fr1(tResult), fr2(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2, TR3>>> WaitValidThenWithAll<T, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3)
    {
        var tResultVal = await task;
        return tResultVal.Then<ValueTuple<T, TR1, TR2, TR3>>(tResult => (tResult, fr1(tResult), fr2(tResult), fr3(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2, TR3, TR4>>> WaitValidThenWithAll<T, TR1, TR2, TR3, TR4>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3,
        Func<T, TR4> fr4)
    {
        var tResultVal = await task;
        return tResultVal.Then<ValueTuple<T, TR1, TR2, TR3, TR4>>(tResult => (tResult, fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2, TR3, TR4, TR5>>> WaitValidThenWithAll<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3,
        Func<T, TR4> fr4,
        Func<T, TR5> fr5)
    {
        var tResultVal = await task;
        return tResultVal.Then<ValueTuple<T, TR1, TR2, TR3, TR4, TR5>>(tResult => (tResult, fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult)));
    }


    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2>>> WaitValidThenWithAllAsync<T, TR1, TR2>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2, TR3>>> WaitValidThenWithAllAsync<T, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult), fr3(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2, TR3, TR4>>> WaitValidThenWithAllAsync<T, TR1, TR2, TR3, TR4>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<T, TR1, TR2, TR3, TR4, TR5>>> WaitValidThenWithAllAsync<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4,
        Func<T, Task<TR5>> fr5)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(tResult.ToTask(), fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult)));
    }


    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2>>> WaitValidThenGetAll<T, TR1, TR2>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2)
    {
        var tResultVal = await task;
        return tResultVal.Then(tResult => (fr1(tResult), fr2(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3>>> WaitValidThenGetAll<T, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3)
    {
        var tResultVal = await task;
        return tResultVal.Then(tResult => (fr1(tResult), fr2(tResult), fr3(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3, TR4>>> WaitValidThenGetAll<T, TR1, TR2, TR3, TR4>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3,
        Func<T, TR4> fr4)
    {
        var tResultVal = await task;
        return tResultVal.Then(tResult => (fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3, TR4, TR5>>> WaitValidThenGetAll<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, TR1> fr1,
        Func<T, TR2> fr2,
        Func<T, TR3> fr3,
        Func<T, TR4> fr4,
        Func<T, TR5> fr5)
    {
        var tResultVal = await task;
        return tResultVal.Then(tResult => (fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult)));
    }


    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2>>> WaitValidThenGetAllAsync<T, TR1, TR2>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3>>> WaitValidThenGetAllAsync<T, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult), fr3(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3, TR4>>> WaitValidThenGetAllAsync<T, TR1, TR2, TR3, TR4>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3, TR4, TR5>>> WaitValidThenGetAllAsync<T, TR1, TR2, TR3, TR4, TR5>(
        this Task<PlatformValidationResult<T>> task,
        Func<T, Task<TR1>> fr1,
        Func<T, Task<TR2>> fr2,
        Func<T, Task<TR3>> fr3,
        Func<T, Task<TR4>> fr4,
        Func<T, Task<TR5>> fr5)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(fr1(tResult), fr2(tResult), fr3(tResult), fr4(tResult), fr5(tResult)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2>>> WaitValidThenGetAllAsync<T1, T2, TR1, TR2>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2>>> task,
        Func<T1, T2, Task<TR1>> fr1,
        Func<T1, T2, Task<TR2>> fr2)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(tResult => Util.TaskRunner.WhenAll(fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3>>> WaitValidThenGetAllAsync<T1, T2, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2>>> task,
        Func<T1, T2, Task<TR1>> fr1,
        Func<T1, T2, Task<TR2>> fr2,
        Func<T1, T2, Task<TR3>> fr3)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(
            tResult => Util.TaskRunner.WhenAll(
                fr1(tResult.Item1, tResult.Item2),
                fr2(tResult.Item1, tResult.Item2),
                fr3(tResult.Item1, tResult.Item2)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2>>> WaitValidThenGetAllAsync<T1, T2, T3, TR1, TR2>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3>>> task,
        Func<T1, T2, T3, Task<TR1>> fr1,
        Func<T1, T2, T3, Task<TR2>> fr2)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(
            tResult => Util.TaskRunner.WhenAll(fr1(tResult.Item1, tResult.Item2, tResult.Item3), fr2(tResult.Item1, tResult.Item2, tResult.Item3)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3>>> WaitValidThenGetAllAsync<T1, T2, T3, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3>>> task,
        Func<T1, T2, T3, Task<TR1>> fr1,
        Func<T1, T2, T3, Task<TR2>> fr2,
        Func<T1, T2, T3, Task<TR3>> fr3)
    {
        var tResultVal = await task;
        return await tResultVal.ThenAsync(
            tResult => Util.TaskRunner.WhenAll(
                fr1(tResult.Item1, tResult.Item2, tResult.Item3),
                fr2(tResult.Item1, tResult.Item2, tResult.Item3),
                fr3(tResult.Item1, tResult.Item2, tResult.Item3)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2>>> WaitValidThenGetAll<T1, T2, TR1, TR2>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2>>> task,
        Func<T1, T2, TR1> fr1,
        Func<T1, T2, TR2> fr2)
    {
        var tResultVal = await task;
        return tResultVal.Then(tResult => (fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3>>> WaitValidThenGetAll<T1, T2, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2>>> task,
        Func<T1, T2, TR1> fr1,
        Func<T1, T2, TR2> fr2,
        Func<T1, T2, TR3> fr3)
    {
        var tResultVal = await task;
        return tResultVal.Then(
            tResult => (fr1(tResult.Item1, tResult.Item2), fr2(tResult.Item1, tResult.Item2), fr3(tResult.Item1, tResult.Item2)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2>>> WaitValidThenGetAll<T1, T2, T3, TR1, TR2>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3>>> task,
        Func<T1, T2, T3, TR1> fr1,
        Func<T1, T2, T3, TR2> fr2)
    {
        var tResultVal = await task;
        return tResultVal.Then(tResult => (fr1(tResult.Item1, tResult.Item2, tResult.Item3), fr2(tResult.Item1, tResult.Item2, tResult.Item3)));
    }

    public static async Task<PlatformValidationResult<ValueTuple<TR1, TR2, TR3>>> WaitValidThenGetAll<T1, T2, T3, TR1, TR2, TR3>(
        this Task<PlatformValidationResult<ValueTuple<T1, T2, T3>>> task,
        Func<T1, T2, T3, TR1> fr1,
        Func<T1, T2, T3, TR2> fr2,
        Func<T1, T2, T3, TR3> fr3)
    {
        var tResultVal = await task;
        return tResultVal.Then(
            tResult =>
                (fr1(tResult.Item1, tResult.Item2, tResult.Item3),
                    fr2(tResult.Item1, tResult.Item2, tResult.Item3),
                    fr3(tResult.Item1, tResult.Item2, tResult.Item3)));
    }
}
