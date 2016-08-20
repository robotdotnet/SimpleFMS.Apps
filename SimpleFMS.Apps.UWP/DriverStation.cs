using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Autofac;
using SimpleFMS.Base.DriverStation;
using SimpleFMS.Networking.Client.NetworkClients;

namespace SimpleFMS.Apps.UWP
{
    class DriverStation : IDisposable
    {
        private TextBox m_teamNumberView;
        private CheckBox m_bypassView;
        private Rectangle m_dsCommView;
        private Rectangle m_rioCommView;
        private Rectangle m_eStoppedView;
        private TextBlock m_batteryView;
        private MainPage m_mainPage;

        public AllianceStation Station { get; }

        private int m_teamNumber;

        public int TeamNumber
        {
            get { return m_teamNumber;}
            set
            {
                int old = m_teamNumber;
                m_teamNumber = value;
                if (value != old)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        m_teamNumberView.Text = m_teamNumber.ToString();
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        private bool m_bypassed = true;
        public bool Bypass
        {
            get { return m_bypassed; }
            set
            {
                bool old = m_bypassed;
                m_bypassed = value;
                if (value != old)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        m_bypassView.IsChecked = m_bypassed;
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        private int m_defaultTeamNumber = 0;

        public DriverStation(MainPage parent, int index, TextBox teamNumber, CheckBox bypass, Rectangle dsComm,
            Rectangle rioComm, Rectangle eStop, TextBlock battery)
        {
            m_mainPage = parent;
            m_teamNumberView = teamNumber;
            m_bypassView = bypass;
            m_dsCommView = dsComm;
            m_rioCommView = rioComm;
            m_eStoppedView = eStop;
            m_batteryView = battery;

            m_defaultTeamNumber = index;
            Station = new AllianceStation((byte)index);
            //m_teamNumber = index;
            TeamNumber = m_defaultTeamNumber;

            m_teamNumberView.TextChanged += M_teamNumberView_TextChanged;

            m_bypassView.Checked += M_bypassView_Checked;
            m_bypassView.Unchecked += M_bypassView_Unchecked;
        }

        public void Dispose()
        {
            source.Cancel();
        }

        public void UpdateMatchState(bool running)
        {
            if (running)
            {
                m_teamNumberView.IsEnabled = false;
                m_bypassView.IsEnabled = false;
            }
            else
            {
                m_teamNumberView.IsEnabled = true;
                m_bypassView.IsEnabled = true;
            }
        }


        private CancellationTokenSource source = new CancellationTokenSource();

        internal void DriverStationDisconnect()
        {
            source.Cancel();
        }

        internal void DriverStationReconnect()
        {
            source = new CancellationTokenSource();
        }

        private async Task<bool> UpdateStationBypass(bool newValue)
        {

            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<DriverStationClient>();
                bool success = await client.UpdateDriverStationBypass(Station, newValue, source.Token);
                return success;
            }
        }

        public IDriverStationConfiguration GetCurrentState(int teamNumberToSetIfInvalid)
        {
            DriverStationConfiguration ds;


            int teamNumber = 0;
            if (!int.TryParse(m_teamNumberView.Text, out teamNumber))
            {
                ds = new DriverStationConfiguration(teamNumberToSetIfInvalid, Station, true);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    m_teamNumberView.Text = teamNumberToSetIfInvalid.ToString();
                    m_teamNumber = teamNumberToSetIfInvalid;
                    m_bypassView.IsChecked = true;
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            else
            {
                ds = new DriverStationConfiguration(teamNumber, Station, m_bypassView.IsChecked.Value);
            }
            return ds;
        }

        public void UpdateStationReport(IDriverStationReport report)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            TeamNumber = report.TeamNumber;
            Bypass = report.IsBypassed;
            m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {

                
                m_dsCommView.Fill = new SolidColorBrush(report.DriverStationConnected ? Colors.Green : Colors.Red);

                m_rioCommView.Fill = new SolidColorBrush(report.RoboRioConnected ? Colors.Green : Colors.Red);

                bool eStopped = report.IsBeingSentEStopped || report.IsReceivingEStopped;
                m_eStoppedView.Fill = new SolidColorBrush(eStopped ? Colors.Red : Colors.Green);

                double battery = Math.Round(report.RobotBattery, 2, MidpointRounding.ToEven);
                if (battery < 0) battery *= -1;
                m_batteryView.Text = battery.ToString();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public void SetFMSDisconnected()
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                m_eStoppedView.Fill = new SolidColorBrush(Colors.CornflowerBlue);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }


        private void M_bypassView_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            m_mainPage.SetDirtySettings();
        }

        private void M_bypassView_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            m_mainPage.SetDirtySettings();
        }

        private void M_teamNumberView_TextChanged(object sender, TextChangedEventArgs e)
        {
            string newValue = m_teamNumberView.Text.ToString();
            int teamNumber = 0;

            if (int.TryParse(newValue, out teamNumber))
            {
                if (teamNumber < 0)
                {
                    m_bypassView.IsChecked = true;
                    teamNumber = m_defaultTeamNumber;
                }
                else
                {
                    m_bypassView.IsChecked = false;
                }
            }
            else
            {
                m_bypassView.IsChecked = true;
                teamNumber = m_defaultTeamNumber;
            }
            m_teamNumber = teamNumber;
            m_mainPage.SetDirtySettings();
        }
    }
}
