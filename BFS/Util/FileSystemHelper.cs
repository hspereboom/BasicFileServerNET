using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BFS.Util {

	public sealed class FileSystemHelper {

		#region ListNative

		#region "WinBase.h"

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

		#endregion

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

				if (k == 0 && s.Length == 2 && s[1] == ':') {
					char d = char.ToUpper(s[0]);

					if (d >= 'A' && d <= 'Z') {
						continue;
					}
				}

				if (s.IndexOfAny(RESERVED_CHARS) >= 0) {
					throw new ArgumentException(s);
				}
			}

			return string.Join("/", tbd);
		}

		#endregion

	}

}