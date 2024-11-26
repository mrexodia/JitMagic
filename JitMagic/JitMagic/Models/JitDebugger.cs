using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
#if WPF
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace JitMagic.Models {
	[JsonConverter(typeof(StringEnumConverter))]
	public enum Architecture {
		x64 = 1 << 0,
		x86 = 1 << 1,
		All = x86 | x64
	}
	public class JitDebugger {
		public JitDebugger(string name, Architecture architecture) {
			Name = name;
			Architecture = architecture;
		}
		public JitDebugger Clone() => (JitDebugger)this.MemberwiseClone();
		public string Name { get; set; }
		public Architecture Architecture { get; }
		public string FileName { get; set; }
		public string Arguments { get; set; }
		public string IconOverridePath { get; set; }
		public int AdditionalDelaySecs { get; set; } = 0; // Additional time after it would normally exit where it exits.  Good for  misbehaving / non-signalling debuggers.

		[JsonIgnore]
		public bool Exists => File.Exists(FileName);
		public void LoadIcon(Icon fallback) {
			var iconFromAppRegex = new Regex(@"^(?<path>.+[.](?:exe|dll))(?:[,](?<index>[\-0-9]+))?$", RegexOptions.IgnoreCase);

			var GetIconMethod = (string path, int index) => Icon.ExtractAssociatedIcon(path);//backup method, downside is it can't take an index
			try {
				var mInfo = typeof(Icon).GetMethod(nameof(Icon.ExtractAssociatedIcon), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
				if (mInfo != null) {
					var args = mInfo.GetParameters();
					if (args.Length == 2 && args[0].ParameterType == typeof(string) && args[1].ParameterType == typeof(int))
						GetIconMethod = (string path, int index) => (Icon)mInfo.Invoke(null, [path, index]);
				}
			} catch { }



			if (!File.Exists(FileName))
				return;
			icon = null;
			try {
				var extractPath = FileName;
				var extractIndex = 0;
				if (!String.IsNullOrWhiteSpace(IconOverridePath)) {
					var extractMatch = iconFromAppRegex.Match(IconOverridePath);
					if (extractMatch.Success) {
						extractPath = extractMatch.Groups["path"].Value;
						if (extractMatch.Groups["index"].Success)
							extractIndex = int.Parse(extractMatch.Groups["index"].Value);
					} else
						icon = new Icon(IconOverridePath);

				}
				if (icon == null)
					icon = GetIconMethod(extractPath, extractIndex);
			} catch { }
			if (icon == null)
				icon = fallback;
#if WPF
			DisplayIcon = ToImageSource(icon);
#endif
		}

		public Icon icon;
#if WPF
		[JsonIgnore]
		public ImageSource DisplayIcon { get; set; }
		private static ImageSource ToImageSource(Icon icon) {
			ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
				icon.Handle,
				Int32Rect.Empty,
				BitmapSizeOptions.FromEmptyOptions());

			return imageSource;
		}
#endif
	}
}
