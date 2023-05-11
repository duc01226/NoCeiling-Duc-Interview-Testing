namespace Easy.Platform.Common.Extensions;

public static class WithExtension
{
    /// <summary>
    /// Pipe the current target object through all the actions
    /// and return the updated current target object
    /// </summary>
    public static T With<T>(this T target, params Action<T>[] actions)
    {
        actions.ForEach(action => action(target));

        return target;
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static async Task<T> With<T>(this T target, params Func<T, Task>[] actions)
    {
        await actions.ForEachAsync(action => action(target));

        return target;
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static async Task<T> With<T>(this T target, params Func<T, Task<T>>[] actions)
    {
        await actions.ForEachAsync(action => action(target));

        return target;
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static T With<T>(this T target, params Func<T, T>[] actions)
    {
        actions.ForEach(action => action(target));

        return target;
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static Task<T> With<T>(this Task<T> targetTask, params Action<T>[] actions)
    {
        return targetTask.Then(target => target.With(actions));
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static Task<T> With<T>(this Task<T> targetTask, params Func<T, Task>[] actions)
    {
        return targetTask.Then(target => target.With(actions));
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static Task<T> With<T>(this Task<T> targetTask, params Func<T, T>[] actions)
    {
        return targetTask.Then(target => target.With(actions));
    }

    /// <inheritdoc cref="With{T}(T,System.Action{T}[])" />
    public static Task<T> With<T>(this Task<T> targetTask, params Func<T, Task<T>>[] actions)
    {
        return targetTask.Then(target => target.With(actions));
    }

    #region WithIf

    public static Task<T> WithIf<T>(this Task<T> targetTask, bool when, params Action<T>[] actions)
    {
        return targetTask.Then(target => target.WithIf(when, actions));
    }

    public static Task<T> WithIf<T>(this Task<T> targetTask, Func<T, bool> when, params Action<T>[] actions)
    {
        return targetTask.Then(target => target.WithIf(when, actions));
    }

    public static T WithIf<T>(this T target, bool when, params Action<T>[] actions)
    {
        if (when)
            actions.ForEach(action => action.ToFunc()(target).Pipe(_ => target));
        return target;
    }

    public static T WithIf<T>(this T target, Func<T, bool> @if, params Action<T>[] actions)
    {
        if (@if(target))
            actions.ForEach(action => action.ToFunc()(target).Pipe(_ => target));
        return target;
    }

    public static async Task<T> WithIf<T>(this Task<T> targetTask, bool when, params Func<T, Task>[] actions)
    {
        var target = await targetTask;

        if (when)
            await actions.ForEachAsync(action => action(target));

        return target;
    }

    #endregion
}
