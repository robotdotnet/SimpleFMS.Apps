using System;
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Autofac;
using SimpleFMS.Base.DriverStation;
using SimpleFMS.Networking.Base;
using SimpleFMS.Networking.Client;
using SimpleFMS.Networking.Client.NetworkClients;

namespace SimpleFMS.Apps.Android
{
    public class RetainedDeviceState : Fragment
    {
        public IContainer AutoFacContainer { get; }



        public RetainedDeviceState()
        {
            INetworkClientManager networkManager = new NetworkClientManager("Android FMS Client");
            DriverStationClient dsClient = new DriverStationClient(networkManager);
            MatchTimingClient matchClient = new MatchTimingClient(networkManager);

            networkManager.AddClient(dsClient);
            networkManager.AddClient(matchClient);

            var builder = new ContainerBuilder();
            builder.RegisterInstance(networkManager).As<INetworkClientManager>();
            builder.RegisterInstance(dsClient).As<DriverStationClient>();
            builder.RegisterInstance(matchClient).As<MatchTimingClient>();

            AutoFacContainer = builder.Build();
        }

        private CurrentDeviceState m_state = CurrentDeviceState.Dirty;
        public CurrentDeviceState State
        {
            get { return m_state; }
            set { m_state = value; }
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            RetainInstance = true;
        }

        public void StopApplication()
        {
            using (var scope = AutoFacContainer.BeginLifetimeScope())
            {
                var dsClient = scope.Resolve<DriverStationClient>();
                var netManager = scope.Resolve<INetworkClientManager>();

                netManager.OnFmsConnectionChanged -= m_fmsConnectionAction;
                dsClient.OnDriverStationReportsChanged -= m_dsReportAction;


                netManager.SuspendNetworking();
            }
        }

        public void StartApplication()
        {
            using (var scope = AutoFacContainer.BeginLifetimeScope())
            {
                var dsClient = scope.Resolve<DriverStationClient>();
                var netManager = scope.Resolve<INetworkClientManager>();

                netManager.ResumeNetworking();
                netManager.OnFmsConnectionChanged += m_fmsConnectionAction;
                dsClient.OnDriverStationReportsChanged += m_dsReportAction;
            }
        }

        public void CreateApplication(Action<IReadOnlyDictionary<AllianceStation, IDriverStationReport>> dsAction, Action<bool> fmsAction)
        {
            m_fmsConnectionAction = fmsAction;
            m_dsReportAction = dsAction;
        }

        private Action<IReadOnlyDictionary<AllianceStation, IDriverStationReport>> m_dsReportAction;
        private Action<bool> m_fmsConnectionAction;
    }

    public enum CurrentDeviceState
    {
        Dirty,
        ReadyToRun,
        Running
    }

}