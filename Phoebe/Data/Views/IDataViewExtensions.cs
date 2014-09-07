using System;
using System.Collections.Generic;

namespace Toggl.Phoebe.Data.Views
{
    public static class IDataViewExtensions
    {
        public static IEnumerable<T> Filter<T> (this IDataView<T> thisObj, Func<T, bool> filterFunc)
        {
            foreach (var item in thisObj.Data) {
                if (filterFunc == null || filterFunc (item)) {
                    yield return item;
                }
            }
        }
    }
}