# 阶段 26：中望 PDF 预设与 PMP 校准保护

## 确认的缺陷

1. ZwPdfMediaFiles 原来要求 DriverName 必须等于 DWG to PDF，导致四种官方 ZWCAD PDF 预设在缺少精确纸张时被误拒绝。五种预设使用同一 ZwPDFDriver.dll 和 PDF.ini，但 DriverName 不同。
2. 原代码不检查 pmp_filepath，生成新 PMP 时把原引用直接换掉，可能丢失已有校准和设备覆盖设置。旧测试甚至以 old.pmp 作为正常输入，未检验该语义。

本机官方配置只读检查在 audit-evidence/phase26-native-presets.json。五种预设分别为 DWG to PDF、ZWCAD PDF(General Documentation)、ZWCAD PDF(High Quality Print)、ZWCAD PDF(Smallest File)、ZWCAD PDF(Web and Mobile)，其 resolution_x/y 分别为 500、1000、2000、300、300。这里只记录配置字段，不以此证明实际渲染分辨率。

## 修复

- 接受上述五种明确名称，继续同时核对设备类型、原生驱动文件、配置文件、端口及自动提交/外部引用限制。未知预设及带额外后缀的名称仍拒绝，不用包含 PDF 的字符串宽泛放行。
- 非空 PMP 引用在创建临时目录之前明确拒绝，包括不存在的 PMP。提示不能丢弃校准，源 PC5/PMP 原样保留。此阶段未实现旧 PMP 的无损迁移。
- 只生成任务私有纸张配置，原预设的分辨率、文字、线条等全部非纸张字段保持原值；不修改用户设备或系统搜索路径。

## 验证与边界

- 修复前新增专项测试 7 失败/1 通过，复现四种预设误拒绝及三种 PMP 引用被替换。
- 修复后 Release 零警告、零错误，467 项核心测试、272 项离线 UI 检查通过；独立 net48 PDF 检查程序运行通过。
- 新增测试覆盖五种预设保留设置、已有/缺失/绝对 PMP 引用拒绝、未知近似名称拒绝，原文件字节及临时目录不变。
- 通过实际官方文件生成任务私有副本，再由 Python configparser 独立解析、逐项比较所有非纸张字段，核对生成的纸张关联、尺寸、源文件 SHA-256 以及清理结果。证据脚本为 phase26-check-presets.ps1、phase26-verify-presets.py。

此次从 PDF 参数审查中发现并先修复以上可靠性问题。原版帮助确有矢量/光栅分辨率、文字转图形、直线合并和 PDF 图层开关；本项目尚未完整接入。AutoCAD PC3 的 Hardcopy_Resolution 等字段和中望 PC5 的字段并非同一结构，不能把找到同名或相似字段当作已实现跨宿主设置。

没有启动、连接或操作 CAD，没有安装插件或替换设备配置。自动纸张是否被原生驱动接受仍待验收，nativeAcceptance 保持 pending。证据副本中的 PMP 绝对引用指向已清理的临时目录，不能当安装配置使用。

## 本次产物与独立证据

新包：`artifacts/FastBatchPlot-20260920-180756-630`。两宿主包清单校验通过，8 个项目程序集的绝对路径、修改时间、大小与 SHA-256 已记录于 `phase26-package-artifacts.json`，全部晚于本次构建起点。检查 EXE 的记录在 `phase26-check-artifacts.json`。

两套包分别在独立 Windows PowerShell/.NET Framework 进程实际运行 UI、PDF 合并、书签和任务历史检查。两套包各自读取五份官方 PC5、生成并清理私有配置；独立 Python 比较全部非纸张字段保持一致，原文件 SHA-256 及修改时间保持不变。证据：`phase26-presets-AutoCAD2018/verification.json`、`phase26-presets-ZWCAD2026/verification.json` 及对应 package 日志。

以上是包内共享文件服务和界面运行证据，未加载 CAD 宿主 DLL。
