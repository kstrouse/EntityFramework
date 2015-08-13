// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Methods;
using Microsoft.Data.Entity.Query.Expressions;

namespace Microsoft.Data.Entity.SqlServer.Query.Methods
{
    public class StringCompareTranslator : IMethodCallTranslator
    {
        private static readonly MethodInfo _methodInfo = typeof(string).GetTypeInfo().GetDeclaredMethods("Compare")
            .Where(m => m.GetParameters().Count() == 2)
            .Single();

        public Expression Translate([NotNull] MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method == _methodInfo)
            {
                var arguments = methodCallExpression.Arguments.ToList();
                var leftString = arguments[0];
                var rightString = arguments[1];

                // TODO: this can be simplified when we implement translation for CASE-WHEN-THEN-WHEN-THEN-...-ELSE
                return Expression.Condition(
                    Expression.Equal(
                        leftString,
                        rightString
                    ),
                    Expression.Constant(0),
                    Expression.Condition(
                        new SqlFunctionExpression(
                            "__StringComparisonTemplate",
                            new[] { leftString, rightString, Expression.Constant(ExpressionType.GreaterThan) },
                            typeof(bool)
                        ),
                        Expression.Constant(1),
                        Expression.Constant(-1)
                    )
                );
            }

            return null;
        }
    }
}
