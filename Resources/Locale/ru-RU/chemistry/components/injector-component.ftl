## UI

injector-volume-transfer-label = Объем: [color=white]{ $currentVolume }/{ $totalVolume }ед.[/color]
    Режим: [color=white]{ $modeString }[/color] ([color=white]{ $transferVolume }ед.[/color])
injector-draw-text = Забор
injector-toggle-verb-text = Переключить режим Инъектора
injector-inject-text = Введение

## Entity

injector-component-inject-mode-name = введение
injector-component-draw-mode-name = забор
injector-component-dynamic-mode-name = динамический
injector-component-mode-changed-text = Выбран режим { $mode }!
injector-invalid-injector-toggle-mode = Неверный режим
injector-volume-label = Объем: [color=white]{ $currentVolume }/{ $totalVolume }ед.[/color]
    Режим: [color=white]{ $modeString }[/color]

## Entity

injector-component-drawing-text = Содержимое набирается
injector-component-injecting-text = Содержимое вводится
injector-component-cannot-transfer-message = Вы не можете ничего переместить в { $target }!
injector-component-cannot-transfer-message-self = Вы не можете переместить что-либо в себя!
injector-component-cannot-draw-message = Вы не можете ничего набрать из { $target }!
injector-component-cannot-draw-message-self = Вы не можете набрать что-либо из себя!
injector-component-cannot-inject-message = Вы не можете ничего ввести в { $target }!
injector-component-cannot-inject-message-self = Вы не можете ввести что-либо в себя!
injector-component-inject-success-message = Вы вводите { $amount } ед. в { $target }!
injector-component-inject-success-message-self = Вы вводите { $amount }ед. в себя!
injector-component-cannot-toggle-dynamic-message = Нельзя включить динамический!
injector-component-empty-message = { CAPITALIZE($injector) } пуст!
injector-component-blocked-user = Защитное снаряжение мешает инъекции!
injector-component-blocked-other = Защитное снаряжение { CAPITALIZE(POSS-ADJ($target)) } помешало { $user } сделать инъекцию!
injector-component-transfer-success-message = Вы перемещаете { $amount } ед. в { $target }.
injector-component-transfer-success-message-self = В переливаете { $amount }ед. в себя.
injector-component-draw-success-message = Вы набираете { $amount } ед. из { $target }.
injector-component-draw-success-message-self = Вы набираете { $amount }ед. из себя.

## Fail Messages

injector-component-target-already-full-message = { CAPITALIZE($target) } полон!
injector-component-target-already-full-message-self = Вы уже полны!
injector-component-ignore-mobs = Возможно взаимодействовать только с ёмкостями!
injector-component-target-is-empty-message = { CAPITALIZE($target) } пуст!

## mob-inject doafter messages

injector-component-needle-injecting-user = Вы начинаете вводить содержимое шприца.
injector-component-needle-injecting-target = { CAPITALIZE($user) } начинает вводить содержимое шприца в вас!
injector-component-needle-drawing-user = Вы начинаете набирать шприц.
injector-component-needle-drawing-target = { CAPITALIZE($user) } начинает набирать шприц из вас!
injector-component-spray-injecting-user = Вы начинаете вводить содержимое инъектора.
injector-component-spray-injecting-target = { CAPITALIZE($user) } начинает вводить содержимое инъектора в вас!
injector-component-target-is-empty-message-self = Вы пусты!

## Target Popup Success messages
injector-component-feel-prick-message = Вы чувствуете легкий укол!
injector-component-cannot-toggle-draw-message = Больше не набрать!
injector-component-cannot-toggle-inject-message = Нечего вводить!

## mob-inject doafter messages

injector-component-drawing-user = Вы начинаете набирать шприц.
injector-component-injecting-user = Вы начинаете вводить содержимое шприца.
injector-component-drawing-target = { CAPITALIZE($user) } начинает набирать шприц из вас!
injector-component-injecting-target = { CAPITALIZE($user) } начинает вводить содержимое шприца в вас!
