using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebSphere.WebUI.Controllers
{
    public class InformationController : Controller
    {
        //
        // GET: /Information/
        //public ActionResult Index()
        //{
        //    return View();
        //}
        public ActionResult Index()
        {
            string[] files = Directory.GetFiles(Server.MapPath("~/Content/Files"));
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileName(files[i]);
            }
            ViewBag.Files = files;
            return View();
        }

        public ActionResult DownloadFile(string fileName="")
        {
            if(string.IsNullOrEmpty(fileName))
            {
                return RedirectToAction("Index", "Information");
            }
            var filepath = System.IO.Path.Combine(Server.MapPath("~/Content/Files/"), fileName);
            return File(filepath, MimeMapping.GetMimeMapping(filepath), fileName);
        }
	}
}