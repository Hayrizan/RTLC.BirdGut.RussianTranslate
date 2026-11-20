# RTLC.BirdGut.RussianTranslate

Русификатор **BirdGut** для Windows и Steam. Включает 237 записей перевода, 90 локализованных текстур, кириллический Slippysloppy и русские заставки.

## Установка

1. Скачайте `RTLC.BirdGut.RussianTranslate.zip` из [релиза](https://github.com/Hayrizan/RTLC.BirdGut.RussianTranslate/releases/latest).
2. В Steam откройте **BirdGut → Свойства → Установленные файлы → Обзор**.
3. Распакуйте содержимое архива рядом с `BirdGut.exe`, подтвердите замену файлов и запустите игру через Steam.

Архив уже включает BepInEx 5.4.23.6 x86 и его настройки для Unity 2017.1.1f1. `Assets.zip` содержит материалы локализации и для установки не нужен.

Журнал загрузки находится в `BepInEx/LogOutput.log`. Для удаления русификатора удалите его папку под `BepInEx/plugins` и проверьте целостность файлов игры через Steam, чтобы восстановить исходную стартовую заставку. Если BepInEx был установлен только для этого русификатора, можно также удалить его загрузчик `winhttp.dll` и папку `BepInEx`.

## Сборка

Для сборки нужны .NET SDK, Python и установленная BirdGut. Укажите свой путь к игре:

```powershell
dotnet build -c Release '-p:BirdGutDir=C:\Games\BirdGut'
python -m pip install UnityPy==1.25.0
python tools/build_bundle.py
```

В папку `BepInEx/plugins/RTLC.BirdGut.RussianTranslate` помещаются два файла: собранная DLL и `assets/rtlc.bundle`. Скрипт `tools/install_native_splash.py` отдельно устанавливает локализованную заставку, которую Unity показывает до загрузки плагина.

## Авторы локализации

- **Hayrizan** — текст и текстуры.
- **DimaLooper** — тестирование.

Используются BepInEx, HarmonyX и Unity Doorstop. Часть черновой работы с текстурами и кодом выполнена с помощью ИИ.
