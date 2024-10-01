command-list-langs-desc = Перечисляет языки, на которых может говорить ваше текущее существо в данный момент.
command-list-langs-help = Использование: {$command}

command-saylang-desc = Отправить сообщение на определённом языке. Для выбора языка можно использовать его название или позицию в списке языков.
command-saylang-help = Использование: {$command} <id языка> <сообщение>. Пример: {$command} TauCetiBasic "Привет, мир!". Пример: {$command} 1 "Привет, мир!"

command-language-select-desc = Выбрать текущий говорящий язык вашего существа. Можно использовать название языка или его позицию в списке языков.
command-language-select-help = Использование: {$command} <id языка>. Пример: {$command} 1. Пример: {$command} TauCetiBasic

command-language-spoken = Говорит:
command-language-understood = Понимает:
command-language-current-entry = {$id}. {$language} - {$name} (текущий)
command-language-entry = {$id}. {$language} - {$name}

command-language-invalid-number = Номер языка должен быть от 0 до {$total}. Либо используйте название языка.
command-language-invalid-language = Язык {$id} не существует или вы не можете на нём говорить.

# toolshed

command-description-language-add = Добавляет новый язык к переданному существу. Два последних аргумента указывают, должен ли язык быть говоримым/понимаемым. Пример: 'self language:add "Canilunzt" true true'
command-description-language-rm = Удаляет язык у переданного существа. Работает аналогично language:add. Пример: 'self language:rm "TauCetiBasic" true true'.
command-description-language-lsspoken = Перечисляет все языки, на которых существо может говорить. Пример: 'self language:lsspoken'
command-description-language-lsunderstood = Перечисляет все языки, которые существо может понимать. Пример: 'self language:lssunderstood'

command-description-translator-addlang = Добавляет новый целевой язык к переданному переводу. Подробности см. в language:add.
command-description-translator-rmlang = Удаляет целевой язык из переданного перевода. Подробности см. в language:rm.
command-description-translator-addrequired = Добавляет новый обязательный язык к переданному переводу. Пример: 'ent 1234 translator:addrequired "TauCetiBasic"'
command-description-translator-rmrequired = Удаляет обязательный язык из переданного перевода. Пример: 'ent 1234 translator:rmrequired "TauCetiBasic"'
command-description-translator-lsspoken = Перечисляет все говоримые языки для переданного перевода. Пример: 'ent 1234 translator:lsspoken'
command-description-translator-lsunderstood = Перечисляет все понимаемые языки для переданного перевода. Пример: 'ent 1234 translator:lssunderstood'
command-description-translator-lsrequired = Перечисляет все обязательные языки для переданного перевода. Пример: 'ent 1234 translator:lsrequired'

command-language-error-this-will-not-work = Это не сработает.
command-language-error-not-a-translator = Сущность {$entity} не является переводчиком.