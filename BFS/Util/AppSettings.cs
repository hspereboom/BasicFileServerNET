using System.Configuration;

namespace BFS.Util {

	public static class AppSettings {

		private static string GetSetting(string appSettingKey) {
			return ConfigurationManager.AppSettings[appSettingKey];
		}

		public static string LogFolder {
			get { return GetSetting("logFolder"); }
		}

		public static string DocFolder {
			get { return GetSetting("docFolder"); }
		}

	}

}