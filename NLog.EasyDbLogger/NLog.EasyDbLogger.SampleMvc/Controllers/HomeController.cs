using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace NLog.EasyDbLogger.SampleMvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
          //  JsonIgnoreAttribute
            var rss=Newtonsoft.Json.JsonConvert.SerializeObject("s");

            ViewBag.Message = "Your application description page.";

            return View();
        }

        public string Die()
        {
            var items = new string[] { "S", "H" };
           
            var die2 = items[4]; return "Good bye";
        }
        public ActionResult Contact()
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();

            ViewBag.Message = "Your contact page.";

            try
            {
                var s = new List<string>();

                var exce = s.First().Trim();
            }
            catch (Exception ex)
            {
                ex.Data.Add("SQL Query","SELECT * FROM SOMETHING");
                ex.Data.Add("KPI Gen instance", "23");
                ex.Data.Add("Org Node Processed", "Dealer ABC");
                logger.Error(ex);
            }

            return View();
        }
        public ActionResult Throw()
        {
            
            var logger = NLog.LogManager.GetCurrentClassLogger();

            try
            {
                var s = new List<string>();

                //var exce = s.First().Trim();

                var items = new string[] { "S", "H" };

                var die2 = items[4];// return "Good bye";

            }
            catch (Exception ex)
            {
                ex.Data.Add("Custom info1","Dealer ABC");
                ex.Data["SQL"] = "SELECT * FROM UDM_CORE.BRND";
                logger.Error(ex);
            }
            return Content("Successfully crashed :)");
        }
    }
}