# Third-Party Notices

This project includes or depends on the following third-party software.

## s2protocol

The decoders and encoders are ported from Blizzard's s2protocol, and the per-build protocol code under
`src/Biz.Bizadm.S2Protocol.NET/Generated/` is generated from its protocol definitions (`json/protocolNNNNN.json`,
checked against `s2protocol/versions/protocolNNNNN.py`). The replay attributes decoder and attribute IDs are ported
from `decode_replay_attributes_events` and `attributes.py`.

The Python s2protocol is also used by `tools/golden/make_golden.py` to produce the test goldens; it is not shipped
with the library package.

- Source: https://github.com/Blizzard/s2protocol (commit 3b40c5ef01834096b4305172ef5485ec00133dcc)
- License: MIT

```
Copyright (c) 2013-2017 Blizzard Entertainment

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```
