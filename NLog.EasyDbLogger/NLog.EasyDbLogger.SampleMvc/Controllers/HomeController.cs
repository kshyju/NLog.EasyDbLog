using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace NLog.EasyDbLogger.SampleMvc.Controllers
{

    public class SomeClass
    {
        public string Name { set; get; }
        public string PageNumber { set; get; }
        public List<string> Items { set; get; }
    }
    public class HomeController : Controller
    {
        Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public ActionResult Index()
        {

           
            logger.Info("Starting at  "+DateTime.Now.ToString());

            var items = new List<int> {2,3};
            Parallel.ForEach(items, (item) =>
            {
                DoIt(item);
            });
          
            return View();
        }

        [HttpPost]
        public ActionResult PostTest(SomeClass test)
        {
            logger.Info("PostTest Starting at  " + DateTime.Now.ToString());

            if (test != null)
            {
                
            }
            else if (test.Items.Any())
            {
                
            }
            return Json(test);
        }

    
        public ActionResult GetTest(string firstName)
        {
            logger.Info("GetTest Starting at  " + DateTime.Now.ToString());

            return Json(firstName);
        }

        private void DoIt(int id)
        {
            logger.Info("Now " +id+"-"+ DateTime.Now.ToString());
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