using System.IO.Ports;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;

namespace Contexts
{
    // The class that handles the creation of the application windows
    class GUI : ApplicationContext
    {
        string port_selected = "";
        string path_a = "info.txt";
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu = new ContextMenuStrip();
        ToolStripMenuItem port = new ToolStripMenuItem();
        ToolStripMenuItem volume_sense = new ToolStripMenuItem();
        ToolStripMenuItem output = new ToolStripMenuItem();
        ToolStripMenuItem increment_one = new ToolStripMenuItem();
        ToolStripMenuItem increment_five = new ToolStripMenuItem();
        ToolStripMenuItem increment_ten = new ToolStripMenuItem();
        ToolStripMenuItem default_audio_output = new ToolStripMenuItem();
        Image close_image = SystemIcons.Asterisk.ToBitmap();
        IEnumerable<CoreAudioDevice>? cad = null;

        string playback_device_s = "";
        static int selected_device = -1;

        static CoreAudioDevice? dpd;
        static DeviceType deviceType = DeviceType.Playback;
        static Role role = Role.Multimedia;

        public static int volume_sens = 5;

        static Image selected_img = SystemIcons.Exclamation.ToBitmap();

        
        private static void Setup(){
            DeviceHandler.playback_device = new CoreAudioController().GetDefaultDevice(deviceType, role);
            dpd = DeviceHandler.playback_device;
        }
        
        private GUI()
        {
            //string path_icon = "chevron_up.ico"; 
            //Image image = Image.FromFile(path_icon);
            Image image = SystemIcons.Error.ToBitmap();
            port = new ToolStripMenuItem("Port", null, new EventHandler(OnPort));
            volume_sense = new ToolStripMenuItem("Volume Knob Sensitivity", null);
            output = new ToolStripMenuItem("Output", null);
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit", close_image, new EventHandler(OnClose));

            increment_one = new ToolStripMenuItem("±1", null, new EventHandler(OnOne));
            volume_sense.DropDownItems.Add(increment_one);
            increment_five = new ToolStripMenuItem("±5", null, new EventHandler(OnFive));
            volume_sense.DropDownItems.Add(increment_five);
            increment_ten = new ToolStripMenuItem("±10", null, new EventHandler(OnTen));
            volume_sense.DropDownItems.Add(increment_ten);
            
            default_audio_output = new ToolStripMenuItem("Default Device", null, new EventHandler(OnDefaultAudio));
            output.DropDownItems.Add(default_audio_output);
            default_audio_output.Image = selected_img;
            
            exit.MouseUp += new MouseEventHandler(OnClose);
            //
            contextMenu.Items.AddRange(new ToolStripItem[]{port, volume_sense, output, exit});
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Shield;
            notifyIcon.Visible = true;
            notifyIcon.Text = "My icon!";
            notifyIcon.ContextMenuStrip = contextMenu;
            
            notifyIcon.MouseClick += new MouseEventHandler(OnPort);

            output.DropDown.MouseEnter += new EventHandler(OutputEnter);
            output.DropDown.MouseLeave += new EventHandler(OutputLeave);
            volume_sense.DropDown.MouseEnter += new EventHandler(Vol_Sens_Enter);
            volume_sense.DropDown.MouseLeave += new EventHandler(Vol_Sens_Leave);

            Setup();
            SetSelections();
        }

        private void SetSelections(){
            string[] data;
            try{
                data = File.ReadAllLines(path_a);
            }
            catch(FileNotFoundException){
                string[] default_data = {"5", "Default Device", "COM3"};
                File.WriteAllLines(path_a, default_data);
                data = default_data;
            }
            bool vol_parse;
            if(data.Length >= 3){
                playback_device_s = data[1];
                port_selected = data[2];
                vol_parse = int.TryParse(data[0], out volume_sens);
                if(!vol_parse)
                    volume_sens = 5;
                if(volume_sens == 1)
                    increment_one.Image = selected_img;
                else if(volume_sens == 5)
                    increment_five.Image = selected_img;
                else if(volume_sens == 10)
                    increment_ten.Image = selected_img;
            }
            if(playback_device_s != "Default Device"){
                default_audio_output.Image = null;
                SetDevice(true);
            }
            SetPort();
        }

        private void ChangeSelection(int i, string data){
            string[] strings = File.ReadAllLines(path_a);
            strings[i] = data;
            File.WriteAllLines(path_a, strings);
        }

        private void OutputEnter(object? sender, EventArgs e){
            output.DropDown.AutoClose = false;
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

        private void OnDefaultAudio(object? sender, EventArgs e){
            DeviceHandler.playback_device = new CoreAudioController().GetDefaultDevice(deviceType, role);
            default_audio_output.Image = selected_img;
            playback_device_s = "Default Device";
            //notifyIcon.ShowBalloonTip(3000, "Playback device", playback_device_s, ToolTipIcon.Info);
            ChangeSelection(1, playback_device_s);
            int i = 0;
            selected_device = 0;
            foreach(ToolStripItem tsi in output.DropDownItems){
                if(i != 0)
                    tsi.Image = null;
                i++;
           }
        }
        private void OnOne(object? sender, EventArgs e){
           volume_sens = 1;
           increment_one.Image = selected_img;
           increment_five.Image = null;
           increment_ten.Image = null;
           ChangeSelection(0, 1.ToString());
        }
        private void OnFive(object? sender, EventArgs e){
           volume_sens = 5;
           increment_one.Image = null;
           increment_five.Image = selected_img;
           increment_ten.Image = null;
           ChangeSelection(0, 5.ToString());
        }
        private void OnTen(object? sender, EventArgs e){
           volume_sens = 10;
           increment_one.Image = null;
           increment_five.Image = null;
           increment_ten.Image = selected_img;
           ChangeSelection(0, 10.ToString());
        }

        private void OnPort(object? sender, EventArgs e){
            SetPort();
            SetDevice(false);
        }

        private void SetPort(){
            string[] ports_s = SerialPort.GetPortNames();
            port.DropDownItems.Clear();
            int i = 0;
            foreach(string p in ports_s){
                //ports_found += p;
                port.DropDownItems.Add(new ToolStripMenuItem(p, close_image, new EventHandler(OnPortSelected)));
                if(p.Equals(port_selected))
                    output.DropDownItems[i].Image = selected_img;
            }
        }

        private void SetDevice(bool name_check){
            cad = null;
            cad = new CoreAudioController().GetPlaybackDevices(DeviceState.Active);
            
            int output_count = output.DropDownItems.Count;
            output.DropDownItems.Clear();
            output.DropDownItems.Add(default_audio_output);
            int i = 0;
            foreach(CoreAudioDevice c in cad){
                i++;
                output.DropDownItems.Add(new ToolStripMenuItem(c.Name, null, new EventHandler(OnOutputSelected)));
                if(!name_check){
                    if(i == selected_device)
                        output.DropDownItems[i].Image = selected_img;
                }
                else{
                    if(c.Name.Equals(playback_device_s)){}{
                        selected_device = i;
                    }
                }
            }
        }

        private void OnPortSelected(object? sender, EventArgs e){
            if(sender == null){
                return;
            }
            port_selected = sender.ToString() ?? "f";
            //notifyIcon.ShowBalloonTip(3000, "Selected port ", port_selected, ToolTipIcon.Info);
            ChangeSelection(2, port_selected);
        }

        private void OnOutputSelected(object? sender, EventArgs e){
            if(sender == null || cad == null || !cad.Any() || dpd == null)
                return;
            DeviceHandler.playback_device = cad.FirstOrDefault(c => c != null && c.Name == sender.ToString(), dpd);
            int index = output.DropDownItems.IndexOf((ToolStripItem)sender);
            if(index <= -1)
                return;
            selected_device = index;
            default_audio_output.Image = null;
            output.DropDownItems[1].Image = selected_img;
            playback_device_s = DeviceHandler.playback_device.Name;
            ChangeSelection(1, playback_device_s);
        }
        private void OnSource(object? sender, EventArgs e){
            
        }

        private void OnClose(object? sender, EventArgs e){            
            ExitThread();
        }

        //[STAThread]
        public static async Task Main(string[] args)
        {

            // Create the MyApplicationContext, that derives from ApplicationContext,
            // that manages when the application should exit.
            Setup();
            //GUI context = new GUI();
            //ApplicationContext app_context = new Form1();
            // Run the application with the specific context. It will exit when
            await DeviceHandler.HandlerSetup(args);
            // all forms are closed.
            //Application.Run(context);
        }
    }
}//net7.0-windows10.0.17763.0