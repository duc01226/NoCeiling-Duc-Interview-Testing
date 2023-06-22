using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.RequestContext;

public static class PlatformRequestContextHelper
{
    public static bool TryGetValue<T>(IDictionary<string, object> requestContext, string contextKey, out T item)
    {
        // contextKey.ToLower() to support search case-insensitive for some server auto normalize the header context key
        var originalValue = requestContext.ContainsKey(contextKey)
            ? requestContext[contextKey]
            : requestContext.TryGetValueOrDefault(contextKey.ToLower());

        if (originalValue != null)
        {
            if (originalValue is string originalValueStr && typeof(T) != typeof(string))
            {
                var isParsedSuccess = TryGetParsedValuesFromStringValues(out item, Util.ListBuilder.New(originalValueStr));

                return isParsedSuccess;
            }

            item = (T)requestContext[contextKey];
            return true;
        }

        item = default;

        return false;
    }

    /// <summary>
    /// Try Get Deserialized value from matchedClaimStringValues
    /// </summary>
    public static bool TryGetParsedValuesFromStringValues<T>(out T foundValue, List<string> stringValues)
    {
        if (FindFirstValueListInterfaceType<T>() == null && !stringValues.Any())
        {
            foundValue = default;
            return false;
        }

        if (typeof(T) == typeof(string) || typeof(T) == typeof(object))
        {
            if (!stringValues.Any())
            {
                foundValue = default;
                return false;
            }

            foundValue = (T)(object)stringValues.LastOrDefault();
            return true;
        }

        // If T is number type
        if (typeof(T).IsAssignableTo(typeof(double)) ||
            typeof(T) == typeof(int) ||
            typeof(T) == typeof(float) ||
            typeof(T) == typeof(double) ||
            typeof(T) == typeof(long) ||
            typeof(T) == typeof(short))
        {
            var parsedSuccess = double.TryParse(stringValues.LastOrDefault(), out var parsedValue);
            if (parsedSuccess)
            {
                // WHY: Serialize then Deserialize to ensure could parse from double to int, long, float, etc.. any of number type T
                foundValue = PlatformJsonSerializer.Deserialize<T>(parsedValue.ToJson());
                return true;
            }
        }

        if (typeof(T) == typeof(bool))
        {
            var parsedSuccess = bool.TryParse(stringValues.LastOrDefault(), out var parsedValue);
            if (parsedSuccess)
            {
                foundValue = (T)(object)parsedValue;
                return true;
            }
        }

        // Handle case if type T is a list with many items and each stringValue is a json represent an item
        var isTryGetListValueSuccess =
            TryGetParsedListValueFromUserClaimStringValues(stringValues, out foundValue);
        if (isTryGetListValueSuccess)
            return true;

        return PlatformJsonSerializer.TryDeserialize(
            stringValues.LastOrDefault(),
            out foundValue);
    }

    public static Type FindFirstValueListInterfaceType<T>()
    {
        var firstValueListInterface = typeof(T)
            .GetInterfaces()
            .FirstOrDefault(
                p =>
                    p.IsGenericType &&
                    (p.GetGenericTypeDefinition().IsAssignableTo(typeof(IEnumerable<>)) ||
                     p.GetGenericTypeDefinition().IsAssignableTo(typeof(ICollection<>))));
        return firstValueListInterface;
    }

    public static bool TryGetParsedListValueFromUserClaimStringValues<T>(
        List<string> matchedClaimStringValues,
        out T foundValue)
    {
        var firstValueListInterface = FindFirstValueListInterfaceType<T>();

        if (firstValueListInterface != null)
        {
            var listItemType = firstValueListInterface.GetGenericArguments()[0];

            var isParsedAllItemSuccess = true;

            var parsedItemList = matchedClaimStringValues
                .Select(
                    matchedClaimStringValue =>
                    {
                        if (listItemType == typeof(string))
                            return matchedClaimStringValue;

                        var parsedItemResult = PlatformJsonSerializer.TryDeserialize(
                            matchedClaimStringValue,
                            listItemType,
                            out var itemDeserializedValue);

                        if (parsedItemResult == false)
                            isParsedAllItemSuccess = false;

                        return itemDeserializedValue;
                    })
                .ToList();

            if (isParsedAllItemSuccess)
            {
                // Serialize then Deserialize to type T so ensure parse matchedClaimStringValues to type T successfully
                foundValue = PlatformJsonSerializer.Deserialize<T>(parsedItemList.ToJson());
                return true;
            }
        }

        foundValue = default;

        return false;
    }
}
