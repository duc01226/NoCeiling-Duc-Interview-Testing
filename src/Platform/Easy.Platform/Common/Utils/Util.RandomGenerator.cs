namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    /// <summary>
    /// Provides utility methods for generating random values and performing actions based on chance.
    /// </summary>
    public static class RandomGenerator
    {
        /// <summary>
        /// Performs the specified action based on a given percentage chance.
        /// </summary>
        /// <param name="percentChance">The percentage chance for the action to be executed (1-100).</param>
        /// <param name="action">The action to be performed.</param>
        public static void DoByChance(int percentChance, Action action)
        {
            if (Random.Shared.Next(1, 100) <= percentChance) action();
        }

        /// <summary>
        /// Returns a value based on a given percentage chance, or a default value if the chance is not met.
        /// </summary>
        /// <typeparam name="T">The type of values to be returned.</typeparam>
        /// <param name="percentChance">The percentage chance for the chance value to be returned (1-100).</param>
        /// <param name="chanceReturnValue">The value to be returned if the chance is met.</param>
        /// <param name="defaultReturnValue">The default value to be returned if the chance is not met.</param>
        /// <returns>The chance value if the condition is met; otherwise, the default value.</returns>
        public static T ReturnByChanceOrDefault<T>(int percentChance, T chanceReturnValue, T defaultReturnValue)
        {
            return Random.Shared.Next(1, 100) <= percentChance ? chanceReturnValue : defaultReturnValue;
        }

        /// <summary>
        /// Returns a random integer within the specified range.
        /// </summary>
        /// <param name="min">The inclusive minimum value of the range.</param>
        /// <param name="max">The exclusive maximum value of the range.</param>
        /// <returns>A random integer within the specified range.</returns>
        public static int Next(int min, int max)
        {
            return Random.Shared.Next(min, max >= min ? max : min);
        }

        /// <summary>
        /// Selects a single random value from the provided array of values.
        /// </summary>
        /// <typeparam name="T">The type of values in the array.</typeparam>
        /// <param name="values">The array of values to choose from.</param>
        /// <returns>A randomly selected value from the array.</returns>
        public static T SelectSingleRandom<T>(params T[] values)
        {
            return values[Random.Shared.Next(0, values.Length - 1)];
        }
    }
}
