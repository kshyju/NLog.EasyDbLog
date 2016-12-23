using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using NLog.Config;
using NLog.Targets;
using System.Reflection;

namespace NLog.EasyDbLogger
{
    [Target("EasyDbLoggerTarget")]
    public sealed class EasyDbLoggerTarget : TargetWithLayout
    {

        [RequiredParameter]
        public string ApplicationName { get; set; }


        //Connnection string name used to write to the db

        [RequiredParameter]
        public string ConnectionStringName { set; get; }

        /// <summary>
        /// How many times we should retry to write it to the db if something went wrong the first time
        /// </summary>
        public int MaxRetryCount => 2;

        protected override void Write(LogEventInfo logEvent)
        {
            var error = new Error
            {
                ApplicationName = ApplicationName,
                Type = logEvent.Level.Name,
                Message = logEvent.FormattedMessage,
                Detail = logEvent.FormattedMessage,
                CreationDate = DateTime.UtcNow,
                Guid = Guid.NewGuid(),
                DuplicateCount = 1,
                Source = logEvent.LoggerName
            };

            var httpContext = HttpContext.Current;
            if (httpContext != null)
            {
                var req = httpContext.Request;

                error.Host = req.ServerVariables["HTTP_HOST"] ?? "";
                error.IPAddress = req.ServerVariables.GetRemoteIP();

                error.HTTPMethod = req.HttpMethod;
                error.Url = req.RawUrl;


                SetContextProperties(httpContext, error);

            }

            if (logEvent.Exception != null)
            {
                error.Source = logEvent.Exception.Source;
                error.Message = logEvent.Exception.Message;
                error.Detail = logEvent.Exception.GetAllInnerExceptionsAsString();

                if (logEvent.Exception.Data.Values.Count > 0)
                {
                    Dictionary<string, string> customData = new Dictionary<string, string>();
                    foreach (var data in logEvent.Exception.Data.Keys)
                    {
                        customData.Add(data.ToString(), logEvent.Exception.Data[data].ToString());
                    }
                    error.CustomData = customData;
                }

            }

            if (error.GetHash() != null)
            {
                error.ErrorHash = error.GetHash().Value;
            }

            try
            {

                Assembly assembly = Assembly.Load("Newtonsoft.Json");

                var JsonConvertType = assembly.GetType("Newtonsoft.Json.JsonConvert");

                var p = JsonConvertType.GetProperty("JsonIgnore");
                
                var metod = JsonConvertType.GetMethod("SerializeObject", new[] { typeof(object) });
                //BindingFlags.Static | BindingFlags.Public);
                if (metod != null)
                {
                    var a = new object[] { error };
                    var res = metod.Invoke(null, a);
                    if (res != null)
                    {
                        error.FullJson = (string) res;
                    }
                }
            }
            catch (Exception ex)
            {
                var ss = ex.Message;
                throw;
            }

            
            WriteToTable(error);
        }

        /// <summary>
        /// Sets Error properties pulled from HttpContext, if present
        /// </summary>
        /// <param name="context">The HttpContext related to the request</param>
        private void SetContextProperties(HttpContext context, Error error)
        {
            if (context == null) return;

            var request = context.Request;

            Func<Func<HttpRequest, NameValueCollection>, NameValueCollection> tryGetCollection;
            tryGetCollection = getter =>
            {
                try
                {
                    return new NameValueCollection(getter(request));
                }
                catch (HttpRequestValidationException e)
                {
                    Trace.WriteLine("Error parsing collection: " + e.Message);
                    return new NameValueCollection { { "CollectionFetchError", e.Message } };
                }
            };

            error.ServerVariables = tryGetCollection(r => r.ServerVariables);
            error.QueryString = tryGetCollection(r => r.QueryString);
            error.Form = tryGetCollection(r => r.Form);


            try
            {
                error.Cookies = new NameValueCollection(request.Cookies.Count);
                for (var i = 0; i < request.Cookies.Count; i++)
                {
                    var name = request.Cookies[i].Name;
                    error.Cookies.Add(name, request.Cookies[i].Value);
                }
            }
            catch (HttpRequestValidationException e)
            {
                Trace.WriteLine("Error parsing cookie collection: " + e.Message);
            }

            error.RequestHeaders = new NameValueCollection(request.Headers.Count);
            foreach (var header in request.Headers.AllKeys)
            {
                // Cookies are handled above, no need to repeat
                if (string.Compare(header, "Cookie", StringComparison.OrdinalIgnoreCase) == 0)
                    continue;

                if (request.Headers[header] != null)
                    error.RequestHeaders[header] = request.Headers[header];
            }
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
                                                IsProtected, 
                                                Type,                                               
                                                Host,
                                                Url,
                                                HTTPMethod, 
                                                IPAddress, 
                                                Source, 
                                                Message,
                                                Detail,
                                                FullJson, 
                                                ErrorHash, DuplicateCount,SQL)
Values (@GUID, @ApplicationName, @MachineName, @CreationDate,@IsProtected, @Type, @Host, @Url, @HTTPMethod, @IPAddress, @Source,
@Message, @Detail,  @FullJson, @ErrorHash, @DuplicateCount,@SQL)";



                var connStr =
                    System.Configuration.ConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString as string;
                using (var conn = new SqlConnection(connStr))
                {
                    var machineName = Environment.MachineName;

                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@GUID", error.Guid);
                        cmd.Parameters.AddWithValue("@ApplicationName", error.ApplicationName.Truncate(50));
                        cmd.Parameters.AddWithValue("@MachineName", error.MachineName.Truncate(50));
                        cmd.Parameters.AddWithValue("@CreationDate", error.CreationDate);
                        cmd.Parameters.AddWithValue("@Type", error.Type.Truncate(100));
                        cmd.Parameters.AddWithValue("@IsProtected", false);
                        cmd.Parameters.AddWithValue("@Host",error.Host!=null?error.Host.Truncate(100):(object) DBNull.Value);
                        cmd.Parameters.AddWithValue("@Url", error.Url!=null?error.Url.Truncate(500) : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@HTTPMethod", (object) error.HTTPMethod ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IPAddress", (object) error.IPAddress ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Source", (object) error.Source ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Message", error.Message.Truncate(1000));
                        cmd.Parameters.AddWithValue("@Detail", error.Detail);
                        cmd.Parameters.AddWithValue("@FullJson", error.FullJson);
                        cmd.Parameters.AddWithValue("@ErrorHash", error.ErrorHash);
                        cmd.Parameters.AddWithValue("@DuplicateCount", error.DuplicateCount);
                        cmd.Parameters.AddWithValue("@SQL", (object)error.SQL ?? DBNull.Value);

                       

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {

                error.RetryCount++;
                if (error.RetryCount < MaxRetryCount)
                {
                    AddToQueue(error);
                }
                //SWALLOW :(
                // throw ex;
            }
        }

        


        private void AddToQueue(Error error)
        {
            errorQueue.Enqueue(error);

            DequeAndTryToWrite();
        }


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

    //Thanks to StackExchange.Exceptional(https://github.com/NickCraver/StackExchange.Exceptional). Copied some code from that as we are trying to add records to the same table.

    public static class Extensions
    {
        //Thanks to StackExchange.Exceptional(https://github.com/NickCraver/StackExchange.Exceptional). Copied from that repo.

        private static readonly Regex IPv4Regex = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        public const string UnknownIP = "0.0.0.0";

        private static bool IsPrivateIP(string s)
        {
            return (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0."));
        }

        public static bool HasValue(this string s)
        {
            return !IsNullOrEmpty(s);
        }
        public static bool IsNullOrEmpty(this string s)
        {
            return String.IsNullOrEmpty(s);
        }
        public static string GetRemoteIP(this NameValueCollection serverVariables)
        {
            var ip = serverVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            var ipForwarded = serverVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue())
            {
                ipForwarded = IPv4Regex.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : UnknownIP;
        }
        public static string Truncate(this string s, int maxLength)
        {
            return (s.HasValue() && s.Length > maxLength) ? s.Remove(maxLength) : s;
        }

        public static string GetAllInnerExceptionsAsString(this Exception ex)
        {
            StringBuilder innerExceptionText = new StringBuilder();
            var exception = ex;
            innerExceptionText.Append(exception.ToString());
            while (exception.InnerException != null)
            {
                innerExceptionText.Append(exception.InnerException);
                exception = exception.InnerException;
            }
            return innerExceptionText.ToString();
        }
    }

    [Serializable]
    internal class Error
    {
        public int RetryCount { internal set; get; }
        public long Id { internal set; get; }
        public Guid Guid { internal set; get; }
        public string ApplicationName { internal set; get; }
        public string MachineName => Environment.MachineName;
        public DateTime CreationDate { get; internal set; }
        public string Type { internal set; get; }
        public string Host { internal set; get; }
        public string Url { internal set; get; }
        public string HTTPMethod { internal set; get; }
        public string IPAddress { set; get; }
        public string Source { set; get; }

        public string Message { set; get; }
        public string Detail { set; get; }
        public string SQL { set; get; }

        public DateTime? DeletionDate { set; get; }

        public string FullJson { set; get; }

        public int ErrorHash { set; get; }

        public int DuplicateCount { set; get; }


        /// <summary>
        /// Gets a collection representing the headers sent with the request
        /// </summary>
        //  [ScriptIgnore]
        //  [JsonIgnore]
        public NameValueCollection RequestHeaders { get; set; }


        //   [JsonIgnore]
        public NameValueCollection ServerVariables { get; set; }

        /// <summary>
        /// Gets the query string collection for the request
        /// </summary>
      //  [JsonIgnore]
        public NameValueCollection QueryString { get; set; }

        /// <summary>
        /// Gets the form collection for the request
        /// </summary>
    //    [JsonIgnore]
        public NameValueCollection Form { get; set; }

        /// <summary>
        /// Gets a collection representing the client cookies of the request
        /// </summary>
       // [JsonIgnore]
        public NameValueCollection Cookies { get; set; }

        public bool ShouldSerializeCookies()
        {
            return false;
        }
        public bool ShouldSerializeForm()
        {
            return false;
        }
        public bool ShouldSerializeQueryString()
        {
            return false;
        }
        public bool ShouldSerializeServerVariables()
        {
            return false;
        }
        public bool ShouldSerializeRequestHeaders()
        {
            return false;
        }
        public List<NameValuePair> ServerVariablesSerializable
        {
            get { return GetPairs(ServerVariables); }
            set { ServerVariables = GetNameValueCollection(value); }
        }
        /// <summary>
        /// Variables strictly for JSON serialziation, to maintain non-dictonary behavior
        /// </summary>
        public List<NameValuePair> QueryStringSerializable
        {
            get { return GetPairs(QueryString); }
            set { QueryString = GetNameValueCollection(value); }
        }
        /// <summary>
        /// Variables strictly for JSON serialziation, to maintain non-dictonary behavior
        /// </summary>
        public List<NameValuePair> FormSerializable
        {
            get { return GetPairs(Form); }
            set { Form = GetNameValueCollection(value); }
        }
        /// <summary>
        /// Variables strictly for JSON serialziation, to maintain non-dictonary behavior
        /// </summary>
        public List<NameValuePair> CookiesSerializable
        {
            get { return GetPairs(Cookies); }
            set { Cookies = GetNameValueCollection(value); }
        }

        public List<NameValuePair> RequestHeadersSerializable
        {
            get { return GetPairs(RequestHeaders); }
            set { RequestHeaders = GetNameValueCollection(value); }
        }

        public Dictionary<string, string> CustomData { get; set; }

        /// <summary>
        /// Gets a unique-enough hash of this error.  Stored as a quick comparison mechanism to rollup duplicate errors.
        /// </summary>
        /// <returns>"Unique" hash for this error</returns>
        public int? GetHash()
        {
            if (!Detail.HasValue()) return null;

            var result = Detail.GetHashCode();
            if (MachineName.HasValue())
                result = (result * 397) ^ MachineName.GetHashCode();

            return result;
        }

        internal void AddFromData(Exception exception)
        {
            if (exception.Data == null) return;

            // Historical special case
            if (exception.Data.Contains("SQL"))
                SQL = exception.Data["SQL"] as string;

            var se = exception as SqlException;
            if (se != null)
            {
                if (CustomData == null)
                    CustomData = new Dictionary<string, string>();

                CustomData["SQL-Server"] = se.Server;
                CustomData["SQL-ErrorNumber"] = se.Number.ToString();
                CustomData["SQL-LineNumber"] = se.LineNumber.ToString();

                if (se.Procedure.HasValue())
                {
                    CustomData["SQL-Procedure"] = se.Procedure;
                }
            }

        }

        private List<NameValuePair> GetPairs(NameValueCollection nvc)
        {
            var result = new List<NameValuePair>();
            if (nvc == null) return null;

            for (int i = 0; i < nvc.Count; i++)
            {
                result.Add(new NameValuePair { Name = nvc.GetKey(i), Value = nvc.Get(i) });
            }
            return result;
        }
        private NameValueCollection GetNameValueCollection(List<NameValuePair> pairs)
        {
            var result = new NameValueCollection();
            if (pairs == null) return null;

            foreach (var p in pairs)
            {
                result.Add(p.Name, p.Value);
            }
            return result;
        }
    }


    public class NameValuePair
    {
        /// <summary>
        /// The name for this variable
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The value for this variable
        /// </summary>
        public string Value { get; set; }
    }
}
