using System;
using Windows.Win32.Storage.FileSystem;
using System.ComponentModel;
using System.Linq;
using System.IO;
using Windows.Win32;

namespace JitMagic.Models {
	static class FileHelper {
		/// <summary>
		/// Tries to read or write the file directly, if it fails due to it being a symlink and control flow guard try to work around that.  if toWrite is null it reads the file and returns the data otherwise it writes toWrite to the file.
		/// </summary>
		/// <param name="ConfigFile"></param>
		/// <param name="toWrite"></param>
		/// <returns></returns>
		/// <exception cref="Win32Exception"></exception>
		/// <exception cref="Exception"></exception>
		/// <exception cref="FileNotFoundException"></exception>
		public static unsafe string ReadWriteFile(string ConfigFile, string toWrite = null) {
			Func<string, string> action = (string fileName) => {
				if (toWrite == null)
					return File.ReadAllText(fileName);
				File.WriteAllText(fileName, toWrite);
				return null;
			};

			try {
				return action(ConfigFile);
			} catch (IOException) { //This is to try and work around an issue where for actual exceptions (vs debugger.breaks) RedirectionGuard is enabled and it prevents us from reading the config if it is a symlink.  This is the only way to read it that I have found.
				var fileName = GetSymLink(ConfigFile, RELATIVE_LINK_MODE.Resolve);
				if (File.Exists(fileName))
					return action(fileName);
				throw new Exception("Config file not found");

			}
		}
		public enum RELATIVE_LINK_MODE { Disallow, Preserve, Resolve }
		private const string WIN32_NAMESPACE_PREFIX = @"\??\";
		private const string UNC_PREFIX = @"UNC\";
		/// <summary>
		/// Manually resolve a file to its target, needed, for example, if GetFinalPathNameByHandle cannot be called due to RedirectionGuard preventing it in certain security contexts
		/// </summary>
		/// <param name="file">file to resolve to path</param>
		/// <param name="rel_mode">What to do with relative sym links (ie ../test.txt)</param>
		/// <param name="AllowVolumeMountpoints">Allow junctions that resolve to \??\Volume{«guid»}\....</param>
		/// <returns></returns>
		/// <exception cref="Win32Exception"></exception>
		/// <exception cref="IOException"></exception>
		public static unsafe string GetSymLink(string file, RELATIVE_LINK_MODE rel_mode = RELATIVE_LINK_MODE.Disallow, bool AllowVolumeMountpoints = false) {
			using var handle = PInvoke.CreateFile(file, default, default, null, FILE_CREATION_DISPOSITION.OPEN_EXISTING, FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT, default);
			if (handle.IsInvalid)
				throw new Win32Exception();

			Span<sbyte> buffer = new sbyte[PInvoke.MAXIMUM_REPARSE_DATA_BUFFER_SIZE];

			fixed (sbyte* ptr = buffer) {
				ref var itm = ref *(Windows.Wdk.Storage.FileSystem.REPARSE_DATA_BUFFER*)ptr;


				uint bytes;
				if (!PInvoke.DeviceIoControl(handle, PInvoke.FSCTL_GET_REPARSE_POINT, null, 0, ptr, (uint)buffer.Length, &bytes, null))
					throw new Win32Exception();
				Span<char> returnPath = null;

				static Span<char> ParsePathBuffer(ref VariableLengthInlineArray<char> buffer, int nameOffsetInBytes, int lengthInBytes, out bool WasPrefixed) {
					var ret = buffer.AsSpan((nameOffsetInBytes + lengthInBytes) / sizeof(char)).Slice(nameOffsetInBytes / sizeof(char));
					WasPrefixed = ret.Length >= WIN32_NAMESPACE_PREFIX.Length && ret.StartsWith(WIN32_NAMESPACE_PREFIX.ToArray());
					if (WasPrefixed)
						ret = ret.Slice(WIN32_NAMESPACE_PREFIX.Length);
					return ret;
				}

				if (itm.ReparseTag == PInvoke.IO_REPARSE_TAG_SYMLINK) {

					ref var reparse = ref itm.Anonymous.SymbolicLinkReparseBuffer;
					returnPath = ParsePathBuffer(ref reparse.PathBuffer, reparse.SubstituteNameOffset, reparse.SubstituteNameLength, out var wasWin32NamespacePrefixed);

					var shouldBeRelativeLink = (reparse.Flags & Windows.Wdk.PInvoke.SYMLINK_FLAG_RELATIVE) != 0;
					if (returnPath.Length == 0 || (!wasWin32NamespacePrefixed && !shouldBeRelativeLink))
						throw new IOException("Invalid symlink read");
					else if (shouldBeRelativeLink) { //this should be a relative link as was not prefixed
						if (rel_mode == RELATIVE_LINK_MODE.Disallow)
							throw new IOException($"Relative symlink found of: {returnPath.ToString()} but relative links disabled");
						else if (rel_mode == RELATIVE_LINK_MODE.Resolve)
							return Path.Combine(new FileInfo(file).DirectoryName, returnPath.ToString());
						//netcore only: return Path.GetFullPath(returnPath.ToString(), new FileInfo(file).DirectoryName);
					}
				} else if (itm.ReparseTag == PInvoke.IO_REPARSE_TAG_MOUNT_POINT) {
					ref var reparse = ref itm.Anonymous.MountPointReparseBuffer;
					returnPath = ParsePathBuffer(ref reparse.PathBuffer, reparse.SubstituteNameOffset, reparse.SubstituteNameLength, out var wasWin32NamespacePrefixed);
					if (!wasWin32NamespacePrefixed)
						throw new IOException("Invalid junction read");
					if (!AllowVolumeMountpoints && (!IsAsciiLetter(returnPath[0]) || returnPath[1] != ':'))
						throw new IOException("File is a junction to a volume mount point and that is disabled");
				}

				return returnPath.ToString();
			}
		}
		static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
	}
}
