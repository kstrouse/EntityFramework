// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class DefaultQueryExpressionVisitor : ExpressionVisitorBase
    {
        private EntityQueryModelVisitor _entityQueryModelVisitor;

        public virtual EntityQueryModelVisitor QueryModelVisitor
        {
            get { return _entityQueryModelVisitor; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                _entityQueryModelVisitor = value;
            }
        }

        protected override Expression VisitSubQuery(SubQueryExpression subQueryExpression)
        {
            Check.NotNull(subQueryExpression, nameof(subQueryExpression));

            var queryModelVisitor = CreateQueryModelVisitor();

            queryModelVisitor.VisitQueryModel(subQueryExpression.QueryModel);

            return queryModelVisitor.Expression;
        }

        protected EntityQueryModelVisitor CreateQueryModelVisitor()
            => QueryModelVisitor.QueryCompilationContext
                .CreateQueryModelVisitor(_entityQueryModelVisitor);

        protected override Expression VisitParameter(ParameterExpression parameterExpression)
        {
            if (parameterExpression.Name
                .StartsWith(CompiledQueryCache.CompiledQueryParameterPrefix, StringComparison.Ordinal))
            {
                return Expression.Call(
                    _getParameterValueMethodInfo.MakeGenericMethod(parameterExpression.Type),
                    EntityQueryModelVisitor.QueryContextParameter,
                    Expression.Constant(parameterExpression.Name));
            }

            return parameterExpression;
        }

        private static readonly MethodInfo _getParameterValueMethodInfo
            = typeof(DefaultQueryExpressionVisitor)
                .GetTypeInfo().GetDeclaredMethod(nameof(GetParameterValue));

        [UsedImplicitly]
        private static T GetParameterValue<T>(QueryContext queryContext, string parameterName)
            => (T)queryContext.ParameterValues[parameterName];
    }
}
