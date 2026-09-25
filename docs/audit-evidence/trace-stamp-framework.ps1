$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @"
using System;
using System.Reflection;
public static class StampDependencyTrace {
 public static void Start() {AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{
  Console.Error.WriteLine("RESOLVE="+args.Name+"; REQUESTER="+(args.RequestingAssembly==null?"NULL":args.RequestingAssembly.FullName+"; PATH="+args.RequestingAssembly.Location));return null;};}
}
"@
[StampDependencyTrace]::Start()
& "$PSScriptRoot/check-stamp-framework.ps1" -Contents 'E:/366256/vibecoding/批打印-new/src/FastBatchPlot.UI/bin/Release/net48'
