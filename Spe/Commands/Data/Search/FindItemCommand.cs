using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Utilities;
using Spe.Core.Extensions;
using Spe.Core.Validation;

namespace Spe.Commands.Data.Search
{
    [Cmdlet(VerbsCommon.Find, "Item", DefaultParameterSetName = "Criteria")]
    [OutputType(typeof(SearchResultItem))]
    public class FindItemCommand : BaseSearchCommand
    {
        public static string[] Indexes
        {
            get { return ContentSearchManager.Indexes.Select(i => i.Name).ToArray(); }
        }

        [AutocompleteSet(nameof(Indexes))]
        [Parameter(Mandatory = true, Position = 0)]
        public string Index { get; set; }

        [Parameter(ParameterSetName = "Criteria")]
        public SearchCriteria[] Criteria { get; set; }

        [Parameter(ParameterSetName = "Dynamic")]
        public string Where { get; set; }

        [Parameter(ParameterSetName = "Dynamic")]
        public object[] WhereValues { get; set; }

        [Parameter(ParameterSetName = "Predicate")]
        public Expression<Func<SearchResultItem, bool>> Predicate { get; set; }

        [Parameter(ParameterSetName = "ScopeQuery")]
        public string ScopeQuery { get; set; }

        [Parameter]
        public string OrderBy { get; set; }

        [Parameter]
        public int First { get; set; }

        [Parameter]
        public int Last { get; set; }

        [Parameter]
        public int Skip { get; set; }

        protected override void EndProcessing()
        {
            string index = string.IsNullOrEmpty(Index) ? "sitecore_master_index" : Index;

            using (var context = ContentSearchManager.GetIndex(index).CreateSearchContext())
            {
                // get all items in medialibrary
                var query = context.GetQueryable<SearchResultItem>();

                if (!string.IsNullOrEmpty(Where))
                {
                    // ReSharper disable once AccessToModifiedClosure
                    query = query.Where(Where, WhereValues.BaseArray());
                }

                if (Criteria != null)
                {
                    var predicate = ProcessCriteria(Criteria, SearchOperation.And);

                    query = query.Where(predicate);
                }

                if (Predicate != null)
                {
                    query = query.Where(Predicate);
                }

                if (ScopeQuery != null)
                {
                    query = ProcessScopeQuery(context, ScopeQuery);
                }

                if (!string.IsNullOrEmpty(OrderBy))
                {
                    query = OrderIfSupported(query);
                }

                WriteObject(FilterByPosition(query), true);
            }
        }

        private IQueryable<SearchResultItem> OrderIfSupported(IQueryable<SearchResultItem> query)
        {
            query = query.OrderBy(OrderBy);
            return query;
        }

        private List<SearchResultItem> FilterByPosition(IQueryable<SearchResultItem> query)
        {
            var count = query.Count();
            var skipEnd = (Last != 0 && First == 0);
            var skipFirst = skipEnd ? 0 : Skip;
            var takeFirst = First;
            if (Last == 0 && First == 0)
            {
                takeFirst = count - skipFirst;
            }
            var firstObjects = query.Skip(skipFirst).Take(takeFirst);
            var takenAndSkipped = (skipFirst + takeFirst);
            if (takenAndSkipped >= count || Last == 0 || (skipEnd && Skip >= (count - takenAndSkipped)))
            {
                return firstObjects.ToList();
            }
            var takeAndSkipAtEnd = Last + (skipEnd ? Skip : 0);
            var skipBeforeEnd = count - takenAndSkipped - takeAndSkipAtEnd;
            var takeLast = Last;
            if (skipBeforeEnd >= 0)
            {
                // Concat not support by Sitecore.
                return firstObjects.ToList().Concat(query.Skip(takenAndSkipped + skipBeforeEnd).Take(takeLast)).ToList();
            }
            takeLast += skipBeforeEnd;
            skipBeforeEnd = 0;
            // Concat not support by Sitecore.
            return firstObjects.ToList().Concat(query.Skip(takenAndSkipped + skipBeforeEnd).Take(takeLast)).ToList();
        }
    }
}