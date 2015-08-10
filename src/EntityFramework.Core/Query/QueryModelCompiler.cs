// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;
using Remotion.Linq;

namespace Microsoft.Data.Entity.Query
{
    public class QueryModelCompiler : IQueryModelCompiler
    {
        private readonly IQueryCompilationContextFactory _compilationContextFactory;
        private readonly IQueryAnnotationExtractor _annotationExtractor;
        private readonly IQueryOptimizer _optimizer;

        private QueryCompilationContext _compilationContext;

        public QueryModelCompiler(
            [NotNull] IQueryCompilationContextFactory compilationContextFactory,
            [NotNull] IQueryAnnotationExtractor annotationExtractor,
            [NotNull] IQueryOptimizer optimizer)
        {
            Check.NotNull(compilationContextFactory, nameof(compilationContextFactory));
            Check.NotNull(annotationExtractor, nameof(annotationExtractor));
            Check.NotNull(optimizer, nameof(optimizer));

            _compilationContextFactory = compilationContextFactory;
            _annotationExtractor = annotationExtractor;
            _optimizer = optimizer;
        }

        public virtual Func<QueryContext, IEnumerable<TResult>> CreateQueryExecutor<TResult>([NotNull] QueryModel queryModel)
        {
            Check.NotNull(queryModel, nameof(queryModel));

            _compilationContext = _compilationContextFactory.Create();

            _compilationContext.Initialize(false);

            using (_compilationContext.Logger.BeginScopeImpl(this))
            {
                _compilationContext.Logger.LogInformation(queryModel, Strings.LogCompilingQueryModel);

                _compilationContext.QueryAnnotations = _annotationExtractor.ExtractQueryAnnotations(queryModel);

                var queryModelVisitor = _compilationContext.CreateQueryModelVisitor();

                _optimizer.OptimizeQuery(_compilationContext, queryModelVisitor, queryModel);

                _compilationContext.FindQuerySourcesRequiringMaterialization(queryModelVisitor, queryModel);

                return queryModelVisitor.CreateQueryExecutor<TResult>(queryModel);
            }
        }

        public virtual Func<QueryContext, IAsyncEnumerable<TResult>> CreateAsyncQueryExecutor<TResult>([NotNull] QueryModel queryModel)
        {
            Check.NotNull(queryModel, nameof(queryModel));

            _compilationContext = _compilationContextFactory.Create();

            _compilationContext.Initialize(true);

            using (_compilationContext.Logger.BeginScopeImpl(this))
            {
                _compilationContext.Logger.LogInformation(queryModel, Strings.LogCompilingQueryModel);

                _compilationContext.QueryAnnotations = _annotationExtractor.ExtractQueryAnnotations(queryModel);

                var queryModelVisitor = _compilationContext.CreateQueryModelVisitor();

                _optimizer.OptimizeQuery(_compilationContext, queryModelVisitor, queryModel);

                _compilationContext.FindQuerySourcesRequiringMaterialization(queryModelVisitor, queryModel);

                return queryModelVisitor.CreateAsyncQueryExecutor<TResult>(queryModel);
            }
        }
    }
}
