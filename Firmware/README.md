# Flashing instructions

- Connect your ESP32 to your computer with a USB cable, then find out which port it is connected to either using Thoth's Oracle Software or through your OS.
- Download the ESP flashing tool from [here](https://docs.espressif.com/projects/esp-test-tools/en/latest/esp32s3/production_stage/tools/flash_download_tool.html), then extract the folder.
- Download and extract the latest release of Thoths_Oracle_Firmware. Place all three files into the bin folder of the flashing tool.
- Run the flashing tool exe.
- Set the chip type to ESP32-S3, Workflow to Develop, and Load mode to UART, then click OK.
- Set up the next screen according to the screenshot below, ensuring the COM matches the port your ESP32 is connected to.
- Press start and let the tool finish.
- The tool does not restart the ESP32, so if you are doing this when the Oracle is assembled, disconnect and reconnect the USB cable and it should start displaying. 
  
![flashing_instructions](https://github.com/ACloakedFly/Thoths-Oracle/blob/main/Images/Flash%20tool%20configuration.png)
