## Strings for the "grant_connect_bypass" command.

cmd-grant_connect_bypass-desc = Временно разрешите пользователю обходить регулярные проверки подключения.
cmd-grant_connect_bypass-help =
    Использование: grant_connect_bypass <пользователь> [продолжительность в минутах]
    Временно предоставляет пользователю возможность обходить обычные ограничения на подключение.
    Режим обхода применяется только к данному игровому серверу и истекает (по умолчанию) через 1 час.
    Они смогут присоединиться независимо от наличия белого списка, бункера паники или шапки игрока.
cmd-grant_connect_bypass-arg-user = <user>
cmd-grant_connect_bypass-arg-duration = [duration minutes]
cmd-grant_connect_bypass-invalid-args = Ожидается 1 или 2 аргумента
cmd-grant_connect_bypass-unknown-user = Не получилось найти '{ $user }'
cmd-grant_connect_bypass-invalid-duration = Некорректная длительность '{ $duration }'
cmd-grant_connect_bypass-success = Успешно добавлен bypass для '{ $user }'
