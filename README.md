# midi2vipi

воспроизводит midi-файлы через системный синтезатор или как макрос для virtual piano. управление глобальными хоткеями без переключения окна.
бла бла бла ложные детекты рандомных антивирусов вы поняли

## использование

открой `midi2vipi.exe`

- нажми **open midi** и выбери файл `.mid` или `.midi`
- отметь нужные треки галочками (или сними все через **none**)
- выбери режим:
  - **synth** - воспроизведение через системный midi-синтезатор
  - **macro** - автонажатие клавиш по раскладке virtual piano в активном окне
- слайдеры `vol`, `bpm`, `shift` - громкость, темп и транспонирование в реальном времени
- кнопка `r` рядом с `bpm` - сброс темпа к исходному из файла
- **macro hotkey** - хоткей запуска/остановки макроса (по умолчанию `-`)
- **pause hotkey** - хоткей паузы (по умолчанию `=`)

> для режима macro активное окно должно быть на EN-раскладке. если нет - появится предупреждение `[!] switch to en layout`

## сборка

никаких зависимостей - используется встроенный компилятор C# из .NET Framework 4.8.

```
запусти build.bat
```

получишь на выходе `midi2vipi.exe`.

**требования:** Windows 7+, .NET Framework 4.8

---

# midi2vipi

plays midi files through the system synthesizer or as a macro for virtual piano. controlled with global hotkeys without switching windows.
bla bla bla false detections of random av you know

## usage

open `midi2vipi.exe`

- click **open midi** and pick a `.mid` or `.midi` file
- check the tracks you want (or toggle all via **all** / **none**)
- choose a mode:
  - **synth** - playback through the system midi synthesizer
  - **macro** - automatic key presses using the virtual piano layout in the active window
- `vol`, `bpm`, `shift` sliders - volume, tempo, and note offset in real time
- `r` button next to `bpm` - resets tempo to the original value from the file
- **macro hotkey** - starts/stops the macro (default `-`)
- **pause hotkey** - pauses playback (default `=`)

> macro mode requires the active window to use the EN keyboard layout. if not, the warning `[!] switch to en layout` will appear

## build

no dependencies - uses the built-in C# compiler from .NET Framework 4.8.

```
run build.bat
```

outputs a single `midi2vipi.exe`.

**requirements:** Windows 7+, .NET Framework 4.8
