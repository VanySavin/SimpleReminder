namespace ReminderApp.Services;

public static class AppSettingsDefaults
{
    public static IReadOnlyDictionary<string, string> Descriptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["themeMode"] =
                "Тема приложения. Auto — как в Windows, Light — всегда светлая, Dark — всегда тёмная.",
            ["snoozeOptionsMinutes"] =
                "Legacy-поле старого формата кнопок отложения (до snoozeButtons). Сохраняется для обратной совместимости при миграции.",
            ["snoozeButtons"] =
                "Кнопки отложения. Minutes — готовая кнопка на указанное число минут, Custom — кнопка для ввода срока словами.",
            ["autoSnoozeOnDismissMinutes"] =
                "Если показанное напоминание-уведомление закрыли крестиком без выбора действия, программа автоматически отложит его на это количество минут.",
            ["submitReminderOnEnter"] =
                "Если true, напоминание добавляется по клавише Enter в поле ввода.",
            ["autoFocusInputOnStart"] =
                "Если true, при запуске курсор сразу ставится в поле ввода напоминания.",
            ["showInputExamples"] =
                "Если true, под полем ввода показываются примеры фраз.",
            ["showCreatedNotification"] =
                "Если true, после создания напоминания показывается маленькое уведомление.",
            ["playNotificationSound"] =
                "Если true, при уведомлениях проигрывается звук.",
            ["notificationDurationSeconds"] =
                "Сколько секунд обычное уведомление будет видно на экране.",
            ["maxVisibleNotifications"] =
                "Сколько напоминаний-уведомлений одновременно видно на экране; остальные ждут в очереди и не теряются.",
            ["notificationPlacement"] =
                "Позиция уведомлений: preset-режим или пользовательские координаты с привязкой к экрану.",
            ["customSnoozeWindowPlacement"] =
                "Позиция окна «Отложить напоминание»: рядом с основным окном, на экране уведомления или пользовательская.",
            ["mainWindowPlacement"] =
                "Последнее положение и размер главного окна программы.",
            ["minimizeToTrayOnClose"] =
                "Если true, крестик окна сворачивает программу в трей, а не закрывает её.",
            ["showTrayNoticeOncePerSession"] =
                "Если true, программа один раз за запуск покажет подсказку, что продолжает работать в трее.",
            ["schedulerIntervalSeconds"] =
                "Как часто программа проверяет наступившие напоминания. Обычно 2 секунды менять не нужно.",
            ["autoStartWithWindows"] =
                "Если true, программа будет запускаться автоматически при входе в Windows.",
            ["startMinimizedOnAutoStart"] =
                "Если true, при автозапуске программа не будет открывать главное окно, а сразу останется работать в трее."
        };

    public static AppSettings CreateDefault()
    {
        var s = new AppSettings
        {
            ThemeMode = ThemeMode.Auto,
            SnoozeButtons =
            [
                new() { Kind = SnoozeButtonKind.Minutes, Enabled = true, Minutes = 5 },
                new() { Kind = SnoozeButtonKind.Minutes, Enabled = false, Minutes = 10 },
                new() { Kind = SnoozeButtonKind.Minutes, Enabled = true, Minutes = 30 },
                new() { Kind = SnoozeButtonKind.Custom, Enabled = true }
            ],
            AutoSnoozeOnDismissMinutes = 20,
            SubmitReminderOnEnter = true,
            AutoFocusInputOnStart = true,
            ShowInputExamples = true,
            ShowCreatedNotification = true,
            PlayNotificationSound = true,
            NotificationDurationSeconds = 5,
            MaxVisibleNotifications = 4,
            NotificationPlacement = new NotificationPlacementSettings { Mode = NotificationPlacementMode.BottomRight },
            CustomSnoozeWindowPlacement = new CustomSnoozeWindowPlacementSettings
            {
                Mode = CustomSnoozeWindowPlacementMode.NearMainWindow
            },
            MainWindowPlacement = new MainWindowPlacementSettings(),
            MinimizeToTrayOnClose = true,
            ShowTrayNoticeOncePerSession = true,
            SchedulerIntervalSeconds = 2,
            AutoStartWithWindows = false,
            StartMinimizedOnAutoStart = true
        };
        foreach (var kv in Descriptions)
        {
            s.Descriptions[kv.Key] = kv.Value;
        }

        return s;
    }
}
