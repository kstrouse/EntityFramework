// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query.Methods;
using Microsoft.Data.Entity.Query.Sql;
using Microsoft.Data.Entity.Storage;

namespace Microsoft.Data.Entity.Query
{
    public class RelationalQueryCompilationContextServices
    {
        public RelationalQueryCompilationContextServices(
            [NotNull] IMethodCallTranslator compositeMethodCallTranslator,
            [NotNull] IMemberTranslator compositeMemberTranslator,
            [NotNull] IRelationalValueBufferFactoryFactory valueBufferFactoryFactory,
            [NotNull] IRelationalTypeMapper typeMapper,
            [NotNull] IRelationalMetadataExtensionProvider relationalExtensions,
            [NotNull] ISqlQueryGeneratorFactory sqlQueryGeneratorFactory)
        {
            CompositeMethodCallTranslator = compositeMethodCallTranslator;
            CompositeMemberTranslator = compositeMemberTranslator;
            ValueBufferFactoryFactory = valueBufferFactoryFactory;
            TypeMapper = typeMapper;
            RelationalExtensions = relationalExtensions;
            SqlQueryGeneratorFactory = sqlQueryGeneratorFactory;
        }

        public virtual IMethodCallTranslator CompositeMethodCallTranslator { get; }

        public virtual IMemberTranslator CompositeMemberTranslator { get; }

        public virtual IRelationalValueBufferFactoryFactory ValueBufferFactoryFactory { get; }

        public virtual IRelationalTypeMapper TypeMapper { get; }

        public virtual ISqlQueryGeneratorFactory SqlQueryGeneratorFactory { get; }

        public virtual IRelationalMetadataExtensionProvider RelationalExtensions { get; }
    }
}
