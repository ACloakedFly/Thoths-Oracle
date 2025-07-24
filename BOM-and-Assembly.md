# BOM

## Hardware
| Qty | Item | Notes |
| ---- | -- | ------| 
| 9 | M3x20 SHCS | |
| 9 | M3x4x5 brass heatset inserts | |
| 2 | 6mm shaft guitar knobs | 18mm outer diameter works well. Knobs are 38mm apart, so anything larger might get tight. |
| 3 | Keycaps for the switches | |

## Electronics
| Qty | Item | Notes |
| ------ | -- | --------- |
| 1 | PCB | Can be purchased from PCBWay or JLCPCB or similar using the Thoths_Oracle_PCB_xxx.rar file in the latest release | 
| 1 | 3.5" 320x480 SPI display | ST7796U will work, ILI9488 should work but has not been tested. [link](https://www.aliexpress.com/item/1005005878590372.html?spm=a2g0o.order_list.order_list_main.75.163a1802D7gBJg#nav-description) to the 3.5" no touch IPS display I use. |
| 1 | ESP32-S3 zero | As long as it has the ESP32-FH4R2 chip it will work. Presoldered headers are nice |
| 3 | Mechanical keyboard switches | Standard MX pinout or Kailh Choc V2 will work. |
| 3-6 | Matching hotswap sockets | PG1511 for standard, or PG1350 for Choc V2. Both types can be installed on the same switch location if you want to experiment. |
| 1 | EC11 rotary encoder | A 12.5mm long shaft will work. Longer would be better. |
| 1 | RV09 rotary potentiometer | A 12.5mm long shaft will work. Longer would be better. |
| 2 | 2.54mm pitch feamle header sockets 1x9 pins | Longer header sockets can be cut shorter (eg. 1x20 cut to two 1x9s) |
| 1 | XH 2.54mm horizontal male header 1x8 pins | |
| 1 | JST 2.54mm connector cable 1x8 pins | At least 5cm is needed but it might be hard to route. 10cm works fine. |
| 1 | USB-c data cable | |

## 3D Prints
| Qty | Item |
| ------ | -- |
| 1 | Oracle_inputs_plate.stl |
| 1 | Oracle_inputs.stl |
| 1 | Oracle_screen_plate.stl |
| 1 | Oracle_screen.stl |
| 2 | Oracle_thumbscrew.stl |
| 2 | Oracle_thumbscrew_cap.stl |


# Assembly

## 3D printing Instructions

All STLs are in the correct orientation for printing. The Oracle_inputs STL is the only one that needs supports. Any stiff material should work fine (I used PLA).
Here are the support settings I have found to work well
![Support_settings](https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Support_settings.png)

## PCB Assembly Instructions

##  Firmware Flashing Instructions

- Connect your ESP32 to your computer with a USB cable, then find out which port it is connected to either using Thoth's Oracle Software or through your OS.
- Download the ESP flashing tool from [here](https://docs.espressif.com/projects/esp-test-tools/en/latest/esp32s3/production_stage/tools/flash_download_tool.html), then extract the folder.
- Download and extract the latest release of Thoths_Oracle_Firmware. Place all three files into the bin folder of the flashing tool.
- Run the flashing tool exe.
- Set the chip type to ESP32-S3, Workflow to Develop, and Load mode to UART, then click OK.
- Set up the next screen according to the screenshot below, ensuring the COM matches the port your ESP32 is connected to.
- Press start and let the tool finish.
- The tool does not restart the ESP32, so if you are doing this when the Oracle is assembled, disconnect and reconnect the USB cable and it should start displaying. 
  
![flashing_instructions](https://github.com/ACloakedFly/Thoths-Oracle/blob/main/Images/Flash%20tool%20configuration.png)

