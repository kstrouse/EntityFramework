// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query
{
    public class InMemoryQueryModelVisitor : EntityQueryModelVisitor
    {
        public InMemoryQueryModelVisitor([NotNull] QueryCompilationContext queryCompilationContext)
            : base(Check.NotNull(queryCompilationContext, nameof(queryCompilationContext)))
        {
        }

        protected override void IncludeNavigations(
            IncludeSpecification includeSpecification,
            Type resultType,
            LambdaExpression accessorLambda,
            bool querySourceRequiresTracking)
        {
            Check.NotNull(includeSpecification, nameof(includeSpecification));
            Check.NotNull(resultType, nameof(resultType));
            Check.NotNull(accessorLambda, nameof(accessorLambda));

            var primaryKeyParameter = Expression.Parameter(typeof(EntityKey));
            var relatedKeyFactoryParameter = Expression.Parameter(typeof(Func<ValueBuffer, EntityKey>));
            var navigationPath = includeSpecification.NavigationPath;

            Expression
                = Expression.Call(
                    _includeMethodInfo.MakeGenericMethod(resultType),
                    QueryContextParameter,
                    Expression,
                    Expression.Constant(navigationPath),
                    accessorLambda,
                    Expression.NewArrayInit(
                        typeof(RelatedEntitiesLoader),
                        navigationPath.Select(
                            n =>
                                {
                                    var targetType = n.GetTargetType();

                                    var materializer
                                        = new MaterializerFactory(
                                            QueryCompilationContext
                                                .EntityMaterializerSource)
                                            .CreateMaterializer(targetType);

                                    return Expression.Lambda<RelatedEntitiesLoader>(
                                        Expression.Call(
                                            _getRelatedValueBuffersMethodInfo,
                                            QueryContextParameter,
                                            Expression.Constant(targetType),
                                            primaryKeyParameter,
                                            relatedKeyFactoryParameter,
                                            materializer),
                                        primaryKeyParameter,
                                        relatedKeyFactoryParameter);
                                })),
                    Expression.Constant(querySourceRequiresTracking));
        }

        private static readonly MethodInfo _includeMethodInfo
            = typeof(InMemoryQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(Include));

        [UsedImplicitly]
        private static IEnumerable<TResult> Include<TResult>(
            QueryContext queryContext,
            IEnumerable<TResult> source,
            IReadOnlyList<INavigation> navigationPath,
            Func<TResult, object> accessorLambda,
            IReadOnlyList<RelatedEntitiesLoader> relatedEntitiesLoaders,
            bool querySourceRequiresTracking)
        {
            return
                source
                    .Select(result =>
                        {
                            queryContext.QueryBuffer
                                .Include(
                                    accessorLambda.Invoke(result),
                                    navigationPath,
                                    relatedEntitiesLoaders,
                                    querySourceRequiresTracking);

                            return result;
                        });
        }

        private static readonly MethodInfo _getRelatedValueBuffersMethodInfo
            = typeof(InMemoryQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(GetRelatedValueBuffers));

        [UsedImplicitly]
        private static IEnumerable<EntityLoadInfo> GetRelatedValueBuffers(
            QueryContext queryContext,
            IEntityType targetType,
            EntityKey primaryKey,
            Func<ValueBuffer, EntityKey> relatedKeyFactory,
            Func<IEntityType, ValueBuffer, object> materializer)
        {
            return ((InMemoryQueryContext)queryContext).Store
                .GetTables(targetType)
                .SelectMany(t =>
                    t.Select(vs => new EntityLoadInfo(
                        new ValueBuffer(vs), vb => materializer(t.EntityType, vb)))
                        .Where(eli => relatedKeyFactory(eli.ValueBuffer).Equals(primaryKey)));
        }

        public static readonly MethodInfo EntityQueryMethodInfo
            = typeof(InMemoryQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(EntityQuery));

        [UsedImplicitly]
        private static IEnumerable<TEntity> EntityQuery<TEntity>(
            QueryContext queryContext,
            IEntityType entityType,
            Func<ValueBuffer, EntityKey> entityKeyFactory,
            Func<IEntityType, ValueBuffer, object> materializer,
            bool queryStateManager)
            where TEntity : class
        {
            return ((InMemoryQueryContext)queryContext).Store
                .GetTables(entityType)
                .SelectMany(t =>
                    t.Select(vs =>
                        {
                            var valueBuffer = new ValueBuffer(vs);
                            var entityKey = entityKeyFactory(valueBuffer);

                            return (TEntity)queryContext
                                .QueryBuffer
                                .GetEntity(
                                    entityType,
                                    entityKey,
                                    new EntityLoadInfo(
                                        valueBuffer,
                                        vr => materializer(t.EntityType, vr)),
                                    queryStateManager);
                        }));
        }

        public static readonly MethodInfo ProjectionQueryMethodInfo
            = typeof(InMemoryQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(ProjectionQuery));

        [UsedImplicitly]
        private static IEnumerable<ValueBuffer> ProjectionQuery(
            QueryContext queryContext,
            IEntityType entityType)
        {
            return ((InMemoryQueryContext)queryContext).Store
                .GetTables(entityType)
                .SelectMany(t => t.Select(vs => new ValueBuffer(vs)));
        }
    }
}
