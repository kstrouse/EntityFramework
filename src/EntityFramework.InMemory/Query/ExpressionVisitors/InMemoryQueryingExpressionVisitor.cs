// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors
{
    public class InMemoryQueryingExpressionVisitor : IQueryingExpressionVisitor
    {
        public virtual Expression Visit(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] IQuerySource querySource,
            [NotNull] Expression expression)
        {
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(expression, nameof(expression));

            var visitor = new InMemoryEntityQueryableExpressionVisitor(queryModelVisitor, querySource);

            return visitor.Visit(expression);
        }
        private class InMemoryEntityQueryableExpressionVisitor : EntityQueryableExpressionVisitor
        {
            private readonly IQuerySource _querySource;

            public InMemoryEntityQueryableExpressionVisitor(
                EntityQueryModelVisitor entityQueryModelVisitor, IQuerySource querySource)
                : base(entityQueryModelVisitor)
            {
                _querySource = querySource;
            }

            private new InMemoryQueryModelVisitor QueryModelVisitor => (InMemoryQueryModelVisitor)base.QueryModelVisitor;

            protected override Expression VisitEntityQueryable(Type elementType)
            {
                Check.NotNull(elementType, nameof(elementType));

                var entityType
                    = QueryModelVisitor.QueryCompilationContext.Model
                        .GetEntityType(elementType);

                var keyProperties
                    = entityType.GetPrimaryKey().Properties;

                var keyFactory
                    = QueryModelVisitor
                        .QueryCompilationContext
                        .EntityKeyFactorySource.GetKeyFactory(entityType.GetPrimaryKey());

                Func<ValueBuffer, EntityKey> entityKeyFactory
                    = vr => keyFactory.Create(keyProperties, vr);

                if (QueryModelVisitor.QueryCompilationContext
                    .QuerySourceRequiresMaterialization(_querySource))
                {
                    var materializer
                       = new MaterializerFactory(QueryModelVisitor
                           .QueryCompilationContext
                           .EntityMaterializerSource)
                           .CreateMaterializer(entityType);

                    return Expression.Call(
                        InMemoryQueryModelVisitor.EntityQueryMethodInfo.MakeGenericMethod(elementType),
                        EntityQueryModelVisitor.QueryContextParameter,
                        Expression.Constant(entityType),
                        Expression.Constant(entityKeyFactory),
                        materializer,
                        Expression.Constant(QueryModelVisitor.QuerySourceRequiresTracking(_querySource)));
                }

                return Expression.Call(
                    InMemoryQueryModelVisitor.ProjectionQueryMethodInfo,
                    EntityQueryModelVisitor.QueryContextParameter,
                    Expression.Constant(entityType));
            }
        }
    }
}
