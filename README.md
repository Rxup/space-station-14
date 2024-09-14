# Backmen&Ataraxia

<p style='text-align: center;'><img alt="Backmen&Ataraxia" src="https://raw.githubusercontent.com/Rxup/space-station-14/master/Resources/Textures/Logo/commune.png" width="989px" /></p>

---

Backmen&Ataraxia - это форк [Space Wizards](https://github.com/space-wizards/space-station-14), ориентирующийся на идеи [СтароTG](https://github.com/tgstation/tgstation) и [Shiptest](https://github.com/shiptest-ss13/Shiptest) из Space Station 13, включая в это свои собственные идеи.

Space Station 14 - это ремейк SS13, который работает на собственном движке [Robust Toolbox](https://github.com/space-wizards/Robust-Toolbox), написанном на C#.

## Ссылки

[<img src="https://i.imgur.com/XiS9QP5.png" alt="ASF" width="150" align="left">](https://github.com/AtaraxiaSpaceFoundation)
**Ataraxia Space Foundation**<br>Специализируемся на разработке этого билда.

[<img src="https://i.imgur.com/xMzKtYK.png" alt="Discord" width="150" align="left">](https://discord.gg/ss-14-backmen-ru-1053200453829132298)
**Discord Server**<br>В космосе вас никто не услышит.

[<img src="https://imagizer.imageshack.com/img922/4959/8KTh9r.png" alt="Wiki" width="150" align="left">](https://wiki.backmen.ru)
**Wiki**<br>Что за блобы и с чем их едят?.

## Сборка

Следуйте гайду от [Space Wizards](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) по настройке рабочей среды, но учитывайте, что репозитории отличаются друг от друга и некоторые вещи могут отличаться.
Ниже перечислены скрипты и методы облегчающие работу с билдом.

### Windows

> 1. Склонируйте данный репозиторий.
> 2. Запустите `git submodule update --init --recursive` в командной строке, чтобы скачать движок игры.
> 3. Запускайте `Scripts/bat/buildAllDebug.bat` после любых изменений в коде проекта.
> 4. Запустите `Scripts/bat/runQuickAll.bat`, чтобы запустить клиент и сервер.
> 5. Подключитесь к локальному серверу и играйте.

### Linux

> 1. Склонируйте данный репозиторий.
> 2. Запустите `git submodule update --init --recursive` в командной строке, чтобы скачать движок игры.
> 3. Запускайте `Scripts/sh/buildAllDebug.sh` после любых изменений в коде проекта.
> 4. Запустите `Scripts/sh/runQuickAll.sh`, чтобы запустить клиент и сервер.
> 5. Подключитесь к локальному серверу и играйте.

### MacOS

> Предположительно, также, как и на Линуксе, сами разберётесь.

---

## GptChat

```toml
[gpt]
enabled = true
api = "https://gigachat.devices.sberbank.ru/api/v1/"
model = "GigaChat"
token = ""
giga_token = ["ВСТАВИТЬ СЮДА СЕКРЕТНЫЙ КЛЮЧ"](https://developers.sber.ru/portal/products/gigachat-api)
```

токен запрашивается автоматически по секретному ключу и автоматически обновляется.

---

## Лицензия

Содержимое, добавленное в этот репозиторий после коммита 254687f3d1d1a02aa9dba61d7c114c73dc8e4754 (`17 June 2024 12:00:00 UTC`), распространяется по лицензии GNU Affero General Public License версии 3.0, если не указано иное.
См. [LICENSE-AGPLv3](./LICENSE-AGPLv3.txt).

Содержимое, добавленное в этот репозиторий до коммита 254687f3d1d1a02aa9dba61d7c114c73dc8e4754 (`17 June 2024 12:00:00 UTC`) распространяется по лицензии MIT, если не указано иное.
См. [LICENSE-MIT](./LICENSE-MIT.txt).

Большинство ресурсов лицензировано под [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/), если не указано иное. Лицензия и авторские права на ресурсах указаны в файле метаданных.
[Example](./Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

Обратите внимание, что некоторые активы лицензированы под некоммерческой [CC-BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/) или аналогичной некоммерческой лицензией и должны быть удалены, если вы хотите использовать этот проект в коммерческих целях.
