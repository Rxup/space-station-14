command-list-langs-desc = Список языков, на которых ваше текущее существо может говорить в данный момент.
command-list-langs-help = Использование: { $command }
command-saylang-desc = Отправить сообщение на конкретном языке. Чтобы выбрать язык, вы можете использовать либо название языка, либо его позицию в списке языков.
command-saylang-help = Использование: { $command } <id языка> <сообщение>. Пример: { $command } TauCetiBasic "Привет, мир!". Пример: { $command } 1 "Привет, мир!"
command-language-select-desc = Выбрать язык, на котором говорит ваше существо. Вы можете использовать либо название языка, либо его позицию в списке языков.
command-language-select-help = Использование: { $command } <id языка>. Пример: { $command } 1. Пример: { $command } TauCetiBasic
command-language-spoken = Говорится:
command-language-understood = Понимается:
command-language-current-entry = { $id }. { $language } - { $name } (текущий)
command-language-entry = { $id }. { $language } - { $name }
command-language-invalid-number = Номер языка должен быть между 0 и { $total }. Либо используйте название языка.
command-language-invalid-language = Язык { $id } не существует или вы не можете говорить на нем.

# toolshed

command-description-language-add = Добавляет новый язык в сущность. Два последних аргумента указывают, должен ли он быть произносимым/понимаемым. Пример: 'self language:add "Canilunzt" true true'
command-description-language-rm = Удаляет язык из сущности. Работает аналогично language:add. Пример: 'self language:rm "TauCetiBasic" true true'.
command-description-language-lsspoken = Перечисляет все языки, на которых может говорить сущность. Пример: 'self language:lsspoken'
command-description-language-lsunderstood = Перечисляет все языки, которые сущность может понимать. Пример: 'self language:lssunderstood'
command-description-translator-addlang = Добавляет новый целевой язык в сущность переводчика. См. language:add для подробностей.
command-description-translator-rmlang = Удаляет целевой язык из сущности переводчика. См. language:rm для подробностей.
command-description-translator-addrequired = Добавляет новый обязательный язык в сущность переводчика. Пример: 'ent 1234 translator:addrequired "TauCetiBasic"'
command-description-translator-rmrequired = Удаляет обязательный язык из сущности переводчика. Пример: 'ent 1234 translator:rmrequired "TauCetiBasic"'
command-description-translator-lsspoken = Перечисляет все языки, на которых говорит сущность переводчика. Пример: 'ent 1234 translator:lsspoken'
command-description-translator-lsunderstood = Перечисляет все языки, которые сущность переводчика может понимать. Пример: 'ent 1234 translator:lssunderstood'
command-description-translator-lsrequired = Перечисляет все обязательные языки для сущности переводчика. Пример: 'ent 1234 translator:lsrequired'
command-language-error-this-will-not-work = Это не сработает.
command-language-error-not-a-translator = Сущность { $entity } не является переводчиком.
