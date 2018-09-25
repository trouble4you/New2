using System;
using System.Collections.Generic;
using System.Linq;
using WebSphere.ClientOPC;
using WebSphere.Domain.Abstract;
using WebSphere.Domain.Concrete;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.HPSF;
using NPOI.POIFS.FileSystem;
using NPOI.SS.UserModel;
using System.Text;
using NPOI.SS.Util;

namespace WebSphere.Trends
{
    public class Trend : ITrends
    {

        private static HSSFWorkbook _workbook;
        private EFDbContext context = new EFDbContext();
        private static readonly Logging logger = new Logging();
        private static IOpcPoller _opcPoller = new OpcPoller();
        public class Obj
        {
            public int Id;
            public int Type;
            public int? Parent;
            public string Name;
            public string Prop;
        }
        public class TSqlPropsTag
        {
            public int Id;
            public string Name = "";
            public string Alias = "";
            public int Opc;
            public string Connection = "";
            public string Description = "";
            public int ControllerType;
            public int RealType;
            public string Register = "";
            public int AccessType;
            public int Order;
            public double InMin;
            public double InMax;
            public double OutMin;
            public double OutMax;
            public string IsSpecialTag = "";
            public bool History_IsPermit;
            public int RegPeriod;
            public double Deadbend;
            public int State;
            public bool UpdateAnyway = false;
            //public Event Events;
            //public Alarm Alarms;
        }
        private List<Obj> _objects;
        private List<Obj> _tags;
        public void LoadObjSign()
        {
            _objects = (from ti in context.Objects
                        where (ti.Type == 1 || ti.Type == 2 || ti.Type == 5 || ti.Type == 21)
                         && !ti.Name.Contains("Setting") && !ti.Name.Contains("cfg") && !ti.Name.Contains("Rez")
                        select new Obj { Id = ti.Id, Name = ti.Name, Parent = ti.ParentId, Type = ti.Type, Prop = null }).ToList();

            _tags = (from ti in context.Objects
                     join to in context.Properties on ti.Id equals to.ObjectId
                     where ti.Type == 2 && to.PropId == 0
                     select new Obj { Id = ti.Id, Name = ti.Name, Parent = ti.ParentId, Type = ti.Type, Prop = to.Value }).ToList();


        }
        // GET: Reports
        public List<int> GetChildTags(int parID)
        {
            var rez = new List<int>();
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

        public List<OrderedDictionary> GetTagTree(List<Obj> tags)
        {
            var rez = new List<OrderedDictionary>();

            foreach (var tag in tags)
            {
                var props = JsonConvert.DeserializeObject<TSqlPropsTag>(tag.Prop);

                if (props.Description != null && props.Description != "" && !props.Description.Contains("*"))
                {
                    var _signal = new OrderedDictionary();
                    _signal.Add("signal_name", props.Description);
                    _signal.Add("signal_id", tag.Id);
                    rez.Add(_signal);
                }
            }
            return rez;

        }

        MyTrendPage ITrends.GetTrend(int id)
        {

            var mtp = new MyTrendPage();
            mtp.objects= context.Objects.Where(x => (x.Type == 1)||(x.Type == 5)).ToList();
            var _object = context.Objects.FirstOrDefault(x => (x.Id == id));
            if (_object != null)
            {
                var objectId = id;
                string objectName = _object.Name;
                var typeId = _object.Type;

                var _object_type = context.ObjectTypes.FirstOrDefault(x => (x.Id == _object.Type));
                string objectTypeStr = ""; 
                  objectTypeStr = "Объект";
                LoadObjSign();
                var tagsId = GetChildTags(_object.Id);
                var tags = _tags.Where(x => tagsId.Contains(x.Id)).ToList();
                mtp.signals = GetTagTree(tags);

                DateTime dt2 = DateTime.Now;
                DateTime dt1 = dt2.AddHours(-1);



                mtp.start_date = (ulong)ConvertToUnixTimestamp(dt1);
                mtp.end_date = (ulong)ConvertToUnixTimestamp(dt2);
                // mtp.start_date= WebSphere.WebUI.MyTime.GetDatetimeFromDate(dt1);
                // mtp.end_date = WebSphere.WebUI.MyTime.GetDatetimeFromDate(dt2);

                mtp.object_type_str = objectTypeStr;

                mtp.object_name = objectName;

                mtp.object_id = objectId;
            }
            return mtp;
        }


        public static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }

        public static double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date - origin;
            return Math.Floor(diff.TotalSeconds);
        }
        public static List<int> ParseSignalsList(string s)
        {
            var signals = new List<int>();
            int state = 0;
            string tmp = "";
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (state == 0 && c == '{') state = 1;
                else if (state == 1 && c == ',')
                {
                    signals.Add(Convert.ToInt32(tmp));
                    tmp = "";
                }
                else if (state == 1 && c == '}')
                {
                    if (tmp.Length > 0) signals.Add(Convert.ToInt32(tmp));
                    break;
                }
                else if (state == 1)
                {
                    tmp += c;
                }
            }

            return signals;
        }
        public MemoryStream GetExcel(string sd1, string ed1, string signalId)
        {
            MemoryStream reportstStream = null;
            var report = GetTrend(sd1, ed1, signalId);
            reportstStream = GetExcelReport(report);
            //journal = report.Name;
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

        public MemoryStream GetExcelReport(MyTrendData report)
        {
            InitializeWorkbook();
            var _date = DateTime.Now;
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

            foreach (var t in report.trends)
            {
                ICell cell;
                IRow row;
               var _tag = (from ti in  context.Properties 
                         where ti.ObjectId == t.id &&  ti.PropId == 0
                         select new Obj { Id = ti.Id, Prop = ti.Value }).FirstOrDefault();

                var props = JsonConvert.DeserializeObject<TSqlPropsTag>(_tag.Prop);
 
                HSSFSheet sheet = (HSSFSheet)_workbook.CreateSheet(props.Description);

                var col = 0; var rown = 0; var icell = 0;
                rown = 0;
                row = sheet.CreateRow(rown);
                row.CreateCell(0).SetCellValue("Время");
                row.CreateCell(1).SetCellValue("Значение");
                rown++;

                for (var j = 0; j < t.Points.Count; j++)
                {
                    row = sheet.CreateRow(rown);
                    row.CreateCell(0).SetCellValue(ConvertFromUnixTimestamp(t.Points[j].dt).ToString());
                    row.CreateCell(1).SetCellValue(Math.Round(t.Points[j].v,3)); 
                     rown++;
                 }

                for (int i = 0; i < 2; i++)
                {
                    sheet.AutoSizeColumn(i);
                    sheet.SetColumnWidth(i, sheet.GetColumnWidth(i) + 3 * 512);
                }
            }

            //ICell cell;
            //IRow row;
            //string head = "Суточная отчетность";
            //var col = 0; var rown = 0; var icell = 0;
            //col = report.signals.Count - 1;
            //rown = 0;
            //HSSFSheet sheet = (HSSFSheet)_workbook.CreateSheet(head);

            //IRow _name = sheet.CreateRow(rown);
            //_name.HeightInPoints = 30;
            //cell = _name.CreateCell(0);
            ////set the title of the sheet
            //cell.SetCellValue(head);
            //cell.CellStyle = journal_name_style;
            //var headerr = new CellRangeAddress(rown, rown, 0, col);
            //sheet.AddMergedRegion(headerr);
            //((HSSFSheet)sheet).SetEnclosedBorderOfRegion(headerr, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            //rown++;

            //IRow jor_name = sheet.CreateRow(rown);
            //jor_name.HeightInPoints = 30;
            //cell = jor_name.CreateCell(0);
            ////set the title of the sheet

            ////cell.SetCellValue(report.Name);

            //cell.CellStyle = journal_name_style;
            //var region_name = new CellRangeAddress(rown, rown, 0, col);
            //sheet.AddMergedRegion(region_name);
            //((HSSFSheet)sheet).SetEnclosedBorderOfRegion(region_name, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            //rown++;

            //IRow row_period = sheet.CreateRow(rown);
            //row_period.HeightInPoints = 30;
            //ICell cell_period = row_period.CreateCell(0);
            //cell_period.SetCellValue("Отчетные сутки: " + _date.Day + "." + _date.Month + "." + _date.Year + "");
            //cell_period.CellStyle = journal_period_style;
            //var region_date = new CellRangeAddress(rown, rown, 0, col);
            //sheet.AddMergedRegion(region_date);
            //((HSSFSheet)sheet).SetEnclosedBorderOfRegion(region_date, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            //rown++;


            //IRow date_form = sheet.CreateRow(rown);
            //ICell date = date_form.CreateCell(0);
            //var dateTime = DateTime.Now;
            //date.SetCellValue("Тренд сформирован: " + dateTime + "");
            //CellRangeAddress region = new CellRangeAddress(rown, rown, 0, col);
            //sheet.AddMergedRegion(region);
            //((HSSFSheet)sheet).SetEnclosedBorderOfRegion(region, NPOI.SS.UserModel.BorderStyle.Thin, 8);
            //rown++;

            //row = sheet.CreateRow(rown);
            //rown++;
            //IRow headerRow = sheet.CreateRow(rown);
            //rown++;

            //for (var rc = 0; rc < report.signals.Count; rc=rc+2)
            //    headerRow.CreateCell(rc).SetCellValue(report.signals[rc/2]);

            //for (var j = 0; j < report.trends.Count*2; j++) headerRow.GetCell(j).CellStyle = header;
            ////замопозить область
            ////sheet.CreateFreezePane(0, rown);  
            ////sheet.SetAutoFilter(new CellRangeAddress(rown - 1, report.Head.Count + rown, 0, col));
            //foreach (var t in report.trends)
            //{
            //    row = sheet.CreateRow(rown);
            //    for (var j = 0; j < t.Points.Count; j++)
            //    {
            //        row.CreateCell(j).SetCellValue(t[j]);
            //        row.GetCell(j).CellStyle = zerorow;
            //    }
            //    rown++;
            //}



            //for (int i = 0; i < col + 1; i++)
            //{
            //    sheet.AutoSizeColumn(i);
            //    sheet.SetColumnWidth(i, sheet.GetColumnWidth(i) + 3 * 512);
            //}





            if (_workbook != null)
            {
                var output = new MemoryStream();
                _workbook.Write(output);

                return output;
            }
            else
                return null;


        }

        public MyTrendData GetTrend(string sd1, string ed1, string signalId)
        {
            var res = new MyTrendData();
            //начало тренда
            /*     string sd1 = data.Get("start_date");
                 //конец тренда
                 string ed1 = data.Get("end_date");
                 //список сигналов тренда
                 string signalId = data.Get("signal_id");
                 */
            List<int> signals = ParseSignalsList(signalId);

            /* string reset_cach = data.Get("reset_cach");
             string object_id = data.Get("object_id");
             string object_type_id = data.Get("object_id");
             string auto_update = data.Get("auto_update");
             */
            if (sd1 == null || ed1 == null)
            {
                sd1 = "0";
                ed1 = "0";
            }
            sd1 = sd1.Replace(',', '.');
            ed1 = ed1.Replace(',', '.');

            var sD = Convert.ToUInt64(sd1);
            var eD = Convert.ToUInt64(ed1);

            var dt1 = ConvertFromUnixTimestamp(sD);
            var dt2 = ConvertFromUnixTimestamp(eD);
            if (sD > 2000000000) return res;
            if (eD > 2000000000) return res;

            /*     if (auto_update != "0")
                 {
                     dt2 = DateTime.UtcNow;
                     dt1 = dt2.AddSeconds(-1 * Convert.ToInt32(auto_update));

                     sD = (ulong)ConvertToUnixTimestamp(dt1);
                     eD = (ulong)ConvertToUnixTimestamp(dt2);

                 }
                 */

            double max = 0;
            double min = 0;

            var myTrends = new List<MyTrend>();
            foreach (int sid in signals)
            {
                var myTrend = new MyTrend();
                myTrend.Points = new List<Point>();
                res.signals.Add(sid);
                myTrend.id = sid;
                var analog = context.SignalsAnalog.FirstOrDefault(x => (x.TagId == sid));
                var discrete = context.SignalsDiscrete.FirstOrDefault(x => (x.TagId == sid));
                var points = new List<Point>();
                if (analog != null)
                {
                    //запись до начала тренда 
                    var _analog = context.SignalsAnalog.Where(x => (x.TagId == sid && x.Datetime < dt1))
                        .OrderByDescending(x => x.Datetime)
                        .FirstOrDefault();
                    //запись после конца тренда 
                    var analog_ = context.SignalsAnalog.Where(x => (x.TagId == sid && x.Datetime > dt2))
                        .OrderBy(x => x.Datetime)
                        .FirstOrDefault();
                    //записи в промежутке между началом тренда и его концом
                    var analogs =
                        context.SignalsAnalog.Where(x => (x.TagId == sid && x.Datetime > dt1 && x.Datetime < dt2)).ToList();

                    if (_analog != null) analogs.Add(_analog);
                    if (analog_ != null) analogs.Add(analog_);

                    points.AddRange(analogs.Select(sa => new Point
                    {
                        v = sa.Value,
                        dt = (ulong)ConvertToUnixTimestamp(sa.Datetime.AddSeconds(DateTime.Now.Second))
                    }));
                }
                else if (discrete != null)
                {
                    var _discrete = context.SignalsDiscrete.Where(x => (x.TagId == sid && x.Datetime < dt1))
                        .OrderByDescending(x => x.Datetime)
                        .FirstOrDefault();
                    var discrete_ = context.SignalsDiscrete.Where(x => (x.TagId == sid && x.Datetime > dt2))
                        .OrderBy(x => x.Datetime)
                        .FirstOrDefault();
                    var discretes =
                        context.SignalsDiscrete.Where(x => (x.TagId == sid && x.Datetime > dt1 && x.Datetime < dt2)).ToList();

                    if (_discrete != null) discretes.Add(_discrete);
                    if (discrete_ != null) discretes.Add(discrete_);
                    points.AddRange(discretes.Select(sa => new Point
                    {
                        v = Convert.ToDouble(sa.Value),
                        dt = (ulong)ConvertToUnixTimestamp(sa.RegTime.AddSeconds(DateTime.Now.Second))
                    }));
                }

                var maxsign = points.OrderByDescending(x => x.v).FirstOrDefault();
                var minsign = points.OrderBy(x => x.v).FirstOrDefault();

                if (minsign != null && minsign.v < min) min = minsign.v;
                if (maxsign != null && maxsign.v > max) max = maxsign.v;

                foreach (var point in points.OrderBy(x => x.dt))
                {
                    myTrend.Points.Add(point);
                }

                if (myTrend.Points.Count > 0 && myTrend.Points.Last().dt < eD)
                {
                    var p = myTrend.Points.Last();
                    var p2 = new Point { v = p.v, dt = eD };
                    myTrend.Points.Add(p2);
                }
                myTrends.Add(myTrend);
            }
            res.trends = myTrends;
            res.min = min;
            res.max = max;

            res.date_min_sec = sD;
            res.date_max_sec = eD;

            return res;
        }

        public MyTrendData GetNewTrend(string sd1, string ed1, string signalId)
        {
            var res = new MyTrendData();

            List<int> signals = ParseSignalsList(signalId);

            /* string reset_cach = data.Get("reset_cach");
             string object_id = data.Get("object_id");
             string object_type_id = data.Get("object_id");
             string auto_update = data.Get("auto_update");
             */
            if (sd1 == null || ed1 == null)
            {
                sd1 = "0";
                ed1 = "0";
            }
            sd1 = sd1.Replace(',', '.');
            ed1 = ed1.Replace(',', '.');

            var sD = Convert.ToUInt64(sd1);
            var eD = Convert.ToUInt64(ed1);

            var dt1 = ConvertFromUnixTimestamp(sD);
            var dt2 = ConvertFromUnixTimestamp(eD);
            if (sD > 2000000000) return res;
            if (eD > 2000000000) return res;

            /*     if (auto_update != "0")
                 {
                     dt2 = DateTime.UtcNow;
                     dt1 = dt2.AddSeconds(-1 * Convert.ToInt32(auto_update));

                     sD = (ulong)ConvertToUnixTimestamp(dt1);
                     eD = (ulong)ConvertToUnixTimestamp(dt2);

                 }
                 */

            double max = 0;
            double min = 0;

            var myTrends = new List<MyTrend>();
            foreach (int sid in signals)
            {
                var myTrend = new MyTrend();
                myTrend.Points = new List<Point>();
                res.signals.Add(sid);
                myTrend.id = sid;
                var analog = context.SignalsAnalog.FirstOrDefault(x => (x.TagId == sid));
                var discrete = context.SignalsDiscrete.FirstOrDefault(x => (x.TagId == sid));
                var points = new List<Point>();
                if (analog != null)
                {
                    //запись до начала тренда 
                    var _analog = context.SignalsAnalog.Where(x => (x.TagId == sid && x.Datetime < dt1))
                        .OrderByDescending(x => x.Datetime)
                        .FirstOrDefault();
                    //запись после конца тренда 
                    var analog_ = context.SignalsAnalog.Where(x => (x.TagId == sid && x.Datetime > dt2))
                        .OrderBy(x => x.Datetime)
                        .FirstOrDefault();
                    //записи в промежутке между началом тренда и его концом
                    var analogs =
                        context.SignalsAnalog.Where(x => (x.TagId == sid && x.Datetime > dt1 && x.Datetime < dt2)).ToList();

                    if (_analog != null) analogs.Add(_analog);
                    if (analog_ != null) analogs.Add(analog_);
                    points.AddRange(analogs.Select(sa => new Point
                    {
                        v = sa.Value,
                        dt = (ulong)ConvertToUnixTimestamp(sa.Datetime)
                    }));
                }
                else if (discrete != null)
                {
                    var _discrete = context.SignalsDiscrete.Where(x => (x.TagId == sid && x.Datetime < dt1))
                        .OrderByDescending(x => x.Datetime)
                        .FirstOrDefault();
                    var discrete_ = context.SignalsDiscrete.Where(x => (x.TagId == sid && x.Datetime > dt2))
                        .OrderBy(x => x.Datetime)
                        .FirstOrDefault();
                    var discretes =
                        context.SignalsDiscrete.Where(x => (x.TagId == sid && x.Datetime > dt1 && x.Datetime < dt2)).ToList();

                    if (_discrete != null) discretes.Add(_discrete);
                    if (discrete_ != null) discretes.Add(discrete_);
                    points.AddRange(discretes.Select(sa => new Point
                    {
                        v = Convert.ToDouble(sa.Value),
                        dt = (ulong)ConvertToUnixTimestamp(sa.Datetime)
                    }));
                }

                var maxsign = points.OrderByDescending(x => x.v).FirstOrDefault();
                var minsign = points.OrderBy(x => x.v).FirstOrDefault();

                if (minsign != null && minsign.v < min) min = minsign.v;
                if (maxsign != null && maxsign.v > max) max = maxsign.v;

                foreach (var point in points.OrderBy(x => x.dt))
                {
                    myTrend.Points.Add(point);
                }

                if (myTrend.Points.Count > 0 && myTrend.Points.Last().dt < eD)
                {
                    var p = myTrend.Points.Last();
                    var p2 = new Point { v = p.v, dt = eD };
                    myTrend.Points.Add(p2);
                }
                myTrends.Add(myTrend);
            }
            res.trends = myTrends;
            res.min = min;
            res.max = max;

            res.date_min_sec = sD;
            res.date_max_sec = eD;

            return res;
        }


        public List<MyTrend> GetTrendOpc(string signalId)
        {
            List<int> signals = ParseSignalsList(signalId);
            return (from sign in signals
                    let rawVal = _opcPoller.ReadTag(sign)
                    where rawVal.LastValue != null
                    select new MyTrend
                    {
                        id = sign,
                        Points = new List<Point>
                    {
                        new Point
                        {
                            v = Math.Round(Convert.ToDouble(rawVal.LastValue.Replace(".", ",")), 3), dt = (ulong) ConvertToUnixTimestamp(DateTime.Now)
                        }
                    }
                    }).ToList();
        }
    }
}
