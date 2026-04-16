using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Helpers
{
    public class PropertyChangeBatch : IDisposable
    {
        private readonly ObservableObject _target;
        private readonly List<Action> _actions = new();

        public PropertyChangeBatch(ObservableObject target)
        {
            _target = target;
        }

        public void SetProperty<T>(Expression<Func<T>> propertyExpression, T value)
        {
            _actions.Add(() => {
                var memberExpression = (MemberExpression)propertyExpression.Body;
                var propertyInfo = (PropertyInfo)memberExpression.Member;
                propertyInfo.SetValue(_target, value);
            });
        }

        public void Dispose()
        {
            foreach (var action in _actions)
                action();
        }
    }
}
