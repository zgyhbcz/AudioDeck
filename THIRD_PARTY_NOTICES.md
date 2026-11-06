# Third-party notices

AudioDeck is distributed under the MIT License. See [LICENSE](LICENSE).

## IPolicyConfig interface definition

Switching the default audio endpoint uses the undocumented Windows COM interface
`IPolicyConfig`. The interface layout used in
`src/Audio/CoreAudioInterop.cs` is based on the public `PolicyConfig.h` header
from the DefSound project by EreTIk, as distributed with
[AudioEndPointController](https://github.com/DanStevens/AudioEndPointController):

- Header: <https://github.com/DanStevens/AudioEndPointController/blob/master/EndPointController/PolicyConfig.h>
- Original author: EreTIk / DefSound
- License: MIT

The relevant interface declaration is included here as a compatibility
definition only. AudioDeck does not include or redistribute DefSound binaries.

```text
Copyright (c) 2015 DefSound

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Windows system sounds

AudioDeck plays sounds from the local Windows installation through the WinMM
`PlaySound` API. No Windows audio files are redistributed with this project.
