namespace Easy.Platform.AutomationTest.Helpers;

public static partial class Helper
{
    public static class AssertMsgBuilder
    {
        public static string Failed(string generalMsg)
        {
            return generalMsg;
        }

        public static string Failed(string generalMsg, string expected, string actual)
        {
            return $"{generalMsg}.{Environment.NewLine}Expected: {expected}.{Environment.NewLine}Actual: {actual}";
        }
    }
}
