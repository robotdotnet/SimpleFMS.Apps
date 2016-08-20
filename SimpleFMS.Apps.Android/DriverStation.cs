using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Autofac;
using SimpleFMS.Base.DriverStation;
using SimpleFMS.Networking.Client.NetworkClients;

namespace SimpleFMS.Apps.Android
{
    class DriverStation : IDisposable
    {
        private EditText m_teamNumberView;
        private CheckBox m_bypassView;
        private View m_dsCommView;
        private View m_rioCommView;
        private View m_eStoppedView;
        private TextView m_batteryView;
        private MainActivity m_parentActivity;

        public AllianceStation Station { get; }

        private RetainedDeviceState m_retainedState;

        private int m_teamNumber;
        public int TeamNumber
        {
            get { return m_teamNumber; }
            set
            {
                int old = m_teamNumber;
                m_teamNumber = value;
                if (value != old)
                {
                    m_parentActivity.RunOnUiThread(() => m_teamNumberView.Text = m_teamNumber.ToString());
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
                    m_parentActivity.RunOnUiThread(() => m_bypassView.Checked = m_bypassed);
                }
            }
        }

        private int m_defaultTeamNumber = 0;

        public DriverStation(MainActivity parent, int index, IReadOnlyDictionary<string, int> idConstants,
            RetainedDeviceState retainedState)
        {
            m_retainedState = retainedState;
            m_teamNumberView = parent.FindViewById<EditText>(idConstants[$"teamNumberStation{index + 1}"]);
            m_bypassView = parent.FindViewById<CheckBox>(idConstants[$"bypassCheckStation{index + 1}"]);
            m_dsCommView = parent.FindViewById<View>(idConstants[$"dsCommStation{index + 1}"]);
            m_rioCommView = parent.FindViewById<View>(idConstants[$"rioCommStation{index + 1}"]);
            m_eStoppedView = parent.FindViewById<View>(idConstants[$"eStopStation{index + 1}"]);
            m_batteryView = parent.FindViewById<TextView>(idConstants[$"batteryStation{index + 1}"]);

            m_defaultTeamNumber = index;
            m_parentActivity = parent;
            Station = new AllianceStation((byte)index);
            m_teamNumber = index;
            TeamNumber = m_defaultTeamNumber;

            m_teamNumberView.TextChanged += (sender, args) =>
            {
                string newValue = args.Text.ToString();
                int teamNumber = 0;

                if (int.TryParse(newValue, out teamNumber))
                {
                    if (teamNumber < 0)
                    {
                        m_bypassView.Checked = true;
                        teamNumber = m_defaultTeamNumber;
                    }
                    else
                    {
                        m_bypassView.Checked = false;
                    }
                }
                else
                {
                    m_bypassView.Checked = true;
                    teamNumber = m_defaultTeamNumber;
                }
                m_teamNumber = teamNumber;
                m_parentActivity.SetDirtySettings();
            };

            m_bypassView.CheckedChange += (sender, args) =>
            {
                m_parentActivity.SetDirtySettings();
                /*
                bool newValue = args.IsChecked;
                m_bypassed = newValue;
                //TODO: Add auto updating to bypass
                //bool success = await UpdateStationBypass(newValue);
                // TODO: Handle an unsuccessful set
                */
            };
        }

        public void Dispose()
        {
            source.Cancel();
        }

        public void UpdateMatchState(bool running)
        {
            if (running)
            {
                m_teamNumberView.Enabled = false;
                m_bypassView.Enabled = false;
            }
            else
            {
                m_teamNumberView.Enabled = true;
                m_bypassView.Enabled = true;
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

            using (var scope = m_retainedState.AutoFacContainer.BeginLifetimeScope())
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
                m_parentActivity.RunOnUiThread(() =>
                {
                    m_teamNumberView.Text = teamNumberToSetIfInvalid.ToString();
                    m_teamNumber = teamNumberToSetIfInvalid;
                    m_bypassView.Checked = true;
                });
            }
            else
            {
                ds = new DriverStationConfiguration(teamNumber, Station, m_bypassView.Checked);
            }
            return ds;
        }

        public void UpdateStationReport(IDriverStationReport report)
        {
            m_parentActivity.RunOnUiThread(() =>
            {
                TeamNumber = report.TeamNumber;
                Bypass = report.IsBypassed;

                m_dsCommView.SetBackgroundColor(report.DriverStationConnected ? Color.Green : Color.Red);

                m_rioCommView.SetBackgroundColor(report.RoboRioConnected ? Color.Green : Color.Red);

                bool eStopped = report.IsBeingSentEStopped || report.IsReceivingEStopped;
                m_eStoppedView.SetBackgroundColor(eStopped ? Color.Red : Color.Green);

                double battery = Math.Round(report.RobotBattery, 2, MidpointRounding.ToEven);
                if (battery < 0) battery *= -1;
                m_batteryView.Text = battery.ToString();
            });
        }

        /*
        public void UpdateStationConnection(bool dsConnection, bool roboRioConnection, bool eStopped)
        {
            m_parentActivity.RunOnUiThread(() =>
            {
                m_dsCommView.SetBackgroundColor(dsConnection ? Color.GreenYellow : Color.Red);
                m_rioCommView.SetBackgroundColor(roboRioConnection ? Color.GreenYellow : Color.Red);
                m_eStoppedView.SetBackgroundColor(eStopped ? Color.Red : Color.GreenYellow);
            });
        }
        */

        public void SetFMSDisconnected()
        {
            m_parentActivity.RunOnUiThread(() =>
            {
                m_eStoppedView.SetBackgroundColor(Color.CornflowerBlue);
            });
        }
    }
}