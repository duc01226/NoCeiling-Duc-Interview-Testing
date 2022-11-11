namespace ApiJwtAuthenticationExample.Extensions;

public static class ObjectExtensions
{
    public static T With<T>(this T target, Action<T> editDataAction)
    {
        editDataAction(target);

        return target;
    }
}
