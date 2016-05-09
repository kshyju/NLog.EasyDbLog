using System;
using System.Collections.Concurrent;
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

        /// <summary>
        /// How many times we should retry to write it to the db if something went wrong the first time
        /// </summary>
        public int MaxRetryCount { set; get; }

        protected override void Write(LogEventInfo logEvent)
        {
            var httpContext = HttpContext.Current;
            if (httpContext != null)
            {
                var req = httpContext.Request;
            }
            string logMessage = this.Layout.Render(logEvent);

            var error = new Error();
            error.Type = logEvent.Level.Name;

            if (httpContext != null)
            {
                var req = httpContext.Request;
                error.HTTPMethod = req.HttpMethod;
                error.Url = req.RawUrl;

            }

            errorQueue.Enqueue(error);

            WriteToTable(error);
        }


        readonly ConcurrentQueue<Error> errorQueue = new ConcurrentQueue<Error>();

        private void WriteToTable(Error error)
        {
            try
            {
                var q = @"Insert Into Exceptions (GUID,
                                                ApplicationName, 
                                                MachineName,
                                                CreationDate, 
                                                Type,                                               
                                                Host,
                                                Url,
                                                HTTPMethod, 
                                                IPAddress, 
                                                Source, 
                                                Message,
                                                Detail,                                                
                                                SQL, 
                                                FullJson, 
                                                ErrorHash, DuplicateCount)
Values (@GUID, @ApplicationName, @MachineName, @CreationDate, @Type, @IsProtected, @Host, @Url, @HTTPMethod, @IPAddress, @Source,
@Message, @Detail, @SQL, @FullJson, @ErrorHash, @DuplicateCount)";



                var connStr =
                    System.Configuration.ConfigurationManager.ConnectionStrings["NLogDb"].ConnectionString as string;
                using (var conn = new SqlConnection(connStr))
                {
                    // var q = "INSERT INTO  Log(Application,Logged,Level,Message) VALUES(@app,@date,@level,@msg)";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@GUID", Guid.NewGuid());
                        cmd.Parameters.AddWithValue("@ApplicationName", error.ApplicationName.Truncate(50));
                        cmd.Parameters.AddWithValue("@MachineName", error.MachineName);
                        cmd.Parameters.AddWithValue("@CreationDate", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@Type", error.Type);
                        cmd.Parameters.AddWithValue("@IsProtected", false);
                        cmd.Parameters.AddWithValue("@Host", error.Host);
                        cmd.Parameters.AddWithValue("@Url", error.Url);
                        cmd.Parameters.AddWithValue("@HTTPMethod", error.HTTPMethod);
                        cmd.Parameters.AddWithValue("@IPAddress", error.IPAddress);
                        cmd.Parameters.AddWithValue("@Source", error.Source);
                        cmd.Parameters.AddWithValue("@Message", error.Message);
                        cmd.Parameters.AddWithValue("@Detail", error.Detail);
                        cmd.Parameters.AddWithValue("@SQL", error.SQL);
                        cmd.Parameters.AddWithValue("@FullJson", error.FullJson);
                        cmd.Parameters.AddWithValue("@ErrorHash", error.ErrorHash);
                        cmd.Parameters.AddWithValue("@DuplicateCount", error.DuplicateCount);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {

                error.RetryCount++;
                AddToQueue(error);
                // throw ex;
            }
        }



        private void AddToQueue(Error error)
        {
            errorQueue.Enqueue(error);

            DequeAndTryToWrite();
        }

        //Some code copied from Stack's Exceptional. (Queuing aand Retrying)

        private void DequeAndTryToWrite()
        {
            while (!errorQueue.IsEmpty)
            {
                Error e;
                
                if (!errorQueue.TryDequeue(out e)) return;

                try
                {
                   WriteToTable(e);
                }
                catch
                {
                    e.RetryCount++;
                    if (e.RetryCount < MaxRetryCount)
                    {
                        AddToQueue(e);
                        break;
                    }
                    else
                    {
                        
                    }
                }
            }
        }
    }

    public static class StringExtensions
    {
        public static string Truncate(this string s, int maxLength)
        {
            return (string.IsNullOrEmpty(s) && s.Length > maxLength) ? s.Remove(maxLength) : s;
        }
    }

    internal class Error
    {
        public int RetryCount { internal set; get; }
        public long Id { internal set; get; }
        public Guid Guid { internal set; get; }
        public string ApplicationName { internal set; get; }
        public string MachineName { internal set; get; }
        public DateTime CreationDate { get; internal set; }
        public string Type { set; get; }
        public string Host { set; get; }
        public string Url { set; get; }
        public string HTTPMethod { set; get; }
        public string IPAddress { set; get; }
        public string Source { set; get; }

        public string Message { set; get; }
        public string Detail { set; get; }
        public string SQL { set; get; }

        public DateTime? DeletionDate { set; get; }

        public string FullJson { set; get; }

        public int ErrorHash { set; get; }

        public int DuplicateCount { set; get; }
    }
}
