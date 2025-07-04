#include "data.h"

TaskHandle_t LCDTask, uart_task, uart_writer_task;
lv_disp_t disp;
const char TAG[] = {"dev"};

void app_main(void){
    info_mutex = xSemaphoreCreateBinary();
    xSemaphoreGive(info_mutex);
    xTaskCreatePinnedToCore(ui_setup, "ui_task", configMINIMAL_STACK_SIZE + 50000, NULL, 0, &LCDTask, 0);//should be higher than Update Info
    esp_log_level_set(TAG, ESP_LOG_INFO);
    serial_setup();
    xTaskCreatePinnedToCore(serial_task, "serial_event_task", 4096, NULL, 5, &uart_task, 1);
    xTaskCreatePinnedToCore(inputs_main, "inputs_task", configMINIMAL_STACK_SIZE + 1000 + 1024, NULL, 1, &uart_writer_task, 1);
}