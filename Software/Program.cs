using System.IO.Ports;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;

namespace Contexts
{
    // The class that handles the creation of the application windows
    class GUI : ApplicationContext
    {
        static class VolumeKnob
        {
            public const UInt16 Low = 1;
            public const UInt16 Medium = 5;
            public const UInt16 High = 10;

        }
        public static bool program_config_changed = true;
        public static bool program_changed = false;
        public static Mutex playback_mutex = new();
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu = new ContextMenuStrip();
        ToolStripMenuItem port = new ToolStripMenuItem();
        ToolStripMenuItem volume_sense = new ToolStripMenuItem();
        ToolStripMenuItem output = new ToolStripMenuItem();
        ToolStripMenuItem default_audio_output = new ToolStripMenuItem();
        Image close_image = SystemIcons.Asterisk.ToBitmap();
        IEnumerable<CoreAudioDevice>? cad = null;
        public static bool continue_read = true;
        public static bool continue_config = true;
        public static bool continue_media = true;

        static Image selected_img = SystemIcons.Exclamation.ToBitmap();

        public static Thread read_thread = new(DeviceHandler.Read);
        public static Thread config_thread = new(ConfigHandler.ConfigChangeHandler);
        public static Thread media_thread = new(DeviceHandler.HandlerSetup);

        static DeviceHandler.Oracle_Configuration oracle_Configuration = new();

        private GUI()
        {
            //string path_icon = "chevron_up.ico"; 
            //Image image = Image.FromFile(path_icon);
            Image image = SystemIcons.Error.ToBitmap();
            port = new ToolStripMenuItem("Port", null);
            volume_sense = new ToolStripMenuItem("Volume Knob Sensitivity", null);
            output = new ToolStripMenuItem("Output", null);
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit", close_image, new EventHandler(OnClose));

            default_audio_output = new ToolStripMenuItem("Default Device", null, new EventHandler(OnSetAudioDevice));
            output.DropDownItems.Add(default_audio_output);
            //default_audio_output.Image = selected_img;

            exit.MouseUp += new MouseEventHandler(OnClose);
            //
            contextMenu.Items.AddRange(new ToolStripItem[] { port, volume_sense, output, exit });
            volume_sense.BackColor = Color.AliceBlue; 
            output.BackColor = Color.AliceBlue; 
            contextMenu.BackColor = Color.AliceBlue;
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Shield;
            notifyIcon.Visible = true;
            notifyIcon.Text = "My icon!";
            notifyIcon.ContextMenuStrip = contextMenu;

            notifyIcon.MouseClick += new MouseEventHandler(TopMenuClick);
            notifyIcon.ContextMenuStrip.Closed += new ToolStripDropDownClosedEventHandler(TopMenuExit);
            notifyIcon.ContextMenuStrip.MouseEnter += new EventHandler(MenuEnter);
            //port.DropDown.MouseEnter += new EventHandler(OnPort);
            output.DropDown.MouseEnter += new EventHandler(OutputEnter);
            output.DropDown.MouseLeave += new EventHandler(OutputLeave);
            volume_sense.DropDown.MouseEnter += new EventHandler(Vol_Sens_Enter);
            volume_sense.DropDown.MouseLeave += new EventHandler(Vol_Sens_Leave);
        }
        private void TopMenuExit(object? sender, EventArgs args)
        {
            program_changed = true;
            ConfigHandler.SaveConfig(oracle_Configuration);
        }

        private void TopMenuClick(object? sender, EventArgs args)
        {
            oracle_Configuration = ConfigHandler.LoadConfig();
            GetPorts();
            if (program_config_changed)
            {
                volume_sense.DropDownItems.Clear();
                program_config_changed = false;
                List<ushort>? ushorts = oracle_Configuration.VolumeSensitivityOptions;
                if (ushorts != null)
                {
                    ToolStripItem tool;
                    foreach (ushort sense in ushorts)
                    {
                        tool = volume_sense.DropDownItems.Add("±", null, OnVolumeChange);
                        tool.Text += sense.ToString();
                        tool.Tag = sense;
                    }
                }
            }
            foreach (ToolStripMenuItem strip in volume_sense.DropDownItems)
            {
                if (strip.Tag == null)
                    continue;
                if ((ushort)strip.Tag == oracle_Configuration.VolumeSensitivity)
                {
                    strip.Image = selected_img;
                }
                else
                    strip.Image = null;
            }
        }
        private void OnVolumeChange(object? sender, EventArgs args)
        {
            if (sender == null)
                return;
            string? sender_text = sender.ToString();
            if (sender_text == null)
                return;
            foreach (ToolStripItem tool in volume_sense.DropDownItems)
            {
                if (tool.Tag == null)
                    continue;
                if (sender_text.Equals(tool.Text))
                {
                    oracle_Configuration.VolumeSensitivity = (UInt16)tool.Tag;
                    tool.Image = selected_img;
                }
                else
                    tool.Image = null;
            }

        }
        private void MenuEnter(object? sender, EventArgs args)
        {
            if (notifyIcon.ContextMenuStrip == null)
                return;
            notifyIcon.ContextMenuStrip.BringToFront();
        }
        private void OutputEnter(object? sender, EventArgs e)
        {
            if (sender == null)
                return;
            DeviceHandler.WriteLog("entered from " + sender.ToString());
            output.DropDown.AutoClose = false;
            GetAudioDevices();
        }
        private void OutputLeave(object? sender, EventArgs e){
            output.DropDown.AutoClose = true;
        }
        private void Vol_Sens_Enter(object? sender, EventArgs e){
            volume_sense.DropDown.AutoClose = false;
        }
        private void Vol_Sens_Leave(object? sender, EventArgs e){
            volume_sense.DropDown.AutoClose = true;
        }

        private void GetPorts(){
            string[] ports_s = SerialPort.GetPortNames();
            port.DropDownItems.Clear();
            int i = 0;
            foreach(string p in ports_s){
                //ports_found += p;
                port.DropDownItems.Add(new ToolStripMenuItem(p, close_image, new EventHandler(OnPortSelected)));
                if(p.Equals(oracle_Configuration.ComPort))
                    port.DropDownItems[i].Image = selected_img;
            }
        }
        private async void GetAudioDevices()
        {
            cad = null;
            cad = await new CoreAudioController().GetPlaybackDevicesAsync(DeviceState.Active);
            int index = 1;
            foreach (CoreAudioDevice c in cad)
            {
                if (index < output.DropDownItems.Count)
                    continue;
                output.DropDownItems.Add(new ToolStripMenuItem(c.Name, null, new EventHandler(OnSetAudioDevice)));
                index++;
            }
            foreach (ToolStripItem tool in output.DropDownItems)
            {
                if (tool.Text.Equals(oracle_Configuration.PlaybackDevice))
                {
                    tool.Image = selected_img;
                }
                else
                    tool.Image = null;
            }
        }
        private void OnSetAudioDevice(object? sender, EventArgs args)
        {
            if (sender == null || cad == null || !cad.Any())
                return;
            foreach (ToolStripItem toool in output.DropDownItems)
                toool.Image = null;
            oracle_Configuration.PlaybackDevice = sender.ToString();
            ToolStripItem tool = (ToolStripItem)sender;
            tool.Image = selected_img;
        }


        private void OnPortSelected(object? sender, EventArgs e)
        {
            if (sender == null)
                return;
            oracle_Configuration.ComPort = sender.ToString() ?? "COM3";
            foreach (ToolStripItem toool in output.DropDownItems)
                toool.Image = null;
            ToolStripItem tool = (ToolStripItem)sender;
            tool.Image = selected_img;
        }

        private void OnSource(object? sender, EventArgs e){
            
        }

        private void OnClose(object? sender, EventArgs e){
            continue_config = false;
            continue_read = false;
            continue_media = false;
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Dispose();
            ExitThread();
        }

        [STAThread]
        //public static async Task Main(string[] args)
        public static void Main(string[] args)
        {
            //Setup();
            GUI context = new();
            media_thread.Start();
            Application.Run(context);
            //await DeviceHandler.HandlerSetup(args);
            // all forms are closed.

        }
    }
}//net7.0-windows10.0.17763.0