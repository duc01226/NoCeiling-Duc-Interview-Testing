namespace Easy.Platform.Common.Extensions;

public static class WithExtension
{
    /// <summary>
    /// Applies a sequence of actions to the target object and returns the updated object.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object to which the actions are applied.</param>
    /// <param name="actions">The actions to apply to the target object.</param>
    /// <returns>The updated target object after applying all actions.</returns>
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

    /// <summary>
    /// Executes the provided actions on the target object if the specified condition is met.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object.</param>
    /// <param name="when">A boolean value representing the condition to be met.</param>
    /// <param name="actions">The actions to be executed on the target object if the condition is met.</param>
    /// <returns>The target object after potentially applying the actions.</returns>
    /// <remarks>
    /// This method is useful for applying changes to an object conditionally in a fluent manner.
    /// </remarks>
    public static T WithIf<T>(this T target, bool when, params Action<T>[] actions)
    {
        if (when)
            actions.ForEach(action => action.ToFunc()(target).Pipe(_ => target));
        return target;
    }

    /// <summary>
    /// Applies a sequence of asynchronous actions to the target object if the specified condition is true, and returns the updated object.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object to which the actions are applied.</param>
    /// <param name="when">The condition that determines whether the actions should be applied.</param>
    /// <param name="actions">The asynchronous actions to apply to the target object.</param>
    /// <returns>The updated target object after applying all actions, or the original object if the condition is false.</returns>
    public static async Task<T> WithIf<T>(this T target, bool when, params Func<T, Task<T>>[] actions)
    {
        if (when)
            await actions.ForEachAsync(action => action(target));
        return target;
    }

    /// <summary>
    /// Applies a sequence of actions to the target object if the specified asynchronous condition is met, and returns the updated object.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object to which the actions are applied.</param>
    /// <param name="when">The asynchronous condition that determines whether the actions should be applied.</param>
    /// <param name="actions">The asynchronous actions to apply to the target object.</param>
    /// <returns>The updated target object after applying all actions, or the original object if the condition is not met.</returns>
    /// <remarks>
    /// This method is useful for applying changes to an object conditionally in a fluent manner. The condition is evaluated asynchronously.
    /// </remarks>
    public static async Task<T> WithIf<T>(this T target, Func<T, Task<bool>> when, params Func<T, Task<T>>[] actions)
    {
        if (await when(target))
            await actions.ForEachAsync(action => action(target));
        return target;
    }

    /// <summary>
    /// Executes the provided actions on the target object if the specified condition is met.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object.</param>
    /// <param name="if">A function that defines the condition based on the target object.</param>
    /// <param name="actions">The actions to be executed on the target object if the condition is met.</param>
    /// <returns>The target object after potentially applying the actions.</returns>
    /// <remarks>
    /// This method is useful for applying changes to an object conditionally in a fluent manner.
    /// </remarks>
    public static T WithIf<T>(this T target, Func<T, bool> @if, params Action<T>[] actions)
    {
        if (@if(target))
            actions.ForEach(action => action.ToFunc()(target).Pipe(_ => target));
        return target;
    }

    /// <summary>
    /// Applies a sequence of asynchronous actions to the target object if the specified condition is true, and returns the updated object.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="targetTask">The task that produces the target object.</param>
    /// <param name="when">The condition that determines whether the actions should be applied.</param>
    /// <param name="actions">The asynchronous actions to apply to the target object.</param>
    /// <returns>A task that represents the updated target object after applying all actions, or the original object if the condition is false.</returns>
    /// <remarks>
    /// This method allows for conditional chaining of asynchronous operations without the need for nested callbacks or explicit continuation tasks.
    /// </remarks>
    public static async Task<T> WithIf<T>(this Task<T> targetTask, bool when, params Func<T, Task>[] actions)
    {
        var target = await targetTask;

        if (when)
            await actions.ForEachAsync(action => action(target));

        return target;
    }

    #endregion
}
