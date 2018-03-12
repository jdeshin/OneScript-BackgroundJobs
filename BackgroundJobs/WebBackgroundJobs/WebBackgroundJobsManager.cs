using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.HostedScript.Library;

namespace OneScript.HttpServices
{
    public static class WebBackgroundJobsManager
    {
        static System.Collections.Hashtable jobsKeys = new System.Collections.Hashtable();
        static System.Collections.Hashtable jobs = new System.Collections.Hashtable();

        public static void ExecuteJob(object stateInfo)
        {
            WebBackgroundJob job = (WebBackgroundJob)stateInfo;

            System.Collections.Hashtable syncJobsKeys;
            syncJobsKeys = System.Collections.Hashtable.Synchronized(jobsKeys);

            if (job.Key != "" && job.Key != null)
            {
                // Пробуем вставить в таблицу ключей
                //, если вставка неудачна, значит фоновое задание уже выполняется

                try
                {
                    syncJobsKeys.Add(job.Key, job.Key);
                }
                catch (ArgumentException)
                {
                    // Такое значение уже есть в списке, не запускаем задание
                    return;
                }
            }

            // Заполняем значения работы и вставляем ее в список
            System.Collections.Hashtable syncJobs = System.Collections.Hashtable.Synchronized(jobs);
            syncJobs.Add(job.UUID, job);
            job.Begin = DateTime.Now;

            try
            {
                AspNetHostEngine engine = new AspNetHostEngine();
                engine.CallCommonModuleProcedure(job.MethodName, job.ExecutionParameters);
                job.State = BackgroundJobState.Completed;
                job.ExecutionParameters = null;
            }
            catch (ScriptEngine.ScriptException ex)
            {
                job.ErrorInfo = new ExceptionInfoContext(ex);
                job.State = BackgroundJobState.Failed;

                System.IO.TextWriter logWriter = AspNetLog.Open();
                AspNetLog.Write(logWriter, "Error executing background job ");
                AspNetLog.Write(logWriter, ex.ToString());
                AspNetLog.Close(logWriter);
            }
            catch (Exception ex)
            {
                job.State = BackgroundJobState.Failed;

                System.IO.TextWriter logWriter = AspNetLog.Open();
                AspNetLog.Write(logWriter, "Error executing background job ");
                AspNetLog.Write(logWriter, ex.ToString());
                AspNetLog.Close(logWriter);
            }
            finally
            {
                job.End = DateTime.Now;

                try
                {
                    syncJobs.Remove(job.UUID);
                }
                catch {  /* Ничего не делаем*/}

                try
                {
                    if (job.Key != null)
                        syncJobsKeys.Remove(job.Key);
                }
                catch {  /* Ничего не делаем*/}
            }
        }
    }
}
