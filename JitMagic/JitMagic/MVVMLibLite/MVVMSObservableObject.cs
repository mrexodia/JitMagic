using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace JitMagic.MVVMLibLite {

	//modified from https://raw.githubusercontent.com/lbugnion/mvvmlight/b23c4d5bf6df654ad885be26ea053fb0efa04973/GalaSoft.MvvmLight/GalaSoft.MvvmLight%20(PCL)/ObservableObject.cs
	/* Original copyright */
	// ****************************************************************************
	// <copyright file="ObservableObject.cs" company="GalaSoft Laurent Bugnion">
	// Copyright © GalaSoft Laurent Bugnion 2011-2016
	// </copyright>
	// ****************************************************************************
	// <author>Laurent Bugnion</author>
	// <email>laurent@galasoft.ch</email>
	// <date>10.4.2011</date>
	// <project>GalaSoft.MvvmLight.Messaging</project>
	// <web>http://www.mvvmlight.net</web>
	// <license>
	// See license.txt in this project or http://www.galasoft.ch/license_MIT.txt
	// </license>
	// ****************************************************************************

	public class MVVMSObservableObject : INotifyPropertyChanged {
		public event PropertyChangedEventHandler PropertyChanged;
		protected PropertyChangedEventHandler PropertyChangedHandler => PropertyChanged;

		public virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
			var args = new PropertyChangedEventArgs(propertyName);
			PropertyChanged?.Invoke(this, args);

		}
		public virtual void RaisePropertyChanged<T>(Expression<Func<T>> propertyExpression) {
			var handler = PropertyChanged;

			if (handler != null) {
				var propertyName = GetPropertyName(propertyExpression);
				if (!string.IsNullOrWhiteSpace(propertyName))
					RaisePropertyChanged(propertyName);

			}
		}

		protected static string GetPropertyName<T>(Expression<Func<T>> propertyExpression) {
			var body = propertyExpression.Body as MemberExpression;
			var property = body.Member as PropertyInfo;
			return property.Name;
		}
		internal static string IntGetPropertyName<T>(Expression<Func<T>> propertyExpression) => GetPropertyName(propertyExpression);

		protected bool Set<T>(Expression<Func<T>> propertyExpression, ref T field, T newValue) {
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;

			field = newValue;
			RaisePropertyChanged(propertyExpression);
			return true;
		}
		protected bool Set<T>(string propertyName, ref T field, T newValue) {
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;

			field = newValue;
			RaisePropertyChanged(propertyName);
			return true;
		}

		protected bool Set<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null) => Set(propertyName, ref field, newValue);

		protected bool Set<T>(ref T field, T newValue, Expression<Func<T>> propertyExpression) => Set(propertyExpression, ref field, newValue);

	}
}
