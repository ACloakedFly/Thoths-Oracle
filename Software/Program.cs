/*
===========================================================================
Copyright (C) 2025 Dominique Negm

This file is part of Thoth's Oracle source code.

Thoth's Oracle source code is free software; you can redistribute it
and/or modify it under the terms of the GNU General Public License as
published by the Free Software Foundation; either version 3 of the License,
or (at your option) any later version.

Thoth's Oracle source code is distributed in the hope that it will be
useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Thoth's Oracle; if not, see <https://www.gnu.org/licenses/>
===========================================================================
*/
using System.IO.Ports;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;
using System.ComponentModel;

namespace Contexts
{
    public class OracleRenderer : ToolStripProfessionalRenderer
    {
        public Color border_colour = Color.FromArgb(255, 0, 85, 177);
        public Color highlight_colour = Color.FromArgb(255, 60, 50, 40);
        public Color default_colour = Color.FromArgb(255, 120, 110, 94);
        public Color default_text_colour = Color.FromArgb(255, 240, 240, 240);
        public Color default_border_colour = Color.FromArgb(255, 100, 90, 84);
        public Color menu_border_colour = Color.FromArgb(255, 20, 85, 140);

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = default_text_colour;
            base.OnRenderArrow(e);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle rc = new(Point.Empty, e.Item.Size);
            Color border = e.Item.Selected ? border_colour : default_border_colour;
            Color highlight = e.Item.Selected ? highlight_colour : default_colour;
            Rectangle rc_big = new(new Point(1, 0), new Size(rc.Width - 2, rc.Height - 1));
            using (SolidBrush brush = new(highlight))
            {
                e.Graphics.FillRectangle(brush, rc);
            }
            using Pen pen = new(border, 1);
            e.Graphics.DrawRectangle(pen, rc_big);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle rc = new(new Point(0, 1), new Size(e.ToolStrip.Size.Width, e.ToolStrip.Size.Height - 2));
            using (Pen pen = new(menu_border_colour, 2))
                e.Graphics.DrawRectangle(pen, rc);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = default_text_colour;
            base.OnRenderItemText(e);
        }
    } 
    class GUI : ApplicationContext
    {
        public static NotifyIcon notifyIcon = new();
        public static Mutex balloon_mutex = new();
        private readonly ContextMenuStrip contextMenu = new();
        readonly ToolStripMenuItem port = new();
        readonly ToolStripMenuItem volume_sense = new();
        readonly ToolStripMenuItem output = new();
        readonly ToolStripMenuItem default_audio_output = new();
        readonly ToolStripMenuItem title = new();
        readonly ToolStripMenuItem exit = new();
        readonly ToolStripMenuItem wallpaper_mode = new();
        IEnumerable<CoreAudioDevice>? cad = null;
        public static bool continue_read = true;
        public static bool continue_config = true;
        public static bool continue_media = true;
        static Image selected_img = SystemIcons.Exclamation.ToBitmap();
        public static Thread read_thread = new(DeviceHandler.Read);
        public static Thread config_thread = new(ConfigHandler.ConfigChangeHandler);
        public static Thread media_thread = new(DeviceHandler.HandlerSetup);
        static DeviceHandler.Oracle_Configuration oracle_Configuration = new();
        static readonly DeviceHandler.Oracle_Configuration oracle_Config_Old = new();

        private GUI()
        {
            string icon_paths = "Icons_Images\\";
            string path_icon = icon_paths + "huge.png";
            Image logo_img = SystemIcons.Application.ToBitmap();
            Image exit_symbol = SystemIcons.Error.ToBitmap();
            Bitmap logo_icon = SystemIcons.Error.ToBitmap();
            try
            {
                logo_img = Image.FromFile(icon_paths + "small.png");
                selected_img = Image.FromFile(icon_paths + "Selected.png");
                exit_symbol = Image.FromFile(icon_paths + "Exit_Symbol.png");
                logo_icon = (Bitmap)Image.FromFile(path_icon);
            }
            catch (Exception ex)
            {
                DeviceHandler.WriteLog(ex.ToString());
            }
            port = new ToolStripMenuItem("Port", null, new EventHandler(GetPorts));
            volume_sense = new ToolStripMenuItem("Volume Knob Sensitivity", null);
            output = new ToolStripMenuItem("Playback Devices", null);
            exit = new("Exit", exit_symbol, new EventHandler(OnClose));
            wallpaper_mode = new("Wallpaper Mode", null);

            wallpaper_mode.DropDownItems.Add(new ToolStripMenuItem("Disabled", null, new EventHandler(OnWallpaperToggle)));
            wallpaper_mode.DropDownItems.Add(new ToolStripMenuItem("Enabled", null, new EventHandler(OnWallpaperToggle)));
            default_audio_output = new ToolStripMenuItem("Default Device", null, new EventHandler(OnSetAudioDevice));
            output.DropDownItems.Add(default_audio_output);
            default_audio_output.Image = selected_img;


            title = new("Thoth's Oracle", null)
            {
                Image = logo_img,
                Font = new Font(title.Font, FontStyle.Bold),                
            };


            IntPtr logo_ptr = logo_icon.GetHicon();
            exit.MouseUp += new MouseEventHandler(OnClose);
            contextMenu.Items.AddRange(new ToolStripItem[] { title, port, volume_sense, output, wallpaper_mode, exit });
            contextMenu.Renderer = new OracleRenderer();
            notifyIcon = new NotifyIcon
            {
                Icon = Icon.FromHandle(logo_ptr),
                Visible = true,
                Text = "Thoth's Oracle",
                ContextMenuStrip = contextMenu
            };
            notifyIcon.ContextMenuStrip.AutoClose = true;
            notifyIcon.MouseClick += new MouseEventHandler(TopMenuClick);
            notifyIcon.ContextMenuStrip.Closed += new ToolStripDropDownClosedEventHandler(TopMenuExit);
            volume_sense.DropDown.MouseEnter += new EventHandler(OnAutoCloseDisable);
            volume_sense.DropDown.MouseLeave += new EventHandler(OnAutoCloseEnable);
            port.DropDown.MouseEnter += new EventHandler(OnAutoCloseDisable);
            port.DropDown.MouseLeave += new EventHandler(OnAutoCloseEnable);
            output.DropDown.MouseEnter += new EventHandler(OnAutoCloseDisable);
            output.DropDown.MouseLeave += new EventHandler(OnAutoCloseEnable);
            wallpaper_mode.DropDown.MouseEnter += new EventHandler(OnAutoCloseDisable);
            wallpaper_mode.DropDown.MouseLeave += new EventHandler(OnAutoCloseEnable);
        }

        private void OnWallpaperToggle(object? sender, EventArgs args)
        {
            if (sender == null)
                return;
            string? sender_string = sender.ToString();
            sender_string ??= "";
            if (sender_string.Equals("Disabled"))
            {
                oracle_Configuration.WallpaperMode = false;
                wallpaper_mode.DropDown.Items[0].Image = selected_img;
                wallpaper_mode.DropDown.Items[1].Image = null;
            }
            else
            {
                oracle_Configuration.WallpaperMode = true;
                wallpaper_mode.DropDown.Items[0].Image = null;
                wallpaper_mode.DropDown.Items[1].Image = selected_img;
            }
            DeviceHandler.WriteLog("Wallpaper? " + oracle_Configuration.WallpaperMode);
        }
        private void TopMenuExit(object? sender, EventArgs args)
        {
            ConfigHandler.SaveConfig(oracle_Configuration);
            DeviceHandler.WriteLog("Change config");
        }

        private void TopMenuClick(object? sender, EventArgs args)
        {
            oracle_Configuration = ConfigHandler.LoadConfig();
            GetPorts(null, args);
            int wp_mode = oracle_Configuration.WallpaperMode ? 1 : 0;
            int not_wp_mode = !oracle_Configuration.WallpaperMode ? 1 : 0;
            wallpaper_mode.DropDown.Items[wp_mode].Image = selected_img;
            wallpaper_mode.DropDown.Items[not_wp_mode].Image = null;
            oracle_Config_Old.VolumeSensitivityOptions ??= new List<ushort> { };
            if (oracle_Configuration.VolumeSensitivityOptions == null || oracle_Config_Old.VolumeSensitivityOptions == null)
                return;
            if (Enumerable.SequenceEqual(oracle_Config_Old.VolumeSensitivityOptions, oracle_Configuration.VolumeSensitivityOptions))
                return;

            volume_sense.DropDownItems.Clear();
            List<ushort>? ushorts = oracle_Configuration.VolumeSensitivityOptions;
            if (ushorts != null)
            {
                ToolStripItem tool;
                foreach (ushort sense in ushorts)
                {
                    tool = volume_sense.DropDownItems.Add("±", null, OnVolumeChange);
                    tool.Text += sense.ToString();
                    tool.Tag = sense;
                    if (sense == oracle_Configuration.VolumeSensitivity)
                        tool.Image = selected_img;
                }
            }
            oracle_Config_Old.VolumeSensitivityOptions = oracle_Configuration.VolumeSensitivityOptions;
        }
        private void OnVolumeChange(object? sender, EventArgs args)
        {
            if (sender == null)
                return;
            ToolStripItem sender_item = (ToolStripItem)sender;
            if (sender_item.Tag == null)
                return;
            oracle_Configuration.VolumeSensitivity = (ushort)sender_item.Tag;
            ResetList(sender);

        }
        private void OnAutoCloseEnable(object? sender, EventArgs args)
        {
            if (sender == null)
                return;
            ToolStripDropDownMenu dropDown = (ToolStripDropDownMenu)sender;
            dropDown.AutoClose = true;
        }
        private void OnAutoCloseDisable(object? sender, EventArgs args)
        {
            if (sender == null)
                return;
            ToolStripDropDownMenu dropDown = (ToolStripDropDownMenu)sender;
            dropDown.AutoClose = false;
            if (dropDown.OwnerItem.Text.Equals("Playback Devices"))
                GetAudioDevices();
        }
        private void GetPorts(object? sender, EventArgs args)
        {
            try
            {
                string[] ports_s = SerialPort.GetPortNames();
                port.DropDownItems.Clear();
                int i = 0;
                foreach (string p in ports_s)
                {
                    port.DropDownItems.Add(p, null, new EventHandler(OnSetPort));
                    if (p.Equals(oracle_Configuration.ComPort))
                        port.DropDownItems[i].Image = selected_img;
                    i++;
                }
            }
            catch (Exception ex)
            {
                DeviceHandler.WriteLog(ex.ToString());
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
                output.DropDownItems.Add(c.Name, null, new EventHandler(OnSetAudioDevice));
                if (c.Name.Equals(oracle_Configuration.PlaybackDevice))
                {
                    output.DropDownItems[index].Image = selected_img;
                    default_audio_output.Image = null;
                }
                index++;
            }
        }
        private void OnSetAudioDevice(object? sender, EventArgs args)
        {
            if (sender == null || cad == null || !cad.Any())
                return;
            oracle_Configuration.PlaybackDevice = sender.ToString();
            ResetList(sender);
        }


        private void OnSetPort(object? sender, EventArgs e)
        {
            if (sender == null)
                return;
            oracle_Configuration.ComPort = sender.ToString() ?? "COM3";
            ResetList(sender);
        }

        private static void ResetList(object sender)
        {
            ToolStripItem sender_item = (ToolStripItem)sender;
            ToolStripMenuItem dropDown = (ToolStripMenuItem)sender_item.OwnerItem;
            foreach (ToolStripItem tool in dropDown.DropDownItems)
            {
                tool.Image = null;
            }
            sender_item.Image = selected_img;
        }

        private void OnClose(object? sender, EventArgs e)
        {
            continue_config = false;
            continue_read = false;
            continue_media = false;
            contextMenu.Close();
            contextMenu.Visible = false;
            Dispose();
            ExitThread();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            media_thread.Start();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.Run(new GUI());
        }
    }
}//net7.0-windows10.0.17763.0