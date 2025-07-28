# BOM

## Hardware
| Qty | Item | Notes |
| ---- | -- | ------| 
| 7 or 9 | M3x20 SHCS | [Example](https://www.aliexpress.com/item/32810872544.html?spm=a2g0o.order_list.order_list_main.31.5b9d1802S5DEg1) |
| 2 | M3x16 SHCS | (optional) shorter screws can be used for the thumbscrews. |
| 9 | M3x4x5 (Screw, height, outer diameter) brass heatset inserts | [Example](https://www.aliexpress.com/item/1005003582355741.html?spm=a2g0o.order_list.order_list_main.63.5b9d1802S5DEg1) |
| 2 | 6mm shaft guitar knobs | 18mm outer diameter works well. Knobs are 38mm apart, so anything larger might get tight. [Example](https://www.aliexpress.com/item/1005003800832352.html) |
| 3 | Keycaps for the switches | [Example](https://www.aliexpress.com/item/1005002976015114.html) |

## Electronics
| Qty | Item | Product Number | Notes |
| ------ | -- | -- | --------- |
| 1 | PCB |  | Can be purchased from PCBWay or JLCPCB or similar using the Thoths_Oracle_PCB_xxx.rar file in the latest release | 
| 1 | 3.5" 320x480 SPI display | [MSP3525](https://www.lcdwiki.com/3.5inch_IPS_SPI_Module_ST7796) | ST7796U will work, ILI9488 should work but has not been tested. [link](https://www.aliexpress.com/item/1005005878590372.html?spm=a2g0o.order_list.order_list_main.75.163a1802D7gBJg#nav-description) to the IPS display I use. |
| 1 | ESP32-S3 zero | [ESP32-S3-Zero](https://www.waveshare.com/wiki/ESP32-S3-Zero) | As long as it has the ESP32-FH4R2 chip it will work. Presoldered headers are nice |
| 3 | Mechanical keyboard switches |  | Standard MX pinout or Kailh Choc V2 will work. [MX](https://www.cherry.de/en-gb/product/mx2a-red), [Choc V2](https://www.kailh.net/products/kailh-choc-v2-low-profile-switch-set)|
| 3-6 | Matching hotswap sockets | [PG1511](http://www.kailh.com/en/Products/Ks/HPC/), [PG1350](https://cdn.shopify.com/s/files/1/0588/1108/9090/files/5118-Choc-Socket.pdf?v=1686715063) | PG1511 for standard, or PG1350 for Choc V2. Both types can be installed on the same switch location if you want to experiment. |
| 1 | EC11 rotary encoder | [EC11E15244G1](https://tech.alpsalpine.com/e/products/detail/EC11E15244G1/) | A 12.5mm long shaft will work. Longer would be better. |
| 1 | RV09 rotary potentiometer | [RV09BCF-40](https://www.taiwanalpha.com/downloads?target=products&id=72) | A 12.5mm long shaft will work. Longer would be better. |
| 2 | female header sockets 1x9 pins | [66951-009LF](https://www.digikey.com/en/products/detail/amphenol-cs-fci/66951-009LF/1002554) | Ideally 8.4mm insulation height. Longer header sockets can be cut shorter (eg. 1x20 cut to two 1x9s) |
| 1 | XH 2.5mm horizontal male header 1x8 pins | [S8B-XH-A-1](https://www.digikey.com/en/products/detail/jst-sales-america-inc/S8B-XH-A-1/9961927) |
| 1 | JST 2.5mm connector cable 1x8 pins | [XHP-8](https://www.digikey.com/en/resources/datasheets/jst/xh-connector) | These can be found as [preassebled cables](https://www.aliexpress.com/item/4000698884218.html?spm=a2g0o.order_list.order_list_main.5.5e661802USnLWp). At least 5cm is needed but it might be hard to route. 10cm works fine. |
| 1 | USB-C data cable | | C to C or C to A depending on your preference |

## 3D Prints
| Qty | Item |
| ------ | -- |
| 1 | Oracle_inputs_plate.stl |
| 1 | Oracle_inputs.stl |
| 1 | Oracle_screen_plate.stl |
| 1 | Oracle_screen.stl |
| 2 | Oracle_thumbscrew.stl |
| 2 | Oracle_thumbscrew_cap.stl |


# Shell Instructions

## 3D Printing

All STLs are in the correct orientation for printing. The Oracle_inputs STL is the only one that needs supports. Any stiff material should work fine (I used PLA).
Here are the support settings I have found to work well
![Support_settings](https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Support_settings.png)

# PCB Assembly and Heat Sets Instructions

**Disclaimer:** I am not the best solderer. This is more for where everything goes, and an order that works. You do not need to be a highly skilled solderer to do this. 
If you are new at this, I recommend watching tutorials and safely practicing a bit. 

**Required Tools and Materials**
- Solder
- Soldering iron
- Ventilation fan
- Safety glasses
- M2.5 hex bit
- M2.0 hex bit
- Flush cutters
- Pliers

Collect the components. Only one set of hot swap sockets is required, both can be used.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_142303.JPG" width="500">

If you are using header sockets longer than 9 pins, snip across the 10th.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_142508.JPG" width="500">

The cut end will need to be cleaned up. This can be done with the snips.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_142556.JPG" width="500">

Try not to cut too far into the next socket. Repeat for the second row.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_142640.JPG" width="500"><img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_142642.JPG" width="500">

Depending on which hot swap sockets you are using, place them in the corresponding holes and solder them onto the pads.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_142912.JPG" width="500">

Yikes! But it will do. These are only switches, so precision is not required.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_143912.JPG" width="500">

The pins for the screen go on the same side as the hotswap sockets. So solder them from the top of the board.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_144026.JPG" width="500">

For the ESP32, placing the sockets on its header pins keeps them straight.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_144604.JPG" width="500">

They also go on the bottom of the board, same as the screen pins. If you are worried about overheating the ESP32, solder a couple pins on each row to keep them steady, then remove the ESP32.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_144715.JPG" width="500">

The rotary encoder and potentiometer go on the top of the board.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_145233.JPG" width="500">

Hopefully your board looks better than mine!

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_145635.JPG" width="500"><img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_145638.JPG" width="500">

Soldering is complete!
The iron will still be needed for the heat set inserts.

All the heat set inserts should be pressed flush with their top surfaces. A few of them are fairly close to tall walls, so I found it easier to press them in with the side of the soldering tip once they were hot enough to go in with not much force.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_145738.JPG" width="500">

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_145826.JPG" width="500"><img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_145850.JPG" width="500">

That's the end of the soldering iron.


# Final Assembly Instructions

The PCB goes into the shell with the ESP facing outwards.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150105.JPG" width="500">

As you slide the PCB into the shell, pull the wall with USB hole away from the ESP32 slightly to clear the USB port. The port should fit snuggly into the hole afterwards.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150129.JPG" width="500">

Screw the inputs plate onto the shell

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250727_092545.JPG" width="500">

Line up the screen with the heat sets and connect the ribbon cable. Make sure GND through LED are the ones connected.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150522.JPG" width="500">

Screw the screen plate on.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150537.JPG" width="500">

Line up the plug with the hole in the inputs shell, then push it in until it is fully seated.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150623.JPG" width="500"><img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150647.JPG" width="500">

The two halves can now be put together. It will be a tight fit.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150727.JPG" width="500">

Place the screw as far into the thumbscrew without pushing the head into it. The cap needs to be aligned with the screw first.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_150823.JPG" width="500">

Push the cap into the head and line the tabs up with the hole.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151108.JPG" width="500"><img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151123.JPG" width="500">

Flip the whole thing over and push the thumbscrew down until everything is pressed into place. Repeat these steps for the second thumbscrew.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151205.JPG" width="500"><img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151227.JPG" width="500">

Screw each thumbscrew into the screen.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151311.JPG" width="500">

Depending on the length of your potentiometer and encoder shafts, the knobs might drag along the shell. So lift them up a little before securing them with the grub screw. This is especially important for the encoder to keep the switch functioning.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151440.JPG" width="500">

Try to make sure the pins on your switches are fairly straight. The pin on the left might be too crooked to fit nicely. 

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250726_151537.JPG" width="500">

Line up the pins and push the switches into place. The keycaps go on next.

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250724_144318.JPG" width="500">

Your build is complete!

<img src="https://github.com/ACloakedFly/Thoths-Oracle/blob/dev/Images/Assembly_Pics/20250727_091937.JPG" width="500">

# Firmware Flashing Instructions

- Connect your ESP32 to your computer with a USB cable, then find out which port it is connected to either using Thoth's Oracle Software or through your OS.
- Download the ESP flashing tool from [here](https://docs.espressif.com/projects/esp-test-tools/en/latest/esp32s3/production_stage/tools/flash_download_tool.html), then extract the folder.
- Download and extract the latest release of Thoths_Oracle_Firmware. Place all three files into the bin folder of the flashing tool.
- Run the flashing tool exe.
- Set the chip type to ESP32-S3, Workflow to Develop, and Load mode to UART, then click OK.
- Set up the next screen according to the screenshot below, ensuring the COM matches the port your ESP32 is connected to.
- Press start and let the tool finish.
- The tool does not restart the ESP32, so if you are doing this when the Oracle is assembled, disconnect and reconnect the USB cable and it should start displaying. 
  
![flashing_instructions](https://github.com/ACloakedFly/Thoths-Oracle/blob/main/Images/Flash%20tool%20configuration.png)

