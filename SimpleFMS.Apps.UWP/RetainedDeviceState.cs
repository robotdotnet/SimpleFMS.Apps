using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using SimpleFMS.Base.DriverStation;
using SimpleFMS.Networking.Base;
using SimpleFMS.Networking.Client;
using SimpleFMS.Networking.Client.NetworkClients;

namespace SimpleFMS.Apps.UWP
{
    public class RetainedDeviceState
    {
        public IContainer AutoFacContainer { get; }

        private static RetainedDeviceState s_instance;

        public static RetainedDeviceState Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new RetainedDeviceState();

                return s_instance;
            }
        }

        private RetainedDeviceState()
        {
            INetworkClientManager networkManager = new NetworkClientManager("Windows UWP Client");
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

        public void StopApplication()
        {
            using (var scope = AutoFacContainer.BeginLifetimeScope())
            {
                var netManager = scope.Resolve<INetworkClientManager>();


                netManager.SuspendNetworking();
            }
        }

        public void StartApplication()
        {
            using (var scope = AutoFacContainer.BeginLifetimeScope())
            {
                var netManager = scope.Resolve<INetworkClientManager>();

                netManager.ResumeNetworking();
            }
        }
    }

    public enum CurrentDeviceState
    {
        Dirty,
        ReadyToRun,
        Running
    }
}
