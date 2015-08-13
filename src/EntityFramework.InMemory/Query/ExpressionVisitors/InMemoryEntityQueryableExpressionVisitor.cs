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
    public  class InMemoryEntityQueryableExpressionVisitor : EntityQueryableExpressionVisitor
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

        public virtual new InMemoryQueryModelVisitor QueryModelVisitor
        {
            get { return (InMemoryQueryModelVisitor)base.QueryModelVisitor; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                base.QueryModelVisitor = value;
            }
        }

        protected override Expression VisitEntityQueryable(Type elementType)
        {
            Check.NotNull(elementType, nameof(elementType));

            var entityType
                = QueryModelVisitor.QueryCompilationContext.Services.Model
                    .GetEntityType(elementType);

            var keyProperties
                = entityType.GetPrimaryKey().Properties;

            var keyFactory
                = QueryModelVisitor
                    .QueryCompilationContext
                    .Services
                    .EntityKeyFactorySource.GetKeyFactory(entityType.GetPrimaryKey());

            Func<ValueBuffer, EntityKey> entityKeyFactory
                = vr => keyFactory.Create(keyProperties, vr);

            if (QueryModelVisitor.QueryCompilationContext
                .QuerySourceRequiresMaterialization(_querySource))
            {
                var materializer
                   = new MaterializerFactory(QueryModelVisitor
                       .QueryCompilationContext
                       .Services
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
