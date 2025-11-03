using System;
using System.ServiceModel.Activation;
using System.Web;
using System.Web.Routing;

using BFS;

namespace Arachnid {

	public class Global : HttpApplication {

		protected void Application_Start(object sender, EventArgs args) {
			RouteTable.Routes.Add(new ServiceRoute("api", new WebServiceHostFactory(), typeof(BasicFileServerNET)));
		}

		public void Application_BeginRequest(object sender, EventArgs args) {
			string root = "/api/webfs";
			string path = Request.FilePath;

			var rlen = root.Length;
			var plen = path.Length;

			if (path == root || plen == 0 || path[0] != '/')
				return;
			if (plen <= rlen || !path.StartsWith(root) || path[rlen] != '/')
				HttpContext.Current.RewritePath(root + path);
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

	}

}
