# Third-party notices

Gridworks macOS 내부 테스트 빌드는 Godot Engine 4.7.1 Mono와 .NET 8 runtime 구성요소를 포함한다.
Gridworks 자체 저작물에는 [LICENSE.md](LICENSE.md)의 별도 법적 상태가 적용된다.

## Godot Engine

Copyright (c) 2014-present Godot Engine contributors.

Copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur.

Godot은 MIT License로 제공된다. Engine에 포함된 제3자 라이브러리의 저작권과 개별 라이선스는
Godot 4.7.1의 [COPYRIGHT.txt](https://github.com/godotengine/godot/blob/4.7.1-stable/COPYRIGHT.txt)에
정리되어 있다.

## .NET runtime

Copyright (c) .NET Foundation and Contributors.

.NET runtime 구성요소는 MIT License로 제공된다. 원문은 .NET runtime 저장소의
[LICENSE.TXT](https://github.com/dotnet/runtime/blob/v8.0.0/LICENSE.TXT)에서도 확인할 수 있다.

## MIT License text

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Package 확인 경계

실제 패키지를 만들 때에는 고정된 공식 Godot export template을 사용하고, 패키지에 포함된 engine과
runtime이 위 고지와 일치하는지 확인한다. 이 문서는 Gridworks 저작물에 공개 라이선스를 부여하지
않는다.
