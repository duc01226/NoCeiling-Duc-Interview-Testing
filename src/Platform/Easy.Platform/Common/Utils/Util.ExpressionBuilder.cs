using System.Linq.Expressions;
using System.Reflection;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class ExpressionBuilder
    {
        /// <summary>
        /// Return a contains <see cref="containStringValue" /> expression on string property by <see cref="propertyName" /> of instance type of <see cref="T" />
        /// Example: BuildStringContainsExpression("Search", "abc") equal to p => p.Search.Contains("abc")
        /// </summary>
        public static Expression<Func<T, bool>> BuildStringContainsExpression<T>(
            string propertyName,
            string containStringValue)
        {
            var parameterExpr = Expression.Parameter(typeof(T), "instanceT");

            var propertyExpr = Expression.Property(parameterExpr, propertyName);
            var containsMethodCallExpr =
                BuildMethodCallExpr(
                    calledOnExpressionTarget: propertyExpr,
                    methodClassType: typeof(string),
                    methodName: "Contains",
                    methodParams: new object[]
                    {
                        containStringValue
                    });

            return Expression.Lambda<Func<T, bool>>(body: containsMethodCallExpr, parameters: parameterExpr);
        }

        /// <summary>
        /// Return a chain method called expression on a property by <see cref="propertyName" /> of instance type of <see cref="TChainTarget" />
        /// Example: BuildChainExpression("Search", new (string, object[])[] { ("ToLower", null), ("Contains", new[] { "abc" }) } ) equal to p => p.Search.ToLower().Contains("abc")
        /// </summary>
        /// <param name="propertyName">Selected property name of type <see cref="TChainTarget" /></param>
        /// <param name="chainMethods">List of (MethodName, MethodParams, MethodReturnType)</param>
        public static Expression<Func<TChainTarget, bool>> BuildChainExpression<TChainTarget>(
            string propertyName,
            params ValueTuple<string, object[]>[] chainMethods)
        {
            if (!chainMethods.Any())
                throw new Exception("At least one chain method must be given");

            var chainTargetParameterExpr = Expression.Parameter(typeof(TChainTarget), "instanceT");

            var chainTargetPropertyExpr = Expression.Property(chainTargetParameterExpr, propertyName);

            // Chain from [(A0),(A1, ["a1Param01", "a1Param02"])] equals to x.A0().A1("a1Param01", "a1Param02") equal to A1(A0(x), "a1Param01", "a1Param02").
            var (firstChainMethodName, firstChainMethodParams) = chainMethods[0];
            var firstChainMethodCallExpr = BuildMethodCallExpr(
                chainTargetPropertyExpr,
                ((PropertyInfo)chainTargetPropertyExpr.Member).PropertyType,
                firstChainMethodName,
                firstChainMethodParams);

            var aggregatedChainMethodCallExpr = chainMethods.Skip(1)
                .Aggregate(
                    firstChainMethodCallExpr,
                    (prevChainMethodCallExpr, chainMethodInfo) => BuildMethodCallExpr(
                        calledOnExpressionTarget: prevChainMethodCallExpr,
                        methodClassType: prevChainMethodCallExpr.Method.ReturnType,
                        methodName: chainMethodInfo.Item1,
                        methodParams: chainMethodInfo.Item2));

            return Expression.Lambda<Func<TChainTarget, bool>>(
                body: aggregatedChainMethodCallExpr,
                parameters: chainTargetParameterExpr);
        }

        private static MethodCallExpression BuildMethodCallExpr(
            Expression calledOnExpressionTarget,
            Type methodClassType,
            string methodName,
            object[] methodParams)
        {
            var firstReversedChainMethodInfo = methodClassType.GetMethod(
                methodName,
                methodParams?.Select(param => param?.GetType() ?? typeof(object)).ToArray() ?? Array.Empty<Type>());
            var firstChainMethodParamArgumentExpressions =
                methodParams?.Select(
                    methodParam => Expression.Constant(methodParam, methodParam?.GetType() ?? typeof(object))) ??
                new List<ConstantExpression>();
            return Expression.Call(
                calledOnExpressionTarget,
                firstReversedChainMethodInfo!,
                firstChainMethodParamArgumentExpressions);
        }
    }
}
