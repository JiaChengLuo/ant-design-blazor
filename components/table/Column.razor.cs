﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using AntDesign.Core.Reflection;
using AntDesign.Internal;
using AntDesign.TableModels;
using Microsoft.AspNetCore.Components;

namespace AntDesign
{
    public partial class Column<TData> : ColumnBase, IFieldColumn
    {
        [CascadingParameter(Name = "ItemType")]
        public Type ItemType { get; set; }

        [Parameter]
        public EventCallback<TData> FieldChanged { get; set; }

        [Parameter]
        public Expression<Func<TData>> FieldExpression { get; set; }

        [Parameter]
        public RenderFragment<TData> CellRender { get; set; }

        [Parameter]
        public TData Field
        {
            get
            {
                return GetValue != null ? GetValue(RowData) : _field;
            }
            set
            {
                if (GetValue == null)
                {
                    _field = value;
                }
            }
        }

        private TData _field;

        [Parameter]
        public string DataIndex { get; set; }

        [Parameter]
        public string Format { get; set; }

        [Parameter]
        public bool Sortable { get; set; }

        [Parameter]
        public Func<TData, TData, int> SorterCompare { get; set; }

        [Parameter]
        public int SorterMultiple { get; set; }

        [Parameter]
        public bool ShowSorterTooltip { get; set; } = true;

        [Parameter]
        public SortDirection[] SortDirections { get; set; }

        [Parameter]
        public SortDirection DefaultSortOrder { get; set; }

        [Parameter]
        public Func<RowData, Dictionary<string, object>> OnCell { get; set; }

        [Parameter]
        public Func<Dictionary<string, object>> OnHeaderCell { get; set; }

        [Parameter]
        public IEnumerable<TableFilter<TData>> Filters { get; set; }

        [Parameter]
        public bool FilterMultiple { get; set; } = true;

        /// <summary>
        /// Function that determines if the row is displayed when filtered
        /// <para>
        /// Parameter 1: The value of the filter item
        /// </para>
        /// <para>
        /// Parameter 2: The value of the column
        /// </para>
        /// </summary>
        [Parameter]
        public Expression<Func<TData, TData, bool>> OnFilter { get; set; }

        public string DisplayName { get; private set; }

        public string FieldName { get; private set; }

        public ITableSortModel SortModel { get; private set; }

        public ITableFilterModel FilterModel { get; private set; }

        private SortDirection _sortDirection;

        public Func<RowData, TData> GetValue { get; private set; }

        public LambdaExpression GetFieldExpression { get; private set; }

        void IFieldColumn.ClearSorter() => SetSorter(SortDirection.None);

        private static readonly EventCallbackFactory _callbackFactory = new EventCallbackFactory();

        private bool _filterOpened;

        private bool _hasFilterSelected;

        private string[] _selectedFilterValues;

        private ElementReference _filterTriggerRef;

        protected override void OnInitialized()
        {
            base.OnInitialized();

            Sortable = Sortable || SorterMultiple != default || SorterCompare != default || DefaultSortOrder != default || SortDirections?.Any() == true;

            if (IsHeader)
            {
                if (FieldExpression != null)
                {
                    var paramExp = Expression.Parameter(ItemType);
                    var member = ColumnExpressionHelper.GetReturnMemberInfo(FieldExpression);
                    var bodyExp = Expression.MakeMemberAccess(paramExp, member);
                    GetFieldExpression = Expression.Lambda(bodyExp, paramExp);
                }
                else if (DataIndex != null)
                {
                    (_, GetFieldExpression) = ColumnDataIndexHelper<TData>.GetDataIndexConfig(this);
                }

                if (Sortable && GetFieldExpression != null)
                {
                    SortModel = new SortModel<TData>(GetFieldExpression, SorterMultiple, DefaultSortOrder, SorterCompare);
                }

                if (GetFieldExpression != null)
                {
                    var member = ColumnExpressionHelper.GetReturnMemberInfo(GetFieldExpression);
                    DisplayName = member.GetCustomAttribute<DisplayNameAttribute>(true)?.DisplayName ?? member.Name;
                    FieldName = member.Name;
                }
            }
            else if (IsBody)
            {
                SortModel = Context.HeaderColumns[ColIndex] is IFieldColumn fieldColumn ? fieldColumn.SortModel : null;

                (GetValue, _) = ColumnDataIndexHelper<TData>.GetDataIndexConfig(this);
            }

            SortDirections ??= Table.SortDirections;

            Sortable = Sortable || SortModel != null;
            _sortDirection = SortModel?.SortDirection ?? DefaultSortOrder ?? SortDirection.None;

            ClassMapper
               .If("ant-table-column-has-sorters", () => Sortable)
               .If($"ant-table-column-sort", () => Sortable && SortModel != null && SortModel.SortDirection.IsIn(SortDirection.Ascending, SortDirection.Descending));
        }

        private void HandleSort()
        {
            if (Sortable)
            {
                SetSorter(NextSortDirection());
                Table.ColumnSorterChange(this);
            }
        }

        private string SorterTooltip
        {
            get
            {
                var next = NextSortDirection();
                return next?.Value switch
                {
                    0 => Table.Locale.CancelSort,
                    1 => Table.Locale.TriggerAsc,
                    2 => Table.Locale.TriggerDesc,
                    _ => Table.Locale.CancelSort
                };
            }
        }

        private SortDirection NextSortDirection()
        {
            if (_sortDirection == SortDirection.None)
            {
                return SortDirections[0];
            }
            else
            {
                var index = Array.IndexOf(SortDirections, _sortDirection);
                if (index >= SortDirections.Length - 1)
                {
                    return SortDirection.None;
                }

                return SortDirections[index + 1];
            }
        }

        private void SetSorter(SortDirection sortDirection)
        {
            _sortDirection = sortDirection;
            SortModel.SetSortDirection(sortDirection);
            StateHasChanged();
        }

        private void FilterSelected(TableFilter<TData> filter)
        {
            if (!FilterMultiple)
            {
                Filters.ForEach(x => x.Selected = false);
                filter.Selected = true;
            }
            else
            {
                filter.Selected = !filter.Selected;
            }

            _selectedFilterValues = Filters.Where(x => x.Selected).Select(x => x.Value.ToString()).ToArray();
            StateHasChanged();
        }

        private void FilterConfirm()
        {
            _filterOpened = false;
            _hasFilterSelected = Filters?.Any(x => x.Selected) == true;

            FilterModel = _hasFilterSelected ? new FilterModel<TData>(GetFieldExpression, OnFilter, Filters.Where(x => x.Selected).ToList()) : null;

            Table?.ReloadAndInvokeChange();
        }

        private void FilterReset()
        {
            Filters.ForEach(x => x.Selected = false);
            FilterConfirm();
        }
    }
}
