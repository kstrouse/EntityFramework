// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Query.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;

namespace Microsoft.Data.Entity.Query
{
    public class QueryPreprocessor : IQueryPreprocessor
    {
        public QueryPreprocessor()
        {
        }

        public virtual Expression Preprocess([NotNull] Expression query, [NotNull] QueryContext queryContext)
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(queryContext, nameof(queryContext));

            query = new QueryAnnotatingExpressionVisitor().Visit(query);

            query = new FunctionEvaluationDisablingVisitor().Visit(query);

            var partialEvaluationInfo = EvaluatableTreeFindingExpressionVisitor.Analyze(query, new NullEvaluatableExpressionFilter());

            query = new ParameterExtractingExpressionVisitor(partialEvaluationInfo, queryContext).Visit(query);

            return query;
        }

        private class QueryAnnotatingExpressionVisitor : ExpressionVisitorBase
        {
            protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.GetCustomAttribute<QueryAnnotationMethodAttribute>() != null)
                {
                    string argumentName;

                    methodCallExpression
                        = Expression.Call(
                            QueryAnnotationExtensions.QueryAnnotationMethodInfo
                                .MakeGenericMethod(methodCallExpression.Method.GetGenericArguments()),
                            methodCallExpression.Arguments[0],
                            Expression.Constant(
                                new QueryAnnotation(
                                    methodCallExpression.Method,
                                    methodCallExpression.Arguments
                                        .Select(a => ExpressionEvaluationHelpers.Evaluate(a, out argumentName))
                                        .ToArray())));
                }
                return base.VisitMethodCall(methodCallExpression);
            }
        }

        private class FunctionEvaluationDisablingVisitor : ExpressionVisitorBase
        {
            public static readonly MethodInfo DbContextSetMethodInfo
                = typeof(DbContext).GetTypeInfo().GetDeclaredMethod("Set");

            private static readonly MethodInfo[] _nonDeterministicMethodInfos =
            {
            typeof(Guid).GetTypeInfo().GetDeclaredMethod("NewGuid"),
            typeof(DateTime).GetTypeInfo().GetDeclaredProperty("Now").GetMethod
        };

            protected override Expression VisitMethodCall(MethodCallExpression expression)
            {
                if (expression.Method.IsGenericMethod)
                {
                    var genericMethodDefinition = expression.Method.GetGenericMethodDefinition();
                    if (ReferenceEquals(genericMethodDefinition, EntityQueryModelVisitor.PropertyMethodInfo)
                        || ReferenceEquals(genericMethodDefinition, DbContextSetMethodInfo))
                    {
                        return base.VisitMethodCall(expression);
                    }
                }

                if (IsQueryable(expression.Object)
                    || IsQueryable(expression.Arguments.FirstOrDefault()))
                {
                    return base.VisitMethodCall(expression);
                }

                var newObject = Visit(expression.Object);
                var newArguments = VisitAndConvert(expression.Arguments, "VisitMethodCall");

                var newMethodCall = newObject != expression.Object || newArguments != expression.Arguments
                    ? Expression.Call(newObject, expression.Method, newArguments)
                    : expression;

                return _nonDeterministicMethodInfos.Contains(expression.Method)
                    ? (Expression)new MethodCallEvaluationPreventingExpression(newMethodCall)
                    : newMethodCall;
            }

            private static bool IsQueryable(Expression expression)
            {
                return expression != null
                       && typeof(IQueryable).GetTypeInfo()
                           .IsAssignableFrom(expression.Type.GetTypeInfo());
            }

            protected override Expression VisitMember(MemberExpression expression)
            {
                var propertyInfo = expression.Member as PropertyInfo;

                return propertyInfo != null && _nonDeterministicMethodInfos.Contains(propertyInfo.GetMethod)
                    ? (Expression)new PropertyEvaluationPreventingExpression(expression)
                    : expression;
            }

            protected override Expression VisitSubQuery(SubQueryExpression expression)
            {
                var clonedModel = expression.QueryModel.Clone();

                clonedModel.TransformExpressions(Visit);

                return new SubQueryExpression(clonedModel);
            }
        }

        public class NullEvaluatableExpressionFilter : EvaluatableExpressionFilterBase
        {
        }

        private class ParameterExtractingExpressionVisitor : ExpressionVisitorBase
        {
            private readonly PartialEvaluationInfo _partialEvaluationInfo;
            private readonly QueryContext _queryContext;

            public ParameterExtractingExpressionVisitor(
                [NotNull] PartialEvaluationInfo partialEvaluationInfo,
                [NotNull] QueryContext queryContext)
            {
                Check.NotNull(partialEvaluationInfo, nameof(partialEvaluationInfo));
                Check.NotNull(queryContext, nameof(queryContext));

                _partialEvaluationInfo = partialEvaluationInfo;
                _queryContext = queryContext;
            }

            protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.IsGenericMethod)
                {
                    var methodInfo = methodCallExpression.Method.GetGenericMethodDefinition();

                    if (ReferenceEquals(methodInfo, EntityQueryModelVisitor.PropertyMethodInfo))
                    {
                        return methodCallExpression;
                    }
                }

                return base.VisitMethodCall(methodCallExpression);
            }

            public override Expression Visit(Expression expression)
            {
                if (expression == null)
                {
                    return null;
                }

                if (expression.NodeType == ExpressionType.Lambda
                    || !_partialEvaluationInfo.IsEvaluatableExpression(expression))
                {
                    return base.Visit(expression);
                }

                var e = expression;

                if (expression.NodeType == ExpressionType.Convert)
                {
                    if (expression.RemoveConvert() is ConstantExpression)
                    {
                        return expression;
                    }

                    var unaryExpression = (UnaryExpression)expression;

                    if ((unaryExpression.Type.IsNullableType()
                         && !unaryExpression.Operand.Type.IsNullableType())
                        || unaryExpression.Type == typeof(object))
                    {
                        e = unaryExpression.Operand;
                    }
                }

                if (e.NodeType != ExpressionType.Constant
                    && !typeof(IQueryable).GetTypeInfo().IsAssignableFrom(e.Type.GetTypeInfo()))
                {
                    try
                    {
                        string parameterName;
                        var parameterValue = ExpressionEvaluationHelpers.Evaluate(e, out parameterName);

                        var compilerPrefixIndex = parameterName.LastIndexOf(">", StringComparison.Ordinal);
                        if (compilerPrefixIndex != -1)
                        {
                            parameterName = parameterName.Substring(compilerPrefixIndex + 1);
                        }

                        parameterName
                            = $"{CompiledQueryCache.CompiledQueryParameterPrefix}{parameterName}_{_queryContext.ParameterValues.Count}";

                        _queryContext.ParameterValues.Add(parameterName, parameterValue);

                        return e.Type == expression.Type
                            ? Expression.Parameter(e.Type, parameterName)
                            : (Expression)Expression.Convert(
                                Expression.Parameter(e.Type, parameterName),
                                expression.Type);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            Strings.ExpressionParameterizationException(expression),
                            exception);
                    }
                }

                return expression;
            }
        }
    }
}
