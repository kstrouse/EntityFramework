// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.Data.Entity.Query.Sql
{
    public class SqliteQuerySqlGeneratorFactory : ISqlQueryGeneratorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SqliteQuerySqlGeneratorFactory([NotNull] IServiceProvider serviceProvider)
        {
            Check.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }
        public virtual ISqlQueryGenerator CreateGenerator([NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            var querySqlGenerator = _serviceProvider.GetService<SqliteQuerySqlGenerator>();
            querySqlGenerator.SelectExpression = selectExpression;

            return querySqlGenerator;
        }

        public virtual ISqlQueryGenerator CreateRawCommandGenerator(
            [NotNull] SelectExpression selectExpression,
            [NotNull] string sql,
            [NotNull] object[] parameters)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            Check.NotNull(sql, nameof(sql));
            Check.NotNull(parameters, nameof(parameters));

            var querySqlGenerator = _serviceProvider.GetService<RawSqlQueryGenerator>();

            querySqlGenerator.Initialize(selectExpression, sql, parameters);

            return querySqlGenerator;
        }
    }
}
