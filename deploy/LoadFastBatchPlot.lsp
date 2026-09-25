;;; FastBatchPlot 发布包手动加载辅助；不会自动执行 NETLOAD。
;;; 在明确允许加载插件后运行 LoadFastBatchPlot，选择发布包中的目标 DLL。
(defun c:LoadFastBatchPlot (/ dllPath kw)
  (setq dllPath (getfiled "选择与当前 CAD 匹配的发布版插件 DLL" "" "dll" 0))
  (if dllPath
    (progn
      (princ "\n仅支持 AutoCAD 2018 / ZWCAD 2026；请核对当前 CAD 版本与 DLL。")
      (initget "Yes No")
      (setq kw (getkword "\n确认加载该插件 DLL？[Yes/No] <Yes>: "))
      (if (/= kw "No")
        (progn
          (command "_.NETLOAD" dllPath)
          (princ "\n已提交 NETLOAD。加载成功后请输入 BP 打开批打印界面。")
        )
        (princ "\n已取消加载。")
      )
    )
    (princ "\n未选择 DLL，已取消。")
  )
  (princ)
)
(princ "\n[FastBatchPlot] 已定义 LoadFastBatchPlot 命令。")
(princ "\n>>> 请在命令行输入 LoadFastBatchPlot 选择 DLL 加载；")
(princ "\n>>> 或在命令行直接输入 NETLOAD 选择发布包中的 FastBatchPlot.ZWCAD.dll。")
(princ "\n>>> 加载成功后，在命令行输入 BP 即可调出批打印主界面。\n")
(princ)
