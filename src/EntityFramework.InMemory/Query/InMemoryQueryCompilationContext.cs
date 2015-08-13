// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Query
{
    public class InMemoryQueryCompilationContext : QueryCompilationContext
    {
        public InMemoryQueryCompilationContext(
            [NotNull] QueryCompilationContextServices services,
            [NotNull] ILoggerFactory loggerFactory)
            : base(
                Check.NotNull(services, nameof(services)),
                Check.NotNull(loggerFactory, nameof(loggerFactory)))
        {
        }

        public override EntityQueryModelVisitor CreateQueryModelVisitor(EntityQueryModelVisitor parentEntityQueryModelVisitor)
            => new InMemoryQueryModelVisitor(this);
    }
}
