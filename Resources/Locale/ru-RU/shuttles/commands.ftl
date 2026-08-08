# FTLdiskburner
cmd-ftldisk-desc = Создаёт диск координат FTL для полёта на карту, где находится указанный EntityID
cmd-ftldisk-help = ftldisk [EntityID]

cmd-ftldisk-no-transform = У сущности { $destination } нет Transform Component!
cmd-ftldisk-no-map = У сущности { $destination } нет карты!
cmd-ftldisk-no-map-comp = Сущность { $destination } каким-то образом на карте { $map } без компонента карты.
cmd-ftldisk-map-not-init = Сущность { $destination } на неинициализированной карте { $map }! Убедитесь, что инициализация безопасна, затем инициализируйте карту — иначе игроки застрянут на месте!
cmd-ftldisk-map-paused = Сущность { $desintation } на приостановленной карте { $map }! Сначала снимите паузу с карты, иначе игроки застрянут на месте.
cmd-ftldisk-planet = Сущность { $desintation } на планетарной карте { $map } и потребует точку FTL. Она может уже существовать.
cmd-ftldisk-already-dest-not-enabled = Сущность { $destination } на карте { $map }, у которой уже есть FTLDestinationComponent, но он не Enabled! Для безопасности задайте это вручную.
cmd-ftldisk-requires-ftl-point = Сущность { $destination } на карте { $map }, для полёта на которую нужна точка FTL! Она может уже существовать.

cmd-ftldisk-hint = netID карты
