namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class RandomGenerator
    {
        private static readonly Random DefaultRandomInstance = new();

        public static void DoByChance(int percentChance, Action action)
        {
            if (DefaultRandomInstance.Next(1, 100) <= percentChance) action();
        }

        public static T ReturnByChanceOrDefault<T>(int percentChance, T chanceReturnValue, T defaultReturnValue)
        {
            return DefaultRandomInstance.Next(1, 100) <= percentChance ? chanceReturnValue : defaultReturnValue;
        }

        public static int Next(int min, int max)
        {
            return DefaultRandomInstance.Next(min, max >= min ? max : min);
        }
    }
}
