using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace praktik
{
   
    public partial class App : Application
    {
        // Статическое свойство для хранения ID задачи из QR-кода
        public static int? TaskIdFromQR { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Проверяем аргументы командной строки на наличие TASK:{TaskId}, task:{TaskId} или просто ID
            if (e.Args.Length > 0)
            {
                string arg = e.Args[0];
                int taskId = 0;
                
                if (arg.StartsWith("TASK:", StringComparison.OrdinalIgnoreCase))
                {
                    string taskIdStr = arg.Substring(5);
                    if (int.TryParse(taskIdStr, out taskId))
                    {
                        TaskIdFromQR = taskId;
                    }
                }
                else if (arg.StartsWith("task:", StringComparison.OrdinalIgnoreCase))
                {
                    string taskIdStr = arg.Substring(5);
                    if (int.TryParse(taskIdStr, out taskId))
                    {
                        TaskIdFromQR = taskId;
                    }
                }
                else if (int.TryParse(arg, out taskId))
                {
                    // Поддерживаем просто число (ID задачи)
                    TaskIdFromQR = taskId;
                }
            }

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            
            // После успешной авторизации откроем задачу из QR, если она есть
            loginWindow.Closed += (s, args) =>
            {
                if (LoginWindow.CurrentUser != null && TaskIdFromQR.HasValue)
                {
                    // Задача будет открыта в MainWindow после загрузки
                }
            };
        }
    }
}
