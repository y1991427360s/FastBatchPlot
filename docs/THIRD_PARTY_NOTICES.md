# 第三方许可说明

## ezdxf ACI 参考调色板

FastBatchPlot.Core/Common/AciColorHelper.cs 使用 ezdxf 的 DXF_DEFAULT_COLORS 调色板数据，来源：
https://github.com/mozman/ezdxf/blob/d6f2ac10caeddc712ed1824aaeb3b9c050de4a04/src/ezdxf/colors.py

以下为随源码及二进制分发保留的完整许可：

MIT License

Copyright (c) 2020 Manfred Moitzi

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

## 其他运行依赖

发布包 `licenses/` 保存当前恢复版本的 NuGet nuspec、许可证与第三方公告原文。包含 PdfSharpCore 1.3.67（MIT）、SharpZipLib 1.4.2（MIT）、SixLabors.Fonts 1.0.0-beta17（Apache-2.0）、SixLabors.ImageSharp 1.0.4（Apache-2.0）及 Microsoft System.* 支持库。具体版权持有人见各 nuspec、许可证和公告；许可适用于这些固定版本，不能外推到后续版本。

SharpZipLib 和 SixLabors 包只提供 SPDX 表达式，完整许可证补自 nuspec 中记录的固定源码提交：

- https://github.com/icsharpcode/SharpZipLib/blob/33f64eb0f28cdd2b084cb822fcc224c7c5aba553/LICENSE.txt
- https://github.com/SixLabors/Fonts/blob/eb8742818d6c5d85e5e3ea442cc3ae355375ef7b/LICENSE
- https://github.com/SixLabors/ImageSharp/blob/5321ca862acc48343de60b2c06f78bbafcac2a03/LICENSE

其他许可证直接取自本机 NuGet 恢复包。更新任何 NuGet 依赖时必须同步重新核对并更新 `deploy/licenses/`，不能沿用旧版本许可。
