idf_component_register(SRCS "dejavusans.c" "dejavu_sans_16_phl.c" "lcd_setup.c" "lvgl_ui.c" "icons.c" "inputs.c" "main.c" "serial_rw.c" "lv_conf.h"
                        INCLUDE_DIRS "." 
                    )

littlefs_create_partition_image(storage ../flash_data FLASH_IN_PROJECT)