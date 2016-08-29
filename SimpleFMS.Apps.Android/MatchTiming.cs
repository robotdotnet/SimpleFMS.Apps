using System;
using System.Threading;
using Android.Views;
using Android.Widget;
using Autofac;
using NetworkTables.Independent;
using SimpleFMS.Base.Enums;
using SimpleFMS.Base.Extensions;
using SimpleFMS.Base.MatchTiming;
using SimpleFMS.Networking.Client.NetworkClients;
using static SimpleFMS.Base.MatchTiming.MatchTimingConstants;

namespace SimpleFMS.Apps.Android
{
    public class MatchTiming : IDisposable
    {
        private RelativeLayout m_parentLayout;
        private LinearLayout m_matchStoppedLayout;
        private LinearLayout m_matchRunningLayout;

        private Button m_startMatchButton;
        private Button m_startAutoButton;
        private Button m_startTeleButton;
        private Button m_stopPeriodButton;
        private Button m_eStopButton;
        private TextView m_timeRemaining;
        private EditText m_autoTime;
        private EditText m_teleopTime;
        private EditText m_delayTime;

        private TextView m_matchStateView;

        private MainActivity m_parentActivity;

        private RetainedDeviceState m_retainedState;

        public MatchTiming(RelativeLayout parentLayout, LinearLayout matchStopped, LinearLayout matchRunning,
            MainActivity parent, RetainedDeviceState retainedState)
        {
            m_retainedState = retainedState;
            m_parentActivity = parent;
            m_parentLayout = parentLayout;
            m_matchStoppedLayout = matchStopped;
            m_matchRunningLayout = matchRunning;

            m_startMatchButton = m_parentActivity.FindViewById<Button>(Resource.Id.matchStartButton);
            m_startMatchButton.Click += OnMatchStartClick;
            m_startAutoButton = m_parentActivity.FindViewById<Button>(Resource.Id.startAutonomousButton);
            m_startAutoButton.Click += OnAutonomousStartClick;
            m_startTeleButton = m_parentActivity.FindViewById<Button>(Resource.Id.startTeleoperatedButton);
            m_startTeleButton.Click += OnTeleoperatedStartClick;
            m_stopPeriodButton = m_parentActivity.FindViewById<Button>(Resource.Id.stopPeriodButton);
            m_stopPeriodButton.Click += OnMatchStoppedClick;
            m_eStopButton = m_parentActivity.FindViewById<Button>(Resource.Id.eStopButton);
            m_eStopButton.Click += OnEStopClick;

            m_timeRemaining = m_parentActivity.FindViewById<TextView>(Resource.Id.remainingTextView);

            m_autoTime = m_parentActivity.FindViewById<EditText>(Resource.Id.autoTimeView);
            m_teleopTime = m_parentActivity.FindViewById<EditText>(Resource.Id.teleopTimeView);
            m_delayTime = m_parentActivity.FindViewById<EditText>(Resource.Id.delayTimeView);

            m_matchStateView = m_parentActivity.FindViewById<TextView>(Resource.Id.matchState);

            m_autoTime.Text = ((int)DefaultAutonomousTime.TotalSeconds).ToString();
            m_teleopTime.Text = ((int)DefaultTeleoperatedTime.TotalSeconds).ToString();
            m_delayTime.Text = ((int)DefaultDelayTime.TotalSeconds).ToString();

            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var timeManager = scope.Resolve<MatchTimingClient>();

                timeManager.OnMatchTimeReportChanged += OnMatchTimingReportChanged;


            }
        }

        public void Dispose()
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var timeManager = scope.Resolve<MatchTimingClient>();

                timeManager.OnMatchTimeReportChanged -= OnMatchTimingReportChanged;
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

        private void OnMatchTimingReportChanged(IMatchStateReport report)
        {
            if (report == null) return;

            m_parentActivity.RunOnUiThread(() =>
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
        }

        private CancellationTokenSource m_tokenSource = new CancellationTokenSource();

        private async void OnMatchStartClick(object sender, EventArgs e)
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
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
                bool started = await client.StartMatch(m_tokenSource.Token);

                if (started)
                    SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
                else
                    MatchFailedToStart();
            }
        }

        public void DisableMatchStarting()
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report == null) return;

                if (report.MatchState == MatchState.Stopped)
                {
                    m_parentActivity.RunOnUiThread(() =>
                    {
                        m_matchRunningLayout.Visibility = ViewStates.Gone;
                        m_matchStoppedLayout.Visibility = ViewStates.Gone;
                    });
                }
            }
        }

        public void EnableMatchStarting()
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                IMatchStateReport report = client.GetMatchStateReport();

                if (report == null) return;

                m_parentActivity.RunOnUiThread(() =>
                {
                    if (report.MatchState == MatchState.Stopped)
                    {
                        m_matchRunningLayout.Visibility = ViewStates.Gone;
                        m_matchStoppedLayout.Visibility = ViewStates.Visible;
                    }
                    else
                    {
                        m_matchRunningLayout.Visibility = ViewStates.Visible;
                        m_matchStoppedLayout.Visibility = ViewStates.Gone;
                    }
                });
            }
        }

        private void SetMatchRunningLayout(MatchState state)
        {
            m_parentActivity.RunOnUiThread(() =>
            {
                if (state != MatchState.Stopped)
                {
                    m_parentActivity.UpdateMatchState(true);
                    m_matchRunningLayout.Visibility = ViewStates.Visible;
                    m_matchStoppedLayout.Visibility = ViewStates.Gone;
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
                    m_parentActivity.UpdateMatchState(false);
                    m_matchRunningLayout.Visibility = ViewStates.Gone;
                    m_matchStoppedLayout.Visibility = ViewStates.Visible;
                    m_matchStateView.Text = "Waiting For Match";
                }
            });
        }

        private void MatchFailedToStart()
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var dsManager = scope.Resolve<DriverStationClient>();

                if (dsManager.IsReadyToStartMatch())
                {
                    // Match failed to start because of an unknown reason
                    Toast.MakeText(m_parentActivity.ApplicationContext, "Failed to start.", ToastLength.Short).Show();
                }
                else
                {
                    // Match failed to start because not ready to start
                    Toast.MakeText(m_parentActivity.ApplicationContext, "Failed to start, robots not all connected?", ToastLength.Short).Show();
                }
            }
        }

        private async void OnAutonomousStartClick(object sender, EventArgs e)
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
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
                bool started = await client.StartAutonomous(m_tokenSource.Token);

                if (started)
                    SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
                else
                    MatchFailedToStart();
            }
        }

        private async void OnTeleoperatedStartClick(object sender, EventArgs e)
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
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
                bool started = await client.StartTeleoperated(m_tokenSource.Token);

                if (started)
                    SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
                else
                    MatchFailedToStart();
            }
        }

        private async void OnMatchStoppedClick(object sender, EventArgs e)
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();

                await client.StopPeriod(m_tokenSource.Token);

                SetMatchRunningLayout(client.GetMatchStateReport().MatchState);
            }
        }

        private async void OnEStopClick(object sender, EventArgs e)
        {
            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
            {
                var client = scope.Resolve<MatchTimingClient>();
                var dsClient = scope.Resolve<DriverStationClient>();

                // Add an EStopAllRobots
                await dsClient.GlobalEStop(m_tokenSource.Token);

                await client.StopPeriod(m_tokenSource.Token);

                m_parentActivity.SetDirtySettings();
            }
        }
    }
}