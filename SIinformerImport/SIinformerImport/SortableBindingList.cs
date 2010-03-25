using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Linq.Expressions;

namespace SIinformer
{
        public class MySortableBindingList<T> : BindingList<T>
        {

            ListSortDirection sortDirection;
            PropertyDescriptor sortProperty;

            // a cache of functions that perform the sorting
            // for a given type, property, and sort direction
            static Dictionary<string, Func<IList<T>, IEnumerable<T>>>
               cachedOrderByExpressions = new Dictionary<string, Func<IList<T>,
                                                         IEnumerable<T>>>();

            public void Sort(string PropertyName, ListSortDirection direction)
            {
                PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
                System.ComponentModel.PropertyDescriptor myProperty = properties.Find(PropertyName, false);
                if (myProperty != null) ((IBindingList)this).ApplySort(myProperty, direction);
            }

            protected override void ApplySortCore(PropertyDescriptor prop,
                                    ListSortDirection direction)
            {
                /*
                 Look for an appropriate sort method in the cache if not found .
                 Call CreateOrderByMethod to create one. 
                 Apply it to the original list.
                 Notify any bound controls that the sort has been applied.
                 */

                sortProperty = prop;
                sortDirection = direction;

                var orderByMethodName = sortDirection ==
                    ListSortDirection.Ascending ? "OrderBy" : "OrderByDescending";

                var cacheKey = typeof(T).GUID + prop.Name + orderByMethodName;

                if (!cachedOrderByExpressions.ContainsKey(cacheKey))
                {
                    CreateOrderByMethod(prop, orderByMethodName, cacheKey);
                }
                IEnumerable<T> sorted = cachedOrderByExpressions[cacheKey](this);

                ResetItems(sorted.ToList<T>());

                ResetBindings();
            }

            private void CreateOrderByMethod(PropertyDescriptor prop,
                         string orderByMethodName, string cacheKey)
            {

                /*
                 Create a generic method implementation for IEnumerable<T>.
                 Cache it.
                */

                var sourceParameter = Expression.Parameter(typeof(IList<T>), "source");
                var lambdaParameter = Expression.Parameter(typeof(T), "lambdaParameter");
                var accesedMember = typeof(T).GetProperty(prop.Name);
                var propertySelectorLambda =
                    Expression.Lambda(Expression.MakeMemberAccess(lambdaParameter,
                                      accesedMember), lambdaParameter);
                var orderByMethod = typeof(Enumerable).GetMethods()
                                              .Where(a => a.Name == orderByMethodName &&
                                                           a.GetParameters().Length == 2)
                                              .Single()
                                              .MakeGenericMethod(typeof(T), prop.PropertyType);

                var orderByExpression = Expression.Lambda<Func<IList<T>, IEnumerable<T>>>(
                                            Expression.Call(orderByMethod,
                                                    new Expression[] { sourceParameter, 
                                                               propertySelectorLambda }),
                                                    sourceParameter);

                cachedOrderByExpressions.Add(cacheKey, orderByExpression.Compile());
            }

            private void ResetItems(IEnumerable<T> items)
            {

                base.ClearItems();
                foreach (T it in items)
                {
                    Add(it);
                }
            }

            protected override bool SupportsSortingCore
            {
                get
                {
                    // indeed we do
                    return true;
                }
            }
    }
}
