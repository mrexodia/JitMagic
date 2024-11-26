using System;
using System.Collections.Generic;
using System.ComponentModel;
namespace JitMagic.MVVMLibLite {

	public abstract class ObservableClass : MVVMSObservableObject {
		protected virtual void RaisePropertyChanged<T>(string propertyName, T oldValue, T newValue) {
			if (string.IsNullOrEmpty(propertyName))
				throw new ArgumentException("This method cannot be called with an empty string", "propertyName");
			base.RaisePropertyChanged(propertyName);
		}
		protected virtual void RaisePropertyChanged<T>(System.Linq.Expressions.Expression<Func<T>> propertyExpression, T oldValue, T newValue) {
			var propertyChangedHandler = PropertyChangedHandler;
			if (propertyChangedHandler == null)
				return;
			string propertyName = GetPropertyName(propertyExpression);
			propertyChangedHandler(this, new PropertyChangedEventArgs(propertyName));
		}
		protected new bool Set<T>(System.Linq.Expressions.Expression<Func<T>> propertyExpression, ref T field, T newValue) {
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;
			T oldValue = field;
			field = newValue;

			RaisePropertyChanged(propertyExpression, oldValue, field);
			return true;
		}
		protected new bool Set<T>(string propertyName, ref T field, T newValue) {
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;
			T oldValue = field;
			field = newValue;
			RaisePropertyChanged(propertyName, oldValue, field);
			return true;
		}
	}
}
