using System;
using System.ServiceModel.Activation;
using System.Web;
using System.Web.Routing;

using BFS;
using BFS.Util;

namespace Ostracod {

	public class Global : HttpApplication {

		private static MapToken share = null;

		protected void Application_Start(object sender, EventArgs args) {
			Global_Init(sender, args);
			RouteTable.Routes.Add(new ServiceRoute("api", new WebServiceHostFactory(), typeof(BasicFileServerNET)));
		}

		//
		// IIS apps will only react to orderly (container-managed) site stops and
		// pool drains; there is no way to capture process kills or debugger kills.
		//
		// That is, these detection mechanisms are scenario-equivalent:
		// - Application_End
		// - HostingEnvironment.RegisterObject + IRegisteredObject.Stop
		// - Process.EnableRaisingEvents + AppDomain.ProcessExit
		// - SetConsoleCtrlHandler
		//
		// Services may benefit from RegisterServiceCtrlHandlerEx, but we have not
		// tested this.
		//
		protected void Application_End(object sender, EventArgs args) {
			Global_Exit(sender, args);
		}

		public void Application_BeginRequest(object sender, EventArgs args) {
			string app = Request.ApplicationPath;
			string api = app.TrimEnd('/') + "/api/webfs";
			string req = Request.Path;

			if (!req.StartsWith(app))
				return;
			if (!req.StartsWith(api))
				HttpContext.Current.RewritePath(api + '/' + req.Substring(app.Length).Trim('/'));
		}

		/*
		 * https://learn.microsoft.com/en-us/previous-versions/bb552862(v=vs.100)
		 *   "The HttpHandler for Web services consumes any exception
		 *    that occurs while a Web service is executing [...]"
		 *
		 * ERGO: Application_Error never fires for WCF-level exceptions
		 *
		protected void Application_Error(object sender, EventArgs args) {
			Exception error = Server.GetLastError();

			if (error is ThreadAbortException) {
				return; // 302
			}

			if (Context.IsCustomErrorEnabled) {
				// ... //
			}
		}*
		 * [UNUSABLE]
		 */

		private static Tuple<string, string> Global_Creds() {
			var user = AppRegistry.DocFolderUser;
			var pass = AppRegistry.DocFolderPass;

			if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass)) {
				return Tuple.Create(user, pass);
			}

			return null;
		}

		private static void Global_Init(object sender, EventArgs args) {
			if (share != null) return;

			var store = AppSettings.DocFolder ?? "";
			var creds = Global_Creds();

			if (store.StartsWith(@"\\") && creds != null) {
				share = FileSystemHelper.MapShare(MapRoute.MODERN, store, creds.Item1, creds.Item2);
				return;
			}
		}

		private static void Global_Exit(object sender, EventArgs args) {
			if (share == null) return;

			try { share.Dispose(); }
			finally { share = null; }
		}

	}

}
