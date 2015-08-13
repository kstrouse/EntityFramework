// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;


namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class RelationalProjectionExpressionVisitor : IProjectionExpressionVisitor
    {
        public virtual Expression Visit(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] IQuerySource querySource,
            [NotNull] Expression expression)
        {
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(expression, nameof(expression));

            var visitor = new RelationalEntityProjectionExpressionVisitor(
                (RelationalQueryModelVisitor)queryModelVisitor,
                querySource);

            return visitor.Visit(expression);
        }
    }
}
