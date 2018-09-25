using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Opc.Da;
using WebSphere.Domain.Abstract;
using WebSphere.Domain.Concrete;

namespace WebSphere.ClientOPC
{

    public class DbSaver
    {
       public List<TagValueContainer> signals_to_save = new List<TagValueContainer>();
        Thread saver;
        bool keepWork;
        bool logSqlQueries = false;

        private static EFDbContext context = new EFDbContext(); 
        private static readonly Logging Logger = new Logging();

        public DbSaver()
        {
            keepWork = true;
            logSqlQueries = (System.Configuration.ConfigurationSettings.AppSettings["log_sql_queries"] == "yes");
            saver = new Thread(Run);
            saver.Start();
        }

        ~DbSaver() { if (!keepWork) StopAll(); }

        // TODO: сделать все-таки отдельный объект, без привязки к БД чтобы все не сломалось при автогенерации
        public void SaveSignal(TagValueContainer signal)
        {
            lock (signals_to_save)
            {
                signals_to_save.Add(signal);
                Logger.Logged("Info", "Добавлен сигнал в очередь для логгирования "+ signal.Tag.Id, "DbSaver",
                    "StopAll");
              ///  Logger.Trace();
            }
        }

        public void StopAll()
        {
            keepWork = false;
           // Logger.Info("Останавливаем поток модуля сохранения записей в БД...");

            Logger.Logged("Info", "Останавливаем поток модуля сохранения записей в БД", "DbSaver",
                "StopAll");
            saver.Join();
        }

        void Run()
        {
            TagValueContainer item;
            bool logged = false;
            //Logger.Info("Запущен поток модуля сохранения записей в БД...");
            Logger.Logged("Info", "Запущен поток модуля сохранения записей в БД...", "DbSaver",
                "Run");
            while (keepWork)
            {
                while (signals_to_save.Count > 0)
                {
                    lock (signals_to_save) { item = signals_to_save.First(); }
                     
                    if (item.LastAnalogValue!=null)
                    { 
                        var signal_ = context.SignalsAnalog.Create();                         
                        signal_.TagId = item.Tag.Id;
                        signal_.Value = Convert.ToSingle(item.LastAnalogValue);
                        signal_.Datetime = item.LastLogged.AddSeconds(DateTime.Now.Second);
                        signal_.RegTime = DateTime.Now.AddSeconds(DateTime.Now.Second);

                        context.SignalsAnalog.Add(signal_);
                        // сохраняем
                    }
                    else
                    //{
                        if (item.LastDiscreteValue != null)
                    { 
                        var signal_ = context.SignalsDiscrete.Create();
                        signal_.TagId = item.Tag.Id;
                        signal_.Value = Convert.ToBoolean(item.LastDiscreteValue);
                        signal_.Datetime = item.LastLogged.AddSeconds(DateTime.Now.Second);
                        signal_.RegTime = DateTime.Now.AddSeconds(DateTime.Now.Second);

                        context.SignalsDiscrete.Add(signal_);
                    }
                    try
                    {
                        context.SaveChanges();
                        signals_to_save.Remove(item);
                    }
                    //}

                    //try
                    //{
                    //    logged = SaveDataGag(tableName, item.Tag.Id, val, item.LastLogged.AddMilliseconds(-item.LastLogged.Millisecond));
                    //}
                    catch (Exception ex)
                    {
                        Logger.Logged("Error", "Не удалось сохранить запись в БД: ["+ ex.GetType() + "] " + ex.Message,  "DbSaver",
                "Run");  
                        Logger.Logged("Warn", "Повторим попытку после обработки всех остальных данных в очереди...", "DbSaver",
                "Run");
                        Thread.Sleep(100);
                        lock (signals_to_save)
                        {
                            signals_to_save.Remove(item);
                            signals_to_save.Add(item);
                        }
                        continue;
                    }
                    //if (logged) Logger.Trace("Cигнал записан в БД: {0} ", item.TagId);
                    //else Logger.Trace("Cигнал незаписан в БД: {0} Вероятнее всего там уже есть запись с такими параметрами", item.TagId);
                    //lock (signals_to_save) { signals_to_save.Remove(item); }
                }
                Thread.Sleep(10);
            }

        }

      }
}
