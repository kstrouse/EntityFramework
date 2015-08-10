// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using JetBrains.Annotations;

namespace Microsoft.Data.Entity.Query
{
    public interface IQueryCompiler
    {
        Func<QueryContext, TResult> CompileQuery<TResult>([NotNull] Expression query);
        Func<QueryContext, IAsyncEnumerable<TResult>> CompileAsyncQuery<TResult>([NotNull] Expression query);
    }
}
