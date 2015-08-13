// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class ProjectionExpressionVisitorFactory : IProjectionExpressionVisitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ProjectionExpressionVisitorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public virtual ExpressionVisitor Create(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] IQuerySource querySource)
        {
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));
            Check.NotNull(querySource, nameof(querySource));

            var visitor = _serviceProvider.GetService<ProjectionExpressionVisitor>();

            visitor.QueryModelVisitor = queryModelVisitor;

            return visitor;
        }
    }
}
