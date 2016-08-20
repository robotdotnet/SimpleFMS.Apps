using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI.Notifications;
using Windows.UI.Xaml;

namespace SimpleFMS.Apps.UWP
{
    sealed partial class App : Application
    {

        private bool m_isInBackgroundMode;


        void Construct()
        {
            EnteredBackground += App_EnteredBackground;
            LeavingBackground += App_LeavingBackground;
        }

        private void App_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            ShowToast("Leaving Background");
            m_isInBackgroundMode = false;

            RetainedDeviceState.Instance.StartApplication();


            if (Window.Current.Content == null)
            {
                ShowToast("Loading view");
                CreateRootFrame(ApplicationExecutionState.Running, string.Empty);
            }
        }

        private void App_EnteredBackground(object sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
        {
            ShowToast("Entered Background");
            m_isInBackgroundMode = true;

            RetainedDeviceState.Instance.StopApplication();

        }

        /// <summary>
        /// Sends a toast notification
        /// </summary>
        /// <param name="msg">Message to send</param>
        public void ShowToast(string msg)
        {
            return;
            Debug.WriteLine(msg + "\n");

            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

            var toastTextElements = toastXml.GetElementsByTagName("text");
            toastTextElements[0].AppendChild(toastXml.CreateTextNode(msg));

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
    }
}
