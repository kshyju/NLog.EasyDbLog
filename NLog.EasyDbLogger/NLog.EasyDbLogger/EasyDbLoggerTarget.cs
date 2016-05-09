using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NLog.Config;
using NLog.Targets;

namespace NLog.EasyDbLogger
{
    [Target("MyFirst")]
    public sealed class EasyDbLoggerTarget : TargetWithLayout
    {

        [RequiredParameter]
        public string ApplicationName { get; set; }

        public string Host { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            var httpContext = HttpContext.Current;
            if (httpContext != null)
            {
                var req = httpContext.Request;
            }
            string logMessage = this.Layout.Render(logEvent);

            SendTheMessageToRemoteHost(this.Host, logMessage);
        }

        private void SendTheMessageToRemoteHost(string host, string message)
        {
            try
            {


                var connStr = System.Configuration.ConfigurationManager.ConnectionStrings["NLogDb"].ConnectionString as string;
                using (var conn = new SqlConnection(connStr))
                {
                    var q = "INSERT INTO  Log(Application,Logged,Level,Message) VALUES(@app,@date,@level,@msg)";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@app", "T");
                        cmd.Parameters.AddWithValue("@date", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@level", "Info");
                        cmd.Parameters.AddWithValue("@msg", message);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }
    }
}
