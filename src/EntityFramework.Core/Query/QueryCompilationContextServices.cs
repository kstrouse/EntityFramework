// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Query
{
    public class QueryCompilationContextServices
    {
        public QueryCompilationContextServices(
            [NotNull] IModel model,
            [NotNull] ILoggerFactory loggerFactory,
            [NotNull] IResultOperatorHandler resultOperatorHandler,
            [NotNull] IEntityMaterializerSource entityMaterializerSource,
            [NotNull] IEntityKeyFactorySource entityKeyFactorySource,
            [NotNull] IClrAccessorSource<IClrPropertyGetter> clrPropertyGetterSource,
            [NotNull] IQueryingExpressionVisitor queryingExpressionVisitor,
            [NotNull] IProjectionExpressionVisitor projectionExpressionVisitor)
        {
            Model = model;
            ResultOperatorHandler = resultOperatorHandler;
            EntityMaterializerSource = entityMaterializerSource;
            EntityKeyFactorySource = entityKeyFactorySource;
            ClrPropertyGetterSource = clrPropertyGetterSource;
            QueryingExpressionVisitor = queryingExpressionVisitor;
            ProjectionExpressionVisitor = projectionExpressionVisitor;
        }

        public virtual IModel Model { get; }
        public virtual IResultOperatorHandler ResultOperatorHandler { get; }
        public virtual IEntityMaterializerSource EntityMaterializerSource { get; }
        public virtual IEntityKeyFactorySource EntityKeyFactorySource { get; }
        public virtual IClrAccessorSource<IClrPropertyGetter> ClrPropertyGetterSource { get; }
        public virtual IQueryingExpressionVisitor QueryingExpressionVisitor { get; }
        public virtual IProjectionExpressionVisitor ProjectionExpressionVisitor { get; }
    }
}
