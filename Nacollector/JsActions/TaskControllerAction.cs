﻿using Nacollector.Browser;
using NacollectorSpiders;
using NacollectorUtils.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nacollector.JsActions
{
    /// <summary>
    /// 任务控制器操作
    /// </summary>
    [Serializable]
    class TaskControllerAction
    {
        private MainWin _form;
        private CrBrowser crBrowser;

        public TaskControllerAction(MainWin form, CrBrowser crBrowser)
        {
            this._form = form;
            this.crBrowser = crBrowser;
        }
        
        // 创建新任务
        public void createTask(string taskId, string className, string classLabel, string parmsJsonStr)
        {
            // 配置
            var settings = new SpiderSettings
            {
                TaskId = taskId,
                ClassName = className,
                ClassLabel = classLabel,
                ParmsJsonStr = parmsJsonStr
            };

            _form.NewTaskThread(settings);
        }

        // 终止任务
        public bool abortTask(string taskId)
        {
            _form.AbortTask(taskId);

            return true;
        }
    }
}
