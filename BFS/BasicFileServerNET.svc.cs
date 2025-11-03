using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Web;

using BFS.Util;

namespace BFS {

	[ServiceContract]
    public interface IWebFileServer {
		[OperationContract]
		[WebGet(
			UriTemplate = "webfs/{*path}",
			BodyStyle = WebMessageBodyStyle.Bare,
			ResponseFormat = WebMessageFormat.Json
		)]
        void Access(string path);
    }

	[AspNetCompatibilityRequirements(
		RequirementsMode = AspNetCompatibilityRequirementsMode.Required
	)]
	public class BasicFileServerNET : IWebFileServer {

		private readonly string root;
		private readonly string spam;

		public BasicFileServerNET() {
			root = ConfigurationManager.AppSettings["docFolder"];
			spam = ConfigurationManager.AppSettings["logFolder"];
		}

		/*
		 * The built-in HttpResponse methods WriteFile and TransmitFile exhibit severe problems:
		 * - They don't flush intermittently, hence browsers won't show progress;
		 * - They will consistently time out on large files, say 100MB+.
		 *
		 * Configuration does not solve this either:
		 *   <serviceModel>
		 *     <bindings>
		 *       <basicHttpBinding>
		 *         <binding name="basic" transferMode="StreamedResponse">
		 *           <security mode="None"/>
		 *         </binding>
		 *       </basicHttpBinding>
		 *     </bindings>
		 *   </serviceModel>
		 */
		public void Access(string path) {
			var ctx = HttpContext.Current;
			var app = ctx.ApplicationInstance;
			var hsr = ctx.Response;

			try {
				string virt = FileSystemHelper.Normalize(root, path);
				string real = virt.Length == 0 || virt[0] == '/' || virt[0] == '~'
					? HttpContext.Current.Server.MapPath(virt) : virt;

				if (Directory.Exists(real)) {
					hsr.AppendHeader("Content-Type", "text/plain");

					FileSystemHelper.ListNative(real, (name, time, size) =>
						hsr.Write(string.Format(
							"{0}\t{1:0000}-{2:00}-{3:00}T{4:00}:{5:00}:{6:00}Z\t{7}\r\n",
							name, time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second,
							size < 0 ? "-" : size.ToString())));
				} else if (File.Exists(real)) {
					FileInfo file = new FileInfo(real);

					hsr.Clear();
					hsr.AppendHeader("Content-Type", "application/octet-stream");
					hsr.AppendHeader("Content-Disposition", "attachment; filename=" + file.Name);
					hsr.AppendHeader("Content-Length", file.Length.ToString());
					hsr.Flush();

					using (FileStream stream = File.OpenRead(file.FullName)) {
						stream.CopyTo(hsr.OutputStream);
						hsr.Flush();
					}
				} else {
					throw new FileNotFoundException(virt);
				}
			} catch (HttpException err) {
				ctx.AddError(err);
			} catch (Exception err) {
				ctx.AddError(new HttpException(404, err.Message));
			}
		}

	}

}
