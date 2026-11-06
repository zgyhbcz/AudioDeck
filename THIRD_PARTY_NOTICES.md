# Third-party notices

AudioDeck is distributed under the MIT License. See [LICENSE](LICENSE).

## IPolicyConfig interface definition

Switching the default audio endpoint uses the undocumented Windows COM interface
`IPolicyConfig`. The interface layout used in
`src/Audio/CoreAudioInterop.cs` is based on the public `PolicyConfig.h` header
from the DefSound project by EreTIk:

- Project: <https://github.com/DefSound/DefSound>
- Header: `PolicyConfig.h`
- License: MIT

The relevant interface declaration is included here as a compatibility
definition only. AudioDeck does not include or redistribute DefSound binaries.

## Windows system sounds

AudioDeck plays sounds from the local Windows installation through the WinMM
`PlaySound` API. No Windows audio files are redistributed with this project.
