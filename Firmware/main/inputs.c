#include "data.h"

char cmd_vol_dn[2]      = {5, 1};
char cmd_vol_up[2]      = {5, 2};
char cmd_mute[2]        = {5, 3};
char cmd_prev[2]        = {5, 4};
char cmd_play_pause[2]  = {5, 5};
char cmd_next[2]        = {5, 6};
//char* TAG = "egr";

button_config_t gpio_btn_cfg = {
    .type = BUTTON_TYPE_GPIO,
    .long_press_time = CONFIG_BUTTON_LONG_PRESS_TIME_MS,
    .short_press_time = CONFIG_BUTTON_SHORT_PRESS_TIME_MS,
    .gpio_button_config = {
        .gpio_num = 33,
        .active_level = 0,
    },
};

button_handle_t btn_previous;
button_handle_t btn_pp;
button_handle_t btn_next;
button_handle_t btn_mute_tog;

/*//testing
gpio_config_t gp_high = {
    .mode = GPIO_MODE_OUTPUT,
};*/


pcnt_unit_config_t unit_config = {
    .high_limit = 1,
    .low_limit = -1,
};
pcnt_unit_handle_t pcnt_unit = NULL;
pcnt_glitch_filter_config_t filter_config = {
    .max_glitch_ns = 10000,
};
pcnt_chan_config_t chan_config = {
    .edge_gpio_num = ENC_EDGE_PIN,
    .level_gpio_num = ENC_LEVEL_PIN,
};
pcnt_channel_handle_t pcnt_chan = NULL;
int watch_points[] = {-1, 0, 1};

static void button_previous_cb(void *arg, void *data)
{
    usb_serial_jtag_write_bytes(cmd_prev, 2, portDelay);
}

static void button_mute_cb(void *arg, void *data)
{
    usb_serial_jtag_write_bytes(cmd_mute, 2, portDelay);
}

static void button_pause_play_cb(void *arg, void *data)
{
    usb_serial_jtag_write_bytes(cmd_play_pause, 2, portDelay);
}

static void button_next_cb(void *arg, void *data)
{
    usb_serial_jtag_write_bytes(cmd_next, 2, portDelay);
}

static bool pcnt_on_reach(pcnt_unit_handle_t unit, const pcnt_watch_event_data_t *edata, void *user_ctx)
{
    BaseType_t high_task_wakeup;
    QueueHandle_t queue = (QueueHandle_t)user_ctx;
    // send event data to queue, from this interrupt callback
    xQueueSendFromISR(queue, &(edata->watch_point_value), &high_task_wakeup);
    return (high_task_wakeup == pdTRUE);
}

void inputs_main(){
    //testing
    //gpio_config(&gp_high);
    //gpio_set_level(GPIO_NUM_23, 1);

    //previous track
    gpio_btn_cfg.gpio_button_config.gpio_num = PREV_PIN;
    btn_previous = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_previous, BUTTON_PRESS_DOWN, button_previous_cb, NULL);

    //mute toggle
    gpio_btn_cfg.gpio_button_config.gpio_num = MUTE_PIN;
    btn_mute_tog = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_mute_tog, BUTTON_PRESS_DOWN, button_mute_cb, NULL);

    //pause/play
    gpio_btn_cfg.gpio_button_config.gpio_num = PLAY_PIN;
    btn_mute_tog = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_mute_tog, BUTTON_PRESS_DOWN, button_pause_play_cb, NULL);

    //next track
    gpio_btn_cfg.gpio_button_config.gpio_num = NEXT_PIN;
    btn_mute_tog = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_mute_tog, BUTTON_PRESS_DOWN, button_next_cb, NULL);
    
    //pulse counter for encoder
    pcnt_new_unit(&unit_config, &pcnt_unit);
    pcnt_unit_set_glitch_filter(pcnt_unit, &filter_config);
    pcnt_new_channel(pcnt_unit, &chan_config, &pcnt_chan);
    ESP_ERROR_CHECK(pcnt_channel_set_edge_action(pcnt_chan, PCNT_CHANNEL_EDGE_ACTION_DECREASE, PCNT_CHANNEL_EDGE_ACTION_INCREASE));
    ESP_ERROR_CHECK(pcnt_channel_set_level_action(pcnt_chan, PCNT_CHANNEL_LEVEL_ACTION_KEEP, PCNT_CHANNEL_LEVEL_ACTION_INVERSE));
    
    pcnt_unit_add_watch_point(pcnt_unit, watch_points[0]);
    pcnt_unit_add_watch_point(pcnt_unit, watch_points[2]);

    pcnt_event_callbacks_t cbs = {
        .on_reach = pcnt_on_reach,
    };
    QueueHandle_t queue = xQueueCreate(10, sizeof(int));
    ESP_ERROR_CHECK(pcnt_unit_register_event_callbacks(pcnt_unit, &cbs, queue));

    ESP_ERROR_CHECK(pcnt_unit_enable(pcnt_unit));
    ESP_ERROR_CHECK(pcnt_unit_clear_count(pcnt_unit));
    ESP_ERROR_CHECK(pcnt_unit_start(pcnt_unit));

    // Report counter value
    int event_count = 0;
    while(1){
        if (xQueueReceive(queue, &event_count, pdMS_TO_TICKS(1000))) {
            //printf("Event count '%d'\n", event_count);
            //ESP_LOGI(TAG, "Watch point event, count: %d", event_count);
            if(event_count == -1){
                usb_serial_jtag_write_bytes(cmd_vol_dn, 2, portDelay);
            }
            else{
                usb_serial_jtag_write_bytes(cmd_vol_up, 2, portDelay);
            }
        }
        //vTaskDelay(pdMS_TO_TICKS(50));
    }
}