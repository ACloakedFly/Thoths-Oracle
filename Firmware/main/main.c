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
// main.c

#include "data.h"

TaskHandle_t LCDTask, uart_task, uart_writer_task, update_info, ticker_task;
const char TAG[] = {"dev"};


void app_main(void){
    info_mutex = xSemaphoreCreateBinary();
    xSemaphoreGive(info_mutex);
    img_mutex = xSemaphoreCreateBinary();
    xSemaphoreGive(img_mutex);
    date_time_mutex = xSemaphoreCreateBinary();
    xSemaphoreGive(date_time_mutex);
    ui_setup(NULL);
    xTaskCreatePinnedToCore(timer_incer,"ticker_task", configMINIMAL_STACK_SIZE*2,NULL,1,&ticker_task,0);//should be lower than ui_task
    xTaskCreatePinnedToCore(UpdateInfo,"Update Info", configMINIMAL_STACK_SIZE*2,NULL,10,&update_info,0);//should be lower than ui_task
    //xTaskCreatePinnedToCore(ui_setup, "ui_task", configMINIMAL_STACK_SIZE + 50000, NULL, 0, &LCDTask, 1);//should be higher than Update Info
    esp_log_level_set(TAG, ESP_LOG_INFO);
    xTaskCreatePinnedToCore(serial_task, "serial_event_task", 4096, NULL, 5, &uart_task, 1);
    xTaskCreatePinnedToCore(inputs_main, "inputs_task", configMINIMAL_STACK_SIZE + 1000 + 1024, NULL, 1, &uart_writer_task, 1);
}
