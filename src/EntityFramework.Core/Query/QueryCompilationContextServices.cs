// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Query
{
    public class QueryCompilationContextServices
    {
        public QueryCompilationContextServices(
            [NotNull] IModel model,
            [NotNull] IQueryAnnotationExtractor queryAnnotationExtractor,
            [NotNull] IQueryOptimizer queryOptimizer,
            [NotNull] IResultOperatorHandler resultOperatorHandler,
            [NotNull] IEntityMaterializerSource entityMaterializerSource,
            [NotNull] IEntityKeyFactorySource entityKeyFactorySource,
            [NotNull] IClrAccessorSource<IClrPropertyGetter> clrPropertyGetterSource,
            [NotNull] IEntityQueryableExpressionVisitorFactory entityQueryableExpressionVisitorFactory,
            [NotNull] IProjectionExpressionVisitorFactory projectionExpressionVisitorFactory)
        {
            Check.NotNull(model, nameof(model));
            Check.NotNull(queryAnnotationExtractor, nameof(queryAnnotationExtractor));
            Check.NotNull(queryOptimizer, nameof(queryOptimizer));
            Check.NotNull(resultOperatorHandler, nameof(resultOperatorHandler));
            Check.NotNull(entityMaterializerSource, nameof(entityMaterializerSource));
            Check.NotNull(entityKeyFactorySource, nameof(entityKeyFactorySource));
            Check.NotNull(clrPropertyGetterSource, nameof(clrPropertyGetterSource));
            Check.NotNull(entityQueryableExpressionVisitorFactory, nameof(entityQueryableExpressionVisitorFactory));
            Check.NotNull(projectionExpressionVisitorFactory, nameof(projectionExpressionVisitorFactory));

            Model = model;
            QueryAnnotationExtractor = queryAnnotationExtractor;
            QueryOptimizer = queryOptimizer;
            ResultOperatorHandler = resultOperatorHandler;
            EntityMaterializerSource = entityMaterializerSource;
            EntityKeyFactorySource = entityKeyFactorySource;
            ClrPropertyGetterSource = clrPropertyGetterSource;
            EntityQueryableExpressionVisitorFactory = entityQueryableExpressionVisitorFactory;
            ProjectionExpressionVisitorFactory = projectionExpressionVisitorFactory;
        }

        public virtual IModel Model { get; }
        public virtual IQueryAnnotationExtractor QueryAnnotationExtractor { get; }
        public virtual IQueryOptimizer QueryOptimizer { get; }
        public virtual IResultOperatorHandler ResultOperatorHandler { get; }
        public virtual IEntityMaterializerSource EntityMaterializerSource { get; }
        public virtual IEntityKeyFactorySource EntityKeyFactorySource { get; }
        public virtual IClrAccessorSource<IClrPropertyGetter> ClrPropertyGetterSource { get; }
        public virtual IEntityQueryableExpressionVisitorFactory EntityQueryableExpressionVisitorFactory { get; }
        public virtual IProjectionExpressionVisitorFactory ProjectionExpressionVisitorFactory { get; }
    }
}
