using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JitMagic.ViewModels;

namespace JitMagic.Views {
	/// <summary>
	/// Interaction logic for JITSelectorWindow.xaml
	/// </summary>
	public partial class JITSelectorWindow : Window {
		public JITSelectorWindow() {
			InitializeComponent();
			Loaded += JITSelectorWindow_Loaded;
			KeyDown += JITSelectorWindow_KeyDown;
			Closing += JITSelectorWindow_Closing;
			vm.CloseWin += (_, _) => { if (!closing) Close(); };
			vm.HideWin += (_,_) => Hide();
		}


		


		private void JITSelectorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			closing = true;
			vm.Close();
		}

		private bool closing;
		private void JITSelectorWindow_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape)
				Close();
		}

		private void JITSelectorWindow_Loaded(object sender, RoutedEventArgs e) {
			Visibility = Visibility.Visible;
			vm.Loaded();
			if (listDebuggers.HasItems) {
				var firstUIItem = listDebuggers.ItemContainerGenerator.ContainerFromItem(listDebuggers.SelectedItem);
				Keyboard.Focus(firstUIItem as FrameworkElement);
			}

		}
		public JITSelectorViewModel vm => DataContext as JITSelectorViewModel;


		private void ListBox_KeyDown(object sender, KeyEventArgs e) {
			//sopport wrapping to the next line for selection
			var list = sender as ListBox;
			switch (e.Key) {
				case Key.Right:
					if (!list.Items.MoveCurrentToNext())
						list.Items.MoveCurrentToLast();
					break;
				case Key.Left:
					if (!list.Items.MoveCurrentToPrevious())
						list.Items.MoveCurrentToFirst();
					break;
				default:
					return;
			}

			e.Handled = true;
			if (list.SelectedItem != null)
				list.ScrollIntoView(list.SelectedItem);
		}
	}
}
