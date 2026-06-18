idf_component_register(SRCS "dejavusans.c" "dejavu_sans_16_phl.c" "lcd_setup.c" "display_s3.c" "power_driver.c" "i2c_driver.c" "lvgl_ui.c" "icons.c" "inputs.c" "main.c" "serial_rw.c" 
                    INCLUDE_DIRS ".")


littlefs_create_partition_image(storage ../flash_data FLASH_IN_PROJECT)