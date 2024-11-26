using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JitMagic.Models;
using JitMagic.ViewModels;

namespace JitMagic.Views {
	public partial class JitMagic : Form {
		public JitMagic() {
			InitializeComponent();
			vm.CloseWin += (_, _) => { if (!closing) Close(); };
			vm.HideWin += (_,_) => Hide();
			listViewDebuggers.SelectedIndexChanged += ListViewDebuggers_SelectedIndexChanged;
			listViewDebuggers.ItemActivate += OnItemClicked;
			Load += JitMagic_Load;
			Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

		}

		private void JitMagic_Load(object sender, EventArgs e) {
			vm.Loaded();
			Text = vm.WindowTitle;
			Width = vm.WinWidth;
			Height = vm.WinHeight*2;
			labelProcessInformation.Text = vm.AttachText;
			PopulateJitDebuggers();
		}

		private void PopulateJitDebuggers() {
			listViewDebuggers.LargeImageList = new ImageList();
			listViewDebuggers.LargeImageList.ImageSize = new Size(80, 60);
			foreach (var debugger in vm.debuggers){
				listViewDebuggers.LargeImageList.Images.Add(debugger.icon.ToBitmap());
				listViewDebuggers.Items.Add(new ListViewItem(debugger.Name) {
						ImageIndex = listViewDebuggers.Items.Count,
						Tag = debugger
					});
			}
		}

		private void ListViewDebuggers_SelectedIndexChanged(object sender, EventArgs e) {
			vm.selected_debugger = listViewDebuggers.SelectedItems.Count > 0 ? listViewDebuggers.SelectedItems[0].Tag as JitDebugger : null;
		}

		public JITSelectorViewModel vm = new JITSelectorViewModel();
		private void OnItemClicked(object sender, EventArgs e) => vm.Attach();
		

		private void btnAttach_Click(object sender, EventArgs e) => vm.Attach();
		private bool closing;
		protected override void OnClosing(CancelEventArgs e) {
			closing = true;
			vm.Close();
			base.OnClosing(e);
		}

		protected override void OnKeyDown(KeyEventArgs e) {
			if (e.KeyCode == Keys.Escape)
				Close();
			base.OnKeyDown(e);
		}
		private void btnIgnoreAll_Click(object sender, EventArgs e) => vm.IgnoreAll();

		private void btnBlacklistPath_Click(object sender, EventArgs e) => vm.BlacklistApp();

		private void btnRemoveUs_Click(object sender, EventArgs e) => vm.RemoveAsJIT();
	}
}
