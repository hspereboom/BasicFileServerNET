using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace BFS.Util {

	#region Auxiliary

	public enum MapRoute : uint {
		LEGACY = 1, MODERN = 2
	}

	public abstract class MapToken : IDisposable {

		public MapRoute Type { protected set; get; }
		public string Path { protected set; get; }
		public string Error { protected set; get; }
		public bool Valid { protected set; get; }

		public abstract void Dispose();

	}

	#endregion

	public sealed class FileSystemHelper {

		#region NetApi32 (lmuse.h)

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private class USE_INFO_2 {
			// USE_INFO_1
			public string ui2_local;        // Local drive letter
			public string ui2_remote;       // UNC share or path
			public string ui2_password;
			public uint   ui2_status;       // (unused)
			public uint   ui2_asg_type = 0; // USE_DISKDEV
			public uint   ui2_refcount;     // (unused)
			public uint   ui2_usecount = 1;
			// Addenda
			public string ui2_username;
			public string ui2_domainname = "";
		}

		[DllImport("NetApi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern int NetUseAdd(
			string lpServerName,
			uint nLevelFlags,
			ref USE_INFO_2 lpUseInfo,
			out uint nParmError);

		[DllImport("NetApi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern int NetUseDel(
			string lpServerName,
			string lpMappingName,
			uint nForceFlags);

		#endregion

		#region MPR (winnetwk.h)

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private class NETRESOURCE {
			public uint dwScope = 2; // RESOURCE_GLOBALNET
			public uint dwType = 1; // RESOURCETYPE_DISK
			public uint dwDisplayType = 3; // RESOURCEDISPLAYTYPE_SHARE
			public uint dwUsage = 1; // RESOURCEUSAGE_CONNECTABLE
			public string lpLocalName;
			public string lpRemoteName;
			public string lpComment; // (unused)
			public string lpProvider; // (unclear)
		}

		[DllImport("mpr.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern int WNetUseConnection(
			IntPtr hwndOwner,
			NETRESOURCE lpNetResource,
			string lpPassword,
			string lpUserId,
			uint dwFlags,
			string lpAccessName,
			string lpBufferSize,
			string lpResult);

		[DllImport("mpr.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern int WNetAddConnection2(
			NETRESOURCE lpNetResource,
			string lpPassword,
			string lpUserName,
			uint dwFlags);

		[DllImport("mpr.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern int WNetCancelConnection2(
			string lpName,
			uint dwFlags,
			bool fForce);

		#endregion

		#region Kernel32 (winbase.h)

		private const int MAX_PATH                 = 260;
		private const int INVALID_HANDLE_VALUE     = -1;
		private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private class FINDDATA {
			public uint   dwFileAttributes    = 0;
			public uint   ftCreationTimeLo    = 0;
			public uint   ftCreationTimeHi    = 0;
			public uint   ftLastAccessTimeLo  = 0;
			public uint   ftLastAccessTimeHi  = 0;
			public uint   ftLastWriteTimeLo   = 0;
			public uint   ftLastWriteTimeHi   = 0;
			public uint   nFileSizeHi         = 0;
			public uint   nFileSizeLo         = 0;
			public uint   dwReserved0         = 0;
			public uint   dwReserved1         = 0;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
			public string tcFileName          = null;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			public string tcAlternateFileName = null;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr FindFirstFile(
			string             lpFileName,
			[In, Out] FINDDATA lpFindFileData);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern bool FindNextFile(
			IntPtr             hndFindFile,
			[In, Out] FINDDATA lpFindFileData);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern bool FindClose(
			IntPtr hndFindFile);

		public delegate void ListHandle(string name, DateTime time, long size);

		public static void ListNative(string path, ListHandle action) {
			FINDDATA attrib = new FINDDATA();
			IntPtr cursor = FindFirstFile(Path.Combine(path, "*"), attrib);

			try {
				int skip = 1;

				do {
					if (cursor.ToInt32() == INVALID_HANDLE_VALUE) {
						break;
					}

					bool file = (attrib.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
					string name = attrib.tcFileName;
					long time = MarshalQW(attrib.ftLastWriteTimeHi, attrib.ftLastWriteTimeLo);
					long size = MarshalQW(attrib.nFileSizeHi, attrib.nFileSizeLo);

					if (skip != 0 && (/* name == "." || */ name == "..")) {
						skip -= 1;
						continue;
					}

					action(name, DateTime.FromFileTimeUtc(time), file ? size : -1);
				} while (FindNextFile(cursor, attrib));
			} finally {
				FindClose(cursor);
			}
		}

		private static long MarshalQW(uint rawHi, uint rawLo) {
			return (((long)rawHi) << 32) | rawLo;
		}

		private static string Translate(int code) {
			return code == 0 ? null : string.Format("{0} ({1})", new Win32Exception(code).Message, code);
		}

		#endregion

		#region Resources

		private sealed class MapTokenLegacy : MapToken {

			public static MapToken Acquire(object model, string site) {
				var state = model as USE_INFO_2;
				var token = new MapTokenLegacy();

				try {
					uint flags = 2; // USE_INFO_2
					uint index;

					token.Type = MapRoute.LEGACY;
					token.Path = state.ui2_remote;
					token.Error = Translate(NetUseAdd(site, flags, ref state, out index));
				} catch (Exception e) {
					token.Error = e.Message;
					Marshal.GetLastWin32Error();
				} finally {
					token.Valid = token.Error == null;
				}

				return token;
			}

			public override void Dispose() {
				if (!Valid) return;

				try {
					NetUseDel(null, Path, 2);
				} catch (Exception e) {
					Error = e.Message;
				} finally {
					Valid = false;
				}
			}

		}

		private sealed class MapTokenModern : MapToken {

			public static MapToken Acquire(object model, string user, string pass) {
				var state = model as NETRESOURCE;
				var token = new MapTokenModern();

				try {
					IntPtr owner = IntPtr.Zero;
					uint flags = 4; // CONNECT_TEMPORARY

					token.Type = MapRoute.MODERN;
					token.Path = state.lpRemoteName;
					token.Error = Translate(WNetAddConnection2(state, pass, user, flags));
//					token.Error = Translate(WNetUseConnection(owner, state, pass, user, flags, null, null, null));
				} catch (Exception e) {
					token.Error = e.Message;
					Marshal.GetLastWin32Error();
				} finally {
					token.Valid = token.Error == null;
				}

				return token;
			}

			public override void Dispose() {
				if (!Valid) return;

				try {
					WNetCancelConnection2(null, 0, true);
				} catch (Exception e) {
					Error = e.Message;
				} finally {
					Valid = false;
				}
			}

		}

		public static MapToken MapShare(
			MapRoute type, string path,
			string user, string pass
		) {
			string proper = path.Replace('/', '\\').TrimEnd('\\');
			string server = proper.Length >= 5 && proper.StartsWith("\\\\")
				? proper.Substring(0, proper.IndexOf('\\', 3))
				: null; // local = null

			switch (type) {
				case MapRoute.LEGACY:
					return MapLegacy(server, proper, user, pass);
				case MapRoute.MODERN:
					return MapModern(server, proper, user, pass);
			}

			return null;
		}

		private static MapToken MapLegacy(
			string site, string path,
			string user, string pass
		) {
			var model = new USE_INFO_2();

			model.ui2_local = null;
			model.ui2_remote = path;
			model.ui2_username = user;
			model.ui2_password = pass;

			return MapTokenLegacy.Acquire(model, site);
		}

		private static MapToken MapModern(
			string site, string path,
			string user, string pass
		) {
			var model = new NETRESOURCE();

			model.lpLocalName = null;
			model.lpRemoteName = path;
			model.lpProvider = null;

			return MapTokenModern.Acquire(model, user, pass);
		}

		#endregion

		#region Normalize

		private static readonly char[] RESERVED_CHARS = "\\:*?\"<>|".ToCharArray();

		public static string Normalize(string path) {
			return Normalize(path.Trim().Trim('/').Split('/'));
		}

		public static string Normalize(string abs, string rel) {
			return Normalize(string
				.Join("/",
					abs.Trim().TrimEnd('/'),
					rel.Trim().TrimStart('/'))
				.Trim('/')
				.Split('/'));
		}

		private static string Normalize(string[] tbd) {
			for (int k = 0, n = tbd.Length; k < n; k++) {
				string s = tbd[k] = tbd[k].Trim();

				switch (s) {
					case "": if (n == 1) continue;
						throw new ArgumentException("consecutive //");
					case "..":
						throw new ArgumentException(".. component");
				}

				int o = 0;

				if (k == 0 && s.Length >= 2) {
					char c0 = char.ToUpperInvariant(s[0]);
					char c1 = s[1];

					if (s.Length == 2 && c1 == ':' && c0 >= 'A' && c0 <= 'Z')
						continue;
					if (s.Length >= 3 && c1 == '\\' && c0 == '\\')
						o = 2;
				}

				if (s.IndexOfAny(RESERVED_CHARS, o) >= 0) {
					throw new ArgumentException(s);
				}
			}

			return string.Join("/", tbd);
		}

		#endregion

	}

}