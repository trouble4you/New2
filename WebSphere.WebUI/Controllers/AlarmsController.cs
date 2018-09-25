using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
//using System.Net.Http.Formatting;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Mvc;
//using Newtonsoft.Json;
using WebSphere.Domain.Abstract;
using WebSphere.Domain.Concrete;
using WebSphere.Domain.Entities;
using WebSphere.WebUI.Models;
//using WebSphere.Alarms; 


namespace WebSphere.WebUI.Controllers
{
    [Authorize]
    public class AlarmsController : Controller
    {


        public ActionResult Alarms(int? id = null)
        {
            if (id == null) ViewBag.Id = 0;
            else ViewBag.Id = id;
            return View();
        }
        public ActionResult Events(int? id = null)
        {
            if (id == null) ViewBag.Id = 0;
            else ViewBag.Id = id;
            return View();
        }

        public ActionResult SetAlarmAck(int id)
        {
            // лог
            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name,2); 

            var result = (MvcApplication.AlarmServer.SetAlarmAck(id, User.Identity.Name));
            return Json(result);
        }

        public ActionResult SetAlarmAckAll()
        {
            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 3);
            var result = (MvcApplication.AlarmServer.SetAlarmAckAll(User.Identity.Name));
            return Json(result);
        }

        public ActionResult GetCurrentAlarms(Int32? id)
        {
            if (id == 0) id = null;
            var alarms = MvcApplication.AlarmServer.GetCurrentAlarms(id);
            return Json(alarms);
        }

        public ActionResult GetCurrentEvents(Int32? id)
        {
            if (id == 0) id = null;
            var events = MvcApplication.AlarmServer.GetCurrentEvents(id);
            return Json(events);
        }
        public ActionResult GetCurrentAlarmsEvents(Int32? id)
        {
            if (id == 0) id = null;
            var events = MvcApplication.AlarmServer.GetCurrentEvents(id);
            var alarms = MvcApplication.AlarmServer.GetCurrentAlarms(id);
            foreach (var event_ in events)
                alarms.Add(new AlarmThreadManagerOut {Id=event_.Id,StartTime=event_.Time,StartReason= event_.Message,Active=false,Ack= 1});
            return Json(alarms.OrderBy(m=>m.StartTime));
        }
        public ActionResult SoundAlarm()
        {
            var events = MvcApplication.AlarmServer.SoundAlarm();
            var json = Json(events);
            return json;
        }

        public ActionResult GetAlarmsCfgStates()
        {
            //var result = TagThreadsManager.GetThreadStates();
            var result = MvcApplication.AlarmServer.GetAlarmConfig();
            return Json(result);
        }
        public ActionResult RestartAlarms()
        {
            var result = MvcApplication.AlarmServer.Restart();
            return Json(result);
        }
    }
}