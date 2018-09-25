using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using WebSphere.Domain.Abstract;
using WebSphere.Domain.Concrete;
using System.Data;
using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.HPSF;
using NPOI.POIFS.FileSystem;
using NPOI.SS.UserModel;
using System.Text;
using NPOI.SS.Util;

namespace WebSphere.Reports
{
    public class Well
    {
        public int AgzuId;
        public int WellId;
        public string AgzuName;
        public string WellName;
        public int Psm;
    }

    public class AGZU
    {
        public string Name;
        public List<Well> Wells;
        public int st_id;
        public int end_id;
        public int Psm;
        public int MinSec;
        public int DayHour;
        public int YearMont;
        public int Tism;
        public int QMfld;
        public int QVfld;
        public int Goil;
        public int Qng;
        public int water;
        public int pofld;
        public int Mfld;
        public int Vfld;
        public int Moil;
        public int Vng;
    }



    public class MySQLRow
    {
        public List<string> values = new List<string>();
    }

    public class MySQLResult
    {
        public List<string> columns = new List<string>();
        public List<MySQLRow> rows = new List<MySQLRow>();
        public int count_rows;

        public String GetValue(int row, string columnName)
        {
            // Получитьь значение по номеру строки и названию колонки
            if (row >= count_rows)
                return "*";
            for (var i = 0; i < columns.Count; i++)
                if (columns[i] == columnName)
                    return rows[row].values[i];
            return "*";
        }

        public bool SetValue(int row, string columnName, string value)
        {
            if (row >= count_rows)
                return false;
            for (var i = 0; i < columns.Count; i++)
                if (columns[i] == columnName)
                {
                    rows[row].values[i] = value;
                    return true;
                }
            return false;
        }



        public string GetValue(int rowIndex, int columnIndex)
        {
            return rows[rowIndex].values[columnIndex];
        }
    }

    public static class MyDB
    {
        private static readonly string ConnectionStringLocal = System.Configuration.ConfigurationSettings.AppSettings["MyConnection"];
        public static MySQLResult sql_query_local(string query)
        {
            {
                var result = new MySQLResult { count_rows = 0 };
                // Выполняем запрос.
                var dataSet = new DataSet();
                //string connectionString = System.Configuration.ConfigurationSettings.AppSettings["MyConnection"];
                string connectionString = ConnectionStringLocal;
                using (var connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        var dbAdapter = new SqlDataAdapter(query, connection);
                        dbAdapter.Fill(dataSet);
                        connection.Close();
                    }
                    catch (Exception ex)
                    {
                        if (connection.State == ConnectionState.Open)
                            connection.Close();
                    }
                }

                // Заполняем данные.
                if (dataSet.Tables.Count == 0)
                    return result;
                var table = dataSet.Tables[0];
                // Собираем имена.
                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                    result.columns.Add(table.Columns[columnIndex].ColumnName);
                // Собираем данные.
                for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    var row = table.Rows[rowIndex];
                    var newRow = new MySQLRow();
                    for (var columnIndex = 0; columnIndex < result.columns.Count; columnIndex++)
                        newRow.values.Add(row[columnIndex].ToString());
                    result.rows.Add(newRow);
                    result.count_rows++;
                }
                return result;
            }
        }


    }



    public class ReportServer : IReportServer
    {
        private static HSSFWorkbook _workbook;
        private readonly EFDbContext _context = new EFDbContext();
        private static readonly Logging logger = new Logging();
        // private static readonly JSON Json = new JSON();
        private static IJSON _json = new JSON();
        private static List<AlarmThreadManagerConfig> _cfgs = new List<AlarmThreadManagerConfig>();
        private static List<EventThreadManagerConfig> _cfgsEv = new List<EventThreadManagerConfig>();

        private static readonly List<Report> reportList = new List<Report>
            {
                 new Report{Id = 1,Name = "Журнал замеров АГЗУ"} ,
                 new Report{Id = 2,Name = "Хронология пусков и остановок скважин"} ,
                 new Report{Id = 3,Name = "Текущее состояние скважин"} ,
                 new Report{Id = 4,Name = "Журнал состояния связи"} ,
                 //new Report{Id = 5,Name = "Площадка конденсатосборников"},
                 new Report{Id = 6,Name = "Журнал тревог"},
                 new Report{Id = 7,Name = "Журнал событий "},
                 new Report{Id = 8,Name = "Журнал действий пользователя "}
            };


        private List<Obj> _objects;
        private List<Obj> _tags;

        public class Obj
        {
            public int Id;
            public int Type;
            public int? Parent;
            public string Name;
            public string Prop;


        }
        public void LoadObjSign()
        {
            _objects = (from ti in _context.Objects
                        where (ti.Type == 1 || ti.Type == 2 || ti.Type == 5 || ti.Type == 21) 
                        select new Obj { Id = ti.Id, Name = ti.Name, Parent = ti.ParentId, Type = ti.Type, Prop = null }).ToList();

            _tags = (from ti in _context.Objects
                     join to in _context.Properties on ti.Id equals to.ObjectId
                     where ti.Type == 2 && to.PropId == 0
                     select new Obj { Id = ti.Id, Name = ti.Name, Parent = ti.ParentId, Type = ti.Type, Prop = to.Value }).ToList();


        }
        public List<int> GetChildTags(int? parID)
        {
            var rez = new List<int>();
            if (parID == null) return rez;
            var root = _objects.Where(c => c.Id == parID).Select(c => c).FirstOrDefault();
            var childs = _objects.Where(c => c.Parent == parID && (c.Type == 21 || c.Type == 2 || c.Type == 5)).ToList();
            foreach (var child in childs)
            {
                if (child.Id == 0) continue;
                rez.AddRange(GetChildTags(child.Id));
            }
            if (root.Type == 2)
            {
                rez.Add(root.Id);
            }
            return rez;

        } 



     
        public string ProcFl(string value, int k)
        {
            double N;
            if (!Double.TryParse(value.Replace(".", ","), out N)) return value;
            else return string.IsNullOrEmpty(value) ? "" : Convert.ToString(Math.Round(N, k));
        }
        public string GetTop1ValBetweenDatesById(int id, DateTime dt1, DateTime dt2)
        {

            return "select top 1 Value from SignalsAnalogs where TagId=" + id.ToString() + " and Datetime between '" +
                        dt1.ToString() + "' and '" + dt2.ToString() + "' order by Datetime desc";
        }
        public string GetTop1ValBeforeDatesById(int id, DateTime dt1)
        {
            return "select top 1 Value from SignalsAnalogs where TagId=" + id.ToString() + " and Datetime <= '" +
                        dt1.ToString() + "'  order by Datetime desc";
        }
 

         public List<Report> ReportList()
        {
            return reportList;
        }
        public string ReportName(int id)
        {
            var firstOrDefault = reportList.FirstOrDefault(x => x.Id == id);
            if (firstOrDefault != null)
                return firstOrDefault.Name;
            else return "Журнал не найден";
        }
        public Report AlarmReport(string name, Dictionary<string, dynamic> parameters)
        {

            var report = new Report();
            var rez = new List<List<string>>();
            var Sdate = DateTime.Now.AddDays(-1);
            var Edate = DateTime.Now;
            int? AgzuId = null;
            foreach (var parameter in parameters)
            {
                if (parameter.Key == "StartDate")
                    Sdate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "EndDate")
                    Edate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "AgzuId")
                    AgzuId = Convert.ToInt32(parameter.Value);
            }

            LoadObjSign();
            List<Obj> tags = new List<Obj>();
            if (AgzuId != null)
            {
                var tagsId = GetChildTags(AgzuId);
                tags = _tags.Where(x => tagsId.Contains(x.Id)).ToList();
            }
            else
                tags = _tags;

            _cfgsEv.Clear();

            var tagsIDList = new List<int>();
            foreach (var tagjson in tags)
            {
                tagsIDList.Add(tagjson.Id);
                dynamic alarm = JsonConvert.DeserializeObject(tagjson.Prop);
                var tag = new TagId
                {
                    TagName = Convert.ToString(alarm.Connection),
                    PollerId = Convert.ToInt32(alarm.Opc)
                };
                try
                {
                    var tagId = Convert.ToInt32(tagjson.Id);
                    var alarms = Convert.ToString(alarm.Alarms);
                    if (alarms != null && alarms != "")
                    {
                        var alarm_ = new AlarmThreadManagerConfig();
                        var _props = _json.Deserialize(alarms, alarm_.GetType());
                        alarm_ = (AlarmThreadManagerConfig)_props;
                        if (alarm_.Enabled)
                        {
                            alarm_.TagId = Convert.ToInt32(tagjson.Id);
                            alarm_.Tag = tag;


                            if (alarm_.HihiSeverity == null) alarm_.HihiSeverity = Double.MaxValue;
                            if (alarm_.HiSeverity == null) alarm_.HiSeverity = Double.MaxValue;
                            if (alarm_.LoSeverity == null) alarm_.LoSeverity = Double.MinValue;
                            if (alarm_.LoloSeverity == null) alarm_.LoloSeverity = Double.MinValue;
                            _cfgs.Add(alarm_);
                        }

                        logger.Logged("Info", "Конфигурация тревоги [" + tag.PollerId + "][" + tag.TagName + "] добавлена...", "AlarmThreadManager", "LoadAlarmsLastStates");
                    }
                }
                catch (Exception ex)
                {
                    logger.Logged("Err", "Неверная конфигурация тревоги [" + tag.PollerId + "][" + tag.TagName + "] : " +
                        ex.Message, "AlarmThreadManager", "LoadAlarmsLastStates");
                }
            }


            try
            {

                using (_context)
                {
                    var Salarms =
                        _context.Alarms.Where(x => x.STime > Sdate && x.STime < Edate && tagsIDList.Contains(x.TagId)).OrderByDescending(x => x.STime);
                    var Salarmslist = Salarms.ToList();

                    foreach (var salarm in Salarmslist)
                    {
                        var row = new List<string>();
                        string endS = "", startS = "";

                        var index = _cfgs.FindIndex(x => x.TagId == salarm.TagId);
                        if (index != -1)
                        {
                            switch (salarm.SRes)
                            {
                                case -2:
                                    startS = _cfgs.ElementAt(index).LoloText;
                                    break;
                                case -1:
                                    startS = _cfgs.ElementAt(index).LoText;
                                    break;
                                case 0:
                                    startS =
                                        _cfgs.ElementAt(index).NormalText;
                                    break;
                                case 1:
                                    startS = _cfgs.ElementAt(index).HiText;
                                    break;
                                case 2:
                                    startS = _cfgs.ElementAt(index).HihiText;
                                    break;
                            }
                            switch (salarm.ERes)
                            {
                                case -2:
                                    endS = _cfgs.ElementAt(index).LoloText;
                                    break;
                                case -1:
                                    endS = _cfgs.ElementAt(index).LoText;
                                    break;
                                case 0:
                                    endS = _cfgs.ElementAt(index).NormalText;
                                    break;
                                case 1:
                                    endS = _cfgs.ElementAt(index).HiText;
                                    break;
                                case 2:
                                    endS = _cfgs.ElementAt(index).HihiText;
                                    break;
                            }

                        }
                        else
                        {
                            endS = "Отсутствует  конфигурация"; startS = "Отсутствует  конфигурация";
                        }
                        row.Add(startS);
                        row.Add(salarm.STime.ToString());
                        row.Add(endS);
                        row.Add(salarm.ETime.ToString());
                        row.Add(salarm.AckTime.ToString());
                        if (salarm.Ack > 1)
                        {
                            var user = _context.Objects.FirstOrDefault(x => (x.Id == salarm.Ack && x.Type == 9));
                            if (user != null)
                                row.Add(user.Name.ToString());
                            else
                                row.Add("");
                        }
                        rez.Add(row);

                    }
                    logger.Logged("Info", " Report loaded..." + name, "ReportServer", "Rep2");
                    report.Name = name;
                    report.Head = new List<string> { "Начало события", "Время начала", "Конец события", "Время конца", "Время квитирования", "Квитировал" };
                    report.Rows = rez;
                }
            }

            catch (Exception ex)
            {
                return report;
            }
            return report;
        }
        public Report EventReport(string name, Dictionary<string, dynamic> parameters)
        {
            var report = new Report();
            var rez = new List<List<string>>();
            var Sdate = DateTime.Now.AddDays(-1);
            var Edate = DateTime.Now;
            int? AgzuId = null;
            foreach (var parameter in parameters)
            {
                if (parameter.Key == "StartDate")
                    Sdate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "EndDate")
                    Edate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "AgzuId")
                    AgzuId = Convert.ToInt32(parameter.Value);
            }

            LoadObjSign();
            List<Obj> tags = new List<Obj>();
            if (AgzuId != null)
            {
                var tagsId = GetChildTags(AgzuId);
                tags = _tags.Where(x => tagsId.Contains(x.Id)).ToList();
            }
            else
                tags = _tags;

            _cfgsEv.Clear();

            var tagsIDList = new List<int>();
            foreach (var tagjson in tags)
            {
                tagsIDList.Add(tagjson.Id);
                dynamic alarm = JsonConvert.DeserializeObject(tagjson.Prop);
                var tag = new TagId
                {
                    TagName = Convert.ToString(alarm.Connection),
                    PollerId = Convert.ToInt32(alarm.Opc)
                };
                try
                {
                    var events = Convert.ToString(alarm.Events);
                    if (events != null && events != "")
                    {
                        var event_ = new EventThreadManagerConfig();
                        var _props = _json.Deserialize(events, event_.GetType());
                        event_ = (EventThreadManagerConfig)_props;
                        if (event_.Enabled)
                        {
                            event_.Tag = tag;
                            event_.TagId = Convert.ToInt32(tagjson.Id);
                            _cfgsEv.Add(event_);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Logged("Err", "add Event tag [" + tag.PollerId + "][" + tag.TagName + "] to alarmserver failed: " + ex.Message,
                        "AlarmThreadManager", "LoadAlarmsLastStates");
                }
            }

            try
            {
                using (_context)
                {
                    var Sevents =
                        _context.Events.Where(x => x.Time > Sdate && x.Time < Edate && tagsIDList.Contains(x.TagId)).OrderByDescending(x => x.Time);
                    var Seventslist = Sevents.ToList();


                    foreach (var salarm in Seventslist)
                    {
                        var row = new List<string>();
                        string endS = "", startS = "";


                        var outEvent = new EventThreadManagerOut();

                        outEvent.Id = salarm.Id;
                        outEvent.TagId = salarm.TagId;
                        outEvent.Time = salarm.Time;

                        var tm = _cfgsEv.FirstOrDefault(x => x.TagId == salarm.TagId) ?? new EventThreadManagerConfig();

                        if (tm.EventMessages != null)
                        {
                            foreach (var a in tm.EventMessages)
                            {
                                if (a.Value == salarm.Value)

                                    outEvent.Message = a.Message;

                            }
                        }
                        if (outEvent.Message == "" || outEvent.Message == null)
                            outEvent.Message = "Сообщение отсутствует( Tag:" + salarm.TagId + ", Value:" + salarm.Value + ")";
                        row.Add(outEvent.Time.ToString());
                        row.Add(outEvent.Message);

                        rez.Add(row);
                    }
                    logger.Logged("Info", " Report loaded..." + name, "ReportServer", "Rep2");
                    report.Name = name;
                    report.Head = new List<string> { "Время", "Cобытие" };
                    report.Rows = rez;
                }
            }

            catch (Exception ex)
            {
                return report;
            }
            return report;
        }
        public Report UserEventReport(string name, Dictionary<string, dynamic> parameters)
        { 

            var report = new Report();
            var rez = new List<List<string>>();
            var Sdate = DateTime.Now.AddDays(-1);
            var Edate = DateTime.Now; 
            foreach (var parameter in parameters)
            {
                if (parameter.Key == "StartDate")
                    Sdate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "EndDate")
                    Edate = Convert.ToDateTime(Convert.ToString(parameter.Value)); 
            }
            List<Obj> _users;
            _users = (from ti in _context.Objects
                      join to in _context.Properties on ti.Id equals to.ObjectId
                      where ti.Type == 9 && to.PropId == 0
                      select new Obj { Id = ti.Id, Name = ti.Name, Parent = ti.ParentId, Type = ti.Type, Prop = to.Value }).ToList();
            List<Obj> tags = new List<Obj>(); 

            _cfgsEv.Clear();

            var tagsIDList = new List<int>();
            foreach (var tagjson in _users)

            {
                tagsIDList.Add(tagjson.Id);
                   var event_ = new EventThreadManagerConfig();
                event_.TagId = tagjson.Id;
                //чтоб поток не проверял события пользователеей
                event_.Active = false;

                event_.Enabled = true;
                event_.EventMessages = new List<EventValMessage>();
                event_.EventMessages.Add(new EventValMessage { Value = 0, Message = "Пользователь " + tagjson.Name + " вошел в систему" });
                event_.EventMessages.Add(new EventValMessage { Value = 1, Message = "Пользователь " + tagjson.Name + " вышел из системы" });
                event_.EventMessages.Add(new EventValMessage { Value = 2, Message = "Пользователь " + tagjson.Name + " квитировал тревогу" });
                event_.EventMessages.Add(new EventValMessage { Value = 3, Message = "Пользователь " + tagjson.Name + " квитировал все тревоги" });
                event_.EventMessages.Add(new EventValMessage { Value = 4, Message = "Пользователь " + tagjson.Name + " подал команду на открытие задвижки" });
                event_.EventMessages.Add(new EventValMessage { Value = 5, Message = "Пользователь " + tagjson.Name + " подал команду на закрытие задвижки" });
                event_.EventMessages.Add(new EventValMessage { Value = 6, Message = "Пользователь " + tagjson.Name + " Изменил заданное положение клапана" });
                event_.EventMessages.Add(new EventValMessage { Value = 7, Message = "Пользователь " + tagjson.Name + " подал команду на изменение положения клапана" });
                event_.EventMessages.Add(new EventValMessage { Value = 8, Message = "Пользователь " + tagjson.Name + " задал имитацию для тега" });
                event_.EventMessages.Add(new EventValMessage { Value = 9, Message = "Пользователь " + tagjson.Name + " записал значение в тег" });
                event_.EventMessages.Add(new EventValMessage { Value = 10, Message = "Пользователь " + tagjson.Name + "  добавил нового пользователя" });
                event_.EventMessages.Add(new EventValMessage { Value = 11, Message = "Пользователь " + tagjson.Name + " подал команду на остановку задвижки" });
                event_.EventMessages.Add(new EventValMessage { Value = 12, Message = "Пользователь " + tagjson.Name + " изменил значение уставки управления задвижками" });
                event_.EventMessages.Add(new EventValMessage { Value = 0, Message = "Пользователь " + tagjson.Name + " вошел в систему" });
                _cfgsEv.Add(event_);
            }

            try
            {
                using (_context)
                {
                    var Sevents =
                        _context.Events.Where(x => x.Time > Sdate && x.Time < Edate && tagsIDList.Contains(x.TagId)).OrderByDescending(x => x.Time);
                    var Seventslist = Sevents.ToList();


                    foreach (var salarm in Seventslist)
                    {
                        var row = new List<string>();
                        string endS = "", startS = "";


                        var outEvent = new EventThreadManagerOut();

                        outEvent.Id = salarm.Id;
                        outEvent.TagId = salarm.TagId;
                        outEvent.Time = salarm.Time;

                        var tm = _cfgsEv.FirstOrDefault(x => x.TagId == salarm.TagId) ?? new EventThreadManagerConfig();

                        if (tm.EventMessages != null)
                        {
                            foreach (var a in tm.EventMessages)
                            {
                                if (a.Value == salarm.Value)

                                    outEvent.Message = a.Message;

                            }
                        }
                        if (outEvent.Message == "" || outEvent.Message == null)
                            outEvent.Message = "Сообщение отсутствует( Tag:" + salarm.TagId + ", Value:" + salarm.Value + ")";
                        row.Add(outEvent.Time.ToString());
                        row.Add(outEvent.Message);

                        rez.Add(row);
                    }
                    logger.Logged("Info", " Report loaded..." + name, "ReportServer", "Rep2");
                    report.Name = name;
                    report.Head = new List<string> { "Время", "Cобытие" };
                    report.Rows = rez;
                }
            }

            catch (Exception ex)
            {
                return report;
            }
            return report;
        }
        public Report LinkReport(string name, Dictionary<string, dynamic> parameters)
        {

            var report = new Report();
            var rez = new List<List<string>>();
            var Sdate = DateTime.Now.AddDays(-1);
            var Edate = DateTime.Now;
            int? AgzuId = null;
            foreach (var parameter in parameters)
            {
                if (parameter.Key == "StartDate")
                    Sdate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "EndDate")
                    Edate = Convert.ToDateTime(Convert.ToString(parameter.Value));
                if (parameter.Key == "AgzuId")
                    AgzuId = Convert.ToInt32(parameter.Value);
            }

            LoadObjSign();
            List<Obj> tags = new List<Obj>();
            if (AgzuId != null)
            {
                var tagsId = GetChildTags(AgzuId);
                tags = _tags.Where(x => tagsId.Contains(x.Id) && x.Name == "ObjectLink").ToList();
            }
            else
                tags = _tags.Where(x => x.Name == "ObjectLink").ToList();

            _cfgsEv.Clear();

            var tagsIDList = new List<int>();
            foreach (var tagjson in tags)
            {
                tagsIDList.Add(tagjson.Id);
                dynamic alarm = JsonConvert.DeserializeObject(tagjson.Prop);
                var tag = new TagId
                {
                    TagName = Convert.ToString(alarm.Connection),
                    PollerId = Convert.ToInt32(alarm.Opc)
                };
                try
                {
                    var tagId = Convert.ToInt32(tagjson.Id);
                    var alarms = Convert.ToString(alarm.Alarms);
                    if (alarms != null && alarms != "")
                    {
                        var alarm_ = new AlarmThreadManagerConfig();
                        var _props = _json.Deserialize(alarms, alarm_.GetType());
                        alarm_ = (AlarmThreadManagerConfig)_props;
                        if (alarm_.Enabled)
                        {
                            alarm_.TagId = Convert.ToInt32(tagjson.Id);
                            alarm_.Tag = tag;


                            if (alarm_.HihiSeverity == null) alarm_.HihiSeverity = Double.MaxValue;
                            if (alarm_.HiSeverity == null) alarm_.HiSeverity = Double.MaxValue;
                            if (alarm_.LoSeverity == null) alarm_.LoSeverity = Double.MinValue;
                            if (alarm_.LoloSeverity == null) alarm_.LoloSeverity = Double.MinValue;
                            _cfgs.Add(alarm_);
                        }

                    }
                }
                catch (Exception ex)
                {
                    logger.Logged("Err", "Неверная конфигурация тревоги [" + tag.PollerId + "][" + tag.TagName + "] : " +
                        ex.Message, "AlarmThreadManager", "LoadAlarmsLastStates");
                }
            }


            try
            {

                using (_context)
                {
                    var Salarms =
                        _context.Alarms.Where(x => x.STime > Sdate && x.STime < Edate && tagsIDList.Contains(x.TagId)).OrderByDescending(x => x.STime);
                    var Salarmslist = Salarms.ToList();

                    foreach (var salarm in Salarmslist)
                    {
                        var row = new List<string>();
                        string endS = "", startS = "";

                        var index = _cfgs.FindIndex(x => x.TagId == salarm.TagId);
                        if (index != -1)
                        {
                            switch (salarm.SRes)
                            {
                                case -2:
                                    startS = _cfgs.ElementAt(index).LoloText;
                                    break;
                                case -1:
                                    startS = _cfgs.ElementAt(index).LoText;
                                    break;
                                case 0:
                                    startS =
                                        _cfgs.ElementAt(index).NormalText;
                                    break;
                                case 1:
                                    startS = _cfgs.ElementAt(index).HiText;
                                    break;
                                case 2:
                                    startS = _cfgs.ElementAt(index).HihiText;
                                    break;
                            }
                            switch (salarm.ERes)
                            {
                                case -2:
                                    endS = _cfgs.ElementAt(index).LoloText;
                                    break;
                                case -1:
                                    endS = _cfgs.ElementAt(index).LoText;
                                    break;
                                case 0:
                                    endS = _cfgs.ElementAt(index).NormalText;
                                    break;
                                case 1:
                                    endS = _cfgs.ElementAt(index).HiText;
                                    break;
                                case 2:
                                    endS = _cfgs.ElementAt(index).HihiText;
                                    break;
                            }

                        }
                        else
                        {
                            endS = "Отсутствует  конфигурация"; startS = "Отсутствует  конфигурация";
                        }
                        row.Add(startS);
                        row.Add(salarm.STime.ToString());
                        row.Add(endS);
                        row.Add(salarm.ETime.ToString());
                        row.Add(salarm.AckTime.ToString());
                        rez.Add(row);

                    }
                    logger.Logged("Info", " Report loaded..." + name, "ReportServer", "Rep2");
                    report.Name = name;
                    report.Head = new List<string> { "Начало события", "Время начала", "Конец события", "Время конца", "Время квитирования" };
                    report.Rows = rez;
                }
            }

            catch (Exception ex)
            {
                return report;
            }
            return report;
        }
      
        public Report GetReport(int id, Dictionary<string, dynamic> parameters)
        {
            var report = new Report();
            switch (id)
            {
                case 1:
                   // report = ZamReport(ReportName(id), parameters);
                    break;
                case 2:
                   // report = UserEvents(ReportName(id), parameters);
                    break;
                case 3:
                    //report = LastStateReport(ReportName(id), parameters);
                    break;
                case 4:
                    report = LinkReport(ReportName(id), parameters);
                    break;
                case 8:
                    report = UserEventReport(ReportName(id), parameters);
                    break;
                case 6:
                    report = AlarmReport(ReportName(id), parameters);
                    break;
                case 7:
                    report = EventReport(ReportName(id), parameters);
                    break;
            }
            return report;
        }

        public MemoryStream GetExcel(int id, Dictionary<string, dynamic> parameters, out string journal)
        {
            MemoryStream reportstStream = null;
            var report = GetReport(id, parameters);
            reportstStream = GetExcelReport(report, parameters);
            journal = report.Name;
            return reportstStream;
        }

        private static void InitializeWorkbook()
        {
            _workbook = new HSSFWorkbook();
            DocumentSummaryInformation dsi = PropertySetFactory.CreateDocumentSummaryInformation();
            dsi.Company = "NPOI Team";
            _workbook.DocumentSummaryInformation = dsi;
            SummaryInformation si = PropertySetFactory.CreateSummaryInformation();
            si.Subject = "NPOI SDK Example";
            _workbook.SummaryInformation = si;
        }

        public MemoryStream GetExcelReport(Report report, Dictionary<string, dynamic> parameters)
        {
            InitializeWorkbook();
            var _date = DateTime.Now;
            string op1 = "";
            string op2 = "";
            #region
            ICellStyle journal_name_style = _workbook.CreateCellStyle();
            journal_name_style.Alignment = HorizontalAlignment.Center;
            //create a font style
            IFont font = _workbook.CreateFont();
            font.FontHeight = 20 * 20;
            journal_name_style.SetFont(font);

            journal_name_style.BorderRight = BorderStyle.Thin;
            journal_name_style.BorderBottom = BorderStyle.Thin;

            ICellStyle journal_period_style = _workbook.CreateCellStyle();
            journal_period_style.Alignment = HorizontalAlignment.Center;
            //create a font style
            IFont font1 = _workbook.CreateFont();
            font1.FontHeight = 14 * 14;
            journal_period_style.SetFont(font1);


            ICellStyle header = _workbook.CreateCellStyle();
            header.VerticalAlignment = VerticalAlignment.Top;
            header.WrapText = true;
            header.BorderRight = BorderStyle.Thin;
            header.BorderBottom = BorderStyle.Thin;

            ICellStyle redrow = _workbook.CreateCellStyle();
            redrow.FillForegroundColor = HSSFColor.Red.Index;
            redrow.FillPattern = FillPattern.SolidForeground;
            redrow.Alignment = HorizontalAlignment.Center;
            redrow.BorderRight = BorderStyle.Thin;
            redrow.BorderBottom = BorderStyle.Thin;

            ICellStyle greenrow = _workbook.CreateCellStyle();
            greenrow.FillForegroundColor = HSSFColor.LightGreen.Index;
            greenrow.FillPattern = FillPattern.SolidForeground;
            greenrow.Alignment = HorizontalAlignment.Center;
            greenrow.BorderRight = BorderStyle.Thin;
            greenrow.BorderBottom = BorderStyle.Thin;

            ICellStyle grey40row = _workbook.CreateCellStyle();
            grey40row.FillForegroundColor = HSSFColor.Grey80Percent.Index;
            grey40row.FillPattern = FillPattern.SolidForeground;
            grey40row.Alignment = HorizontalAlignment.Center;
            grey40row.BorderRight = BorderStyle.Thin;
            grey40row.BorderBottom = BorderStyle.Thin;

            ICellStyle grey20row = _workbook.CreateCellStyle();
            grey20row.FillForegroundColor = HSSFColor.Grey25Percent.Index;
            grey20row.FillPattern = FillPattern.SolidForeground;
            grey20row.Alignment = HorizontalAlignment.Center;
            grey20row.BorderRight = BorderStyle.Thin;
            grey20row.BorderBottom = BorderStyle.Thin;


            ICellStyle bluerow = _workbook.CreateCellStyle();
            bluerow.FillForegroundColor = HSSFColor.LightBlue.Index;
            bluerow.FillPattern = FillPattern.SolidForeground;
            bluerow.Alignment = HorizontalAlignment.Center;
            bluerow.BorderRight = BorderStyle.Thin;
            bluerow.BorderBottom = BorderStyle.Thin;

            ICellStyle zerorow = _workbook.CreateCellStyle();
            zerorow.Alignment = HorizontalAlignment.Center;
            zerorow.BorderRight = BorderStyle.Thin;
            zerorow.BorderBottom = BorderStyle.Thin;

            #endregion

            foreach (var parameter in parameters)
            {
                if (parameter.Key == "StartDate")
                    _date = Convert.ToDateTime(Convert.ToString(parameter.Value));
            }

            ICell cell;
            IRow row;
            string head = "Суточная отчетность";
            var col = 0; var rown = 0; var icell = 0;
            col = report.Head.Count - 1;
            rown = 0;
            HSSFSheet sheet = (HSSFSheet)_workbook.CreateSheet(head);

            IRow _name = sheet.CreateRow(rown);
            _name.HeightInPoints = 30;
            cell = _name.CreateCell(0);
            //set the title of the sheet
            cell.SetCellValue(head);
            cell.CellStyle = journal_name_style;
            var headerr = new CellRangeAddress(rown, rown, 0, col);
            sheet.AddMergedRegion(headerr);
            ((HSSFSheet)sheet).SetEnclosedBorderOfRegion(headerr, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            rown++;

            IRow jor_name = sheet.CreateRow(rown);
            jor_name.HeightInPoints = 30;
            cell = jor_name.CreateCell(0);
            //set the title of the sheet
            cell.SetCellValue(report.Name);
            cell.CellStyle = journal_name_style;
            var region_name = new CellRangeAddress(rown, rown, 0, col);
            sheet.AddMergedRegion(region_name);
            ((HSSFSheet)sheet).SetEnclosedBorderOfRegion(region_name, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            rown++;

            IRow row_period = sheet.CreateRow(rown);
            row_period.HeightInPoints = 30;
            ICell cell_period = row_period.CreateCell(0);
            cell_period.SetCellValue("Отчетные сутки: " + _date.Day + "." + _date.Month + "." + _date.Year + "");
            cell_period.CellStyle = journal_period_style;
            var region_date = new CellRangeAddress(rown, rown, 0, col);
            sheet.AddMergedRegion(region_date);
            ((HSSFSheet)sheet).SetEnclosedBorderOfRegion(region_date, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            rown++;


            IRow date_form = sheet.CreateRow(rown);
            ICell date = date_form.CreateCell(0);
            var dateTime = DateTime.Now;
            date.SetCellValue("Журнал сформирован: " + dateTime + "");
            CellRangeAddress region = new CellRangeAddress(rown, rown, 0, col);
            sheet.AddMergedRegion(region);
            ((HSSFSheet)sheet).SetEnclosedBorderOfRegion(region, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            rown++;

            row = sheet.CreateRow(rown);
            rown++;
            IRow headerRow = sheet.CreateRow(rown);
            rown++;

            for (var rc = 0; rc < report.Head.Count; rc++)
                headerRow.CreateCell(rc).SetCellValue(report.Head[rc]);

            for (var j = 0; j < report.Head.Count; j++) headerRow.GetCell(j).CellStyle = header;
            //замопозить область
            //sheet.CreateFreezePane(0, rown);  
            //sheet.SetAutoFilter(new CellRangeAddress(rown - 1, report.Head.Count + rown, 0, col));
            foreach (List<string> t in report.Rows)
            {
                row = sheet.CreateRow(rown);
                for (var j = 0; j < t.Count; j++)
                {
                    row.CreateCell(j).SetCellValue(t[j]);
                    row.GetCell(j).CellStyle = zerorow;
                }
                rown++;
            }

            for (int i = 0; i < col + 1; i++)
            {
                sheet.AutoSizeColumn(i);
                sheet.SetColumnWidth(i, sheet.GetColumnWidth(i) + 3 * 512);
            }

            if (_workbook != null)
            {
                var output = new MemoryStream();
                _workbook.Write(output);

                return output;
            }
            else
                return null;


        }


        public List<Domain.Abstract.AGZUObject> ObjectList()
        {

            var objList = new List<Domain.Abstract.AGZUObject>();

            objList = (from ti in _context.Objects
                       where (ti.Type == 5)
                        && !ti.Name.Contains("Setting") && !ti.Name.Contains("cfg") && !ti.Name.Contains("Rez")
                       select new Domain.Abstract.AGZUObject { Id = ti.Id, Name = ti.Name, ParentId = ti.ParentId }).ToList();

            return objList;
        }
        public List<Domain.Abstract.AGZUObject> ChildList(int parentId)
        {

            var q = "             DECLARE @ID INT = " + parentId + "                                                          ";
            q = q + "                                                                                                        ";
            q = q + " ; WITH ParentChildCTE                                                                                  ";
            q = q + " AS(                                                                                                    ";
            q = q + "     SELECT ID, ParentId, Type, Name                                                                    ";
            q = q + "     FROM Objects                                                                                       ";
            q = q + "     WHERE Id = @ID                                                                                     ";
            q = q + "                                                                                                        ";
            q = q + "     UNION ALL                                                                                          ";
            q = q + "                                                                                                        ";
            q = q + "     SELECT T1.ID, T1.ParentId, T1.Type, T1.Name                                                        ";
            q = q + "     FROM Objects T1                                                                                    ";
            q = q + "     INNER JOIN ParentChildCTE T ON T.ID = T1.ParentID                                                  ";
            q = q + "     WHERE T1.ParentID IS NOT NULL                                                                      ";
            q = q + "     )                                                                                                  ";
            q = q + " SELECT distinct Id,ParentId,REPLACE(Name,'Well','Скважина ')                                                                                      ";
            q = q + " FROM ParentChildCTE where   name not like 'Well%]' and name like 'Well%' and name not like 'WellsFlags'";


            var objList = new List<Domain.Abstract.AGZUObject>(); var rezult = MyDB.sql_query_local(q);
            foreach (var _row in rezult.rows)
                objList.Add(new AGZUObject { Id = Convert.ToInt32(_row.values[0]), Name = _row.values[2], ParentId = Convert.ToInt32(_row.values[1]) });

            return objList;
        }
    }
}




