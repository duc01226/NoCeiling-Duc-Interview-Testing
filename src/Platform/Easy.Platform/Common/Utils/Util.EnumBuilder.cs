namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class EnumBuilder
    {
        /// <summary>
        /// Parse a string to Enum of specified type
        /// </summary>
        public static TEnum Parse<TEnum>(string enumString) where TEnum : Enum
        {
            return (TEnum)Enum.Parse(typeof(TEnum), enumString);
        }
    }
}
