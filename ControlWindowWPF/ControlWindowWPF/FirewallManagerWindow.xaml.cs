using System;
using System.IO;
using System.Windows;
using NetFwTypeLib;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// FirewallManagerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FirewallManagerWindow : Window
    {
        public FirewallManagerWindow()
        {
            InitializeComponent();
        }

        private void AddFirewallRule()
        {
            try
            {
                INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
                firewallRule.Enabled = true;
                firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;

                string vmcpath = Path.GetFullPath(Globals.GetCurrentAppDir() + "/../VirtualMotionCapture.exe");
                firewallRule.ApplicationName = vmcpath;
                //firewallRule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;
                //firewallRule.LocalPorts = "39540";

                firewallRule.Name = $"Virtual Motion Capture";
                firewallRule.Description = "Firewall settings for Virtual Motion Capture to connect with other apps.\n" + vmcpath;

                firewallRule.InterfaceTypes = "All";
                firewallRule.Profiles = (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL;

                INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                firewallPolicy.Rules.Add(firewallRule);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            AddFirewallRule();
            Environment.Exit(0);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (DontShowAgainCheckBox.IsChecked == true)
            {
                Environment.Exit(0);
            }
            else
            {
                Environment.Exit(-1);
            }
        }
    }
}
