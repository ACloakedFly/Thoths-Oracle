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
// inputs.c

#include "data.h"

//Arrays for sending input commands. First number is to signal we are sending input command, second number is the command
char cmd_vol_dn[2]      = {5, 1};
char cmd_vol_up[2]      = {5, 2};
char cmd_mute[2]        = {5, 3};
char cmd_prev[2]        = {5, 4};
char cmd_play_pause[2]  = {5, 5};
char cmd_next[2]        = {5, 6};

//Button config, reused for simplicity
button_config_t gpio_btn_cfg = {
    .type = BUTTON_TYPE_GPIO,
    .long_press_time = CONFIG_BUTTON_LONG_PRESS_TIME_MS,
    .short_press_time = CONFIG_BUTTON_SHORT_PRESS_TIME_MS,
    .gpio_button_config = {
        .gpio_num = 33,
        .active_level = 0,
    },
};

//Handles
button_handle_t btn_previous;
button_handle_t btn_pp;
button_handle_t btn_next;
button_handle_t btn_mute_tog;

//Pulse counter configs. This is used for the rotary encoder for sending volume commands. 
//Brightness rotary potentiometer acts as voltage divider sending direct voltage to LCD backlight input. No code required.
pcnt_unit_config_t unit_config = {
    .high_limit = 1,
    .low_limit = -1,
};
pcnt_unit_handle_t pcnt_unit = NULL;
pcnt_glitch_filter_config_t filter_config = {
    .max_glitch_ns = 10000,
};
pcnt_chan_config_t chan_config = {
    .edge_gpio_num = ENC_LEVEL_PIN,
    .level_gpio_num = ENC_EDGE_PIN,
};
pcnt_channel_handle_t pcnt_chan = NULL;
//Watch points used for generating pulse counter events
int watch_points[] = {-1, 0, 1};

//Button callbacks. Send command array [input flag, command] over serial connection
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

//Add event to queue on pulse counter watch point reached. Loop at bottom will monitor queue for events
static bool pcnt_on_reach(pcnt_unit_handle_t unit, const pcnt_watch_event_data_t *edata, void *user_ctx)
{
    BaseType_t high_task_wakeup;
    QueueHandle_t queue = (QueueHandle_t)user_ctx;
    // send event data to queue, from this interrupt callback
    xQueueSendFromISR(queue, &(edata->watch_point_value), &high_task_wakeup);
    return (high_task_wakeup == pdTRUE);
}

void inputs_main(){

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
        //What is my purpose? Your purpose is to wait for pcnt events :)
        if (xQueueReceive(queue, &event_count, pdMS_TO_TICKS(1000))) {
            //Send command over serial based on rotation direction 
            if(event_count == -1){
                usb_serial_jtag_write_bytes(cmd_vol_dn, 2, portDelay);
            }
            else{
                usb_serial_jtag_write_bytes(cmd_vol_up, 2, portDelay);
            }
        }
    }
}