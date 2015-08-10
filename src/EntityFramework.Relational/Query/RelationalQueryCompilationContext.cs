// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Query.Internal;
using Microsoft.Data.Entity.Query.Methods;
using Microsoft.Data.Entity.Query.Sql;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query
{
    public class RelationalQueryCompilationContext : QueryCompilationContext
    {
        private readonly List<RelationalQueryModelVisitor> _relationalQueryModelVisitors
            = new List<RelationalQueryModelVisitor>();

        private readonly ISqlQueryGeneratorFactory _sqlQueryGeneratorFactory;
        private IQueryMethodProvider _queryMethodProvider;

        public RelationalQueryCompilationContext(
            [NotNull] IModel model,
            [NotNull] ILoggerFactory loggerFactory,
            [NotNull] IResultOperatorHandler resultOperatorHandler,
            [NotNull] IEntityMaterializerSource entityMaterializerSource,
            [NotNull] IEntityKeyFactorySource entityKeyFactorySource,
            [NotNull] IClrAccessorSource<IClrPropertyGetter> clrPropertyGetterSource,
            [NotNull] IQueryingExpressionVisitor queryingExpressionVisitor,
            [NotNull] IMethodCallTranslator compositeMethodCallTranslator,
            [NotNull] IMemberTranslator compositeMemberTranslator,
            [NotNull] IRelationalValueBufferFactoryFactory valueBufferFactoryFactory,
            [NotNull] IRelationalTypeMapper typeMapper,
            [NotNull] IRelationalMetadataExtensionProvider relationalExtensions,
            [NotNull] ISqlQueryGeneratorFactory sqlQueryGeneratorFactory)
            : base(
                model,
                loggerFactory,
                resultOperatorHandler,
                entityMaterializerSource,
                entityKeyFactorySource,
                clrPropertyGetterSource,
                queryingExpressionVisitor)

        {
            Check.NotNull(compositeMethodCallTranslator, nameof(compositeMethodCallTranslator));
            Check.NotNull(compositeMemberTranslator, nameof(compositeMemberTranslator));
            Check.NotNull(valueBufferFactoryFactory, nameof(valueBufferFactoryFactory));
            Check.NotNull(typeMapper, nameof(typeMapper));
            Check.NotNull(relationalExtensions, nameof(relationalExtensions));

            CompositeMethodCallTranslator = compositeMethodCallTranslator;
            CompositeMemberTranslator = compositeMemberTranslator;
            ValueBufferFactoryFactory = valueBufferFactoryFactory;
            TypeMapper = typeMapper;
            RelationalExtensions = relationalExtensions;
            _sqlQueryGeneratorFactory = sqlQueryGeneratorFactory;
        }

        public override void Initialize(bool isAsync)
        {
            base.Initialize(isAsync);

            if (isAsync)
            {
                _queryMethodProvider = new AsyncQueryMethodProvider();
            }
            else
            {
                _queryMethodProvider = new QueryMethodProvider();
            }
        }

        public override EntityQueryModelVisitor CreateQueryModelVisitor(
            EntityQueryModelVisitor parentEntityQueryModelVisitor)
        {
            var relationalQueryModelVisitor
                = new RelationalQueryModelVisitor(
                    this, (RelationalQueryModelVisitor)parentEntityQueryModelVisitor);

            _relationalQueryModelVisitors.Add(relationalQueryModelVisitor);

            return relationalQueryModelVisitor;
        }

        public override IExpressionPrinter CreateExpressionPrinter()
        {
            return new RelationalExpressionPrinter();
        }

        public virtual SelectExpression FindSelectExpression([NotNull] IQuerySource querySource)
        {
            Check.NotNull(querySource, nameof(querySource));

            return
                (from v in _relationalQueryModelVisitors
                 let selectExpression = v.TryGetQuery(querySource)
                 where selectExpression != null
                 select selectExpression)
                    .First();
        }

        public virtual IQueryMethodProvider QueryMethodProvider
        {
            get { return _queryMethodProvider; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                _queryMethodProvider = value;
            }
        }

        public virtual IMethodCallTranslator CompositeMethodCallTranslator { get; }

        public virtual IMemberTranslator CompositeMemberTranslator { get; }

        public virtual IRelationalValueBufferFactoryFactory ValueBufferFactoryFactory { get; }

        public virtual IRelationalTypeMapper TypeMapper { get; }

        public virtual ISqlQueryGenerator CreateSqlQueryGenerator([NotNull] SelectExpression selectExpression)
            => _sqlQueryGeneratorFactory.Create(selectExpression);

        public virtual IRelationalMetadataExtensionProvider RelationalExtensions { get; }
    }
}
