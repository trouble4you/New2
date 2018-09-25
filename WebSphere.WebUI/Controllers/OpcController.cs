using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebSphere.Domain.Abstract;

namespace WebSphere.WebUI.Controllers
{
    public class OpcController : Controller
    {

        /// <summary>
        /// Выборка всех всех тегов 
        /// </summary>  
        public ActionResult GetAllOpcTagsValues()
        {
            var tags = MvcApplication.OpcPoller.ReadTags();

            return Json(tags);
        }
        /// <summary>
        /// Выборка информации по ОРС серверу 
        /// </summary>  
        public ActionResult GetOpcInfo()
        {
            var tags = MvcApplication.OpcPoller.GetOpcInfo();

            return Json(tags);
        }

        /// <summary>
        /// Управление подключением ОРС сервера 
        /// </summary>  
        public ActionResult ServerChangeState(int cmd, int pollerId)
        {
            bool z;
            if (cmd == 1)
            {
                z = MvcApplication.OpcPoller.Connect(pollerId);
            }
            else if (cmd == 0)
            { z = MvcApplication.OpcPoller.Stop(pollerId); }
            else
            { 
                z = MvcApplication.OpcPoller.Reinicialize(pollerId);}
            return Json(z);
        }
        /// <summary>
        /// Выставление имитации тега 
        /// </summary>  
        public ActionResult OpcTagSetImitation(string tagName, int tagId, int pollerId)
        {
            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 8);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };

            bool z = MvcApplication.OpcPoller.TagImit(tag);
            return Json(z);
        }
        /// <summary>
        /// Запись значения в тег 
        /// </summary>  
        public ActionResult OpcTagWriteValue(string tagName, int tagId, int pollerId, string val)
        {
            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 9);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Команда на открытие задвижки 
        /// </summary>  
        public ActionResult OpenZD(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 4);

            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Команда на закрытие задвижки 
        /// </summary>  
        public ActionResult CloseZD(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name,5);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }       
        
        /// <summary>
                 /// Команда на СТОП задвижки 
                 /// </summary>  
        public ActionResult StopZD(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 11);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Команда на изменение уставки перекачки для управления задвижками 
        /// </summary>  
        public ActionResult PressControl(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 12);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }

        public ActionResult HeatOn(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 13);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Включения отопления 
        /// </summary>  
        public ActionResult OnHeatZD(string tagName, int tagId, int pollerId, string val)
        {
            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 13);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Выключение отопления 
        /// </summary>  
        public ActionResult OffHeatZD(string tagName, int tagId, int pollerId, string val)
        {
            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 14);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Установка значения РК 
        /// </summary>  
        public ActionResult SetPositionRK(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 6);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        /// <summary>
        /// Перевод РК в позицию 
        /// </summary>  
        public ActionResult MoveToPositionRK(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 7);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }

        /// <summary>
        /// сброс датчиков ОПС 
        /// </summary>  
        public ActionResult Acknowledgment(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 15);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        public ActionResult KvitZD(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 16);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        public ActionResult KvitGas(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 17);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }
        public ActionResult UpdateUst(string tagName, int tagId, int pollerId, string val)
        {

            MvcApplication.AlarmServer.AddUserEvent(User.Identity.Name, 18);
            TagId tag = new TagId { TagName = tagName, Id = tagId, PollerId = pollerId };
            bool z = MvcApplication.OpcPoller.WriteTag(tag, val);
            return Json(z);
        }



    }
}