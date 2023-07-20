namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class RandomGenerator
    {
        public static void DoByChance(int percentChance, Action action)
        {
            if (Random.Shared.Next(1, 100) <= percentChance) action();
        }

        public static T ReturnByChanceOrDefault<T>(int percentChance, T chanceReturnValue, T defaultReturnValue)
        {
            return Random.Shared.Next(1, 100) <= percentChance ? chanceReturnValue : defaultReturnValue;
        }

        public static int Next(int min, int max)
        {
            return Random.Shared.Next(min, max >= min ? max : min);
        }

        public static T SelectSingleRandom<T>(params T[] values)
        {
            return values[Random.Shared.Next(0, values.Length - 1)];
        }
    }
}
