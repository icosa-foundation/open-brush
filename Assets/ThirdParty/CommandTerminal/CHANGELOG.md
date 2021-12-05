Changelog
=========

## v1.3.1 - 2021-11-27

### Fixed
- The shell is now looking for the RegisterCommandAttribute in all assemblies.

## v1.3 `c9c7d89` - 2021-08-09

### Added
- Shell command component: shell commands can now be registered through gameObjects

### Changed
- Moved to package: the code has been moved into a package format to work with unity's package manager

## 1.02 `08a66da` - 2018-09-14

### Added
- Variables: defined with `set name value`, accessed with `$name`. Run `set` with no arguments to display all variables and their values.
- Option to change the alpha value of the input background texture.
- Command hint argument for better error messages. Use `RegisterCommand(Hint = "Command $a")]` to show a command's usage.

### Changed
- Better autocompletion: autocomplete can now partially complete words when there are multiple suggestions available.

### Fixed
- Fix background texture being destroyed when loading a scene with the Terminal set to `DontDestroyOnLoad`.
- Fix hotkeys bound to function keys causing the input to not register the first character.
- Fix formatting on autocomplete suggestions.

## 1.01 `9a1b0b3` - 2018-08-09

### Added
- Option to to change the position of the toggle GUI buttons.
- Option to change the window size ratio between the partial and full window height.
- Optional GUI button to run a command (useful for mobile devices).

### Changed
- Autocomplete now uses the last word in the input text, rather than just completing the first word.

## 1.0  `db07b43` - 2018-07-15

### Added
- Customizable toggle hotkey.
- Two new terminal colors (customizable).
- Option to change prompt character (or remove it).
- Option to open a larger terminal window with a separate hotkey.
- Command autocompletion (use the tab key while typing a command).
- Option to toggle window using GUI buttons (disabled by default).
- Option to customize the input background contrast.

### Fixed
- Input registering hotkey character when hotkey was pressed.
- Inspector presentation.

### Removed
- `LS` command in favor of `HELP` with no arguments to list all registered commands.
