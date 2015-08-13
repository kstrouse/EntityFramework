// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.ExpressionVisitors;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;

namespace Microsoft.Data.Entity.Query
{
    public class RelationalQueryModelVisitor : EntityQueryModelVisitor
    {
        private readonly Dictionary<IQuerySource, SelectExpression> _queriesBySource
            = new Dictionary<IQuerySource, SelectExpression>();

        private readonly Dictionary<IQuerySource, SelectExpression> _subqueriesBySource
            = new Dictionary<IQuerySource, SelectExpression>();

        private readonly Dictionary<IQuerySource, RelationalQueryModelVisitor> _subQueryModelVisitorsBySource
            = new Dictionary<IQuerySource, RelationalQueryModelVisitor>();

        private bool _requiresClientProjection;
        private bool _requiresClientFilter;
        private bool _requiresClientResultOperator;
        private bool _bindParentQueries;

        private Dictionary<IncludeSpecification, List<int>> _navigationIndexMap = new Dictionary<IncludeSpecification, List<int>>();

        public RelationalQueryModelVisitor(
            [NotNull] RelationalQueryCompilationContext queryCompilationContext,
            [CanBeNull] RelationalQueryModelVisitor parentQueryModelVisitor)
            : base(Check.NotNull(queryCompilationContext, nameof(queryCompilationContext)))
        {
            ParentQueryModelVisitor = parentQueryModelVisitor;
        }

        public virtual bool RequiresClientEval { get; set; }
        public virtual bool RequiresClientSelectMany { get; set; }
        public virtual bool RequiresClientFilter => _requiresClientFilter || RequiresClientEval;
        public virtual bool RequiresClientProjection
        {
            get { return _requiresClientProjection || RequiresClientEval; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                _requiresClientProjection = value;
            }
        }

        public virtual bool RequiresClientResultOperator
        {
            get { return _requiresClientResultOperator || RequiresClientEval; }
            set { _requiresClientResultOperator = value; }
        }

        public new virtual RelationalQueryCompilationContext QueryCompilationContext
            => (RelationalQueryCompilationContext)base.QueryCompilationContext;

        public virtual ICollection<SelectExpression> Queries => _queriesBySource.Values;

        public virtual RelationalQueryModelVisitor ParentQueryModelVisitor { get; }


        public override Func<QueryContext, IEnumerable<TResult>> CreateQueryExecutor<TResult>([NotNull] QueryModel queryModel)
        {
            return base.CreateQueryExecutor<TResult>(queryModel);
        }

        public override Func<QueryContext, IAsyncEnumerable<TResult>> CreateAsyncQueryExecutor<TResult>([NotNull] QueryModel queryModel)
        {
            return base.CreateAsyncQueryExecutor<TResult>(queryModel);
        }

        public virtual void RegisterSubQueryVisitor(
            [NotNull] IQuerySource querySource, [NotNull] RelationalQueryModelVisitor queryModelVisitor)
        {
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(queryModelVisitor, nameof(queryModelVisitor));

            _subQueryModelVisitorsBySource.Add(querySource, queryModelVisitor);
        }

        public virtual void AddQuery([NotNull] IQuerySource querySource, [NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(selectExpression, nameof(selectExpression));

            _queriesBySource.Add(querySource, selectExpression);
        }

        public virtual void AddSubquery([NotNull] IQuerySource querySource, [NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(querySource, nameof(querySource));
            Check.NotNull(selectExpression, nameof(selectExpression));

            _subqueriesBySource.Add(querySource, selectExpression);
        }

        public virtual SelectExpression TryGetQuery([NotNull] IQuerySource querySource)
        {
            Check.NotNull(querySource, nameof(querySource));

            SelectExpression selectExpression;
            return (_queriesBySource.TryGetValue(querySource, out selectExpression)
                ? selectExpression
                : _queriesBySource.Values.SingleOrDefault(se => se.HandlesQuerySource(querySource)));
        }

        public override void VisitQueryModel(QueryModel queryModel)
        {
            Check.NotNull(queryModel, nameof(queryModel));

            base.VisitQueryModel(queryModel);

            var compositePredicateVisitor
                = new CompositePredicateExpressionVisitor(
                    QueryCompilationContext
                        .GetCustomQueryAnnotations(RelationalQueryableExtensions.UseRelationalNullSemanticsMethodInfo)
                        .Any());

            foreach (var selectExpression in _queriesBySource.Values.Where(se => se.Predicate != null))
            {
                selectExpression.Predicate
                    = compositePredicateVisitor.Visit(selectExpression.Predicate);
            }
        }

        public virtual void VisitSubQueryModel([NotNull] QueryModel queryModel)
        {
            _bindParentQueries = true;

            VisitQueryModel(queryModel);
        }

        protected override void IncludeNavigations(
            QueryModel queryModel,
            IReadOnlyCollection<IncludeSpecification> includeSpecifications)
        {
            Check.NotNull(queryModel, nameof(queryModel));
            Check.NotNull(includeSpecifications, nameof(includeSpecifications));

            _navigationIndexMap = BuildNavigationIndexMap(includeSpecifications);

            base.IncludeNavigations(queryModel, includeSpecifications);
        }

        private static Dictionary<IncludeSpecification, List<int>> BuildNavigationIndexMap(
            IEnumerable<IncludeSpecification> includeSpecifications)
        {
            var openedReaderCount = 0;
            var navigationIndexMap = new Dictionary<IncludeSpecification, List<int>>();

            foreach (var includeSpecification in includeSpecifications.Reverse())
            {
                var indexes = new List<int>();
                var openedNewReader = false;

                foreach (var navigation in includeSpecification.NavigationPath)
                {
                    if (navigation.IsCollection())
                    {
                        openedNewReader = true;
                        openedReaderCount++;
                    }
                    else
                    {
                        var index = openedNewReader ? openedReaderCount : 0;
                        indexes.Add(index);
                    }
                }

                navigationIndexMap.Add(includeSpecification, indexes);
            }

            return navigationIndexMap;
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

            Expression
                = new IncludeExpressionVisitor(
                    includeSpecification.QuerySource,
                    includeSpecification.NavigationPath,
                    QueryCompilationContext,
                    _navigationIndexMap[includeSpecification],
                    querySourceRequiresTracking)
                    .Visit(Expression);
        }

        protected override Expression CompileMainFromClauseExpression(
            MainFromClause mainFromClause, QueryModel queryModel)
        {
            Check.NotNull(mainFromClause, nameof(mainFromClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var expression = base.CompileMainFromClauseExpression(mainFromClause, queryModel);

            return LiftSubQuery(mainFromClause, mainFromClause.FromExpression, queryModel, expression);
        }

        public override void VisitAdditionalFromClause(AdditionalFromClause fromClause, QueryModel queryModel, int index)
        {
            Check.NotNull(fromClause, nameof(fromClause));
            Check.NotNull(queryModel, nameof(queryModel));

            base.VisitAdditionalFromClause(fromClause, queryModel, index);

            RequiresClientSelectMany = true;

            var selectExpression = TryGetQuery(fromClause);

            if (selectExpression != null)
            {
                var previousQuerySource = FindPreviousQuerySource(queryModel, index);

                if (previousQuerySource != null)
                {
                    var previousSelectExpression = TryGetQuery(previousQuerySource);

                    if (previousSelectExpression != null)
                    {
                        var readerOffset = previousSelectExpression.Projection.Count;

                        previousSelectExpression
                            .AddCrossJoin(selectExpression.Tables.Single(), selectExpression.Projection);

                        _queriesBySource.Remove(fromClause);

                        Expression
                            = new QueryFlatteningExpressionVisitor(
                                previousQuerySource,
                                fromClause,
                                QueryCompilationContext,
                                readerOffset,
                                QueryCompilationContext.LinqOperatorProvider.SelectMany)
                                .Visit(Expression);

                        RequiresClientSelectMany = false;
                    }
                }
            }
        }

        private IQuerySource FindPreviousQuerySource(QueryModel queryModel, int index)
        {
            for (var i = index; i >= 0; i--)
            {
                var candidate = i == 0
                    ? queryModel.MainFromClause
                    : queryModel.BodyClauses[i - 1] as IQuerySource;

                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        protected override Expression CompileAdditionalFromClauseExpression(
            AdditionalFromClause additionalFromClause, QueryModel queryModel)
        {
            Check.NotNull(additionalFromClause, nameof(additionalFromClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var expression = base.CompileAdditionalFromClauseExpression(additionalFromClause, queryModel);

            return LiftSubQuery(additionalFromClause, additionalFromClause.FromExpression, queryModel, expression);
        }

        public override void VisitJoinClause(JoinClause joinClause, QueryModel queryModel, int index)
        {
            Check.NotNull(joinClause, nameof(joinClause));
            Check.NotNull(queryModel, nameof(queryModel));

            OptimizeJoinClause(
                joinClause,
                queryModel,
                index,
                () => base.VisitJoinClause(joinClause, queryModel, index),
                QueryCompilationContext.LinqOperatorProvider.Join);
        }

        protected override Expression CompileJoinClauseInnerSequenceExpression(JoinClause joinClause, QueryModel queryModel)
        {
            Check.NotNull(joinClause, nameof(joinClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var expression = base.CompileJoinClauseInnerSequenceExpression(joinClause, queryModel);

            return LiftSubQuery(joinClause, joinClause.InnerSequence, queryModel, expression);
        }

        public override void VisitGroupJoinClause(GroupJoinClause groupJoinClause, QueryModel queryModel, int index)
        {
            Check.NotNull(groupJoinClause, nameof(groupJoinClause));
            Check.NotNull(queryModel, nameof(queryModel));

            OptimizeJoinClause(
                groupJoinClause.JoinClause,
                queryModel,
                index,
                () => base.VisitGroupJoinClause(groupJoinClause, queryModel, index),
                QueryCompilationContext.LinqOperatorProvider.GroupJoin,
                outerJoin: true);
        }

        protected virtual void OptimizeJoinClause(
            [NotNull] JoinClause joinClause,
            [NotNull] QueryModel queryModel,
            int index,
            Action baseVisitAction,
            [NotNull] MethodInfo operatorToFlatten,
            bool outerJoin = false)
        {
            Check.NotNull(joinClause, nameof(joinClause));
            Check.NotNull(queryModel, nameof(queryModel));
            Check.NotNull(operatorToFlatten, nameof(operatorToFlatten));

            var previousQuerySource = FindPreviousQuerySource(queryModel, index);

            var previousSelectExpression
                = previousQuerySource != null
                    ? TryGetQuery(previousQuerySource)
                    : null;

            var previousSelectProjectionCount
                = previousSelectExpression?.Projection.Count ?? -1;

            baseVisitAction();

            if (previousSelectExpression != null)
            {
                var selectExpression = TryGetQuery(joinClause);

                if (selectExpression != null)
                {
                    var filteringExpressionVisitor
                        = new SqlTranslatingExpressionVisitor(this, targetSelectExpression: null);

                    var predicate
                        = filteringExpressionVisitor
                            .Visit(
                                Expression.Equal(
                                    joinClause.OuterKeySelector,
                                    joinClause.InnerKeySelector));

                    if (predicate != null)
                    {
                        _queriesBySource.Remove(joinClause);

                        previousSelectExpression.RemoveRangeFromProjection(previousSelectProjectionCount);

                        var tableExpression = selectExpression.Tables.Single();

                        var projection
                            = QueryCompilationContext
                                .QuerySourceRequiresMaterialization(joinClause)
                                ? selectExpression.Projection
                                : Enumerable.Empty<Expression>();

                        var joinExpression
                            = !outerJoin
                                ? previousSelectExpression.AddInnerJoin(tableExpression, projection)
                                : previousSelectExpression.AddOuterJoin(tableExpression, projection);

                        joinExpression.Predicate = predicate;

                        Expression
                            = new QueryFlatteningExpressionVisitor(
                                previousQuerySource,
                                joinClause,
                                QueryCompilationContext,
                                previousSelectProjectionCount,
                                operatorToFlatten)
                                .Visit(Expression);
                    }
                    else
                    {
                        previousSelectExpression
                            .RemoveRangeFromProjection(previousSelectProjectionCount);
                    }
                }
            }
        }

        protected override Expression CompileGroupJoinInnerSequenceExpression(GroupJoinClause groupJoinClause, QueryModel queryModel)
        {
            Check.NotNull(groupJoinClause, nameof(groupJoinClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var expression = base.CompileGroupJoinInnerSequenceExpression(groupJoinClause, queryModel);

            return LiftSubQuery(groupJoinClause.JoinClause, groupJoinClause.JoinClause.InnerSequence, queryModel, expression);
        }

        private Expression LiftSubQuery(
            IQuerySource querySource, Expression itemsExpression, QueryModel queryModel, Expression expression)
        {
            SelectExpression subSelectExpression;
            if (_subqueriesBySource.TryGetValue(querySource, out subSelectExpression)
                && (!subSelectExpression.OrderBy.Any()
                    || subSelectExpression.Limit != null))
            {
                subSelectExpression.PushDownSubquery().QuerySource = querySource;

                AddQuery(querySource, subSelectExpression);

                _subqueriesBySource.Remove(querySource);

                var shapedQueryMethodExpression
                    = new ShapedQueryFindingExpressionVisitor(QueryCompilationContext)
                        .Find(expression);

                var shaperLambda = (LambdaExpression)shapedQueryMethodExpression.Arguments[2];
                var shaperMethodCall = (MethodCallExpression)shaperLambda.Body;

                var shaperMethod = shaperMethodCall.Method;
                var shaperMethodArgs = shaperMethodCall.Arguments.ToList();

                if (!QueryCompilationContext.QuerySourceRequiresMaterialization(querySource)
                    && shaperMethod.MethodIsClosedFormOf(RelationalEntityQueryableExpressionVisitor.CreateEntityMethodInfo))
                {
                    shaperMethod = RelationalEntityQueryableExpressionVisitor.CreateValueBufferMethodInfo;
                    shaperMethodArgs.RemoveRange(5, 5);
                }
                else
                {
                    subSelectExpression.ExplodeStarProjection();
                }

                var innerQuerySource = (IQuerySource)((ConstantExpression)shaperMethodArgs[0]).Value;

                foreach (var queryAnnotation
                    in QueryCompilationContext.QueryAnnotations
                        .Where(qa => qa.QuerySource == innerQuerySource))
                {
                    queryAnnotation.QuerySource = querySource;
                }

                shaperMethodArgs[0] = Expression.Constant(querySource);

                var querySourceReferenceExpression
                    = queryModel.SelectClause.Selector as QuerySourceReferenceExpression;

                if (querySourceReferenceExpression != null
                    && querySourceReferenceExpression.ReferencedQuerySource == querySource)
                {
                    var subQueryModel = (itemsExpression as SubQueryExpression)?.QueryModel;

                    if (subQueryModel != null)
                    {
                        queryModel.SelectClause.Selector = subQueryModel.SelectClause.Selector;

                        QueryCompilationContext.QuerySourceMapping
                            .ReplaceMapping(
                                subQueryModel.MainFromClause,
                                QueryResultScope.GetResult(
                                    QueryResultScopeParameter,
                                    querySource,
                                    shaperMethod.ReturnType.GenericTypeArguments[0]));
                    }
                }

                return Expression.Call(
                    QueryCompilationContext.QueryMethodProvider.ShapedQueryMethod
                        .MakeGenericMethod(shaperMethod.ReturnType),
                    shapedQueryMethodExpression.Arguments[0],
                    shapedQueryMethodExpression.Arguments[1],
                    Expression.Lambda(
                        Expression.Call(shaperMethod, shaperMethodArgs),
                        shaperLambda.Parameters[0]));
            }

            return expression;
        }

        public override void VisitWhereClause(WhereClause whereClause, QueryModel queryModel, int index)
        {
            Check.NotNull(whereClause, nameof(whereClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var selectExpression = TryGetQuery(queryModel.MainFromClause);
            var requiresClientFilter = selectExpression == null;

            if (!requiresClientFilter)
            {
                var sqlTranslatingExpressionVisitor
                    = new SqlTranslatingExpressionVisitor(
                        this, selectExpression, whereClause.Predicate, _bindParentQueries);

                var sqlPredicateExpression = sqlTranslatingExpressionVisitor.Visit(whereClause.Predicate);

                if (sqlPredicateExpression != null)
                {
                    selectExpression.Predicate
                        = selectExpression.Predicate == null
                            ? sqlPredicateExpression
                            : Expression.AndAlso(selectExpression.Predicate, sqlPredicateExpression);
                }
                else
                {
                    requiresClientFilter = true;
                }

                if (sqlTranslatingExpressionVisitor.ClientEvalPredicate != null
                    && selectExpression.Predicate != null)
                {
                    requiresClientFilter = true;
                    whereClause = new WhereClause(sqlTranslatingExpressionVisitor.ClientEvalPredicate);
                }
            }

            _requiresClientFilter |= requiresClientFilter;

            if (RequiresClientFilter)
            {
                base.VisitWhereClause(whereClause, queryModel, index);
            }
        }

        public override void VisitOrderByClause(OrderByClause orderByClause, QueryModel queryModel, int index)
        {
            Check.NotNull(orderByClause, nameof(orderByClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var selectExpression = TryGetQuery(queryModel.MainFromClause);
            var requiresClientOrderBy = selectExpression == null;

            if (!requiresClientOrderBy)
            {
                var sqlTranslatingExpressionVisitor
                    = new SqlTranslatingExpressionVisitor(
                        this, selectExpression, bindParentQueries: _bindParentQueries);

                var orderings = new List<Ordering>();

                foreach (var ordering in orderByClause.Orderings)
                {
                    var sqlOrderingExpression
                        = sqlTranslatingExpressionVisitor
                            .Visit(ordering.Expression);

                    if (sqlOrderingExpression == null)
                    {
                        break;
                    }

                    orderings.Add(
                        new Ordering(
                            sqlOrderingExpression,
                            ordering.OrderingDirection));
                }

                if (orderings.Count == orderByClause.Orderings.Count)
                {
                    selectExpression.PrependToOrderBy(orderings);
                }
                else
                {
                    requiresClientOrderBy = true;
                }
            }

            if (RequiresClientEval || requiresClientOrderBy)
            {
                base.VisitOrderByClause(orderByClause, queryModel, index);
            }
        }

        public override Expression BindMemberToValueBuffer(MemberExpression memberExpression, Expression expression)
        {
            Check.NotNull(memberExpression, nameof(memberExpression));
            Check.NotNull(expression, nameof(expression));

            return BindMemberExpression(
                memberExpression,
                (property, querySource, selectExpression) =>
                    {
                        var projectionIndex = selectExpression.GetProjectionIndex(property, querySource);

                        Debug.Assert(projectionIndex > -1);

                        return BindReadValueMethod(memberExpression.Type, expression, projectionIndex);
                    },
                bindSubQueries: true);
        }

        public override Expression BindMethodCallToValueBuffer(
            MethodCallExpression methodCallExpression, Expression expression)
        {
            Check.NotNull(methodCallExpression, nameof(methodCallExpression));
            Check.NotNull(expression, nameof(expression));

            return BindMethodCallExpression(
                methodCallExpression,
                (property, querySource, selectExpression) =>
                    {
                        var projectionIndex = selectExpression.GetProjectionIndex(property, querySource);

                        Debug.Assert(projectionIndex > -1);

                        return BindReadValueMethod(methodCallExpression.Type, expression, projectionIndex);
                    },
                bindSubQueries: true)
                   ?? ParentQueryModelVisitor?
                       .BindMethodCallToValueBuffer(methodCallExpression, expression);
        }

        public virtual TResult BindMemberExpression<TResult>(
            [NotNull] MemberExpression memberExpression,
            [NotNull] Func<IProperty, IQuerySource, SelectExpression, TResult> memberBinder,
            bool bindSubQueries = false)
        {
            Check.NotNull(memberExpression, nameof(memberExpression));
            Check.NotNull(memberBinder, nameof(memberBinder));

            return BindMemberExpression(memberExpression, null, memberBinder, bindSubQueries);
        }

        private TResult BindMemberExpression<TResult>(
            [NotNull] MemberExpression memberExpression,
            [CanBeNull] IQuerySource querySource,
            Func<IProperty, IQuerySource, SelectExpression, TResult> memberBinder,
            bool bindSubQueries)
        {
            Check.NotNull(memberExpression, nameof(memberExpression));
            Check.NotNull(memberBinder, nameof(memberBinder));

            return base.BindMemberExpression(memberExpression, querySource,
                (property, qs) => BindMemberOrMethod(memberBinder, qs, property, bindSubQueries));
        }

        public virtual TResult BindMethodCallExpression<TResult>(
            [NotNull] MethodCallExpression methodCallExpression,
            [NotNull] Func<IProperty, IQuerySource, SelectExpression, TResult> memberBinder,
            bool bindSubQueries = false)
        {
            Check.NotNull(methodCallExpression, nameof(methodCallExpression));
            Check.NotNull(memberBinder, nameof(memberBinder));

            return BindMethodCallExpression(methodCallExpression, null, memberBinder, bindSubQueries);
        }

        private TResult BindMethodCallExpression<TResult>(
            MethodCallExpression methodCallExpression,
            IQuerySource querySource,
            Func<IProperty, IQuerySource, SelectExpression, TResult> memberBinder,
            bool bindSubQueries)
        {
            return base.BindMethodCallExpression(methodCallExpression, querySource,
                (property, qs) => BindMemberOrMethod(memberBinder, qs, property, bindSubQueries));
        }

        private TResult BindMemberOrMethod<TResult>(
            Func<IProperty, IQuerySource, SelectExpression, TResult> memberBinder,
            IQuerySource querySource,
            IProperty property,
            bool bindSubQueries)
        {
            if (querySource != null)
            {
                var selectExpression = TryGetQuery(querySource);

                if (selectExpression == null
                    && bindSubQueries)
                {
                    RelationalQueryModelVisitor subQueryModelVisitor;
                    if (_subQueryModelVisitorsBySource.TryGetValue(querySource, out subQueryModelVisitor))
                    {
                        selectExpression = subQueryModelVisitor.Queries.Single();

                        selectExpression
                            .AddToProjection(
                                QueryCompilationContext
                                    .RelationalServices
                                    .RelationalExtensions
                                    .For(property).ColumnName,
                                property,
                                querySource);
                    }
                }

                if (selectExpression != null)
                {
                    return memberBinder(property, querySource, selectExpression);
                }

                selectExpression
                    = ParentQueryModelVisitor?.TryGetQuery(querySource);

                selectExpression?.AddToProjection(
                    QueryCompilationContext
                        .RelationalServices
                        .RelationalExtensions
                        .For(property).ColumnName,
                    property,
                    querySource);
            }

            return default(TResult);
        }
    }
}
