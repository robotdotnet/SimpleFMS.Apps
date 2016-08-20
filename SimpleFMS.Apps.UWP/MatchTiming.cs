using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Autofac;
using SimpleFMS.Base.Enums;
using SimpleFMS.Base.MatchTiming;
using SimpleFMS.Networking.Client.NetworkClients;
using static SimpleFMS.Base.MatchTiming.MatchTimingConstants;

namespace SimpleFMS.Apps.UWP
{
    class MatchTiming : IDisposable
    {
        private MainPage m_mainPage;
        private Grid m_matchStoppedGrid;
        private Grid m_matchRunningGrid;

        private Button m_startMatchButton;
        private Button m_startAutoButton;
        private Button m_startTeleButton;
        private Button m_stopPeriodButton;
        private Button m_eStopButton;
        private TextBlock m_timeRemaining;
        private TextBox m_autoTime;
        private TextBox m_teleopTime;
        private TextBox m_delayTime;

        private TextBlock m_matchStateView;

        private CancellationTokenSource m_tokenSource = new CancellationTokenSource();

        public MatchTiming(MainPage mainPage, Grid matchStopped, Grid matchStarted, Button matchStart, Button startAuto,
            Button startTeleop, Button stopPeriod, Button eStop, TextBlock timeRemaining, TextBlock matchState,
            TextBox autoTime, TextBox teleTime, TextBox delayTime)
        {
            m_mainPage = mainPage;
            m_matchStoppedGrid = matchStopped;
            m_matchRunningGrid = matchStarted;

            m_startMatchButton = matchStart;
            m_startAutoButton = startAuto;
            m_startTeleButton = startTeleop;
            m_stopPeriodButton = stopPeriod;
            m_eStopButton = eStop;
            m_timeRemaining = timeRemaining;
            m_matchStateView = matchState;
            m_autoTime = autoTime;
            m_teleopTime = teleTime;
            m_delayTime = delayTime;

            m_startMatchButton.Click += OnMatchStartClick;
            m_startAutoButton.Click += OnAutonomousStartClick;
            m_startTeleButton.Click += OnTeleoperatedStartClick;
            m_stopPeriodButton.Click += OnMatchStoppedClick;
            m_eStopButton.Click += OnEStopClick;

            m_autoTime.Text = ((int) DefaultAutonomousTime.TotalSeconds).ToString();
            m_teleopTime.Text = ((int)DefaultTeleoperatedTime.TotalSeconds).ToString();
            m_delayTime.Text = ((int)DefaultDelayTime.TotalSeconds).ToString();

            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var timeManager = scope.Resolve<MatchTimingClient>();

                timeManager.OnMatchTimeReportChanged += OnMatchTimeReportChanged;
            }
        }

        public void Dispose()
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var timeManager = scope.Resolve<MatchTimingClient>();

                timeManager.OnMatchTimeReportChanged -= OnMatchTimeReportChanged;
            }

            m_tokenSource.Cancel();
        }

        internal void DriverStationDisconnect()
        {
            m_tokenSource.Cancel();
        }

        internal void DriverStationReconnect()
        {
            m_tokenSource = new CancellationTokenSource();
        }

        private void OnMatchTimeReportChanged(IMatchStateReport report)
        {
            if (report == null) return;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                m_timeRemaining.Text = ((int)report.RemainingPeriodTime.TotalSeconds).ToString();
                int autoTime = (int)report.AutonomousTime.TotalSeconds;
                if (autoTime != int.Parse(m_autoTime.Text))
                {
                    m_autoTime.Text = autoTime.ToString();
                }
                int teleopTime = (int)report.TeleoperatedTime.TotalSeconds;
                if (teleopTime != int.Parse(m_teleopTime.Text))
                {
                    m_teleopTime.Text = teleopTime.ToString();
                }
                int delayTime = (int)report.DelayTime.TotalSeconds;
                if (delayTime != int.Parse(m_delayTime.Text))
                {
                    m_delayTime.Text = delayTime.ToString();
                }

                SetMatchRunningLayout(report.MatchState);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async void OnMatchStartClick(object sender, RoutedEventArgs e)
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report.MatchState != MatchState.Stopped)
                {
                    // TODO: Automaticall switch states
                    SetMatchRunningLayout(report.MatchState);
                    return;
                }

                int teleopTime;
                TimeSpan teleopTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_teleopTime.Text, out teleopTime))
                {
                    teleopTimeSpan = teleopTime > 0 ? TimeSpan.FromSeconds(teleopTime) : DefaultTeleoperatedTime;
                }

                int autoTime;
                TimeSpan autoTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_autoTime.Text, out autoTime))
                {
                    autoTimeSpan = autoTime > 0 ? TimeSpan.FromSeconds(autoTime) : DefaultAutonomousTime;
                }

                int delayTime;
                TimeSpan delayTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_delayTime.Text, out delayTime))
                {
                    delayTimeSpan = delayTime > 0 ? TimeSpan.FromSeconds(delayTime) : DefaultDelayTime;
                }

                MatchTimeReport times = new MatchTimeReport(teleopTimeSpan, delayTimeSpan, autoTimeSpan);

                await client.SetMatchPeriodTimes(times, m_tokenSource.Token);
                await client.StartMatch(m_tokenSource.Token);

                SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
            }
        }

        public void DisableMatchStarting()
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report == null) return;

                if (report.MatchState == MatchState.Stopped)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        m_matchRunningGrid.Visibility = Visibility.Collapsed;
                        m_matchStoppedGrid.Visibility = Visibility.Collapsed;
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        public void EnableMatchStarting()
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report == null) return;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (report.MatchState == MatchState.Stopped)
                    {
                        m_matchRunningGrid.Visibility = Visibility.Collapsed;
                        m_matchStoppedGrid.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        m_matchRunningGrid.Visibility = Visibility.Visible;
                        m_matchStoppedGrid.Visibility = Visibility.Collapsed;
                    }
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        private void SetMatchRunningLayout(MatchState state)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            m_mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (state != MatchState.Stopped)
                {
                    m_mainPage.UpdateMatchState(true);
                    m_matchRunningGrid.Visibility = Visibility.Visible;
                    m_matchStoppedGrid.Visibility = Visibility.Collapsed;
                    switch (state)
                    {
                        case MatchState.Autonomous:
                            m_matchStateView.Text = "Auto Time Left";
                            break;
                        case MatchState.Delay:
                            m_matchStateView.Text = "Delay Time Left";
                            break;
                        case MatchState.Teleoperated:
                            m_matchStateView.Text = "Teleop Time Left";
                            break;
                    }
                }
                else
                {
                    m_mainPage.UpdateMatchState(false);
                    m_matchRunningGrid.Visibility = Visibility.Collapsed;
                    m_matchStoppedGrid.Visibility = Visibility.Visible;
                    m_matchStateView.Text = "Waiting For Match";
                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async void OnAutonomousStartClick(object sender, RoutedEventArgs e)
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report.MatchState != MatchState.Stopped)
                {
                    // TODO: Automaticall switch states
                    SetMatchRunningLayout(report.MatchState);
                    return;
                }

                int teleopTime;
                TimeSpan teleopTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_teleopTime.Text, out teleopTime))
                {
                    teleopTimeSpan = teleopTime > 0 ? TimeSpan.FromSeconds(teleopTime) : DefaultTeleoperatedTime;
                }

                int autoTime;
                TimeSpan autoTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_autoTime.Text, out autoTime))
                {
                    autoTimeSpan = autoTime > 0 ? TimeSpan.FromSeconds(autoTime) : DefaultAutonomousTime;
                }

                int delayTime;
                TimeSpan delayTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_delayTime.Text, out delayTime))
                {
                    delayTimeSpan = delayTime > 0 ? TimeSpan.FromSeconds(delayTime) : DefaultDelayTime;
                }

                MatchTimeReport times = new MatchTimeReport(teleopTimeSpan, delayTimeSpan, autoTimeSpan);

                await client.SetMatchPeriodTimes(times, m_tokenSource.Token);
                await client.StartAutonomous(m_tokenSource.Token);

                SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
            }
        }

        private async void OnTeleoperatedStartClick(object sender, RoutedEventArgs e)
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report.MatchState != MatchState.Stopped)
                {
                    // TODO: Automaticall switch states
                    SetMatchRunningLayout(report.MatchState);
                    return;
                }

                int teleopTime;
                TimeSpan teleopTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_teleopTime.Text, out teleopTime))
                {
                    teleopTimeSpan = teleopTime > 0 ? TimeSpan.FromSeconds(teleopTime) : DefaultTeleoperatedTime;
                }

                int autoTime;
                TimeSpan autoTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_autoTime.Text, out autoTime))
                {
                    autoTimeSpan = autoTime > 0 ? TimeSpan.FromSeconds(autoTime) : DefaultAutonomousTime;
                }

                int delayTime;
                TimeSpan delayTimeSpan = TimeSpan.Zero;
                if (int.TryParse(m_delayTime.Text, out delayTime))
                {
                    delayTimeSpan = delayTime > 0 ? TimeSpan.FromSeconds(delayTime) : DefaultDelayTime;
                }

                MatchTimeReport times = new MatchTimeReport(teleopTimeSpan, delayTimeSpan, autoTimeSpan);

                await client.SetMatchPeriodTimes(times, m_tokenSource.Token);
                await client.StartTeleoperated(m_tokenSource.Token);

                SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
            }
        }

        private async void OnMatchStoppedClick(object sender, RoutedEventArgs e)
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                await client.StopPeriod(m_tokenSource.Token);

                SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
            }
        }

        private async void OnEStopClick(object sender, RoutedEventArgs e)
        {
            using (var scope = RetainedDeviceState.Instance.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();
                var dsClient = scope.Resolve<DriverStationClient>();

                // Add an EStopAllRobots
                await dsClient.GlobalEStop(m_tokenSource.Token);

                await client.StopPeriod(m_tokenSource.Token);

                m_mainPage.SetDirtySettings();
            }
        }
    }
}
