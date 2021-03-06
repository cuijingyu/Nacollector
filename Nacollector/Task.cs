﻿using Nacollector.Ui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using System.Windows.Forms;
using NacollectorUtils;
using NacollectorUtils.Settings;
using System.Diagnostics;

namespace Nacollector
{
    public partial class MainForm : FormBase
    {
        ///
        /// 任务执行
        ///

        public AppDomain _spiderRawDomain = null;
        public SpiderDomain _spiderDomain = null;

        public SpiderDomain GetLoadSpiderDomain()
        {
            if (this._spiderDomain != null)
            {
                return this._spiderDomain;
            }

            // 创建新的 AppDomain
            AppDomainSetup domaininfo = new AppDomainSetup
            {
                ApplicationBase = Environment.CurrentDirectory
            };
            Evidence adevidence = AppDomain.CurrentDomain.Evidence;
            AppDomain rawDomain = AppDomain.CreateDomain("NacollectorSpiders", adevidence, domaininfo);

            // 动态加载 dll
            Type type = typeof(SpiderDomain);
            var spiderDomain = (SpiderDomain)rawDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);

            spiderDomain.LoadAssembly(Path.Combine(Application.StartupPath, "NacollectorSpiders.dll"));

            this._spiderRawDomain = rawDomain;
            this._spiderDomain = spiderDomain;
            return spiderDomain;
        }

        public void UnloadSpiderDomain()
        {
            if (_spiderRawDomain == null)
            {
                return;
            }

            // 卸载 dll
            AppDomain.Unload(_spiderRawDomain);
            _spiderRawDomain = null;
            _spiderDomain = null;
        }

        /// <summary>
        /// 任务线程
        /// </summary>
        public Dictionary<string, Thread> taskThreads = new Dictionary<string, Thread>();

        public void AbortTask(string taskId)
        {
            if (!taskThreads.ContainsKey(taskId))
                return;

            // 主线程委托执行，防止 Abort() 后面的代码无效
            this.BeginInvoke((MethodInvoker)delegate
            {
                if (taskThreads[taskId].IsAlive)
                {
                    taskThreads[taskId].Abort();
                }

                taskThreads.Remove(taskId);
                if (taskThreads.Count <= 0)
                {
                    UnloadSpiderDomain();
                }
            });

            return;
        }

        public void NewTaskThread(SpiderSettings settings)
        {
            // 创建任务执行线程
            var thread = new Thread(new ParameterizedThreadStart(StartTask))
            {
                IsBackground = true
            };
            thread.Start(settings);

            // 加入 Threads Dictionary
            taskThreads.Add(settings.TaskId, thread);
        }

        private void ShowCreateTaskError(string taskId, string msg, Exception ex)
        {
            string errorText = "任务新建失败, " + msg;
            Logging.Error($"{errorText} [{ex.Data.Keys + ": " + ex.Message}]");
            MessageBox.Show(errorText, "Nacollector 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AbortTask(taskId);
        }

        /// <summary>
        /// 开始执行任务
        /// </summary>
        /// <param name="obj"></param>
        public void StartTask(object obj)
        {
            var settings = (SpiderSettings)obj;

            SpiderDomain spiderDomain = null;
            try
            {
                spiderDomain = GetLoadSpiderDomain();
            }
            catch (Exception ex)
            {
                ShowCreateTaskError((string)settings.TaskId, "NacollectorSpiders.dll 调用失败", ex);
#if DEBUG
                Debugger.Break();
#endif
                return;
            }

            // 调用目标函数
            try
            {
                // Funcs
                settings.BrowserJsRunFunc = new Action<string>((string str) => {
                    this.GetCrBrowser().RunJS(str);
                });
                settings.CrBrowserCookieGetter = new Func<CookieGetterSettings, string>((CookieGetterSettings cgSettings) =>
                {
                    GetCrCookieGetter().CreateNew(cgSettings.StartUrl, cgSettings.EndUrlReg, cgSettings.Caption);

                    if (cgSettings.InputAutoCompleteConfig != null)
                    {
                        GetCrCookieGetter().UseInputAutoComplete(
                            (string)cgSettings.InputAutoCompleteConfig["pageUrlReg"],
                            (List<string>)cgSettings.InputAutoCompleteConfig["inputElemCssSelectors"]
                        );
                    }

                    GetCrCookieGetter().BeginWork();

                    return GetCrCookieGetter().GetCookieStr();
                });

                spiderDomain.Invoke("NacollectorSpiders.PokerDealer", "NewTask", settings);
            }
            catch (ThreadAbortException)
            {
                // 进程正在被中止
                // 不进行操作
            }
            catch (Exception ex)
            {
                ShowCreateTaskError((string)settings.TaskId, "无法调用 NacollectorSpiders.PokerDealer.NewTask", ex);
#if DEBUG
                Debugger.Break();
#endif
                return;
            }

            AbortTask((string)settings.TaskId);
        }

        /// <summary>
        /// 执行爬虫任务，新的 AppDomain 中
        /// </summary>
        public class SpiderDomain : MarshalByRefObject
        {
            Assembly assembly = null;

            public void LoadAssembly(string assemblyPath)
            {
                try
                {
                    assembly = Assembly.LoadFile(assemblyPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(ex.Message);
                }
            }

            public bool Invoke(string fullClassName, string methodName, params object[] args)
            {
                if (assembly == null)
                    return false;
                Type tp = assembly.GetType(fullClassName);
                if (tp == null)
                    return false;
                MethodInfo method = tp.GetMethod(methodName);
                if (method == null)
                    return false;
                object obj = Activator.CreateInstance(tp);
                method.Invoke(obj, args);
                return true;
            }
        }
    }
}
