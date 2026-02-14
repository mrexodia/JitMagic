using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JitMagic.Models;
using JitMagic.MVVMLibLite;
using Microsoft.Win32;

namespace JitMagic.ViewModels {
	public class SettingsViewModel : OurViewModelBase {
		private ConfigManager _configManager;
		public SettingsViewModel() {
			// Parameterless constructor for design-time support
		}

		public SettingsViewModel(ConfigManager configManager) {
			_configManager = configManager;
			var config = _configManager.Config;

			// Initialize properties from config
			BlacklistedPaths = new ObservableCollection<string>(config.BlacklistedPaths ?? new List<string>());
			var forceTheme = config.ForcedTheme.ToLower() ?? "";
			if (! String.IsNullOrWhiteSpace(forceTheme)){
				forceTheme = char.ToUpper(forceTheme[0]) + forceTheme.Substring(1);
				ForcedTheme = forceTheme;
			}
			CaptureDebuggerOutputTo = config.CaptureDebuggerOutputTo;
			DefaultIgnoreMinutes = config.DefaultIgnoreMinutes;
			PerformRegisteredCheckOnStart = config.PerformRegisteredCheckOnStart;
			DontKillTargetProcessOnNonDebugExit = config.DontKillTargetProcessOnNonDebugExit;
			DontKillBlacklistedProcesses = config.DontKillBlacklistedProcesses;
			OnProcDieAutoCloseAfterSecs = config.OnProcDieAutoCloseAfterSecs;
			OverrideWidth = config.OverrideWidth;
			OverrideHeight = config.OverrideHeight;
			IgnoreProcessesWithSideBySideFileExtension = config.IgnoreProcessesWithSideBySideFileExtension;
		}

		public ObservableCollection<string> BlacklistedPaths { get; set; }


		public string IgnoreProcessesWithSideBySideFileExtension {
			get; set => Set(ref field, value);
		}


		public string SelectedBlacklistPath {
			get => field;
			set => Set(ref field, value);
		}

		public string ForcedTheme {
			get => field;
			set => Set(ref field, value);
		}

		public string CaptureDebuggerOutputTo {
			get => field;
			set => Set(ref field, value);
		}

		public int DefaultIgnoreMinutes {
			get => field;
			set => Set(ref field, value);
		}
		public bool PerformRegisteredCheckOnStart {
			get => field;
			set => Set(ref field, value);
		}
		public bool DontKillTargetProcessOnNonDebugExit {
			get => field;
			set => Set(ref field, value);
		}
		public bool DontKillBlacklistedProcesses {
			get => field;
			set => Set(ref field, value);
		}
		public int? OnProcDieAutoCloseAfterSecs {
			get => field;
			set => Set(ref field, value);
		}
		public int OverrideWidth {
			get => field;
			set => Set(ref field, value);
		}
		public int OverrideHeight {
			get => field;
			set => Set(ref field, value);
		}

		public event EventHandler CloseRequested;


		public OurCommand SaveCmd => GetOurCmdSync(Save);
		public void Save() {
			var config = _configManager.Config;

			config.BlacklistedPaths = BlacklistedPaths.ToList();
			config.ForcedTheme = ForcedTheme;
			config.CaptureDebuggerOutputTo = CaptureDebuggerOutputTo;
			config.DefaultIgnoreMinutes = DefaultIgnoreMinutes;
			config.PerformRegisteredCheckOnStart = PerformRegisteredCheckOnStart;
			config.DontKillTargetProcessOnNonDebugExit = DontKillTargetProcessOnNonDebugExit;
			config.DontKillBlacklistedProcesses = DontKillBlacklistedProcesses;
			config.OnProcDieAutoCloseAfterSecs = OnProcDieAutoCloseAfterSecs;
			config.OverrideWidth = OverrideWidth;
			config.OverrideHeight = OverrideHeight;
			config.IgnoreProcessesWithSideBySideFileExtension = IgnoreProcessesWithSideBySideFileExtension;

			_configManager.SaveConfig();
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}

		public OurCommand CancelCmd => GetOurCmdSync(Cancel);
		public void Cancel() {
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}

		public OurCommand RemoveBlacklistItemCmd => GetOurCmdSync(RemoveBlacklistItem);
		public void RemoveBlacklistItem() {
			if (!string.IsNullOrEmpty(SelectedBlacklistPath)) {
				BlacklistedPaths.Remove(SelectedBlacklistPath);
			}
		}

		public OurCommand BrowseCaptureFileCmd => GetOurCmdSync(BrowseCaptureFile);
		public void BrowseCaptureFile() {
			var sfd = new SaveFileDialog();
			sfd.Title = "Select File to Capture Debugger Output";
			sfd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
			if (sfd.ShowDialog() == true) {
				CaptureDebuggerOutputTo = sfd.FileName;
			}
		}
	}
}
