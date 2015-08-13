// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class RelationalProjectionExpressionVisitor : ProjectionExpressionVisitor
    {
        private IQuerySource _querySource;

        public virtual IQuerySource QuerySource
        {
            get { return _querySource; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                _querySource = value;
            }
        }

        public virtual new RelationalQueryModelVisitor QueryModelVisitor
        {
            get { return (RelationalQueryModelVisitor)base.QueryModelVisitor; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                base.QueryModelVisitor = value;
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            Check.NotNull(methodCallExpression, nameof(methodCallExpression));

            if (methodCallExpression.Method.IsGenericMethod)
            {
                var methodInfo = methodCallExpression.Method.GetGenericMethodDefinition();

                if (ReferenceEquals(methodInfo, EntityQueryModelVisitor.PropertyMethodInfo))
                {
                    var newArg0 = Visit(methodCallExpression.Arguments[0]);

                    if (newArg0 != methodCallExpression.Arguments[0])
                    {
                        return Expression.Call(
                            methodCallExpression.Method,
                            newArg0,
                            methodCallExpression.Arguments[1]);
                    }

                    return methodCallExpression;
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        protected override Expression VisitNew(NewExpression newExpression)
        {
            Check.NotNull(newExpression, nameof(newExpression));

            var newNewExpression = base.VisitNew(newExpression);

            var selectExpression = QueryModelVisitor.TryGetQuery(_querySource);

            if (selectExpression != null)
            {
                for (var i = 0; i < newExpression.Arguments.Count; i++)
                {
                    var aliasExpression
                        = selectExpression.Projection
                            .OfType<AliasExpression>()
                            .SingleOrDefault(ae => ae.SourceExpression == newExpression.Arguments[i]);

                    if (aliasExpression != null)
                    {
                        aliasExpression.SourceMember 
                            = newExpression.Members?[i] 
                                ?? (newExpression.Arguments[i] as MemberExpression)?.Member;
                    }
                }
            }

            return newNewExpression;
        }

        public override Expression Visit(Expression expression)
        {
            var selectExpression = QueryModelVisitor.TryGetQuery(_querySource);

            if (expression != null
                && !(expression is ConstantExpression)
                && selectExpression != null)
            {
                var sqlExpression
                    = new SqlTranslatingExpressionVisitor(
                        QueryModelVisitor, selectExpression, inProjection: true)
                        .Visit(expression);

                if (sqlExpression == null)
                {
                    if (!(expression is QuerySourceReferenceExpression))
                    {
                        QueryModelVisitor.RequiresClientProjection = true;
                    }
                }
                else
                {
                    if (!(expression is NewExpression))
                    {
                        AliasExpression aliasExpression;

                        int index;

                        if (!(expression is QuerySourceReferenceExpression))
                        {
                            var columnExpression = sqlExpression.TryGetColumnExpression();

                            if (columnExpression != null)
                            {
                                index = selectExpression.AddToProjection(sqlExpression);

                                aliasExpression = selectExpression.Projection[index] as AliasExpression;

                                if (aliasExpression != null)
                                {
                                    aliasExpression.SourceExpression = expression;
                                }

                                return expression;
                            }
                        }

                        if (!(sqlExpression is ConstantExpression))
                        {
                            index = selectExpression.AddToProjection(sqlExpression);

                            aliasExpression = selectExpression.Projection[index] as AliasExpression;

                            if (aliasExpression != null)
                            {
                                aliasExpression.SourceExpression = expression;
                            }

                            return
                                QueryModelVisitor.BindReadValueMethod(
                                    expression.Type,
                                    QueryResultScope.GetResult(
                                        EntityQueryModelVisitor.QueryResultScopeParameter,
                                        _querySource,
                                        typeof(ValueBuffer)),
                                    index);
                        }
                    }
                }
            }

            return base.Visit(expression);
        }
    }
}
