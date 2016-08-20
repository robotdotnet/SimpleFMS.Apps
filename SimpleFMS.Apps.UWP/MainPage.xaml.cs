using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Autofac;
using SimpleFMS.Base.DriverStation;
using SimpleFMS.Networking.Base;
using SimpleFMS.Networking.Client.NetworkClients;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SimpleFMS.Apps.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MatchTiming m_matchTiming;

        public MainPage()
        {
            this.InitializeComponent();

            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var dsManager = scope.Resolve<DriverStationClient>();
                var netManager = scope.Resolve<INetworkClientManager>();

                netManager.OnFmsConnectionChanged += NetManager_OnFmsConnectionChanged;
                dsManager.OnDriverStationReportsChanged += DsManager_OnDriverStationReportsChanged;
            }

            m_fmsAllianceStations.Add(new AllianceStation(0),
                new DriverStation(this, 0, teamNumberStation1, bypassStation1, DsConnStation1, RioConnStation1,
                    eStopStation1, battStation1));

            m_fmsAllianceStations.Add(new AllianceStation(1),
                new DriverStation(this, 1, teamNumberStation2, bypassStation2, DsConnStation2, RioConnStation2,
                    eStopStation2, battStation2));

            m_fmsAllianceStations.Add(new AllianceStation(2),
                new DriverStation(this, 2, teamNumberStation3, bypassStation3, DsConnStation3, RioConnStation3,
                    eStopStation3, battStation3));

            m_fmsAllianceStations.Add(new AllianceStation(3),
                new DriverStation(this, 3, teamNumberStation4, bypassStation4, DsConnStation4, RioConnStation4,
                    eStopStation4, battStation4));

            m_fmsAllianceStations.Add(new AllianceStation(4),
                new DriverStation(this, 4, teamNumberStation5, bypassStation5, DsConnStation5, RioConnStation5,
                    eStopStation5, battStation5));

            m_fmsAllianceStations.Add(new AllianceStation(5),
                new DriverStation(this, 5, teamNumberStation6, bypassStation6, DsConnStation6, RioConnStation6,
                    eStopStation6, battStation6));

            InitMatchButton.Click += InitMatchButton_Click;

            matchNumberTextBox.TextChanged += (sender, args) =>
            {
                SetDirtySettings();
            };


            m_matchTiming = new MatchTiming(this, MatchStoppedGrid, MatchRunningGrid, StartMatchButton,
                StartAutonomousButton, StartTeleoperatedButton, StopMatchButton, EStopRobots, MatchTimeRemainingLabel,
                MatchRunningStateLabel, AutonomousTimeTextBox, TeleopTimeTextBox, DelayTimeTextBox);

            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var dsClient = scope.Resolve<DriverStationClient>();
                var netManager = scope.Resolve<INetworkClientManager>();

                var dsReport = dsClient.GetDriverStationReports();
                DsManager_OnDriverStationReportsChanged(dsReport);

                NetManager_OnFmsConnectionChanged(netManager.FmsConnected);
            }
        }

        public void SetDirtySettings()
        {
            m_matchTiming.DisableMatchStarting();
            RetainedDeviceState.Instance.State = CurrentDeviceState.Dirty;
        }

        public void SetCleanSettings()
        {
            m_matchTiming.EnableMatchStarting();
            RetainedDeviceState.Instance.State = CurrentDeviceState.ReadyToRun;
        }

        public void UpdateMatchState(bool running)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (var fmsAllianceStation in m_fmsAllianceStations)
                {
                    fmsAllianceStation.Value.UpdateMatchState(running);
                }
                matchNumberTextBox.IsEnabled = !running;
                InitMatchButton.IsEnabled = !running;
                RetainedDeviceState.Instance.State = running
                    ? CurrentDeviceState.Running
                    : CurrentDeviceState.ReadyToRun;
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private CancellationTokenSource source = new CancellationTokenSource();

        private async void InitMatchButton_Click(object sender, RoutedEventArgs e)
        {
            List<IDriverStationConfiguration> configurations =
                new List<IDriverStationConfiguration>(m_fmsAllianceStations.Count);
            // Grab all DS Data.
            int invalid = -1;
            foreach (var value in m_fmsAllianceStations.Values)
            {
                configurations.Add(value.GetCurrentState(invalid--));
            }

            int matchNum = 0;
            if (!int.TryParse(matchNumberTextBox.Text, out matchNum))
            {
                matchNum = 1;
            }

            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<DriverStationClient>();
                bool success = await client.UpdateDriverStationConfigurations(configurations, matchNum, 0, source.Token);
                if (success) SetCleanSettings();
            }
        }

        private readonly Dictionary<AllianceStation, DriverStation> m_fmsAllianceStations =
            new Dictionary<AllianceStation, DriverStation>(AllianceStation.MaxNumAllianceStations);

       

        private void NetManager_OnFmsConnectionChanged(bool connected)
        {
            if (connected) FmsConnected();
            else FmsDisconnected();
        }

        private void FmsConnected()
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var dsClient = scope.Resolve<DriverStationClient>();

                var report = dsClient.GetDriverStationReports();

                foreach (var s in report)
                {
                    var station = m_fmsAllianceStations[s.Key];
                    station.UpdateStationReport(s.Value);
                }
                source = new CancellationTokenSource();
                foreach (var fmsAllianceStation in m_fmsAllianceStations)
                {
                    fmsAllianceStation.Value.DriverStationReconnect();
                }
                m_matchTiming.DriverStationReconnect();

                bool foundValidTeamNumber = false;
                foreach (var fmsAllianceStation in m_fmsAllianceStations)
                {
                    if (fmsAllianceStation.Value.TeamNumber >= 0)
                    {
                        foundValidTeamNumber = true;
                        break;
                    }
                }
                if (!foundValidTeamNumber)
                    SetDirtySettings();
            }
        }

        private void FmsDisconnected()
        {
            foreach (var fmsAllianceStation in m_fmsAllianceStations)
            {
                fmsAllianceStation.Value.SetFMSDisconnected();

            }

            source.Cancel();
            foreach (var fmsAllianceStation in m_fmsAllianceStations)
            {
                fmsAllianceStation.Value.DriverStationDisconnect();
            }
            m_matchTiming.DriverStationDisconnect();
            SetDirtySettings();
        }

        private void DsManager_OnDriverStationReportsChanged(IReadOnlyDictionary<Base.DriverStation.AllianceStation, Base.DriverStation.IDriverStationReport> obj)
        {
            if (obj == null) return;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {

                foreach (var s in obj)
                {
                    DriverStation station = null;
                    if (m_fmsAllianceStations.TryGetValue(s.Key, out station))
                    {
                        // Station exists.
                        station.UpdateStationReport(s.Value);
                    }

                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
